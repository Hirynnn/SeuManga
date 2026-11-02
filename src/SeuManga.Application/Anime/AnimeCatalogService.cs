using SeuManga.Application.Library;
using SeuManga.Application.Streaming;
using SeuManga.Domain.Entities;
using SeuManga.Domain.Enums;
namespace SeuManga.Application.Anime;
public sealed class AnimeCatalogService(IAnimeCatalogProvider provider, ITmdbAnimeClient tmdb, IWatchmodeClient watchmode, IExternalIdentifierRepository externalIdentifiers)
{
    public Task<AnimePage> SearchAsync(string query, int page = 1, CancellationToken ct = default)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (query.Trim().Length < 2) throw new ArgumentException("Digite ao menos dois caracteres para pesquisar.", nameof(query));
        return provider.SearchAsync(query, page, ct);
    }
    public Task<AnimeDetails> GetDetailsAsync(int id, CancellationToken ct = default) => provider.GetDetailsAsync(id, ct);
    public Task<AnimePage> GetTopAnimeAsync(int page = 1, string filter = "bypopularity", int limit = 12, CancellationToken ct = default) => provider.GetTopAnimeAsync(page, filter, limit, ct);
    public Task<IReadOnlyList<AnimeEpisode>> GetEpisodesAsync(int id, int page = 1, CancellationToken ct = default)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        return provider.GetEpisodesAsync(id, page, ct);
    }
    public Task<IReadOnlyList<AnimeRecommendation>> GetRecommendationsAsync(int id, CancellationToken ct = default) => provider.GetRecommendationsAsync(id, ct);
    public async Task<TmdbAnimeMetadata?> FindTmdbMetadataAsync(AnimeSearchResult anime, CancellationToken ct = default)
    {
        var localId = anime.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var existing = await externalIdentifiers.GetAsync(LibraryContentKind.Anime, localId, "TMDB", ct);
        if (existing is not null && int.TryParse(existing.ExternalId, out var mappedId))
        {
            try
            {
                var mapped = await tmdb.GetAnimeByIdAsync(mappedId, ct);
                if (mapped is not null) return mapped;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { }
        }
        var metadata = await tmdb.FindAnimeAsync(anime.TitleEnglish ?? anime.Title, anime.Year, ct);
        if (metadata is not null)
            await externalIdentifiers.SaveAsync(new ExternalIdentifier
            {
                ContentKind = LibraryContentKind.Anime,
                LocalId = localId,
                Provider = "TMDB",
                ExternalId = metadata.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                MatchedBy = "normalized-title-japanese-animation",
                Confidence = 0.95
            }, ct);
        return metadata;
    }
    public Task<TmdbSeasonDetails> GetTmdbSeasonDetailsAsync(int seriesId, int seasonNumber, CancellationToken ct = default) => tmdb.GetSeasonDetailsAsync(seriesId, seasonNumber, ct);
    public Task<StreamingAvailability> GetStreamingAvailabilityAsync(AnimeSearchResult anime, CancellationToken ct = default) => watchmode.GetAvailabilityAsync(anime.TitleEnglish ?? anime.Title, ct);
}
