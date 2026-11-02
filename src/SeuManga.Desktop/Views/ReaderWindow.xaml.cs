using System.Collections.Frozen;
using System.IO;
using System.Text.Encodings.Web;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using SeuManga.Application.Abstractions;
using SeuManga.Application.Library;
using SeuManga.Application.Manga;
using SeuManga.Domain.Entities;
namespace SeuManga.Desktop.Views;
public partial class ReaderWindow : Window
{
    private static readonly FrozenSet<string> AdAndTrackerDomains = new[]
    {
        "doubleclick.net", "googlesyndication.com", "googleadservices.com", "adnxs.com",
        "adform.net", "taboola.com", "outbrain.com", "criteo.com", "scorecardresearch.com",
        "moatads.com", "amazon-adsystem.com", "pubmatic.com", "openx.net", "rubiconproject.com",
        "adsafeprotected.com", "zedo.com", "quantserve.com", "adroll.com", "serving-sys.com"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    private MangaChapter _chapter;
    private IReadOnlyList<string> _pages;
    private readonly MangaSearchResult _manga;
    private readonly IReadOnlyList<MangaChapter> _chapters;
    private readonly MangaCatalogService _catalog;
    private int _chapterIndex;
    private readonly LibraryService _library;
    private readonly IChapterImageProvider _images;
    private CancellationTokenSource? _pageLoad;
    private int _index;
    private bool _fullscreen;
    private bool _rightToLeft = true;
    private bool _updatingPageSelector;
    private bool _initialized;
    private byte[]? _currentImage;
    private byte[]? _secondImage;
    private string _currentContentType = "image/jpeg";
    private string _secondContentType = "image/jpeg";
    private readonly Dictionary<int, (byte[] Data, string ContentType, long UsedAt)> _pageCache = [];
    private long _pageCacheBytes;
    private CancellationTokenSource? _prefetchLoad;
    private const long PageCacheLimitBytes = 32L * 1024 * 1024;
    public ReaderWindow(MangaChapter chapter, IReadOnlyList<MangaChapter> chapters, MangaCatalogService catalog, IReadOnlyList<string> pages, MangaSearchResult manga, LibraryService library, IChapterImageProvider images)
    {
        InitializeComponent(); _chapter = chapter; _chapters = chapters; _chapterIndex = Math.Max(0, chapters.ToList().FindIndex(x => x.Id == chapter.Id)); _catalog = catalog; _pages = pages; _manga = manga; _library = library; _images = images;
        _initialized = true;         UpdateChapterTitle(); Loaded += async (_, _) => await InitializeReaderAsync(); Closed += (_, _) => { _pageLoad?.Cancel(); CancelPrefetch(); _pageCache.Clear(); _pageCacheBytes = 0; _currentImage = null; _secondImage = null; }; UpdateControls();
    }
    private async Task InitializeReaderAsync()
    {
        if (_pages.Count == 0) { PageLabel.Text = "Sem páginas"; return; }
        await Reader.EnsureCoreWebView2Async();
        var settings = Reader.CoreWebView2.Settings;
        settings.IsScriptEnabled = false; settings.IsWebMessageEnabled = false; settings.AreDefaultContextMenusEnabled = false; settings.IsStatusBarEnabled = false; settings.IsZoomControlEnabled = false;
        ConfigureAdBlocker();
        Reader.CoreWebView2.NavigationStarting += (_, e) => { if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.Scheme != "about") e.Cancel = true; };
        Reader.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
        var previous = (await _library.GetHistoryAsync()).FirstOrDefault(x => x.MangaDexId == _manga.Id && x.ChapterId == _chapter.Id);
        if (previous is not null) _index = Math.Clamp(previous.LastPage, 0, Math.Max(0, _pages.Count - 1));
        await ShowPageAsync();
    }
    private void ConfigureAdBlocker()
    {
        var core = Reader.CoreWebView2;
        foreach (var domain in AdAndTrackerDomains)
            core.AddWebResourceRequestedFilter($"*://*{domain}/*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);

        core.WebResourceRequested += (_, e) =>
        {
            if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) || !IsAdOrTrackerHost(uri.Host)) return;
            e.Response = core.Environment.CreateWebResourceResponse(
                new MemoryStream(Array.Empty<byte>()), 403, "Blocked by SeuManga", "Content-Type: text/plain");
        };
    }
    private static bool IsAdOrTrackerHost(string host) => AdAndTrackerDomains.Any(domain =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
    private async Task ShowPageAsync()
    {
        if (_pages.Count == 0) return;
        CancelPrefetch();
        _pageLoad?.Cancel(); _pageLoad?.Dispose(); _pageLoad = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            if (IsDoublePage && _index % 2 != 0) _index--;
            (_currentImage, _currentContentType) = await GetPageImageAsync(_index, _pageLoad.Token);
            _secondImage = null;
            if (IsDoublePage && _index + 1 < _pages.Count)
                (_secondImage, _secondContentType) = await GetPageImageAsync(_index + 1, _pageLoad.Token);
            RenderCurrentPage(); UpdateControls(); await SaveProgressAsync();
            StartPrefetchNextPage();
        }
        catch (OperationCanceledException) { }
        catch { UpdateControls(); PageLabel.Text = $"Página {_index + 1} indisponível · tente avançar"; }
    }
    private async Task<(byte[] Data, string ContentType)> GetPageImageAsync(int pageIndex, CancellationToken cancellationToken)
    {
        if (_pageCache.TryGetValue(pageIndex, out var cached))
        {
            _pageCache[pageIndex] = (cached.Data, cached.ContentType, DateTime.UtcNow.Ticks);
            return (cached.Data, cached.ContentType);
        }
        var page = await _images.DownloadAsync(_pages[pageIndex], cancellationToken);
        AddPageToCache(pageIndex, page);
        return page;
    }
    private void AddPageToCache(int pageIndex, (byte[] Data, string ContentType) page)
    {
        if (page.Data.LongLength > PageCacheLimitBytes) return;
        if (_pageCache.Remove(pageIndex, out var existing)) _pageCacheBytes -= existing.Data.LongLength;
        while (_pageCache.Count >= 2 || _pageCacheBytes + page.Data.LongLength > PageCacheLimitBytes)
        {
            var victim = _pageCache.Where(x => x.Key != _index).OrderBy(x => x.Value.UsedAt).Select(x => (int?)x.Key).FirstOrDefault();
            if (victim is null) return;
            _pageCacheBytes -= _pageCache[victim.Value].Data.LongLength;
            _pageCache.Remove(victim.Value);
        }
        _pageCache[pageIndex] = (page.Data, page.ContentType, DateTime.UtcNow.Ticks);
        _pageCacheBytes += page.Data.LongLength;
    }
    private void StartPrefetchNextPage()
    {
        var next = _index + PageStep;
        if (next >= _pages.Count || _pageCache.ContainsKey(next)) return;
        var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        _prefetchLoad = cancellation;
        _ = PrefetchPageAsync(next, cancellation);
    }
    private void CancelPrefetch()
    {
        var pending = _prefetchLoad;
        _prefetchLoad = null;
        if (pending is null) return;
        try { pending.Cancel(); } catch (ObjectDisposedException) { }
    }
    private async Task PrefetchPageAsync(int pageIndex, CancellationTokenSource cancellation)
    {
        try { await GetPageImageAsync(pageIndex, cancellation.Token); }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            if (ReferenceEquals(_prefetchLoad, cancellation)) _prefetchLoad = null;
            cancellation.Dispose();
        }
    }
    private void RenderCurrentPage()
    {
        if (_currentImage is null || Reader.CoreWebView2 is null) return;
        var (maxWidth, maxHeight) = ImageFit.SelectedIndex switch
        {
            1 => ("none", "100vh"),
            2 => ("100%", "100vh"),
            3 => ("none", "none"),
            _ => ("100%", "none")
        };
        var imageWidth = IsDoublePage ? "50vw" : maxWidth;
        var imageItems = new List<(byte[] Data, string ContentType)>();
        if (IsDoublePage && _secondImage is not null && _rightToLeft)
            imageItems.Add((_secondImage, _secondContentType));
        imageItems.Add((_currentImage, _currentContentType));
        if (IsDoublePage && _secondImage is not null && !_rightToLeft)
            imageItems.Add((_secondImage, _secondContentType));
        var images = string.Join("", imageItems.Select(image =>
        {
            var data = Convert.ToBase64String(image.Data);
            return $"<img src='data:{HtmlEncoder.Default.Encode(image.ContentType)};base64,{data}' style='width:auto;height:auto;max-width:{imageWidth};max-height:{maxHeight};object-fit:contain' alt='Página indisponível'>";
        }));
        var html = $"<!doctype html><html><head><meta charset='utf-8'><meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'\"><meta name='viewport' content='width=device-width,initial-scale=1'></head><body style='margin:0;background:#0d0b12;min-height:100vh;display:flex;flex-direction:row;align-items:center;justify-content:center;overflow:auto'>{images}</body></html>";
        Reader.NavigateToString(html);
    }
    private void ImageFit_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => RenderCurrentPage();
    private void ZoomIn_Click(object sender, RoutedEventArgs e) { if (Reader.CoreWebView2 is not null) Reader.ZoomFactor = Math.Min(5, Reader.ZoomFactor + 0.1); }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) { if (Reader.CoreWebView2 is not null) Reader.ZoomFactor = Math.Max(0.25, Reader.ZoomFactor - 0.1); }
    private async Task SaveProgressAsync()
    {
        try
        {
            await _library.AddAsync(_manga.Id, _manga.Title, _manga.CoverUrl);
            await _library.SaveProgressAsync(new ReadingHistoryEntry { MangaDexId = _manga.Id, Title = _manga.Title, CoverUrl = _manga.CoverUrl, ChapterId = _chapter.Id, ChapterNumber = _chapter.Number, LastPage = _index, PageCount = _pages.Count, UpdatedAt = DateTimeOffset.UtcNow });
        }
        catch { /* Falhas ao salvar não devem interromper a leitura. */ }
    }
    private void UpdateChapterTitle() => ChapterTitle.Text = $"Capítulo {_chapter.Number} · {_chapter.Title}" + (string.IsNullOrWhiteSpace(_chapter.Group) ? "" : $" · {_chapter.Group}");
    private async Task MoveChapterAsync(int direction)
    {
        var next = _chapterIndex + direction; if (next < 0 || next >= _chapters.Count) return;
        try
        {
            _pageLoad?.Cancel(); CancelPrefetch(); _pageCache.Clear(); _pageCacheBytes = 0; _currentImage = null; _secondImage = null;
            _chapterIndex = next; _chapter = _chapters[next]; _pages = await _catalog.GetChapterPageUrlsAsync(_chapter.Id); _index = 0;
            var saved = (await _library.GetHistoryAsync()).FirstOrDefault(x => x.MangaDexId == _manga.Id && x.ChapterId == _chapter.Id);
            if (saved is not null) _index = Math.Clamp(saved.LastPage, 0, Math.Max(0, _pages.Count - 1));
            UpdateChapterTitle(); await ShowPageAsync();
        }
        catch { PageLabel.Text = "Não foi possível abrir o capítulo."; }
    }
    private async void PreviousChapter_Click(object sender, RoutedEventArgs e) => await MoveChapterAsync(-1);
    private async void NextChapter_Click(object sender, RoutedEventArgs e) => await MoveChapterAsync(1);
    private void UpdateControls()
    {
        PreviousChapterButton.IsEnabled = _chapterIndex > 0; NextChapterButton.IsEnabled = _chapterIndex < _chapters.Count - 1;
        var lastVisiblePage = Math.Min(_index + PageStep, _pages.Count);
        PageLabel.Text = _pages.Count == 0 ? "0 / 0" : IsDoublePage && lastVisiblePage > _index + 1 ? $"{_index + 1}-{lastVisiblePage} / {_pages.Count}" : $"{_index + 1} / {_pages.Count}";
        PreviousButton.IsEnabled = _index > 0; NextButton.IsEnabled = _index + PageStep < _pages.Count;
        _updatingPageSelector = true;
        try
        {
            if (PageSelector.Items.Count != _pages.Count) PageSelector.ItemsSource = Enumerable.Range(1, _pages.Count).ToArray();
            PageSelector.SelectedIndex = _pages.Count == 0 ? -1 : _index;
        }
        finally { _updatingPageSelector = false; }
    }
    private async void PageSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_updatingPageSelector || PageSelector.SelectedIndex < 0 || PageSelector.SelectedIndex >= _pages.Count || PageSelector.SelectedIndex == _index) return;
        _index = IsDoublePage ? PageSelector.SelectedIndex - PageSelector.SelectedIndex % 2 : PageSelector.SelectedIndex;
        await ShowPageAsync();
    }
    private async void ReadingDirection_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _rightToLeft = ReadingDirection.SelectedIndex == 0;
        if (_initialized && IsDoublePage) await ShowPageAsync();
    }
    private async void PageLayout_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_initialized) await ShowPageAsync();
    }
    private bool IsDoublePage => PageLayout.SelectedIndex == 1;
    private int PageStep => IsDoublePage ? 2 : 1;
    private async void Previous_Click(object sender, RoutedEventArgs e) { if (_index > 0) { _index = Math.Max(0, _index - PageStep); await ShowPageAsync(); } }
    private async void Next_Click(object sender, RoutedEventArgs e) { if (_index + PageStep < _pages.Count) { _index += PageStep; await ShowPageAsync(); } }
    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current.MainWindow is not Window host) return;
        _fullscreen = !_fullscreen;
        host.WindowStyle = _fullscreen ? WindowStyle.None : WindowStyle.SingleBorderWindow;
        host.WindowState = _fullscreen ? WindowState.Maximized : WindowState.Normal;
    }
    public void HandleReaderKeyDown(KeyEventArgs e) => Window_KeyDown(this, e);
    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space || e.Key == (_rightToLeft ? Key.Left : Key.Right)) { e.Handled = true; if (_index + PageStep < _pages.Count) { _index += PageStep; await ShowPageAsync(); } }
        else if (e.Key == (_rightToLeft ? Key.Right : Key.Left)) { e.Handled = true; if (_index > 0) { _index = Math.Max(0, _index - PageStep); await ShowPageAsync(); } }
        else if (e.Key == Key.F11) Fullscreen_Click(sender, e);
    }
}
