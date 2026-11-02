using Microsoft.EntityFrameworkCore;
using SeuManga.Domain.Entities;
namespace SeuManga.Infrastructure.Persistence;
public sealed class SeuMangaDbContext(DbContextOptions<SeuMangaDbContext> options) : DbContext(options)
{
    public DbSet<MangaLibraryEntry> Library => Set<MangaLibraryEntry>();
    public DbSet<ReadingHistoryEntry> History => Set<ReadingHistoryEntry>();
    public DbSet<AnimeLibraryEntry> AnimeLibrary => Set<AnimeLibraryEntry>();
    public DbSet<WatchedAnimeEpisode> WatchedEpisodes => Set<WatchedAnimeEpisode>();
    public DbSet<MangaCustomList> CustomLists => Set<MangaCustomList>();
    public DbSet<MangaCustomListItem> CustomListItems => Set<MangaCustomListItem>();
    public DbSet<ExternalIdentifier> ExternalIdentifiers => Set<ExternalIdentifier>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MangaLibraryEntry>().HasIndex(x => x.MangaDexId).IsUnique();
        modelBuilder.Entity<MangaLibraryEntry>().Property(x => x.Title).HasMaxLength(500);
        modelBuilder.Entity<ReadingHistoryEntry>().HasIndex(x => new { x.MangaDexId, x.ChapterId }).IsUnique();
        modelBuilder.Entity<MangaCustomList>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<MangaCustomListItem>().HasIndex(x => new { x.ListId, x.ContentKind, x.ContentId }).IsUnique();
        modelBuilder.Entity<MangaCustomListItem>().HasOne(x => x.List).WithMany(x => x.Items).HasForeignKey(x => x.ListId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AnimeLibraryEntry>().HasIndex(x => x.JikanId).IsUnique();
        modelBuilder.Entity<WatchedAnimeEpisode>().HasIndex(x => new { x.JikanId, x.EpisodeNumber }).IsUnique();
        modelBuilder.Entity<ReadingHistoryEntry>().Property(x => x.Title).HasMaxLength(500);
        modelBuilder.Entity<ExternalIdentifier>().HasIndex(x => new { x.ContentKind, x.LocalId, x.Provider }).IsUnique();
        modelBuilder.Entity<ExternalIdentifier>().HasIndex(x => new { x.Provider, x.ExternalId });
        modelBuilder.Entity<ExternalIdentifier>().Property(x => x.LocalId).HasMaxLength(200);
        modelBuilder.Entity<ExternalIdentifier>().Property(x => x.Provider).HasMaxLength(80);
        modelBuilder.Entity<ExternalIdentifier>().Property(x => x.ExternalId).HasMaxLength(200);
        modelBuilder.Entity<ExternalIdentifier>().Property(x => x.MatchedBy).HasMaxLength(80);
    }
}


