namespace SeuManga.Application.Abstractions;
public interface ISecureSettingsStore
{
    Task SaveSecretAsync(string name, string value, CancellationToken cancellationToken = default);
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
}
