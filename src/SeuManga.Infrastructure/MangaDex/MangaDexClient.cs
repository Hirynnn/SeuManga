using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using SeuManga.Application.Abstractions;
using SeuManga.Application.Manga;
namespace SeuManga.Infrastructure.MangaDex;
public sealed class MangaDexClient(HttpClient http, IMemoryCache? cache = null) : IMangaCatalogProvider
{
    public async Task<MangaPage> SearchAsync(string? title, int offset, int limit, string? originalLanguage = null, string? status = null, CancellationToken cancellationToken = default, MangaSearchFilters? filters = null)
    {
        var cacheKey = $"mangadex:search:{title}:{offset}:{limit}:{originalLanguage}:{status}:{JsonSerializer.Serialize(filters)}";
        if (cache?.TryGetValue(cacheKey, out MangaPage? cached) == true && cached is not null) return cached;
        var ratings = filters?.ContentRatings is { Count: > 0 } selectedRatings ? selectedRatings.Where(IsValidRating).Distinct(StringComparer.Ordinal).ToArray() : ["safe", "suggestive"]; if (ratings.Length == 0) ratings = ["safe", "suggestive"];
        var sort = IsValidSort(filters?.Sort) ? filters!.Sort : "relevance";
        var query = new List<string> { $"limit={limit}", $"offset={offset}", "includes[]=cover_art", "includes[]=author", "includes[]=artist" };
        query.AddRange(ratings.Select(x => $"contentRating[]={Uri.EscapeDataString(x)}"));
        if (!string.IsNullOrWhiteSpace(title)) query.Add($"title={Uri.EscapeDataString(title)}");
        if (!string.IsNullOrWhiteSpace(originalLanguage)) query.Add($"originalLanguage[]={Uri.EscapeDataString(originalLanguage)}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status[]={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(filters?.TranslatedLanguage)) query.Add($"availableTranslatedLanguage[]={Uri.EscapeDataString(filters.TranslatedLanguage)}");
        if (filters?.Year is >= 0 and <= 9999) query.Add($"year={filters.Year.Value}");
        if (filters?.Demographic is "shounen" or "shoujo" or "josei" or "seinen") query.Add($"publicationDemographic[]={filters.Demographic}");
        query.Add($"order[{sort}]={(sort == "title" ? "asc" : "desc")}");
        if (!string.IsNullOrWhiteSpace(filters?.Author))
            query.AddRange((await ResolveAuthorIdsAsync(filters.Author, cancellationToken)).Select(x => $"authors[]={Uri.EscapeDataString(x)}"));
        if (!string.IsNullOrWhiteSpace(filters?.Artist))
            query.AddRange((await ResolveAuthorIdsAsync(filters.Artist, cancellationToken)).Select(x => $"artists[]={Uri.EscapeDataString(x)}"));
        var includedIds = await ResolveTagIdsAsync(filters?.IncludedTags, cancellationToken);
        var excludedIds = await ResolveTagIdsAsync(filters?.ExcludedTags, cancellationToken);
        query.AddRange(includedIds.Select(x => $"includedTags[]={Uri.EscapeDataString(x)}"));
        query.AddRange(excludedIds.Select(x => $"excludedTags[]={Uri.EscapeDataString(x)}"));
        if (includedIds.Count > 1) query.Add($"includedTagsMode={NormalizeMode(filters?.IncludedTagsMode)}");
        if (excludedIds.Count > 1) query.Add($"excludedTagsMode={NormalizeMode(filters?.ExcludedTagsMode)}");
        using var response = await http.GetAsync($"manga?{string.Join('&', query)}", cancellationToken);
        await EnsureSuccess(response);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = doc.RootElement;
        var items = root.GetProperty("data").EnumerateArray().Select(ParseManga).ToArray();
        var total = root.TryGetProperty("total", out var totalValue) ? totalValue.GetInt32() : items.Length;
        var page = new MangaPage(items, total, offset, limit);
        cache?.Set(cacheKey, page, TimeSpan.FromMinutes(3));
        return page;
    }
    private async Task<IReadOnlyList<string>> ResolveTagIdsAsync(IReadOnlyList<string>? names, CancellationToken cancellationToken)
    {
        var requested = names?.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        if (requested.Length == 0) return [];
        const string cacheKey = "mangadex:tag-names";
        if (cache is null || !cache.TryGetValue(cacheKey, out Dictionary<string, string>? tags) || tags is null)
        {
            using var response = await http.GetAsync("manga/tag", cancellationToken); await EnsureSuccess(response);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tag in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                var id = tag.GetProperty("id").GetString();
                if (string.IsNullOrWhiteSpace(id)) continue;
                foreach (var localizedName in tag.GetProperty("attributes").GetProperty("name").EnumerateObject())
                    if (!string.IsNullOrWhiteSpace(localizedName.Value.GetString())) tags.TryAdd(localizedName.Value.GetString()!, id);
            }
            if (cache is not null) cache.Set(cacheKey, tags, TimeSpan.FromHours(12));
        }
        var ids = new List<string>();
        foreach (var name in requested)
        {
            if (!tags.TryGetValue(name, out var id)) throw new ArgumentException($"A tag MangaDex '{name}' não foi encontrada.");
            ids.Add(id);
        }
        return ids;
    }
    private async Task<IReadOnlyList<string>> ResolveAuthorIdsAsync(string name, CancellationToken cancellationToken)
    {
        var cacheKey = $"mangadex:author:{name.Trim().ToUpperInvariant()}";
        if (cache?.TryGetValue(cacheKey, out string[]? cached) == true && cached is not null) return cached;
        using var response = await http.GetAsync($"author?name={Uri.EscapeDataString(name.Trim())}&limit=100", cancellationToken); await EnsureSuccess(response);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var ids = doc.RootElement.GetProperty("data").EnumerateArray()
            .Where(x => x.GetProperty("attributes").TryGetProperty("name", out var n) && string.Equals(n.GetString()?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(x => x.GetProperty("id").GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct().ToArray();
        if (ids.Length == 0) throw new ArgumentException($"Não encontrei autor ou artista MangaDex com o nome '{name}'.");
        cache?.Set(cacheKey, ids, TimeSpan.FromHours(12));
        return ids;
    }
    private static bool IsValidRating(string value) => value is "safe" or "suggestive" or "erotica" or "pornographic";
    private static bool IsValidSort(string? value) => value is "relevance" or "title" or "updatedAt" or "createdAt" or "followedCount";
    private static string NormalizeMode(string? value) => string.Equals(value, "AND", StringComparison.OrdinalIgnoreCase) ? "AND" : "OR";
    public async Task<MangaDetails> GetDetailsAsync(string mangaId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"mangadex:details:{mangaId}";
        if (cache?.TryGetValue(cacheKey, out MangaDetails? cached) == true && cached is not null) return cached;
        var query = $"manga/{Uri.EscapeDataString(mangaId)}?includes[]=cover_art&includes[]=author&includes[]=artist";
        using var response = await http.GetAsync(query, cancellationToken); await EnsureSuccess(response);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = doc.RootElement; var data = root.GetProperty("data"); var a = data.GetProperty("attributes");
        var relationships = data.GetProperty("relationships").EnumerateArray().ToArray();
        var authors = relationships.Where(x => x.GetProperty("type").GetString() is "author" or "artist").Select(x => x.TryGetProperty("attributes", out var attr) ? GetString(attr, "name") : null).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        var cover = relationships.FirstOrDefault(x => x.GetProperty("type").GetString() == "cover_art");
        var coverFile = cover.ValueKind != JsonValueKind.Undefined && cover.TryGetProperty("attributes", out var ca) && ca.TryGetProperty("fileName", out var fn) ? fn.GetString() : null;
        var coverUrl = string.IsNullOrWhiteSpace(coverFile) ? null : $"https://uploads.mangadex.org/covers/{mangaId}/{Uri.EscapeDataString(coverFile)}.512.jpg";
        var alternate = a.GetProperty("altTitles").EnumerateArray().SelectMany(x => x.EnumerateObject()).Select(x => x.Value.GetString() ?? "").Where(x => x.Length > 0).Distinct().ToArray();
        var tags = ReadTags(a);
        var details = new MangaDetails(mangaId, GetLocalized(a.GetProperty("title"), "pt-br") ?? GetLocalized(a.GetProperty("title"), "en") ?? "Título indisponível", alternate,
            GetLocalized(a.GetProperty("description"), "pt-br") ?? GetLocalized(a.GetProperty("description"), "en") ?? "Sinopse não disponível.", GetString(a, "status") ?? "unknown", GetString(a, "originalLanguage") ?? "",
            a.TryGetProperty("year", out var y) && y.TryGetInt32(out var year) ? year : null, authors!, tags, coverUrl, ReadTranslatedLanguages(a));
        cache?.Set(cacheKey, details, TimeSpan.FromMinutes(10));
        return details;
    }
    public async Task<MangaChapterPage> GetChaptersAsync(string mangaId, string language, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"mangadex:chapters:{mangaId}:{language}:{offset}:{limit}";
        if (cache?.TryGetValue(cacheKey, out MangaChapterPage? cached) == true && cached is not null) return cached;
        var query = $"manga/{Uri.EscapeDataString(mangaId)}/feed?translatedLanguage[]={Uri.EscapeDataString(language)}&limit={limit}&offset={offset}&order[volume]=asc&order[chapter]=asc&includes[]=scanlation_group";
        using var response = await http.GetAsync(query, cancellationToken); await EnsureSuccess(response);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken)); var root = doc.RootElement;
        var sourceItems = root.GetProperty("data").EnumerateArray().Select(e =>
        {
            var a = e.GetProperty("attributes");
            var group = e.GetProperty("relationships").EnumerateArray().FirstOrDefault(x => x.GetProperty("type").GetString() == "scanlation_group");
            return new MangaChapter(e.GetProperty("id").GetString() ?? "", mangaId, GetString(a, "volume"), GetString(a, "chapter"), GetLocalized(a.GetProperty("title"), "pt-br") ?? "", GetString(a, "translatedLanguage") ?? language,
                group.ValueKind == JsonValueKind.Undefined ? null : GetString(group.GetProperty("attributes"), "name"), ParseDate(GetString(a, "publishAt")));
        }).ToArray();
        var result = sourceItems.GroupBy(x => string.IsNullOrWhiteSpace(x.Number) ? x.Id : $"{x.Volume}:{x.Number}").Select(g => g.OrderByDescending(x => x.PublishedAt).First())
          .OrderBy(x => decimal.TryParse(x.Number, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : decimal.MaxValue).ToArray();
        var page = new MangaChapterPage(result, root.TryGetProperty("total", out var total) ? total.GetInt32() : sourceItems.Length, offset + sourceItems.Length);
        cache?.Set(cacheKey, page, TimeSpan.FromMinutes(2));
        return page;
    }
    public async Task<IReadOnlyList<string>> GetChapterPageUrlsAsync(string chapterId, bool dataSaver = false, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"at-home/server/{Uri.EscapeDataString(chapterId)}", cancellationToken); await EnsureSuccess(response);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken)); var root = doc.RootElement; var chapter = root.GetProperty("chapter");
        var filenames = chapter.GetProperty(dataSaver ? "dataSaver" : "data").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var mode = dataSaver ? "data-saver" : "data"; var hash = chapter.GetProperty("hash").GetString(); var baseUrl = root.GetProperty("baseUrl").GetString();
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(hash)) throw new InvalidDataException("Resposta MangaDex@Home incompleta.");
        return filenames.Select(file => $"{baseUrl}/{mode}/{hash}/{Uri.EscapeDataString(file)}").ToArray();
    }
    private static MangaSearchResult ParseManga(JsonElement e)
    {
        var a = e.GetProperty("attributes"); var relationships = e.GetProperty("relationships").EnumerateArray().ToArray();
        var cover = relationships.FirstOrDefault(x => x.GetProperty("type").GetString() == "cover_art");
        var coverFile = cover.ValueKind != JsonValueKind.Undefined && cover.TryGetProperty("attributes", out var ca) && ca.TryGetProperty("fileName", out var fn) ? fn.GetString() : null;
        var coverUrl = string.IsNullOrWhiteSpace(coverFile) ? null : $"https://uploads.mangadex.org/covers/{e.GetProperty("id").GetString()}/{Uri.EscapeDataString(coverFile)}.256.jpg";
        return new MangaSearchResult(e.GetProperty("id").GetString() ?? "", GetLocalized(a.GetProperty("title"), "pt-br") ?? GetLocalized(a.GetProperty("title"), "en") ?? "Título indisponível", GetLocalized(a.GetProperty("description"), "pt-br") ?? GetLocalized(a.GetProperty("description"), "en") ?? "Sinopse não disponível.", GetString(a, "status") ?? "unknown", GetString(a, "originalLanguage") ?? "", coverUrl, ReadTags(a));
    }
    private static IReadOnlyList<string> ReadTags(JsonElement attributes) => attributes.TryGetProperty("tags", out var tags) ? tags.EnumerateArray().Select(t => GetLocalized(t.GetProperty("attributes").GetProperty("name"), "pt-br") ?? GetLocalized(t.GetProperty("attributes").GetProperty("name"), "en") ?? "").Where(x => x.Length > 0).Take(4).ToArray() : [];
    private static string? GetLocalized(JsonElement e, string language) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(language, out var value) ? value.GetString() : null;
    private static IReadOnlyList<string> ReadTranslatedLanguages(JsonElement attributes) =>
        attributes.TryGetProperty("availableTranslatedLanguages", out var languages) && languages.ValueKind == JsonValueKind.Array
            ? languages.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
    private static string? GetString(JsonElement e, string name) => e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static DateTimeOffset? ParseDate(string? value) => DateTimeOffset.TryParse(value, out var date) ? date : null;
    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests) throw new HttpRequestException("A MangaDex limitou temporariamente as consultas. Tente novamente em instantes.", null, response.StatusCode);
        if ((int)response.StatusCode >= 500) throw new HttpRequestException("A MangaDex está temporariamente indisponível.", null, response.StatusCode);
        response.EnsureSuccessStatusCode(); await Task.CompletedTask;
    }
}
