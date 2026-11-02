using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SeuManga.Application.Abstractions;
namespace SeuManga.Infrastructure.Security;
public sealed class DpapiSettingsStore(string settingsPath) : ISecureSettingsStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SeuManga.ApiSecrets.v1");
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly HashSet<string> Allowed = ["TMDB", "Watchmode"];
    public async Task SaveSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("As chaves protegidas dependem do DPAPI do Windows.");
        ValidateName(name); await Gate.WaitAsync(cancellationToken);
        try
        {
            Dictionary<string, string> secrets = File.Exists(settingsPath) ? JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(settingsPath, cancellationToken)) ?? [] : [];
            secrets[name] = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            var temp = settingsPath + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(secrets), cancellationToken);
            File.Move(temp, settingsPath, true);
        }
        finally { Gate.Release(); }
    }
    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("As chaves protegidas dependem do DPAPI do Windows.");
        ValidateName(name); await Gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(settingsPath)) return null;
            var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(settingsPath, cancellationToken));
            if (secrets is null || !secrets.TryGetValue(name, out var protectedValue)) return null;
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedValue), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        finally { Gate.Release(); }
    }
    private static void ValidateName(string name) { if (!Allowed.Contains(name)) throw new ArgumentException("Configuração secreta desconhecida.", nameof(name)); }
}

