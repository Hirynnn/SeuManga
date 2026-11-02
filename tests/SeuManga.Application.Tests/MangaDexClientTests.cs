using System.Net;
using SeuManga.Application.Abstractions;
using SeuManga.Application.Manga;
using SeuManga.Infrastructure.MangaDex;
using Microsoft.Extensions.Caching.Memory;

namespace SeuManga.Application.Tests;

public sealed class MangaDexClientTests
{
    [Fact]
    public async Task SearchParsesTitlesAndPaginates()
    {
        var handler = new StubHandler(_ => Json("""{"data":[{"id":"m-1","attributes":{"title":{"en":"Example"},"description":{"en":"Summary"},"status":"completed","originalLanguage":"ja","tags":[]},"relationships":[]}],"total":51,"offset":24,"limit":24}"""));
        var client = new MangaDexClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.mangadex.org/") });
        var result = await client.SearchAsync("Example Title", 24, 24);
        Assert.Equal("Example", result.Items[0].Title);
        Assert.Equal(51, result.Total);
        Assert.Contains("title=Example%20Title", handler.LastUri!.Query);
        Assert.Contains("offset=24", handler.LastUri.Query);
    }

    [Fact]
    public async Task AtHomeBuildsImageUrlsInProvidedOrder()
    {
        var body = """{"baseUrl":"https://reader.mangadex.network/token","chapter":{"hash":"abc","data":["001.jpg","002 page.jpg"],"dataSaver":["low-001.jpg"]}}""";
        var client = new MangaDexClient(new HttpClient(new StubHandler(_ => Json(body))) { BaseAddress = new Uri("https://api.mangadex.org/") });
        var urls = await client.GetChapterPageUrlsAsync("chapter-id");
        Assert.Equal(["https://reader.mangadex.network/token/data/abc/001.jpg", "https://reader.mangadex.network/token/data/abc/002%20page.jpg"], urls);
    }

    [Fact]
    public async Task RateLimitReturnsFriendlyProviderMessage()
    {
        var client = new MangaDexClient(new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests))) { BaseAddress = new Uri("https://api.mangadex.org/") });
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync("test", 0, 10));
        Assert.Contains("limitou", error.Message);
    }

    [Fact]
    public async Task CatalogServiceValidatesPageSize()
    {
        var service = new MangaCatalogService(new FakeProvider());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SearchAsync("test", 1, 101));
    }

    [Fact]
    public async Task SearchAppliesAdvancedFiltersAndResolvesTagAndAuthorNames()
    {
        var handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/manga/tag", StringComparison.Ordinal))
                return Json("""{"data":[{"id":"tag-action","attributes":{"name":{"en":"Action","pt-br":"Ação"}}},{"id":"tag-comedy","attributes":{"name":{"en":"Comedy"}}}]}""");
            if (path.EndsWith("/author", StringComparison.Ordinal))
                return Json("""{"data":[{"id":"author-1","attributes":{"name":"Sample Author"}}]}""");
            return Json("""{"data":[],"total":0,"offset":0,"limit":24}""");
        });
        var client = new MangaDexClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.mangadex.org/") }, new MemoryCache(new MemoryCacheOptions()));
        var filters = new MangaSearchFilters(
            TranslatedLanguage: "pt-br", Author: "Sample Author", IncludedTags: ["Ação", "Comedy"],
            IncludedTagsMode: "AND", ContentRatings: ["safe", "suggestive"], Sort: "followedCount", Year: 2010, Demographic: "shounen");

        await client.SearchAsync("Sample", 0, 24, "ja", "completed", filters: filters);

        var searchUri = handler.Requests.Single(x => x.AbsolutePath.EndsWith("/manga", StringComparison.Ordinal) && x.Query.Contains("title=")).Query;
        Assert.Contains("originalLanguage[]=ja", searchUri);
        Assert.Contains("availableTranslatedLanguage[]=pt-br", searchUri);
        Assert.Contains("authors[]=author-1", searchUri);
        Assert.Contains("includedTags[]=tag-action", searchUri);
        Assert.Contains("includedTags[]=tag-comedy", searchUri);
        Assert.Contains("includedTagsMode=AND", searchUri);
        Assert.Contains("contentRating[]=safe", searchUri);
        Assert.Contains("order[followedCount]=desc", searchUri);
        Assert.Contains("year=2010", searchUri);
        Assert.Contains("publicationDemographic[]=shounen", searchUri);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { LastUri = request.RequestUri; Requests.Add(request.RequestUri!); return Task.FromResult(respond(request)); }
    }
    private sealed class FakeProvider : IMangaCatalogProvider
    {
        public Task<MangaPage> SearchAsync(string? title, int offset, int limit, string? originalLanguage = null, string? status = null, CancellationToken cancellationToken = default, MangaSearchFilters? filters = null) => Task.FromResult(new MangaPage([], 0, offset, limit));
        public Task<SeuManga.Application.Anime.AnimePage> GetTopAnimeAsync(int page = 1, string filter = "bypopularity", int limit = 12, CancellationToken cancellationToken = default) => Task.FromResult(new SeuManga.Application.Anime.AnimePage([], 0, page));
        public Task<MangaDetails> GetDetailsAsync(string mangaId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<MangaChapterPage> GetChaptersAsync(string mangaId, string language, int offset, int limit, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SeuManga.Application.Anime.AnimeRecommendation>> GetRecommendationsAsync(int animeId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SeuManga.Application.Anime.AnimeRecommendation>>([]);
        public Task<IReadOnlyList<string>> GetChapterPageUrlsAsync(string chapterId, bool dataSaver = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}



