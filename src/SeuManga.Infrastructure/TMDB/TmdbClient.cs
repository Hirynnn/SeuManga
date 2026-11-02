using System.Net.Http.Headers;
using System.Text.Json;
using SeuManga.Application.Abstractions;
using SeuManga.Application.Streaming;
namespace SeuManga.Infrastructure.TMDB;
public sealed class TmdbClient(HttpClient http, ISecureSettingsStore settings) : ITmdbAnimeClient
{
    private const string Attribution = "Dados e imagens fornecidos por TMDB. Disponibilidade de streaming: JustWatch.";
    public async Task<TmdbAnimeMetadata?> FindAnimeAsync(string title, int? year, CancellationToken cancellationToken = default)
    {
        var key = await settings.GetSecretAsync("TMDB", cancellationToken);
        if (string.IsNullOrWhiteSpace(key)) return null;
        using var searchRequest = new HttpRequestMessage(HttpMethod.Get, $"search/tv?query={Uri.EscapeDataString(title)}&language=pt-BR&include_adult=false");
        searchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var searchResponse = await http.SendAsync(searchRequest, cancellationToken); searchResponse.EnsureSuccessStatusCode();
        using var searchDoc = JsonDocument.Parse(await searchResponse.Content.ReadAsStreamAsync(cancellationToken));
        if (!searchDoc.RootElement.TryGetProperty("results", out var results)) return null;
        var candidate = results.EnumerateArray().Where(IsAnime)
            .Select(x => new { Element = x, Name = Text(x, "name") ?? "", Date = Text(x, "first_air_date") })
            .Where(x => IsTitleMatch(title, x.Name, Text(x.Element, "original_name")))
            .OrderByDescending(x => year.HasValue && DateTime.TryParse(x.Date, out var d) && d.Year == year.Value)
            .FirstOrDefault();
        if (candidate is null || !candidate.Element.TryGetProperty("id", out var idElement) || !idElement.TryGetInt32(out var id)) return null;
        return await GetAnimeByIdAsync(id, key, candidate.Name, cancellationToken);
    }
    public async Task<TmdbAnimeMetadata?> GetAnimeByIdAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        var key = await settings.GetSecretAsync("TMDB", cancellationToken);
        if (string.IsNullOrWhiteSpace(key)) return null;
        return await GetAnimeByIdAsync(tmdbId, key, null, cancellationToken);
    }
    private async Task<TmdbAnimeMetadata?> GetAnimeByIdAsync(int id, string key, string? fallbackName, CancellationToken cancellationToken)
    {
        using var detailsRequest = new HttpRequestMessage(HttpMethod.Get, $"tv/{id}?language=pt-BR");
        detailsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var detailsResponse = await http.SendAsync(detailsRequest, cancellationToken); detailsResponse.EnsureSuccessStatusCode();
        using var detailsDoc = JsonDocument.Parse(await detailsResponse.Content.ReadAsStreamAsync(cancellationToken));
        var detail = detailsDoc.RootElement;
        if (!IsAnimeDetail(detail)) return null;
        using var providersRequest = new HttpRequestMessage(HttpMethod.Get, $"tv/{id}/watch/providers");
        providersRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var providersResponse = await http.SendAsync(providersRequest, cancellationToken); providersResponse.EnsureSuccessStatusCode();
        using var providersDoc = JsonDocument.Parse(await providersResponse.Content.ReadAsStreamAsync(cancellationToken));
        return new TmdbAnimeMetadata(id, Text(detail, "name") ?? fallbackName ?? "Título TMDB", Text(detail, "overview"), Image(Text(detail, "poster_path")), Image(Text(detail, "backdrop_path")), Number(detail, "number_of_episodes"), Text(detail, "status"),
            Names(detail, "genres"), Names(detail, "production_companies"), ReadSeasons(detail), ReadProviders(providersDoc.RootElement), Attribution);
    }
    public async Task<TmdbSeasonDetails> GetSeasonDetailsAsync(int seriesId, int seasonNumber, CancellationToken cancellationToken = default)
    {
        var key = await settings.GetSecretAsync("TMDB", cancellationToken);
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Configure uma chave TMDB para consultar episódios e temporadas.");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"tv/{seriesId}/season/{seasonNumber}?language=pt-BR");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await http.SendAsync(request, cancellationToken); response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = doc.RootElement;
        var season = ParseSeason(root);
        var episodes = root.TryGetProperty("episodes", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(x => new TmdbEpisodeInfo(Number(x, "id") ?? 0, Number(x, "episode_number") ?? 0,
                Text(x, "name") ?? $"Episódio {Number(x, "episode_number") ?? 0}", Text(x, "overview"), Text(x, "air_date"),
                Number(x, "runtime"), Image(Text(x, "still_path")))).ToArray()
            : [];
        return new TmdbSeasonDetails(season, episodes);
    }
    private static bool IsAnime(JsonElement e)
    {
        var japanese = e.TryGetProperty("origin_country", out var origins) && origins.EnumerateArray().Any(x => x.GetString() == "JP");
        var animated = e.TryGetProperty("genre_ids", out var genres) && genres.EnumerateArray().Any(x => x.GetInt32() == 16);
        return japanese && animated;
    }
    private static bool IsAnimeDetail(JsonElement e)
    {
        var japanese = e.TryGetProperty("origin_country", out var origins) && origins.EnumerateArray().Any(x => x.GetString() == "JP");
        var animated = e.TryGetProperty("genres", out var genres) && genres.EnumerateArray().Any(x => Number(x, "id") == 16);
        return japanese && animated;
    }
    private static bool IsTitleMatch(string wanted, params string?[] candidates)
    {
        static string Normalize(string? value) => new((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        var w = Normalize(wanted);
        return w.Length >= 3 && candidates.Select(Normalize).Any(x => x == w || (x.Length >= 5 && (x.Contains(w, StringComparison.Ordinal) || w.Contains(x, StringComparison.Ordinal))));
    }
    private static IReadOnlyList<StreamingSource> ReadProviders(JsonElement root)
    {
        if (!root.TryGetProperty("results", out var regions) || !regions.TryGetProperty("BR", out var brazil)) return [];
        var link = Text(brazil, "link"); var sources = new List<StreamingSource>();
        foreach (var (property, kind) in new[] { ("flatrate", "Assinatura"), ("free", "Gratuito"), ("rent", "Aluguel"), ("buy", "Compra") })
        {
            if (!brazil.TryGetProperty(property, out var entries) || entries.ValueKind != JsonValueKind.Array) continue;
            foreach (var entry in entries.EnumerateArray())
            {
                var logo = Text(entry, "logo_path");
                sources.Add(new StreamingSource(Text(entry, "provider_name") ?? "Plataforma", kind, "BR", link, logo is null ? null : $"https://image.tmdb.org/t/p/w92{logo}", "JustWatch (via TMDB)"));
            }
        }
        return sources;
    }
    private static IReadOnlyList<TmdbSeasonInfo> ReadSeasons(JsonElement root) => root.TryGetProperty("seasons", out var seasons) && seasons.ValueKind == JsonValueKind.Array
        ? seasons.EnumerateArray().Select(ParseSeason).ToArray() : [];
    private static TmdbSeasonInfo ParseSeason(JsonElement season) => new(Number(season, "id") ?? 0, Number(season, "season_number") ?? 0,
        Text(season, "name") ?? $"Temporada {Number(season, "season_number") ?? 0}", Text(season, "overview"), Text(season, "air_date"),
        Number(season, "episode_count"), Image(Text(season, "poster_path")));
    private static string? Image(string? path) => path is null ? null : $"https://image.tmdb.org/t/p/w780{path}";
    private static IReadOnlyList<string> Names(JsonElement e, string field) => e.TryGetProperty(field, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Select(x => Text(x, "name")).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray() : [];
    private static string? Text(JsonElement e, string field) => e.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static int? Number(JsonElement e, string field) => e.TryGetProperty(field, out var v) && v.TryGetInt32(out var n) ? n : null;
}
