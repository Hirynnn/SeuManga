namespace SeuManga.Application.Abstractions;
public interface IChapterImageProvider
{
    Task<(byte[] Content, string ContentType)> DownloadAsync(string pageUrl, CancellationToken cancellationToken = default);
}
