namespace SeuManga.Application.Manga;
public sealed record MangaSearchResult(string Id, string Title, string Description, string Status, string OriginalLanguage, string? CoverUrl, IReadOnlyList<string> Tags);
public sealed record MangaPage(IReadOnlyList<MangaSearchResult> Items, int Total, int Offset, int Limit);
public sealed record MangaChapter(string Id, string MangaId, string? Volume, string? Number, string Title, string Language, string? Group, DateTimeOffset? PublishedAt);
public sealed record MangaChapterPage(IReadOnlyList<MangaChapter> Items, int Total, int? NextOffset = null);
public sealed record MangaDetails(string Id, string Title, IReadOnlyList<string> AlternateTitles, string Description, string Status, string OriginalLanguage, int? Year, IReadOnlyList<string> Authors, IReadOnlyList<string> Tags, string? CoverUrl, IReadOnlyList<string>? AvailableTranslatedLanguages = null);
public sealed record MangaSearchFilters(
    string? TranslatedLanguage = null,
    string? Author = null,
    string? Artist = null,
    IReadOnlyList<string>? IncludedTags = null,
    IReadOnlyList<string>? ExcludedTags = null,
    string IncludedTagsMode = "OR",
    string ExcludedTagsMode = "OR",
    IReadOnlyList<string>? ContentRatings = null,
    string Sort = "relevance",
    int? Year = null,
    string? Demographic = null);
