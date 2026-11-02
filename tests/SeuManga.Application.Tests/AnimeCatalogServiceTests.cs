using SeuManga.Application.Anime;
using SeuManga.Application.Library;
using SeuManga.Application.Streaming;

namespace SeuManga.Application.Tests;

public sealed class AnimeCatalogServiceTests
{
    [Fact]
    public async Task GetEpisodesFetchesOnlyRequestedPage()
    {
        var provider = new FakeAnimeProvider();
        var service = new AnimeCatalogService(provider, new FakeTmdbClient(), new FakeWatchmodeClient(), new FakeIdentifierRepository());

        var episodes = await service.GetEpisodesAsync(42, 3);

        Assert.Equal(100, episodes.Count);
        Assert.Equal([3], provider.RequestedPages);
    }

    private sealed class FakeAnimeProvider : IAnimeCatalogProvider
    {
        public List<int> RequestedPages { get; } = [];

        public Task<IReadOnlyList<AnimeEpisode>> GetEpisodesAsync(int animeId, int page = 1, CancellationToken cancellationToken = default)
        {
            RequestedPages.Add(page);
            IReadOnlyList<AnimeEpisode> episodes = Enumerable.Range(1, 100)
                .Select(number => new AnimeEpisode(number, number, $"Episode {number}", null, null, false, false))
                .ToArray();
            return Task.FromResult(episodes);
        }

        public Task<AnimePage> SearchAsync(string query, int page = 1, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AnimePage> GetTopAnimeAsync(int page = 1, string filter = "bypopularity", int limit = 12, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AnimeDetails> GetDetailsAsync(int animeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AnimeRecommendation>> GetRecommendationsAsync(int animeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeTmdbClient : ITmdbAnimeClient
    {
        public Task<TmdbAnimeMetadata?> FindAnimeAsync(string title, int? year, CancellationToken cancellationToken = default) => Task.FromResult<TmdbAnimeMetadata?>(null);
        public Task<TmdbAnimeMetadata?> GetAnimeByIdAsync(int tmdbId, CancellationToken cancellationToken = default) => Task.FromResult<TmdbAnimeMetadata?>(null);
        public Task<TmdbSeasonDetails> GetSeasonDetailsAsync(int seriesId, int seasonNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeWatchmodeClient : IWatchmodeClient
    {
        public Task<StreamingAvailability> GetAvailabilityAsync(string title, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StreamingAvailability(false, false, "", []));
    }

    private sealed class FakeIdentifierRepository : IExternalIdentifierRepository
    {
        public Task<SeuManga.Domain.Entities.ExternalIdentifier?> GetAsync(SeuManga.Domain.Enums.LibraryContentKind contentKind, string localId, string provider, CancellationToken cancellationToken = default) =>
            Task.FromResult<SeuManga.Domain.Entities.ExternalIdentifier?>(null);
        public Task SaveAsync(SeuManga.Domain.Entities.ExternalIdentifier identifier, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
