using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;
using SeuManga.Application.Abstractions;
using SeuManga.Application.Library;
using SeuManga.Application.Manga;
using SeuManga.Desktop.Controls;
using SeuManga.Domain.Entities;
namespace SeuManga.Desktop.Views;

public partial class ReaderWindow : Window
{
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
    private bool _updatingReaderMode;
    private bool _initialized;
    private byte[]? _currentImage;
    private byte[]? _secondImage;
    private readonly List<Image> _visibleImages = [];
    private readonly List<MangaDexReaderPageImage> _continuousImages = [];
    private readonly Dictionary<int, (byte[] Data, string ContentType, long UsedAt)> _pageCache = [];
    private long _pageCacheBytes;
    private CancellationTokenSource? _prefetchLoad;
    private const long PageCacheLimitBytes = 32L * 1024 * 1024;
    private double _zoomFactor = 1;
    public IChapterImageProvider ImageProvider => _images;
    public ReaderWindow(MangaChapter chapter, IReadOnlyList<MangaChapter> chapters, MangaCatalogService catalog, IReadOnlyList<string> pages, MangaSearchResult manga, LibraryService library, IChapterImageProvider images)
    {
        InitializeComponent(); _chapter = chapter; _chapters = chapters; _chapterIndex = Math.Max(0, chapters.ToList().FindIndex(x => x.Id == chapter.Id)); _catalog = catalog; _pages = pages; _manga = manga; _library = library; _images = images;
        _initialized = true;         UpdateChapterTitle(); Loaded += async (_, _) => await InitializeReaderAsync(); Closed += (_, _) => { _pageLoad?.Cancel(); CancelPrefetch(); _pageCache.Clear(); _pageCacheBytes = 0; _currentImage = null; _secondImage = null; }; UpdateControls();
    }
    private async Task InitializeReaderAsync()
    {
        var previous = (await _library.GetHistoryAsync()).FirstOrDefault(x => x.MangaDexId == _manga.Id && x.ChapterId == _chapter.Id);
        if (previous is not null) _index = Math.Clamp(previous.LastPage, 0, Math.Max(0, _pages.Count - 1));
        await ShowPageAsync();
    }
    private async Task ShowPageAsync()
    {
        if (_pages.Count == 0)
        {
            PageImageHost.Children.Clear();
            _visibleImages.Clear();
            ReaderError.Text = "Este capítulo não possui páginas disponíveis.";
            ReaderError.Visibility = Visibility.Visible;
            PageLabel.Text = "Sem páginas";
            UpdateControls();
            return;
        }
        CancelPrefetch();
        _pageLoad?.Cancel(); _pageLoad?.Dispose(); _pageLoad = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            if (IsDoublePage && _index % 2 != 0) _index--;
            (_currentImage, _) = await GetPageImageAsync(_index, _pageLoad.Token);
            _secondImage = null;
            if (IsDoublePage && _index + 1 < _pages.Count)
                (_secondImage, _) = await GetPageImageAsync(_index + 1, _pageLoad.Token);
            RenderCurrentPage(); UpdateControls(); await SaveProgressAsync();
            StartPrefetchNextPage();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Warning(ex, "Não foi possível exibir a página {PageNumber} do capítulo {ChapterId}", _index + 1, _chapter.Id);
            PageImageHost.Children.Clear();
            _visibleImages.Clear();
            ReaderError.Text = "Não foi possível carregar esta página. Verifique a conexão e tente avançar.";
            ReaderError.Visibility = Visibility.Visible;
            UpdateControls();
            PageLabel.Text = $"Página {_index + 1} indisponível · tente avançar";
        }
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
        if (IsContinuous) return;
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
        if (_currentImage is null) return;
        var imageItems = new List<byte[]>();
        if (IsDoublePage && _secondImage is not null && _rightToLeft)
            imageItems.Add(_secondImage);
        imageItems.Add(_currentImage);
        if (IsDoublePage && _secondImage is not null && !_rightToLeft)
            imageItems.Add(_secondImage);
        var controls = imageItems.Select(data =>
        {
            var source = new BitmapImage();
            using var stream = new MemoryStream(data, writable: false);
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.StreamSource = stream;
            source.EndInit();
            source.Freeze();
            return new Image { Source = source, Stretch = Stretch.Uniform };
        }).ToArray();
        PageImageHost.Children.Clear();
        foreach (var control in controls)
            PageImageHost.Children.Add(control);
        _visibleImages.Clear();
        _visibleImages.AddRange(controls);
        ReaderError.Visibility = Visibility.Collapsed;
        UpdateImageLayout();
    }
    private void UpdateImageLayout()
    {
        if (_visibleImages.Count == 0) return;
        var viewportWidth = PageViewport.ViewportWidth > 0 ? PageViewport.ViewportWidth : PageViewport.ActualWidth;
        var viewportHeight = PageViewport.ViewportHeight > 0 ? PageViewport.ViewportHeight : PageViewport.ActualHeight;
        var pageWidth = viewportWidth > 0 ? viewportWidth / _visibleImages.Count : double.PositiveInfinity;
        var maxWidth = ImageFit.SelectedIndex is 0 or 2 ? pageWidth : double.PositiveInfinity;
        var maxHeight = (ImageFit.SelectedIndex is 1 or 2) && viewportHeight > 0 ? viewportHeight : double.PositiveInfinity;
        foreach (var image in _visibleImages)
        {
            image.MaxWidth = maxWidth;
            image.MaxHeight = maxHeight;
            image.LayoutTransform = new ScaleTransform(_zoomFactor, _zoomFactor);
        }
    }
    private void PageViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ReferenceEquals(sender, PageViewport)) UpdateImageLayout();
    }
    private void ImageFit_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateImageLayout();
    private void ZoomIn_Click(object sender, RoutedEventArgs e) { _zoomFactor = Math.Min(5, _zoomFactor + 0.1); UpdateImageLayout(); }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) { _zoomFactor = Math.Max(0.25, _zoomFactor - 0.1); UpdateImageLayout(); }
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
            UpdateChapterTitle();
            if (IsContinuous)
            {
                PopulateContinuousImages();
                if (_pages.Count > 0)
                    await ShowContinuousPageAsync(_index);
                else
                {
                    ReaderError.Text = "Este capítulo não possui páginas disponíveis.";
                    ReaderError.Visibility = Visibility.Visible;
                }
                UpdateControls();
                await SaveProgressAsync();
            }
            else
                await ShowPageAsync();
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
    private async void PageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPageSelector || PageSelector.SelectedIndex < 0 || PageSelector.SelectedIndex >= _pages.Count || PageSelector.SelectedIndex == _index) return;
        _index = IsDoublePage ? PageSelector.SelectedIndex - PageSelector.SelectedIndex % 2 : PageSelector.SelectedIndex;
        await NavigateToPageAsync(_index);
    }
    private async void ReadMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        var continuous = IsContinuous;
        _updatingReaderMode = true;
        try
        {
            if (continuous && IsDoublePage)
                PageLayout.SelectedIndex = 0;
            PageLayout.IsEnabled = !continuous;
            ReadingDirection.IsEnabled = !continuous;
            ImageFit.IsEnabled = !continuous;
        }
        finally
        {
            _updatingReaderMode = false;
        }

        if (continuous)
        {
            _pageLoad?.Cancel();
            CancelPrefetch();
            PageViewport.Visibility = Visibility.Collapsed;
            ContinuousViewport.Visibility = Visibility.Visible;
            PopulateContinuousImages();
            ReaderError.Visibility = _pages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_pages.Count == 0)
                ReaderError.Text = "Este capítulo não possui páginas disponíveis.";
            if (_pages.Count > 0)
                await ShowContinuousPageAsync(_index);
            UpdateControls();
            await SaveProgressAsync();
            return;
        }

        ContinuousViewport.Visibility = Visibility.Collapsed;
        ContinuousImageHost.Children.Clear();
        _continuousImages.Clear();
        PageViewport.Visibility = Visibility.Visible;
        await ShowPageAsync();
    }
    private void PopulateContinuousImages()
    {
        ContinuousImageHost.Children.Clear();
        _continuousImages.Clear();
        foreach (var url in _pages)
        {
            var image = new MangaDexReaderPageImage
            {
                PageUrl = url,
                ImageProvider = _images,
                MaxWidth = GetContinuousPageWidth(),
                Margin = new Thickness(0, 8, 0, 8)
            };
            _continuousImages.Add(image);
            ContinuousImageHost.Children.Add(image);
        }
        UpdateContinuousImageWindow(_index);
    }
    private async Task ShowContinuousPageAsync(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _continuousImages.Count)
            return;

        UpdateContinuousImageWindow(pageIndex);
        ContinuousViewport.UpdateLayout();
        var firstPage = Math.Max(0, pageIndex - 1);
        var lastPage = Math.Min(_continuousImages.Count - 1, pageIndex + 3);
        await Task.WhenAll(_continuousImages
            .Skip(firstPage)
            .Take(lastPage - firstPage + 1)
            .Select(image => image.EnsureLoadedAsync()));

        if (!IsContinuous || pageIndex != _index)
            return;

        ContinuousViewport.UpdateLayout();
        var image = _continuousImages[pageIndex];
        image.BringIntoView();
        ContinuousViewport.UpdateLayout();
        var position = image.TransformToAncestor(ContinuousViewport).Transform(new Point(0, 0));
        ContinuousViewport.ScrollToVerticalOffset(Math.Max(0, ContinuousViewport.VerticalOffset + position.Y));
    }
    private double GetContinuousPageWidth()
    {
        var viewportWidth = ContinuousViewport.ViewportWidth > 0 ? ContinuousViewport.ViewportWidth : ContinuousViewport.ActualWidth;
        return viewportWidth > 0 ? viewportWidth : double.PositiveInfinity;
    }
    private void UpdateContinuousImageWindow(int centerPage)
    {
        var firstPage = Math.Max(0, centerPage - 1);
        var lastPage = Math.Min(_continuousImages.Count - 1, centerPage + 3);
        for (var index = 0; index < _continuousImages.Count; index++)
            _continuousImages[index].IsActive = index >= firstPage && index <= lastPage;
    }
    private void ContinuousViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var pageWidth = GetContinuousPageWidth();
        foreach (var image in _continuousImages)
            image.MaxWidth = pageWidth;
    }
    private async void ContinuousViewport_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (!IsContinuous || e.VerticalChange == 0)
            return;
        var visiblePage = FindFirstVisiblePage();
        if (visiblePage < 0)
            return;
        UpdateContinuousImageWindow(visiblePage);
        if (visiblePage == _index)
            return;
        _index = visiblePage;
        UpdateControls();
        await SaveProgressAsync();
    }
    private int FindFirstVisiblePage()
    {
        for (var index = 0; index < _continuousImages.Count; index++)
        {
            var image = _continuousImages[index];
            var position = image.TransformToAncestor(ContinuousViewport).Transform(new Point(0, 0));
            if (position.Y + image.ActualHeight > 0 && position.Y < ContinuousViewport.ViewportHeight)
                return index;
        }
        return -1;
    }
    private async Task NavigateToPageAsync(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _pages.Count)
            return;
        _index = pageIndex;
        if (IsContinuous)
        {
            await ShowContinuousPageAsync(pageIndex);
            UpdateControls();
            await SaveProgressAsync();
            return;
        }
        await ShowPageAsync();
    }
    private async void ReadingDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _rightToLeft = ReadingDirection.SelectedIndex == 0;
        if (_initialized && IsDoublePage) await ShowPageAsync();
    }
    private async void PageLayout_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initialized && !_updatingReaderMode && !IsContinuous) await ShowPageAsync();
    }
    private bool IsDoublePage => PageLayout.SelectedIndex == 1;
    private bool IsContinuous => ReadMode.SelectedIndex == 1;
    private int PageStep => IsDoublePage && !IsContinuous ? 2 : 1;
    private async void Previous_Click(object sender, RoutedEventArgs e) { if (_index > 0) await NavigateToPageAsync(Math.Max(0, _index - PageStep)); }
    private async void Next_Click(object sender, RoutedEventArgs e) { if (_index + PageStep < _pages.Count) await NavigateToPageAsync(_index + PageStep); }
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
        if (e.Key == Key.Space || e.Key == (_rightToLeft ? Key.Left : Key.Right) || IsContinuous && (e.Key is Key.Down or Key.PageDown))
        {
            e.Handled = true;
            if (_index + PageStep < _pages.Count) await NavigateToPageAsync(_index + PageStep);
        }
        else if (e.Key == (_rightToLeft ? Key.Right : Key.Left) || IsContinuous && (e.Key is Key.Up or Key.PageUp))
        {
            e.Handled = true;
            if (_index > 0) await NavigateToPageAsync(Math.Max(0, _index - PageStep));
        }
        else if (e.Key == Key.F11) Fullscreen_Click(sender, e);
    }
}
