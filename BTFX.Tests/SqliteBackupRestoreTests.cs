using BTFX.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BTFX.Tests;

public sealed class SqliteBackupRestoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "btfx-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreateSnapshotAsync_IncludesCommittedWalData()
    {
        var sourcePath = Path.Combine(_root, "source.db");
        var snapshotPath = Path.Combine(_root, "snapshot.db");
        Directory.CreateDirectory(_root);

        await using var source = new SqliteConnection($"Data Source={sourcePath}");
        await source.OpenAsync();
        await new SqliteCommand("PRAGMA journal_mode=WAL;", source).ExecuteNonQueryAsync();
        await new SqliteCommand("CREATE TABLE Items(Id INTEGER PRIMARY KEY, Name TEXT);", source).ExecuteNonQueryAsync();
        await new SqliteCommand("INSERT INTO Items(Name) VALUES ('kept-in-wal');", source).ExecuteNonQueryAsync();

        var service = new SqliteSnapshotService();
        await service.CreateSnapshotAsync(sourcePath, snapshotPath);

        Assert.True(await service.ValidateAsync(snapshotPath));
        await using var snapshot = new SqliteConnection($"Data Source={snapshotPath};Mode=ReadOnly");
        await snapshot.OpenAsync();
        var value = await new SqliteCommand("SELECT Name FROM Items LIMIT 1;", snapshot).ExecuteScalarAsync();
        Assert.Equal("kept-in-wal", value);
    }

    [Fact]
    public async Task StageAsync_RejectsCorruptDatabase()
    {
        var livePath = Path.Combine(_root, "Data", "Database", "btfx.db");
        var configPath = Path.Combine(_root, "Data", "Config");
        var pendingPath = Path.Combine(_root, "Data", "Temp", "PendingRestore");
        var corruptPath = Path.Combine(_root, "corrupt.db");
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(corruptPath, "not a sqlite database");
        var manager = new PendingRestoreManager(livePath, configPath, pendingPath, new SqliteSnapshotService());

        await Assert.ThrowsAnyAsync<Exception>(() => manager.StageAsync(corruptPath, null));

        Assert.False(Directory.Exists(pendingPath));
    }

    [Fact]
    public async Task ApplyIfPresent_ReplacesDatabaseAndCopiesConfiguration()
    {
        var livePath = Path.Combine(_root, "Data", "Database", "btfx.db");
        var configPath = Path.Combine(_root, "Data", "Config");
        var pendingPath = Path.Combine(_root, "Data", "Temp", "PendingRestore");
        var restorePath = Path.Combine(_root, "restore.db");
        var restoreConfig = Path.Combine(_root, "restore-config");
        Directory.CreateDirectory(Path.GetDirectoryName(livePath)!);
        Directory.CreateDirectory(configPath);
        Directory.CreateDirectory(restoreConfig);
        await CreateDatabaseAsync(livePath, "old");
        await CreateDatabaseAsync(restorePath, "new");
        await File.WriteAllTextAsync(Path.Combine(configPath, "settings.json"), "old-config");
        await File.WriteAllTextAsync(Path.Combine(restoreConfig, "settings.json"), "new-config");

        var manager = new PendingRestoreManager(livePath, configPath, pendingPath, new SqliteSnapshotService());
        await manager.StageAsync(restorePath, restoreConfig);
        await manager.ApplyIfPresentAsync();

        Assert.Equal("new", await ReadValueAsync(livePath));
        Assert.Equal("new-config", await File.ReadAllTextAsync(Path.Combine(configPath, "settings.json")));
        Assert.False(Directory.Exists(pendingPath));
    }

    private static async Task CreateDatabaseAsync(string path, string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await new SqliteCommand("CREATE TABLE Data(Value TEXT);", connection).ExecuteNonQueryAsync();
        var command = new SqliteCommand("INSERT INTO Data(Value) VALUES ($value);", connection);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ReadValueAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        await connection.OpenAsync();
        return (string?)await new SqliteCommand("SELECT Value FROM Data LIMIT 1;", connection).ExecuteScalarAsync();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
