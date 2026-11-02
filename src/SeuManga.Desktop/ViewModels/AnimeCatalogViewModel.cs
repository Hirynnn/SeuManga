using System.Collections.ObjectModel;
using System.Net.Http;
using SeuManga.Application.Anime;
using SeuManga.Application.Library;
using SeuManga.Application.Streaming;
namespace SeuManga.Desktop.ViewModels;
public sealed class TrackedAnimeEpisode(AnimeEpisode episode, bool isWatched) : ViewModelBase
{
    private bool _isWatched = isWatched;
    public int Number => episode.Number;
    public string Title => episode.Title;
    public string? TitleJapanese => episode.TitleJapanese;
    public string? Aired => episode.Aired;
    public bool IsWatched { get => _isWatched; set => Set(ref _isWatched, value); }
}
public sealed class TmdbSeasonViewModel(TmdbSeasonInfo season) : ViewModelBase
{
    private bool _isLoading;
    private bool _isLoaded;
    private string _message = "";
    public int SeriesId { get; set; }
    public int SeasonNumber => season.SeasonNumber;
    public string Name => season.Name;
    public string? AirDate => season.AirDate;
    public int? EpisodeCount => season.EpisodeCount;
    public ObservableCollection<TmdbEpisodeInfo> Episodes { get; } = [];
    public bool IsLoading { get => _isLoading; set => Set(ref _isLoading, value); }
    public bool IsLoaded { get => _isLoaded; set => Set(ref _isLoaded, value); }
    public string Message { get => _message; set => Set(ref _message, value); }
}
public sealed class AnimeCatalogViewModel : ViewModelBase
{
    private readonly AnimeCatalogService _catalog;
    private readonly AnimeTrackingService _tracking;
    private AnimeSearchResult? _selected;
    private string _query = "";
    private string _message = "Pesquise pelo nome para consultar o catálogo Jikan.";
    private bool _busy;
    private int _page = 1;
    private int _episodePage;
    private bool _canLoadMoreEpisodes;
    private bool _isLoadingEpisodes;
    private AnimeDetails? _details;
    private TmdbAnimeMetadata? _tmdb;
    private StreamingAvailability? _availability;
    public AnimeCatalogViewModel(AnimeCatalogService catalog, AnimeTrackingService tracking)
    {
        _catalog = catalog; _tracking = tracking;
        SearchCommand = new AsyncCommand(() => SearchAsync(1), () => !IsBusy);
        NextCommand = new AsyncCommand(() => SearchAsync(Page + 1), () => !IsBusy);
        PreviousCommand = new AsyncCommand(() => SearchAsync(Page - 1), () => !IsBusy && Page > 1);
        LoadMoreEpisodesCommand = new AsyncCommand(LoadMoreEpisodesAsync, () => CanLoadMoreEpisodes && !IsLoadingEpisodes);
    }
    public string Query { get => _query; set => Set(ref _query, value); }
    public string Message { get => _message; private set => Set(ref _message, value); }
    public bool IsBusy { get => _busy; private set => Set(ref _busy, value); }
    public int Page { get => _page; private set => Set(ref _page, value); }
    public bool CanLoadMoreEpisodes
    {
        get => _canLoadMoreEpisodes;
        private set { if (Set(ref _canLoadMoreEpisodes, value)) LoadMoreEpisodesCommand.NotifyCanExecuteChanged(); }
    }
    public bool IsLoadingEpisodes
    {
        get => _isLoadingEpisodes;
        private set { if (Set(ref _isLoadingEpisodes, value)) LoadMoreEpisodesCommand.NotifyCanExecuteChanged(); }
    }
    public AnimeDetails? Details { get => _details; private set => Set(ref _details, value); }
    public TmdbAnimeMetadata? Tmdb { get => _tmdb; private set => Set(ref _tmdb, value); }
    public StreamingAvailability? Availability { get => _availability; private set => Set(ref _availability, value); }
    public string ProgressLabel => Episodes.Count == 0 ? "" : $"{Episodes.Count(x => x.IsWatched)} de {Episodes.Count} episódios assistidos nesta página";
    public ObservableCollection<AnimeSearchResult> Results { get; } = [];
    public ObservableCollection<TrackedAnimeEpisode> Episodes { get; } = [];
    public ObservableCollection<AnimeRecommendation> Recommendations { get; } = [];
    public ObservableCollection<TmdbSeasonViewModel> TmdbSeasons { get; } = [];
    public AsyncCommand SearchCommand { get; }
    public AsyncCommand NextCommand { get; }
    public AsyncCommand PreviousCommand { get; }
    public AsyncCommand LoadMoreEpisodesCommand { get; }
    private async Task SearchAsync(int page)
    {
        IsBusy = true; Message = "Consultando Jikan…";
        try { var result = await _catalog.SearchAsync(Query, page); Results.Clear(); foreach (var anime in result.Items) Results.Add(anime); Page = page; Message = result.Items.Count == 0 ? "Nenhum resultado encontrado." : $"Página {Page} de {result.TotalPages}"; }
        catch (HttpRequestException ex) { Message = ProviderErrorMessage("Jikan", ex); Results.Clear(); }
        catch (ArgumentException ex) { Message = ex.Message; Results.Clear(); }
        catch (TaskCanceledException) { Message = "Jikan não respondeu a tempo. Verifique a conexão e tente novamente."; Results.Clear(); }
        catch { Message = "Não foi possível consultar Jikan. Tente novamente."; Results.Clear(); }
        finally { IsBusy = false; }
    }
    public async Task SelectAnimeAsync(AnimeSearchResult? anime)
    {
        if (anime is null) return;
        _selected = anime; Message = "Carregando informações e episódios…";
        Episodes.Clear(); _episodePage = 0; CanLoadMoreEpisodes = false;
        try
        {
            Details = await _catalog.GetDetailsAsync(anime.Id);
            IsLoadingEpisodes = true;
            IReadOnlyList<AnimeEpisode> episodes;
            try { episodes = await _catalog.GetEpisodesAsync(anime.Id, 1); }
            finally { IsLoadingEpisodes = false; }
            _episodePage = 1;
            CanLoadMoreEpisodes = episodes.Count >= 100;
            var recommendationsTask = _catalog.GetRecommendationsAsync(anime.Id);
            var watched = await _tracking.GetWatchedEpisodesAsync(anime.Id);
            foreach (var ep in episodes.Where(x => x.Number > 0)) Episodes.Add(new TrackedAnimeEpisode(ep, watched.Contains(ep.Number)));
            try { Recommendations.Clear(); foreach (var recommendation in await recommendationsTask) Recommendations.Add(recommendation); } catch { Recommendations.Clear(); }
            Notify(nameof(ProgressLabel));
            try
            {
                Tmdb = await _catalog.FindTmdbMetadataAsync(anime);
                TmdbSeasons.Clear();
                if (Tmdb is not null) foreach (var season in Tmdb.Seasons) TmdbSeasons.Add(new TmdbSeasonViewModel(season) { SeriesId = Tmdb.Id });
            }
            catch { Tmdb = null; TmdbSeasons.Clear(); }
            try { Availability = await _catalog.GetStreamingAvailabilityAsync(anime); } catch { Availability = new(false, false, "Não foi possível confirmar disponibilidade no Brasil.", []); }
            Message = Episodes.Count == 0 ? "Dados fornecidos por Jikan. Episódios não disponíveis." : $"Dados fornecidos por Jikan · {Episodes.Count} episódios nesta página";
        }
        catch (HttpRequestException ex) { Details = null; Tmdb = null; Availability = null; Episodes.Clear(); Recommendations.Clear(); Message = ProviderErrorMessage("Jikan", ex); }
        catch (TaskCanceledException) { Details = null; Tmdb = null; Availability = null; Episodes.Clear(); Recommendations.Clear(); Message = "Jikan não respondeu a tempo. Verifique a conexão e tente novamente."; }
        catch { Details = null; Tmdb = null; Availability = null; Episodes.Clear(); Recommendations.Clear(); Message = "Não foi possível carregar os detalhes deste anime."; }
    }
    private async Task LoadMoreEpisodesAsync()
    {
        if (_selected is null || !CanLoadMoreEpisodes || IsLoadingEpisodes) return;
        IsLoadingEpisodes = true;
        try
        {
            var episodes = await _catalog.GetEpisodesAsync(_selected.Id, _episodePage + 1);
            var watched = await _tracking.GetWatchedEpisodesAsync(_selected.Id);
            _episodePage++;
            foreach (var episode in episodes.Where(x => x.Number > 0))
                Episodes.Add(new TrackedAnimeEpisode(episode, watched.Contains(episode.Number)));
            CanLoadMoreEpisodes = episodes.Count >= 100;
            Message = $"Dados fornecidos por Jikan · {Episodes.Count} episódios carregados";
            Notify(nameof(ProgressLabel));
        }
        catch (HttpRequestException ex) { Message = ProviderErrorMessage("Jikan", ex); }
        catch (TaskCanceledException) { Message = "Jikan não respondeu a tempo. Verifique a conexão e tente novamente."; }
        finally { IsLoadingEpisodes = false; }
    }
    public async Task AddSelectedAnimeAsync()
    {
        if (_selected is null) return;
        await _tracking.AddAsync(_selected.Id, _selected.Title, _selected.ImageUrl, _selected.Episodes);
        Message = "Anime adicionado à biblioteca local.";
    }
    public async Task ToggleEpisodeAsync(TrackedAnimeEpisode episode)
    {
        if (_selected is null) return;
        await _tracking.AddAsync(_selected.Id, _selected.Title, _selected.ImageUrl, _selected.Episodes);
        var watched = !episode.IsWatched;
        await _tracking.SetEpisodeWatchedAsync(_selected.Id, episode.Number, watched);
        episode.IsWatched = watched; Notify(nameof(ProgressLabel));
    }
    public async Task LoadTmdbSeasonAsync(TmdbSeasonViewModel season)
    {
        if (season.IsLoaded || season.IsLoading) return;
        season.IsLoading = true; season.Message = "Carregando episódios do TMDB…";
        try
        {
            var details = await _catalog.GetTmdbSeasonDetailsAsync(season.SeriesId, season.SeasonNumber);
            season.Episodes.Clear(); foreach (var episode in details.Episodes) season.Episodes.Add(episode);
            season.IsLoaded = true; season.Message = details.Episodes.Count == 0 ? "O TMDB não retornou episódios para esta temporada." : $"{details.Episodes.Count} episódios · TMDB";
        }
        catch { season.Message = "Não foi possível carregar os episódios desta temporada."; }
        finally { season.IsLoading = false; }
    }
}
