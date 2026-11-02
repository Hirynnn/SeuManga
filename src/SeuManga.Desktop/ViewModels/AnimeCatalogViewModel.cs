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
    private AnimeDetails? _details;
    private TmdbAnimeMetadata? _tmdb;
    private StreamingAvailability? _availability;
    public AnimeCatalogViewModel(AnimeCatalogService catalog, AnimeTrackingService tracking)
    {
        _catalog = catalog; _tracking = tracking;
        SearchCommand = new AsyncCommand(() => SearchAsync(1), () => !IsBusy);
        NextCommand = new AsyncCommand(() => SearchAsync(Page + 1), () => !IsBusy);
        PreviousCommand = new AsyncCommand(() => SearchAsync(Page - 1), () => !IsBusy && Page > 1);
    }
    public string Query { get => _query; set => Set(ref _query, value); }
    public string Message { get => _message; private set => Set(ref _message, value); }
    public bool IsBusy { get => _busy; private set => Set(ref _busy, value); }
    public int Page { get => _page; private set => Set(ref _page, value); }
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
    private async Task SearchAsync(int page)
    {
        IsBusy = true; Message = "Consultando Jikan…";
        try { var result = await _catalog.SearchAsync(Query, page); Results.Clear(); foreach (var anime in result.Items) Results.Add(anime); Page = page; Message = result.Items.Count == 0 ? "Nenhum resultado encontrado." : $"Página {Page} de {result.TotalPages}"; }
        catch (Exception ex) when (ex is ArgumentException or HttpRequestException) { Message = ex.Message; Results.Clear(); }
        catch { Message = "Não foi possível consultar Jikan. Tente novamente."; Results.Clear(); }
        finally { IsBusy = false; }
    }
    public async Task SelectAnimeAsync(AnimeSearchResult? anime)
    {
        if (anime is null) return;
        _selected = anime; Message = "Carregando informações e episódios…";
        try
        {
            Details = await _catalog.GetDetailsAsync(anime.Id);
            var episodes = await _catalog.GetEpisodesAsync(anime.Id);
            var recommendationsTask = _catalog.GetRecommendationsAsync(anime.Id);
            var watched = await _tracking.GetWatchedEpisodesAsync(anime.Id);
            Episodes.Clear(); foreach (var ep in episodes.Where(x => x.Number > 0)) Episodes.Add(new TrackedAnimeEpisode(ep, watched.Contains(ep.Number)));
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
        catch { Details = null; Tmdb = null; Availability = null; Episodes.Clear(); Recommendations.Clear(); Message = "Não foi possível carregar os detalhes deste anime."; }
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



