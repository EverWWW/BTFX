using System.IO;
using Microsoft.Data.Sqlite;

namespace BTFX.Data;

public sealed class PendingRestoreManager
{
    private const string DatabaseFileName = "database.db";
    private readonly string _databasePath;
    private readonly string _configDirectory;
    private readonly string _pendingDirectory;
    private readonly SqliteSnapshotService _snapshotService;

    public PendingRestoreManager(
        string databasePath,
        string configDirectory,
        string pendingDirectory,
        SqliteSnapshotService snapshotService)
    {
        _databasePath = Path.GetFullPath(databasePath);
        _configDirectory = Path.GetFullPath(configDirectory);
        _pendingDirectory = Path.GetFullPath(pendingDirectory);
        _snapshotService = snapshotService;
    }

    public async Task StageAsync(
        string sourceDatabasePath,
        string? sourceConfigDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!await _snapshotService.ValidateAsync(sourceDatabasePath, cancellationToken))
        {
            throw new InvalidDataException("备份中的数据库文件已损坏或格式无效。");
        }

        var stagingDirectory = _pendingDirectory + ".staging-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(stagingDirectory);
            await _snapshotService.CreateSnapshotAsync(
                sourceDatabasePath,
                Path.Combine(stagingDirectory, DatabaseFileName),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(sourceConfigDirectory) && Directory.Exists(sourceConfigDirectory))
            {
                CopyJsonFiles(sourceConfigDirectory, Path.Combine(stagingDirectory, "Config"));
            }

            if (Directory.Exists(_pendingDirectory))
            {
                Directory.Delete(_pendingDirectory, recursive: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_pendingDirectory)!);
            Directory.Move(stagingDirectory, _pendingDirectory);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    public async Task<bool> ApplyIfPresentAsync(CancellationToken cancellationToken = default)
    {
        var pendingDatabase = Path.Combine(_pendingDirectory, DatabaseFileName);
        if (!File.Exists(pendingDatabase))
        {
            return false;
        }

        if (!await _snapshotService.ValidateAsync(pendingDatabase, cancellationToken))
        {
            throw new InvalidDataException("待恢复数据库完整性校验失败。");
        }

        string? recoveryDirectory = null;
        var liveDatabaseExisted = File.Exists(_databasePath);
        try
        {
            if (liveDatabaseExisted)
            {
                recoveryDirectory = new DatabaseRecoveryManager().CaptureExistingDatabase(_databasePath);
                if (Directory.Exists(_configDirectory))
                {
                    CopyJsonFiles(_configDirectory, Path.Combine(recoveryDirectory, "Config"));
                }
            }

            SqliteSnapshotService.DeleteDatabaseFiles(_databasePath);
            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            File.Copy(pendingDatabase, _databasePath, overwrite: true);

            var pendingConfig = Path.Combine(_pendingDirectory, "Config");
            if (Directory.Exists(pendingConfig))
            {
                CopyJsonFiles(pendingConfig, _configDirectory);
            }

            if (!await _snapshotService.ValidateAsync(_databasePath, cancellationToken))
            {
                throw new InvalidDataException("恢复后的数据库完整性校验失败。");
            }

            Directory.Delete(_pendingDirectory, recursive: true);
            return true;
        }
        catch
        {
            SqliteConnection.ClearAllPools();
            if (liveDatabaseExisted && !string.IsNullOrWhiteSpace(recoveryDirectory))
            {
                RestoreDatabaseFiles(recoveryDirectory, _databasePath);
                var recoveryConfig = Path.Combine(recoveryDirectory, "Config");
                if (Directory.Exists(recoveryConfig))
                {
                    CopyJsonFiles(recoveryConfig, _configDirectory);
                }
            }
            else
            {
                SqliteSnapshotService.DeleteDatabaseFiles(_databasePath);
            }

            throw;
        }
    }

    private static void RestoreDatabaseFiles(string recoveryDirectory, string databasePath)
    {
        SqliteSnapshotService.DeleteDatabaseFiles(databasePath);
        foreach (var sourcePath in Directory.GetFiles(recoveryDirectory))
        {
            var fileName = Path.GetFileName(sourcePath);
            if (fileName.StartsWith(Path.GetFileName(databasePath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, Path.Combine(Path.GetDirectoryName(databasePath)!, fileName), overwrite: true);
            }
        }
    }

    private static void CopyJsonFiles(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var file in Directory.GetFiles(sourceDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)), overwrite: true);
        }
    }
}
