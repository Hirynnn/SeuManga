using System.Net;
using SeuManga.Infrastructure.Jikan;
using SeuManga.Infrastructure.MangaDex;
using SeuManga.Infrastructure.Watchmode;
using SeuManga.Application.Abstractions;
namespace SeuManga.Application.Tests;
public sealed class ExternalProviderTests
{
    [Fact]
    public async Task JikanMapsAnimeSearchAndMissingFieldsSafely()
    {
        const string response = """{"data":[{"mal_id":21,"title":"Sample Anime","title_english":"Example","synopsis":null,"images":{"jpg":{"image_url":"https://cdn.example/poster.jpg","large_image_url":"https://cdn.example/large.jpg"}},"status":"Finished Airing","episodes":12,"score":8.2,"url":"https://myanimelist.net/anime/21","aired":{"prop":{"from":{"year":2024}}}}],"pagination":{"last_visible_page":3,"current_page":1}}""";
        var client = new JikanClient(new HttpClient(new StubHandler(_ => Json(response))) { BaseAddress = new Uri("https://api.jikan.moe/v4/") });
        var page = await client.SearchAsync("Sample");
        Assert.Equal(3, page.TotalPages); Assert.Equal("Sample Anime", page.Items[0].Title); Assert.Equal(2024, page.Items[0].Year); Assert.Equal("Sinopse não disponível.", page.Items[0].Synopsis);
    }

    [Fact]
    public async Task MangaDexImageClientRejectsNonProviderHosts()
    {
        var client = new MangaDexImageClient(new HttpClient(new StubHandler(_ => Json("{}"))));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync("https://untrusted.example/page.jpg"));
    }

    [Fact]
    public async Task MangaDexImageClientRequiresAnImageResponse()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not an image", System.Text.Encoding.UTF8, "text/html") });
        var client = new MangaDexImageClient(new HttpClient(handler));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync("https://node.mangadex.network/page.jpg"));
        Assert.Equal("https://mangadex.org/", handler.LastRequest!.Headers.Referrer!.ToString());
    }

    [Fact]
    public async Task WatchmodeRequestsBrazilAndMapsConfirmedSources()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/search/", StringComparison.Ordinal)
            ? Json("""{"title_results":[{"id":123,"name":"Sample Anime"}]}""")
            : Json("""[{"name":"Sample Stream","type":"sub","web_url":"https://stream.example/title","logo_100px":"https://stream.example/logo.png"}]"""));
        var client = new WatchmodeClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.watchmode.com/v1/") },
            new StaticSettings("watchmode-key"));

        var availability = await client.GetAvailabilityAsync("Sample Anime");

        Assert.True(availability.IsConfigured);
        Assert.True(availability.BrazilSupported);
        var source = Assert.Single(availability.Sources);
        Assert.Equal("Sample Stream", source.Name);
        Assert.Equal("Assinatura", source.Type);
        Assert.Equal("BR", source.Region);
        Assert.Equal("https://stream.example/title", source.Url);
        Assert.Contains("regions=BR", handler.Requests[1].Query);
    }

    [Fact]
    public async Task WatchmodeWithoutCredentialDoesNotMakeNetworkRequests()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("No request should be sent."));
        var client = new WatchmodeClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.watchmode.com/v1/") },
            new StaticSettings(null));

        var availability = await client.GetAvailabilityAsync("Sample Anime");

        Assert.False(availability.IsConfigured);
        Assert.Empty(handler.Requests);
        Assert.Empty(availability.Sources);
    }

    [Fact]
    public async Task WatchmodeDoesNotClaimRegionalCoverageAfterForbiddenResponse()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/search/", StringComparison.Ordinal)
            ? Json("""{"title_results":[{"id":123,"name":"Sample Anime"}]}""")
            : new HttpResponseMessage(HttpStatusCode.Forbidden));
        var client = new WatchmodeClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.watchmode.com/v1/") },
            new StaticSettings("watchmode-key"));

        var availability = await client.GetAvailabilityAsync("Sample Anime");

        Assert.False(availability.BrazilSupported);
        Assert.Empty(availability.Sources);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };
    private sealed class StaticSettings(string? value) : ISecureSettingsStore
    {
        public Task SaveSecretAsync(string name, string secret, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(value);
    }
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { LastRequest = request; Requests.Add(request.RequestUri!); return Task.FromResult(respond(request)); }
    }
}
