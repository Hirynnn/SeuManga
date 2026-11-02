using System.Net;
using SeuManga.Application.Abstractions;
using SeuManga.Infrastructure.TMDB;

namespace SeuManga.Application.Tests;

public sealed class TmdbClientTests
{
    [Fact]
    public async Task AnimeLookupRequiresJapaneseAnimationAndMapsBrazilianProviders()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/3/search/tv" => Json("""{"results":[{"id":42,"name":"Sample Anime","origin_country":["JP"],"genre_ids":[16],"first_air_date":"2020-04-01"}]}"""),
            "/3/tv/42" => Json("""{"id":42,"name":"Sample Anime","overview":"Source overview","origin_country":["JP"],"genres":[{"id":16,"name":"Animation"}],"seasons":[]}"""),
            _ => Json("""{"results":{"BR":{"link":"https://justwatch.example/title","flatrate":[{"provider_name":"Sample Stream","logo_path":"/logo.png"}]}}}""")
        });
        var client = new TmdbClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") }, new StaticSettings());

        var metadata = await client.FindAnimeAsync("Sample Anime", 2020);

        Assert.NotNull(metadata);
        Assert.Equal(42, metadata.Id);
        Assert.Equal("Source overview", metadata.Overview);
        var provider = Assert.Single(metadata.Providers);
        Assert.Equal("Sample Stream", provider.Name);
        Assert.Equal("Assinatura", provider.Type);
        Assert.Equal("BR", provider.Region);
        Assert.Equal("https://justwatch.example/title", provider.Url);
        Assert.Contains(handler.Requests, uri => uri.Query.Contains("language=pt-BR", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnimeLookupRejectsNonJapaneseAnimation()
    {
        var handler = new StubHandler(_ => Json("""{"results":[{"id":42,"name":"Sample Animation","origin_country":["US"],"genre_ids":[16]}]}"""));
        var client = new TmdbClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") }, new StaticSettings());

        var metadata = await client.FindAnimeAsync("Sample Animation", null);

        Assert.Null(metadata);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SeasonDetailsUseBrazilianLocaleAndParseOnlyProvidedEpisodeMetadata()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":120,"name":"Temporada 2","season_number":2,"air_date":"2024-01-01","episode_count":1,"poster_path":"/season.jpg","episodes":[{"id":998,"episode_number":1,"name":"Início","overview":"Resumo fornecido pela fonte.","air_date":"2024-01-03","runtime":24,"still_path":"/still.jpg"}] }""")
        });
        var client = new TmdbClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") }, new StaticSettings());

        var result = await client.GetSeasonDetailsAsync(456, 2);

        Assert.Equal("https://api.themoviedb.org/3/tv/456/season/2?language=pt-BR", handler.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("season-credential", handler.AuthorizationParameter);
        Assert.Equal("Temporada 2", result.Season.Name);
        var episode = Assert.Single(result.Episodes);
        Assert.Equal("Início", episode.Name);
        Assert.Equal("Resumo fornecido pela fonte.", episode.Overview);
        Assert.Equal(24, episode.Runtime);
        Assert.EndsWith("/still.jpg", episode.StillUrl);
    }

    private sealed class StaticSettings : ISecureSettingsStore
    {
        public Task SaveSecretAsync(string name, string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<string?>("season-credential");
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public List<Uri> Requests { get; } = [];
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Requests.Add(request.RequestUri!);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return Task.FromResult(respond(request));
        }
    }
}
