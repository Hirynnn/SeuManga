using SeuManga.Domain.Entities;
using SeuManga.Domain.Enums;
namespace SeuManga.Application.Library;
public interface ILibraryRepository
{
    Task<IReadOnlyList<MangaLibraryEntry>> GetLibraryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReadingHistoryEntry>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task AddOrUpdateAsync(MangaLibraryEntry entry, CancellationToken cancellationToken = default);
    Task SetFavoriteAsync(string mangaDexId, bool favorite, CancellationToken cancellationToken = default);
    Task SetStatusAsync(string mangaDexId, LibraryStatus status, CancellationToken cancellationToken = default);
    Task RemoveAsync(string mangaDexId, CancellationToken cancellationToken = default);
    Task SaveProgressAsync(ReadingHistoryEntry progress, CancellationToken cancellationToken = default);
}
public sealed class LibraryService(ILibraryRepository repository)
{
    public Task<IReadOnlyList<MangaLibraryEntry>> GetLibraryAsync(CancellationToken ct = default) => repository.GetLibraryAsync(ct);
    public Task<IReadOnlyList<ReadingHistoryEntry>> GetHistoryAsync(CancellationToken ct = default) => repository.GetHistoryAsync(ct);
    public Task AddAsync(string id, string title, string? coverUrl, CancellationToken ct = default) => repository.AddOrUpdateAsync(new MangaLibraryEntry { MangaDexId = id, Title = title, CoverUrl = coverUrl }, ct);
    public Task SetFavoriteAsync(string id, bool favorite, CancellationToken ct = default) => repository.SetFavoriteAsync(id, favorite, ct);
    public Task SetStatusAsync(string id, LibraryStatus status, CancellationToken ct = default) => repository.SetStatusAsync(id, status, ct);
    public Task RemoveAsync(string id, CancellationToken ct = default) => repository.RemoveAsync(id, ct);
    public Task SaveProgressAsync(ReadingHistoryEntry progress, CancellationToken ct = default) => repository.SaveProgressAsync(progress, ct);
}
public interface IAnimeLibraryRepository
{
    Task<IReadOnlyList<AnimeLibraryEntry>> GetAnimeLibraryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlySet<int>> GetWatchedEpisodesAsync(int jikanId, CancellationToken cancellationToken = default);
    Task AddAnimeAsync(AnimeLibraryEntry anime, CancellationToken cancellationToken = default);
    Task SetAnimeFavoriteAsync(int jikanId, bool favorite, CancellationToken cancellationToken = default);
    Task SetAnimeStatusAsync(int jikanId, AnimeLibraryStatus status, CancellationToken cancellationToken = default);
    Task RemoveAnimeAsync(int jikanId, CancellationToken cancellationToken = default);
    Task MarkEpisodeWatchedAsync(int jikanId, int episode, bool watched, CancellationToken cancellationToken = default);
}
public sealed class AnimeTrackingService(IAnimeLibraryRepository repository)
{
    public Task<IReadOnlyList<AnimeLibraryEntry>> GetLibraryAsync(CancellationToken ct = default) => repository.GetAnimeLibraryAsync(ct);
    public Task<IReadOnlySet<int>> GetWatchedEpisodesAsync(int id, CancellationToken ct = default) => repository.GetWatchedEpisodesAsync(id, ct);
    public Task AddAsync(int id, string title, string? image, int? total, CancellationToken ct = default) => repository.AddAnimeAsync(new AnimeLibraryEntry { JikanId = id, Title = title, ImageUrl = image, TotalEpisodes = total }, ct);
    public Task SetFavoriteAsync(int id, bool favorite, CancellationToken ct = default) => repository.SetAnimeFavoriteAsync(id, favorite, ct);
    public Task SetStatusAsync(int id, AnimeLibraryStatus status, CancellationToken ct = default) => repository.SetAnimeStatusAsync(id, status, ct);
    public Task RemoveAsync(int id, CancellationToken ct = default) => repository.RemoveAnimeAsync(id, ct);
    public Task SetEpisodeWatchedAsync(int id, int episode, bool watched, CancellationToken ct = default) => repository.MarkEpisodeWatchedAsync(id, episode, watched, ct);
}
public interface ICustomListRepository
{
    Task<IReadOnlyList<MangaCustomList>> GetListsAsync(CancellationToken cancellationToken = default);
    Task<MangaCustomList> CreateAsync(string name, CancellationToken cancellationToken = default);
    Task AddAsync(int listId, LibraryContentKind contentKind, string contentId, string title, string? coverUrl, CancellationToken cancellationToken = default);
    Task RemoveAsync(int listId, LibraryContentKind contentKind, string contentId, CancellationToken cancellationToken = default);
}
public interface IExternalIdentifierRepository
{
    Task<ExternalIdentifier?> GetAsync(LibraryContentKind contentKind, string localId, string provider, CancellationToken cancellationToken = default);
    Task SaveAsync(ExternalIdentifier identifier, CancellationToken cancellationToken = default);
}
public sealed class CustomListService(ICustomListRepository repository)
{
    public Task<IReadOnlyList<MangaCustomList>> GetListsAsync(CancellationToken ct = default) => repository.GetListsAsync(ct);
    public Task<MangaCustomList> CreateAsync(string name, CancellationToken ct = default)
    {
        var clean = name.Trim(); if (clean.Length is < 1 or > 80) throw new ArgumentException("O nome deve ter de 1 a 80 caracteres.", nameof(name));
        return repository.CreateAsync(clean, ct);
    }
    public Task AddAsync(int listId, LibraryContentKind contentKind, string contentId, string title, string? coverUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contentId)) throw new ArgumentException("O identificador do título é obrigatório.", nameof(contentId));
        return repository.AddAsync(listId, contentKind, contentId.Trim(), title.Trim(), coverUrl, ct);
    }
    public Task RemoveAsync(int listId, LibraryContentKind contentKind, string contentId, CancellationToken ct = default) => repository.RemoveAsync(listId, contentKind, contentId, ct);
    public Task AddAsync(int listId, string mangaId, string title, string? coverUrl, CancellationToken ct = default) => AddAsync(listId, LibraryContentKind.Manga, mangaId, title, coverUrl, ct);
    public Task RemoveAsync(int listId, string mangaId, CancellationToken ct = default) => RemoveAsync(listId, LibraryContentKind.Manga, mangaId, ct);
}
