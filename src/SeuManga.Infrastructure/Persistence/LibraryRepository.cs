using Microsoft.EntityFrameworkCore;
using SeuManga.Application.Library;
using SeuManga.Domain.Entities;
using SeuManga.Domain.Enums;
namespace SeuManga.Infrastructure.Persistence;
public sealed class LibraryRepository(IDbContextFactory<SeuMangaDbContext> factory) : ILibraryRepository, IAnimeLibraryRepository, ICustomListRepository, IExternalIdentifierRepository
{
    public async Task<ExternalIdentifier?> GetAsync(LibraryContentKind contentKind, string localId, string provider, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.ExternalIdentifiers.AsNoTracking().SingleOrDefaultAsync(x => x.ContentKind == contentKind && x.LocalId == localId && x.Provider == provider, cancellationToken);
    }
    public async Task SaveAsync(ExternalIdentifier identifier, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.ExternalIdentifiers.SingleOrDefaultAsync(x => x.ContentKind == identifier.ContentKind && x.LocalId == identifier.LocalId && x.Provider == identifier.Provider, cancellationToken);
        if (existing is null) db.ExternalIdentifiers.Add(identifier);
        else { existing.ExternalId = identifier.ExternalId; existing.MatchedBy = identifier.MatchedBy; existing.Confidence = identifier.Confidence; existing.MatchedAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<MangaCustomList>> GetListsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.CustomLists.Include(x => x.Items).AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }
    public async Task<MangaCustomList> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (await db.CustomLists.AnyAsync(x => x.Name == name, cancellationToken)) throw new InvalidOperationException("Já existe uma lista com esse nome.");
        var list = new MangaCustomList { Name = name }; db.CustomLists.Add(list); await db.SaveChangesAsync(cancellationToken); return list;
    }
    public async Task AddAsync(int listId, LibraryContentKind contentKind, string contentId, string title, string? coverUrl, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.CustomLists.AnyAsync(x => x.Id == listId, cancellationToken)) throw new KeyNotFoundException("Lista não encontrada.");
        if (!await db.CustomListItems.AnyAsync(x => x.ListId == listId && x.ContentKind == contentKind && x.ContentId == contentId, cancellationToken))
        { db.CustomListItems.Add(new MangaCustomListItem { ListId = listId, ContentKind = contentKind, ContentId = contentId, Title = title, CoverUrl = coverUrl }); await db.SaveChangesAsync(cancellationToken); }
    }
    public async Task RemoveAsync(int listId, LibraryContentKind contentKind, string contentId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var item = await db.CustomListItems.SingleOrDefaultAsync(x => x.ListId == listId && x.ContentKind == contentKind && x.ContentId == contentId, cancellationToken);
        if (item is not null) { db.CustomListItems.Remove(item); await db.SaveChangesAsync(cancellationToken); }
    }
    public async Task<IReadOnlyList<AnimeLibraryEntry>> GetAnimeLibraryAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entries = await db.AnimeLibrary.AsNoTracking().ToListAsync(cancellationToken);
        return entries.OrderByDescending(x => x.AddedAt).ToArray();
    }
    public async Task<IReadOnlySet<int>> GetWatchedEpisodesAsync(int jikanId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var episodes = await db.WatchedEpisodes.AsNoTracking().Where(x => x.JikanId == jikanId).Select(x => x.EpisodeNumber).ToListAsync(cancellationToken);
        return episodes.ToHashSet();
    }
    public async Task AddAnimeAsync(AnimeLibraryEntry anime, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.AnimeLibrary.SingleOrDefaultAsync(x => x.JikanId == anime.JikanId, cancellationToken);
        if (existing is null) db.AnimeLibrary.Add(anime); else { existing.Title = anime.Title; existing.ImageUrl = anime.ImageUrl; existing.TotalEpisodes = anime.TotalEpisodes; }
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task SetAnimeFavoriteAsync(int jikanId, bool favorite, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entry = await db.AnimeLibrary.SingleOrDefaultAsync(x => x.JikanId == jikanId, cancellationToken);
        if (entry is null) return;
        entry.IsFavorite = favorite;
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task SetAnimeStatusAsync(int jikanId, SeuManga.Domain.Enums.AnimeLibraryStatus status, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entry = await db.AnimeLibrary.SingleOrDefaultAsync(x => x.JikanId == jikanId, cancellationToken);
        if (entry is null) return;
        entry.Status = status;
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task RemoveAnimeAsync(int jikanId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entry = await db.AnimeLibrary.SingleOrDefaultAsync(x => x.JikanId == jikanId, cancellationToken);
        if (entry is null) return;
        db.AnimeLibrary.Remove(entry);
        db.WatchedEpisodes.RemoveRange(db.WatchedEpisodes.Where(x => x.JikanId == jikanId));
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task MarkEpisodeWatchedAsync(int jikanId, int episode, bool watched, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.WatchedEpisodes.SingleOrDefaultAsync(x => x.JikanId == jikanId && x.EpisodeNumber == episode, cancellationToken);
        if (watched && record is null) db.WatchedEpisodes.Add(new WatchedAnimeEpisode { JikanId = jikanId, EpisodeNumber = episode });
        if (!watched && record is not null) db.WatchedEpisodes.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<MangaLibraryEntry>> GetLibraryAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entries = await db.Library.AsNoTracking().ToListAsync(cancellationToken);
        return entries.OrderByDescending(x => x.LastReadAt ?? x.AddedAt).ToArray();
    }
    public async Task<IReadOnlyList<ReadingHistoryEntry>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entries = await db.History.AsNoTracking().ToListAsync(cancellationToken);
        return entries.OrderByDescending(x => x.UpdatedAt).Take(500).ToArray();
    }
    public async Task AddOrUpdateAsync(MangaLibraryEntry entry, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Library.SingleOrDefaultAsync(x => x.MangaDexId == entry.MangaDexId, cancellationToken);
        if (existing is null) db.Library.Add(entry); else { existing.Title = entry.Title; existing.CoverUrl = entry.CoverUrl; }
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task SetFavoriteAsync(string mangaDexId, bool favorite, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entry = await db.Library.SingleOrDefaultAsync(x => x.MangaDexId == mangaDexId, cancellationToken);
        if (entry is null) return; entry.IsFavorite = favorite; await db.SaveChangesAsync(cancellationToken);
    }
    public async Task SetStatusAsync(string mangaDexId, SeuManga.Domain.Enums.LibraryStatus status, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entry = await db.Library.SingleOrDefaultAsync(x => x.MangaDexId == mangaDexId, cancellationToken);
        if (entry is null) return; entry.Status = status; await db.SaveChangesAsync(cancellationToken);
    }
    public async Task RemoveAsync(string mangaDexId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entry = await db.Library.SingleOrDefaultAsync(x => x.MangaDexId == mangaDexId, cancellationToken);
        if (entry is not null) { db.Library.Remove(entry); await db.SaveChangesAsync(cancellationToken); }
    }
    public async Task SaveProgressAsync(ReadingHistoryEntry progress, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.History.SingleOrDefaultAsync(x => x.MangaDexId == progress.MangaDexId && x.ChapterId == progress.ChapterId, cancellationToken);
        if (existing is null) db.History.Add(progress); else { existing.LastPage = progress.LastPage; existing.PageCount = progress.PageCount; existing.UpdatedAt = DateTimeOffset.UtcNow; }
        var library = await db.Library.SingleOrDefaultAsync(x => x.MangaDexId == progress.MangaDexId, cancellationToken);
        if (library is not null) { library.LastReadAt = DateTimeOffset.UtcNow; library.LastChapterId = progress.ChapterId; library.LastChapterNumber = progress.ChapterNumber; library.LastPage = progress.LastPage; }
        await db.SaveChangesAsync(cancellationToken);
    }
}
