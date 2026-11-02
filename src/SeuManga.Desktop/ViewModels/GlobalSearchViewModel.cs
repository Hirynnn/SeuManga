using System.Collections.ObjectModel;
using SeuManga.Application.Anime;
using SeuManga.Application.Manga;
namespace SeuManga.Desktop.ViewModels;
public sealed class GlobalSearchViewModel : ViewModelBase
{
    private readonly MangaCatalogService _mangaCatalog;
    private readonly AnimeCatalogService _animeCatalog;
    private string _query = "";
    private string _message = "Pesquise mangás e animes em um só lugar.";
    private bool _busy;
    private CancellationTokenSource? _activeSearch;
    private CancellationTokenSource? _debounce;
    private readonly List<string> _recentQueries = [];
    public GlobalSearchViewModel(MangaCatalogService mangaCatalog, AnimeCatalogService animeCatalog)
    {
        _mangaCatalog = mangaCatalog; _animeCatalog = animeCatalog;
        SearchCommand = new AsyncCommand(SearchNowAsync, () => !IsBusy);
    }
    public string Query
    {
        get => _query;
        set
        {
            if (!Set(ref _query, value)) return;
            _activeSearch?.Cancel();
            _debounce?.Cancel();
            UpdateSuggestions(value);
            if (value.Trim().Length < 2)
            {
                Mangas.Clear(); Animes.Clear(); Message = "Digite ao menos dois caracteres.";
                return;
            }
            var debounce = new CancellationTokenSource();
            _debounce = debounce;
            _ = DebounceAsync(debounce);
        }
    }
    private void RememberQuery(string query)
    {
        _recentQueries.RemoveAll(x => string.Equals(x, query, StringComparison.OrdinalIgnoreCase));
        _recentQueries.Insert(0, query);
        if (_recentQueries.Count > 8) _recentQueries.RemoveAt(_recentQueries.Count - 1);
        UpdateSuggestions(Query);
    }
    private void UpdateSuggestions(string query)
    {
        var normalized = query.Trim();
        var matches = normalized.Length < 2 ? Array.Empty<string>() : _recentQueries.Where(x => x.Contains(normalized, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (Suggestions.SequenceEqual(matches, StringComparer.Ordinal)) return;
        Suggestions.Clear();
        foreach (var suggestion in matches) Suggestions.Add(suggestion);
        Notify(nameof(HasSuggestions));
    }
    public string Message { get => _message; private set => Set(ref _message, value); }
    public bool IsBusy { get => _busy; private set => Set(ref _busy, value); }
    public ObservableCollection<MangaSearchResult> Mangas { get; } = [];
    public ObservableCollection<AnimeSearchResult> Animes { get; } = [];
    public ObservableCollection<string> Suggestions { get; } = [];
    public bool HasSuggestions => Suggestions.Count > 0;
    public AsyncCommand SearchCommand { get; }
    private async Task DebounceAsync(CancellationTokenSource debounce)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(450), debounce.Token);
            if (!ReferenceEquals(_debounce, debounce)) return;
            _debounce = null;
            await SearchNowAsync();
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_debounce, debounce)) _debounce = null;
            debounce.Dispose();
        }
    }
    private async Task SearchNowAsync()
    {
        if (Query.Trim().Length < 2) { Message = "Digite ao menos dois caracteres."; return; }
        _debounce?.Cancel(); _debounce = null;
        _activeSearch?.Cancel();
        var request = new CancellationTokenSource();
        _activeSearch = request;
        var query = Query.Trim();
        IsBusy = true; Message = "Pesquisando nos catálogos…";
        try
        {
            var mangaTask = SafeMangaAsync(query, request.Token);
            var animeTask = SafeAnimeAsync(query, request.Token);
            await Task.WhenAll(mangaTask, animeTask);
            request.Token.ThrowIfCancellationRequested();
            var manga = await mangaTask; var anime = await animeTask;
            Mangas.Clear(); foreach (var item in manga.Page?.Items ?? []) Mangas.Add(item);
            Animes.Clear(); foreach (var item in anime.Page?.Items ?? []) Animes.Add(item);
            if (manga.Page is not null || anime.Page is not null) RememberQuery(query);
            var failures = new[] { manga.Error, anime.Error }.Where(x => x is not null).ToArray();
            Message = failures.Length == 0 ? $"{Mangas.Count} mangás · {Animes.Count} animes encontrados" : $"Pesquisa parcial · {Mangas.Count} mangás · {Animes.Count} animes · {string.Join("; ", failures)}";
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_activeSearch, request)) { _activeSearch = null; IsBusy = false; }
            request.Dispose();
        }
    }
    private async Task<(MangaPage? Page, string? Error)> SafeMangaAsync(string query, CancellationToken cancellationToken)
    {
        try { return (await _mangaCatalog.SearchAsync(query, 1, 24, cancellationToken: cancellationToken), null); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return (null, "MangaDex indisponível"); }
    }
    private async Task<(AnimePage? Page, string? Error)> SafeAnimeAsync(string query, CancellationToken cancellationToken)
    {
        try { return (await _animeCatalog.SearchAsync(query, 1, cancellationToken), null); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return (null, "Jikan indisponível"); }
    }
}
