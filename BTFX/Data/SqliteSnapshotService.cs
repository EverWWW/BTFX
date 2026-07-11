using System.IO;
using Microsoft.Data.Sqlite;

namespace BTFX.Data;

public sealed class SqliteSnapshotService
{
    public async Task CreateSnapshotAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        DeleteDatabaseFiles(destinationPath);

        await using var source = new SqliteConnection($"Data Source={sourcePath};Mode=ReadOnly");
        await source.OpenAsync(cancellationToken);
        await using var destination = new SqliteConnection($"Data Source={destinationPath}");
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
        await destination.CloseAsync();
        await source.CloseAsync();

        if (!await ValidateAsync(destinationPath, cancellationToken))
        {
            DeleteDatabaseFiles(destinationPath);
            throw new InvalidDataException("SQLite 数据库快照完整性校验失败。");
        }
    }

    public async Task<bool> ValidateAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(databasePath))
        {
            return false;
        }

        try
        {
            await using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken);
            var result = await new SqliteCommand("PRAGMA integrity_check;", connection)
                .ExecuteScalarAsync(cancellationToken);
            return string.Equals(result?.ToString(), "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    internal static void DeleteDatabaseFiles(string databasePath)
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in DatabaseRecoveryManager.GetDatabaseFiles(databasePath))
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
