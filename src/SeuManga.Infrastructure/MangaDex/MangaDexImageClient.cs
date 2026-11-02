using System.Net.Http.Headers;
using SeuManga.Application.Abstractions;
namespace SeuManga.Infrastructure.MangaDex;
public sealed class MangaDexImageClient(HttpClient http) : IChapterImageProvider
{
    private const int MaximumImageBytes = 20 * 1024 * 1024;
    public async Task<(byte[] Content, string ContentType)> DownloadAsync(string pageUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !(uri.Host.EndsWith(".mangadex.network", StringComparison.OrdinalIgnoreCase) || uri.Host == "uploads.mangadex.org"))
            throw new InvalidDataException("A origem da imagem não é permitida.");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Referrer = new Uri("https://mangadex.org/");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaximumImageBytes) throw new InvalidDataException("A página excede o limite de tamanho permitido.");
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length == 0 || bytes.Length > MaximumImageBytes) throw new InvalidDataException("A imagem recebida não é válida.");
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("O servidor não retornou uma imagem.");
        return (bytes, contentType);
    }
}
