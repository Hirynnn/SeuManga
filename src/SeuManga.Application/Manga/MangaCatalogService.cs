using SeuManga.Application.Abstractions;
using SeuManga.Application.Manga;
namespace SeuManga.Application.Manga;

public sealed class MangaCatalogService(IMangaCatalogProvider provider)
{
    public Task<MangaPage> SearchAsync(string? title, int page, int pageSize, string? language = null, string? status = null, CancellationToken cancellationToken = default, MangaSearchFilters? filters = null)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        return provider.SearchAsync(title?.Trim(), (page - 1) * pageSize, pageSize, language, status, cancellationToken, filters);
    }
    public Task<MangaDetails> GetDetailsAsync(string id, CancellationToken cancellationToken = default) => provider.GetDetailsAsync(id, cancellationToken);
    public Task<MangaChapterPage> GetChaptersAsync(string id, string language = "pt-br", int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
        => provider.GetChaptersAsync(id, language, offset, limit, cancellationToken);
    public Task<IReadOnlyList<string>> GetChapterPageUrlsAsync(string chapterId, bool dataSaver = false, CancellationToken cancellationToken = default)
        => provider.GetChapterPageUrlsAsync(chapterId, dataSaver, cancellationToken);
}


