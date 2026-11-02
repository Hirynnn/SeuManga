using System.Net;
using System.Text.Json;
using SeuManga.Application.Abstractions;
using SeuManga.Application.Streaming;
namespace SeuManga.Infrastructure.Watchmode;
public sealed class WatchmodeClient(HttpClient http, ISecureSettingsStore settings) : IWatchmodeClient
{
    public async Task<StreamingAvailability> GetAvailabilityAsync(string title, CancellationToken cancellationToken = default)
    {
        var key = await settings.GetSecretAsync("Watchmode", cancellationToken);
        if (string.IsNullOrWhiteSpace(key)) return new(false, false, "Configure uma chave Watchmode para consultar disponibilidade.", []);
        var searchUrl = $"search/?search_field=name&search_value={Uri.EscapeDataString(title)}&types=tv&apiKey={Uri.EscapeDataString(key)}";
        using var search = await http.GetAsync(searchUrl, cancellationToken);
        if (search.StatusCode == HttpStatusCode.Forbidden) return new(true, false, "O plano Watchmode não confirmou cobertura do Brasil.", []);
        search.EnsureSuccessStatusCode();
        using var searchDoc = JsonDocument.Parse(await search.Content.ReadAsStreamAsync(cancellationToken));
        var matches = searchDoc.RootElement.TryGetProperty("title_results", out var titles) ? titles.EnumerateArray() : Enumerable.Empty<JsonElement>();
        var match = matches.FirstOrDefault(x => string.Equals(Text(x, "name"), title, StringComparison.OrdinalIgnoreCase));
        if (match.ValueKind == JsonValueKind.Undefined || !match.TryGetProperty("id", out var id)) return new(true, true, "Nenhum título correspondente foi localizado no Watchmode.", []);
        using var response = await http.GetAsync($"title/{id.GetInt32()}/sources/?regions=BR&apiKey={Uri.EscapeDataString(key)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.Forbidden) return new(true, false, "O plano Watchmode não confirmou cobertura do Brasil.", []);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var sources = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().Select(x => new StreamingSource(Text(x, "name") ?? "Plataforma", AvailabilityType(Text(x, "type")), "BR", Text(x, "web_url"), Text(x, "logo_100px"), "Watchmode")).ToArray() : [];
        return sources.Length == 0 ? new(true, true, "Nenhuma disponibilidade confirmada no Brasil foi localizada.", []) : new(true, true, "Disponibilidade consultada no Watchmode para o Brasil.", sources);
    }
    private static string AvailabilityType(string? value) => value switch { "sub" => "Assinatura", "free" => "Gratuito", "rent" => "Aluguel", "buy" => "Compra", _ => "Disponibilidade" };
    private static string? Text(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

