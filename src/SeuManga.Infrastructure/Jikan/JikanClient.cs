using System.Net;
using System.Text.Json;
using SeuManga.Application.Anime;
namespace SeuManga.Infrastructure.Jikan;
public sealed class JikanClient(HttpClient http) : IAnimeCatalogProvider
{
    private static readonly SemaphoreSlim RateGate = new(1, 1);
    private static DateTimeOffset _lastRequest = DateTimeOffset.MinValue;
    public async Task<AnimePage> SearchAsync(string query, int page = 1, CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        using var response = await SendAsync($"anime?q={Uri.EscapeDataString(query.Trim())}&page={page}&limit=24&sfw=true", cancellationToken);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = doc.RootElement; var pagination = root.GetProperty("pagination");
        return new AnimePage(root.GetProperty("data").EnumerateArray().Select(ParseAnime).ToArray(), Int(pagination, "last_visible_page") ?? 1, Int(pagination, "current_page") ?? page);
    }
    public async Task<AnimePage> GetTopAnimeAsync(int page = 1, string filter = "bypopularity", int limit = 12, CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (limit is < 1 or > 25) throw new ArgumentOutOfRangeException(nameof(limit));
        if (filter is not ("airing" or "upcoming" or "bypopularity" or "favorite")) throw new ArgumentException("Filtro de ranking Jikan inválido.", nameof(filter));
        using var response = await SendAsync($"top/anime?filter={filter}&page={page}&limit={limit}", cancellationToken);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = doc.RootElement; var pagination = root.GetProperty("pagination");
        var items = root.GetProperty("data").EnumerateArray()
            .Where(x => !IsExplicitRating(Str(x, "rating")))
            .Select(ParseAnime).ToArray();
        return new AnimePage(items, Int(pagination, "last_visible_page") ?? 1, Int(pagination, "current_page") ?? page);
    }
    public async Task<AnimeDetails> GetDetailsAsync(int animeId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync($"anime/{animeId}/full", cancellationToken);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var data = doc.RootElement.GetProperty("data");
        var genres = Names(data, "genres"); var studios = Names(data, "studios");
        return new AnimeDetails(ParseAnime(data), genres, studios, Str(data, "rating"), Str(data, "duration"));
    }
    public async Task<IReadOnlyList<AnimeRecommendation>> GetRecommendationsAsync(int animeId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync($"anime/{animeId}/recommendations", cancellationToken);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return doc.RootElement.GetProperty("data").EnumerateArray().Select(x =>
        {
            var entry = x.GetProperty("entry");
            return new AnimeRecommendation(ParseAnime(entry), Int(x, "votes") ?? 0);
        }).ToArray();
    }    public async Task<IReadOnlyList<AnimeEpisode>> GetEpisodesAsync(int animeId, int page = 1, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync($"anime/{animeId}/episodes?page={page}", cancellationToken);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return doc.RootElement.GetProperty("data").EnumerateArray().Select(e => new AnimeEpisode(Int(e, "mal_id") ?? 0, Int(e, "episode") ?? 0, Str(e, "title") ?? $"Episódio {Int(e, "episode") ?? 0}", Str(e, "title_japanese"), Str(e, "aired"), Bool(e, "filler"), Bool(e, "recap"))).ToArray();
    }
    private async Task<HttpResponseMessage> SendAsync(string path, CancellationToken ct)
    {
        await RateGate.WaitAsync(ct);
        try
        {
            var delay = TimeSpan.FromMilliseconds(1150) - (DateTimeOffset.UtcNow - _lastRequest);
            if (delay > TimeSpan.Zero) await Task.Delay(delay, ct);
            _lastRequest = DateTimeOffset.UtcNow;
            var response = await http.GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests) { response.Dispose(); throw new HttpRequestException("Jikan limitou as consultas. Aguarde alguns segundos e tente novamente.", null, HttpStatusCode.TooManyRequests); }
            if ((int)response.StatusCode >= 500) { response.Dispose(); throw new HttpRequestException("Jikan está temporariamente indisponível.", null, response.StatusCode); }
            response.EnsureSuccessStatusCode(); return response;
        }
        finally { RateGate.Release(); }
    }
    private static AnimeSearchResult ParseAnime(JsonElement e)
    {
        var images = e.GetProperty("images").GetProperty("jpg"); var seasonObj = e.TryGetProperty("season", out var so) && so.ValueKind == JsonValueKind.String ? so.GetString() : null;
        var year = Int(e, "year"); if (year is null && e.TryGetProperty("aired", out var aired) && aired.TryGetProperty("prop", out var prop) && prop.TryGetProperty("from", out var from)) year = Int(from, "year");
        return new AnimeSearchResult(Int(e, "mal_id") ?? 0, Str(e, "title") ?? "Título indisponível", Str(e, "title_english"), Str(e, "synopsis") ?? "Sinopse não disponível.", Str(images, "large_image_url") ?? Str(images, "image_url"), Str(e, "status") ?? "Status desconhecido", seasonObj, year, Int(e, "episodes"), Double(e, "score"), Str(e, "url") ?? "https://myanimelist.net/");
    }
    private static IReadOnlyList<string> Names(JsonElement e, string property) => e.TryGetProperty(property, out var list) && list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().Select(x => Str(x, "name")).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray() : [];
    private static string? Str(JsonElement e, string property) => e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static int? Int(JsonElement e, string property) => e.TryGetProperty(property, out var v) && v.TryGetInt32(out var n) ? n : null;
    private static double? Double(JsonElement e, string property) => e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) ? n : null;
    private static bool Bool(JsonElement e, string property) => e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.True;
    private static bool IsExplicitRating(string? rating) => rating?.Contains("hentai", StringComparison.OrdinalIgnoreCase) == true || rating?.StartsWith("Rx", StringComparison.OrdinalIgnoreCase) == true;
}

