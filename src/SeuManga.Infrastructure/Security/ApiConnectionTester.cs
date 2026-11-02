using System.Net.Http.Headers;
using System.Text.Json;
using SeuManga.Application.Abstractions;
namespace SeuManga.Infrastructure.Security;
public sealed class ApiConnectionTester(IHttpClientFactory clients, ISecureSettingsStore settings)
{
    public async Task<string> TestAsync(string provider, CancellationToken cancellationToken = default)
    {
        var name = provider is "TMDB" or "Watchmode" ? provider : throw new ArgumentException("Provedor desconhecido.", nameof(provider));
        var key = await settings.GetSecretAsync(name, cancellationToken);
        if (string.IsNullOrWhiteSpace(key)) return $"Configure uma chave {name} primeiro.";
        using var http = clients.CreateClient(name);
        HttpResponseMessage response;
        if (name == "TMDB") { using var request = new HttpRequestMessage(HttpMethod.Get, "configuration"); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key); response = await http.SendAsync(request, cancellationToken); }
        else response = await http.GetAsync($"regions/?apiKey={Uri.EscapeDataString(key)}", cancellationToken);
        using (response)
        {
            if (!response.IsSuccessStatusCode) return $"{name} respondeu {(int)response.StatusCode}. Verifique a chave e o acesso da conta.";
            if (name == "Watchmode")
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
                var brazil = doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.EnumerateArray().Any(x => (x.TryGetProperty("country_code", out var code) ? code.GetString() : null) == "BR");
                return brazil ? "Chave válida. Brasil aparece entre as regiões conhecidas; a cobertura do plano será confirmada ao consultar cada título." : "Chave válida, mas a lista de regiões não confirmou o Brasil.";
            }
            return "Conexão TMDB verificada.";
        }
    }
}
