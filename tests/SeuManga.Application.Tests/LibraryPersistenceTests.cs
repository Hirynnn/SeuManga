using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SeuManga.Application.Library;
using SeuManga.Domain.Entities;
using SeuManga.Domain.Enums;
using SeuManga.Infrastructure.Persistence;
namespace SeuManga.Application.Tests;
public sealed class LibraryPersistenceTests
{
    [Fact]
    public async Task DatabaseBackupCanBeRestoredWithoutLosingLibraryData()
    {
        var databaseFile = Path.Combine(Path.GetTempPath(), $"seumanga-{Guid.NewGuid():N}.db");
        var backupFile = Path.Combine(Path.GetTempPath(), $"seumanga-backup-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<SeuMangaDbContext>().UseSqlite($"Data Source={databaseFile}").Options;
            await using (var db = new SeuMangaDbContext(options)) await db.Database.MigrateAsync();
            var factory = new ContextFactory(options);
            var repository = new LibraryRepository(factory);
            await repository.AddOrUpdateAsync(new MangaLibraryEntry { MangaDexId = "manga-backup", Title = "Backup title" });

            var backup = new DatabaseBackupService(factory);
            await backup.BackupAsync(backupFile);
            await repository.RemoveAsync("manga-backup");
            Assert.Empty(await repository.GetLibraryAsync());

            await backup.RestoreAsync(backupFile);

            var restored = Assert.Single(await repository.GetLibraryAsync());
            Assert.Equal("manga-backup", restored.MangaDexId);
            Assert.Equal("Backup title", restored.Title);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(databaseFile)) File.Delete(databaseFile);
            if (File.Exists(backupFile)) File.Delete(backupFile);
        }
    }

    [Fact]
    public async Task LibraryFavoritesAndReadingProgressPersistInSqlite()
    {
        var file = Path.Combine(Path.GetTempPath(), $"seumanga-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<SeuMangaDbContext>().UseSqlite($"Data Source={file}").Options;
            await using (var db = new SeuMangaDbContext(options)) await db.Database.EnsureCreatedAsync();
            var repository = new LibraryRepository(new ContextFactory(options));
            await repository.AddOrUpdateAsync(new MangaLibraryEntry { MangaDexId = "manga-1", Title = "Sample" });
            await repository.SetFavoriteAsync("manga-1", true);
            await repository.SetStatusAsync("manga-1", LibraryStatus.Reading);
            await repository.SaveProgressAsync(new ReadingHistoryEntry { MangaDexId = "manga-1", Title = "Sample", ChapterId = "chapter-1", ChapterNumber = "4", LastPage = 8, PageCount = 20 });
            var library = await repository.GetLibraryAsync();
            var history = await repository.GetHistoryAsync();
            Assert.Single(library); Assert.True(library[0].IsFavorite); Assert.Equal(LibraryStatus.Reading, library[0].Status); Assert.Equal("chapter-1", library[0].LastChapterId);
            Assert.Single(history); Assert.Equal(8, history[0].LastPage);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public async Task CustomListPreventsDuplicateItemsAndCanBeRemoved()
    {
        var file = Path.Combine(Path.GetTempPath(), $"seumanga-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<SeuMangaDbContext>().UseSqlite($"Data Source={file}").Options;
            await using (var db = new SeuMangaDbContext(options)) await db.Database.EnsureCreatedAsync();
            var service = new CustomListService(new LibraryRepository(new ContextFactory(options)));
            var list = await service.CreateAsync("Favorites");
            await service.AddAsync(list.Id, "m1", "Title", null); await service.AddAsync(list.Id, "m1", "Title", null);
            Assert.Single((await service.GetListsAsync())[0].Items);
            await service.RemoveAsync(list.Id, "m1"); Assert.Empty((await service.GetListsAsync())[0].Items);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public async Task CustomListsCanMixMangaAndAnimeAndMigrationPreservesExistingManga()
    {
        var file = Path.Combine(Path.GetTempPath(), $"seumanga-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<SeuMangaDbContext>().UseSqlite($"Data Source={file}").Options;
            await using (var db = new SeuMangaDbContext(options))
            {
                await db.GetService<IMigrator>().MigrateAsync("20261102040205_InitialCreate");
                await db.Database.ExecuteSqlRawAsync("INSERT INTO CustomLists (Id, Name, CreatedAt) VALUES (1, 'Mixed', '2026-01-01T00:00:00+00:00')");
                await db.Database.ExecuteSqlRawAsync("INSERT INTO CustomListItems (Id, ListId, MangaDexId, Title, CoverUrl, AddedAt) VALUES (1, 1, 'manga-old', 'Old manga', NULL, '2026-01-01T00:00:00+00:00')");
                await db.Database.ExecuteSqlRawAsync("INSERT INTO AnimeLibrary (Id, JikanId, Title, ImageUrl, TotalEpisodes, AddedAt) VALUES (9, 99, 'Legacy anime', NULL, 12, '2026-01-01T00:00:00+00:00')");
                await db.Database.MigrateAsync();
            }
            var repository = new LibraryRepository(new ContextFactory(options));
            var service = new CustomListService(repository);
            await service.AddAsync(1, LibraryContentKind.Anime, "42", "Anime", "https://example.test/cover.jpg");
            var list = Assert.Single(await service.GetListsAsync());
            Assert.Equal(2, list.Items.Count);
            Assert.Contains(list.Items, x => x.ContentKind == LibraryContentKind.Manga && x.ContentId == "manga-old");
            Assert.Contains(list.Items, x => x.ContentKind == LibraryContentKind.Anime && x.ContentId == "42");
            await service.RemoveAsync(1, LibraryContentKind.Anime, "42");
            Assert.Single((await service.GetListsAsync())[0].Items);
            var tracking = new AnimeTrackingService(repository);
            var migratedAnime = Assert.Single(await tracking.GetLibraryAsync());
            Assert.Equal(AnimeLibraryStatus.PlanToWatch, migratedAnime.Status);
            Assert.False(migratedAnime.IsFavorite);
            await tracking.SetStatusAsync(99, AnimeLibraryStatus.Watching);
            await tracking.SetFavoriteAsync(99, true);
            var legacyAnime = Assert.Single(await tracking.GetLibraryAsync());
            Assert.Equal(AnimeLibraryStatus.Watching, legacyAnime.Status);
            Assert.True(legacyAnime.IsFavorite);
            await repository.SaveAsync(new ExternalIdentifier { ContentKind = LibraryContentKind.Anime, LocalId = "99", Provider = "TMDB", ExternalId = "701", MatchedBy = "normalized-title-year", Confidence = 1 });
            await repository.SaveAsync(new ExternalIdentifier { ContentKind = LibraryContentKind.Anime, LocalId = "99", Provider = "TMDB", ExternalId = "702", MatchedBy = "corrected-title", Confidence = 0.95 });
            var mapping = await repository.GetAsync(LibraryContentKind.Anime, "99", "TMDB");
            Assert.NotNull(mapping);
            Assert.Equal("702", mapping.ExternalId);
            Assert.Equal("corrected-title", mapping.MatchedBy);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (File.Exists(file)) File.Delete(file); }
    }
    private sealed class ContextFactory(DbContextOptions<SeuMangaDbContext> options) : IDbContextFactory<SeuMangaDbContext>
    {
        public SeuMangaDbContext CreateDbContext() => new(options);
        public Task<SeuMangaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SeuMangaDbContext(options));
    }
}

