using SeuManga.Application.Manga;
namespace SeuManga.Application.Abstractions;

public interface IMangaCatalogProvider
{
    Task<MangaPage> SearchAsync(string? title, int offset, int limit, string? originalLanguage = null, string? status = null, CancellationToken cancellationToken = default, MangaSearchFilters? filters = null);
    Task<MangaDetails> GetDetailsAsync(string mangaId, CancellationToken cancellationToken = default);
    Task<MangaChapterPage> GetChaptersAsync(string mangaId, string language, int offset, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetChapterPageUrlsAsync(string chapterId, bool dataSaver = false, CancellationToken cancellationToken = default);
}


