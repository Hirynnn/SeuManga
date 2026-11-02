using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SeuManga.Infrastructure.Persistence;

public sealed class DatabaseBackupService(IDbContextFactory<SeuMangaDbContext> contextFactory)
{
    public async Task BackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(destinationPath);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var source = GetConnection(db);
        EnsureDifferentDatabase(source, fullPath);
        await source.OpenAsync(cancellationToken);

        await using var destination = CreateConnection(fullPath, SqliteOpenMode.ReadWriteCreate);
        await destination.OpenAsync(cancellationToken);
        await Task.Run(() => source.BackupDatabase(destination), cancellationToken);
    }

    public async Task RestoreAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(backupPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("O arquivo de backup não foi encontrado.", fullPath);

        await using var source = CreateConnection(fullPath, SqliteOpenMode.ReadOnly);
        await source.OpenAsync(cancellationToken);
        await ValidateBackupAsync(source, cancellationToken);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var destination = GetConnection(db);
        EnsureDifferentDatabase(destination, fullPath);
        await destination.OpenAsync(cancellationToken);

        var safetyCopyPath = Path.Combine(Path.GetTempPath(), $"SeuManga-restore-{Guid.NewGuid():N}.db");
        try
        {
            await using var safetyCopy = CreateConnection(safetyCopyPath, SqliteOpenMode.ReadWriteCreate);
            await safetyCopy.OpenAsync(cancellationToken);
            await Task.Run(() => destination.BackupDatabase(safetyCopy), cancellationToken);

            try
            {
                await Task.Run(() => source.BackupDatabase(destination), cancellationToken);
                await db.Database.MigrateAsync(cancellationToken);
            }
            catch (Exception restoreError)
            {
                try
                {
                    await Task.Run(() => safetyCopy.BackupDatabase(destination), CancellationToken.None);
                }
                catch (Exception rollbackError)
                {
                    throw new AggregateException("A restauração falhou e não foi possível recuperar a cópia anterior.", restoreError, rollbackError);
                }

                throw;
            }
        }
        finally
        {
            if (File.Exists(safetyCopyPath)) File.Delete(safetyCopyPath);
        }
    }

    private static async Task ValidateBackupAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var integrityCommand = connection.CreateCommand();
        integrityCommand.CommandText = "PRAGMA integrity_check;";
        var integrity = await integrityCommand.ExecuteScalarAsync(cancellationToken);
        if (!string.Equals(integrity as string, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("O arquivo de backup do SQLite está corrompido.");

        await using var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('__EFMigrationsHistory', 'Library');";
        var expectedTables = Convert.ToInt32(await schemaCommand.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        if (expectedTables != 2) throw new InvalidDataException("O arquivo selecionado não contém um backup válido do SeuManga.");
    }

    private static SqliteConnection GetConnection(SeuMangaDbContext db) =>
        db.Database.GetDbConnection() as SqliteConnection
        ?? throw new InvalidOperationException("O banco configurado não utiliza SQLite.");

    private static SqliteConnection CreateConnection(string path, SqliteOpenMode mode) =>
        new(new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false }.ToString());

    private static string GetFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Informe um caminho de arquivo.", nameof(path));
        return Path.GetFullPath(path);
    }

    private static void EnsureDifferentDatabase(SqliteConnection connection, string otherPath)
    {
        var databasePath = connection.DataSource;
        if (string.IsNullOrWhiteSpace(databasePath) ||
            string.Equals(Path.GetFullPath(databasePath), otherPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("O arquivo de backup deve ser diferente do banco de dados em uso.", nameof(otherPath));
    }
}
