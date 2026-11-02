namespace SeuManga.Application.Anime;
public sealed record AnimeSearchResult(int Id, string Title, string? TitleEnglish, string Synopsis, string? ImageUrl, string Status, string? Season, int? Year, int? Episodes, double? Score, string Url);
public sealed record AnimePage(IReadOnlyList<AnimeSearchResult> Items, int TotalPages, int CurrentPage);
public sealed record AnimeEpisode(int Id, int Number, string Title, string? TitleJapanese, string? Aired, bool Filler, bool Recap);
public sealed record AnimeDetails(AnimeSearchResult Anime, IReadOnlyList<string> Genres, IReadOnlyList<string> Studios, string? Rating, string? Duration);
public sealed record AnimeRecommendation(AnimeSearchResult Anime, int Votes);
public interface IAnimeCatalogProvider
{
    Task<AnimePage> SearchAsync(string query, int page = 1, CancellationToken cancellationToken = default);
    Task<AnimePage> GetTopAnimeAsync(int page = 1, string filter = "bypopularity", int limit = 12, CancellationToken cancellationToken = default);
    Task<AnimeDetails> GetDetailsAsync(int animeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnimeEpisode>> GetEpisodesAsync(int animeId, int page = 1, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnimeRecommendation>> GetRecommendationsAsync(int animeId, CancellationToken cancellationToken = default);
}


