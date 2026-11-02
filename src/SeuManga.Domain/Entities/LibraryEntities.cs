using SeuManga.Domain.Enums;
namespace SeuManga.Domain.Entities;
public sealed class MangaLibraryEntry
{
    public int Id { get; set; }
    public required string MangaDexId { get; set; }
    public required string Title { get; set; }
    public string? CoverUrl { get; set; }
    public LibraryStatus Status { get; set; } = LibraryStatus.PlanToRead;
    public bool IsFavorite { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastReadAt { get; set; }
    public string? LastChapterId { get; set; }
    public string? LastChapterNumber { get; set; }
    public int LastPage { get; set; }
}
public sealed class ReadingHistoryEntry
{
    public int Id { get; set; }
    public required string MangaDexId { get; set; }
    public required string Title { get; set; }
    public string? CoverUrl { get; set; }
    public required string ChapterId { get; set; }
    public string? ChapterNumber { get; set; }
    public int LastPage { get; set; }
    public int PageCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class AnimeLibraryEntry
{
    public int Id { get; set; }
    public int JikanId { get; set; }
    public required string Title { get; set; }
    public string? ImageUrl { get; set; }
    public int? TotalEpisodes { get; set; }
    public AnimeLibraryStatus Status { get; set; } = AnimeLibraryStatus.PlanToWatch;
    public bool IsFavorite { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class WatchedAnimeEpisode
{
    public int Id { get; set; }
    public int JikanId { get; set; }
    public int EpisodeNumber { get; set; }
    public DateTimeOffset WatchedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class MangaCustomList
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<MangaCustomListItem> Items { get; set; } = [];
}
public sealed class MangaCustomListItem
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public MangaCustomList? List { get; set; }
    public LibraryContentKind ContentKind { get; set; } = LibraryContentKind.Manga;
    public required string ContentId { get; set; }
    public required string Title { get; set; }
    public string? CoverUrl { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
}
