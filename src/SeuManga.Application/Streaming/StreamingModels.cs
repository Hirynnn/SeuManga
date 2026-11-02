namespace SeuManga.Application.Streaming;
public sealed record StreamingSource(string Name, string Type, string Region, string? Url, string? LogoUrl, string Attribution);
public sealed record StreamingAvailability(bool IsConfigured, bool BrazilSupported, string Message, IReadOnlyList<StreamingSource> Sources);
public interface IWatchmodeClient { Task<StreamingAvailability> GetAvailabilityAsync(string title, CancellationToken cancellationToken = default); }
public interface ITmdbAnimeClient
{
    Task<TmdbAnimeMetadata?> FindAnimeAsync(string title, int? year, CancellationToken cancellationToken = default);
    Task<TmdbAnimeMetadata?> GetAnimeByIdAsync(int tmdbId, CancellationToken cancellationToken = default);
    Task<TmdbSeasonDetails> GetSeasonDetailsAsync(int seriesId, int seasonNumber, CancellationToken cancellationToken = default);
}
public sealed record TmdbAnimeMetadata(int Id, string Name, string? Overview, string? PosterUrl, string? BackdropUrl, int? EpisodeCount, string? Status, IReadOnlyList<string> Genres, IReadOnlyList<string> Studios, IReadOnlyList<TmdbSeasonInfo> Seasons, IReadOnlyList<StreamingSource> Providers, string Attribution);
public sealed record TmdbSeasonInfo(int Id, int SeasonNumber, string Name, string? Overview, string? AirDate, int? EpisodeCount, string? PosterUrl);
public sealed record TmdbEpisodeInfo(int Id, int EpisodeNumber, string Name, string? Overview, string? AirDate, int? Runtime, string? StillUrl);
public sealed record TmdbSeasonDetails(TmdbSeasonInfo Season, IReadOnlyList<TmdbEpisodeInfo> Episodes);
