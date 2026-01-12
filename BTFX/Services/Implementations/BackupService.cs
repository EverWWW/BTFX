using System.IO;
using System.Timers;
using BTFX.Common;
using BTFX.Data;
using BTFX.Services.Interfaces;
using ToolHelper.DataProcessing.Compression;
using ToolHelper.LoggingDiagnostics.Abstractions;
using Timer = System.Timers.Timer;

namespace BTFX.Services.Implementations;

/// <summary>
/// 备份服务实现
/// 使用 ZipHelper 进行数据库和配置文件的备份与恢复
/// </summary>
public class BackupService : IBackupService, IDisposable
{
    private readonly ILogHelper? _logHelper;
    private readonly ISettingsService? _settingsService;
    private readonly ZipHelper _zipHelper;
    private Timer? _autoBackupTimer;
    private bool _disposed;

    /// <summary>
    /// 备份目录
    /// </summary>
    private string BackupDirectory => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        Constants.BACKUP_DIRECTORY);

    /// <summary>
    /// 数据库文件路径
    /// </summary>
    private string DatabasePath => DatabaseFactory.DatabasePath;

    /// <summary>
    /// 配置文件目录
    /// </summary>
    private string ConfigDirectory => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        Constants.CONFIG_DIRECTORY);

    /// <summary>
    /// 构造函数
    /// </summary>
    public BackupService()
    {
        _zipHelper = new ZipHelper();

        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
            _settingsService = App.Services?.GetService(typeof(ISettingsService)) as ISettingsService;
        }
        catch { }

        // 确保备份目录存在
        if (!Directory.Exists(BackupDirectory))
        {
            Directory.CreateDirectory(BackupDirectory);
        }
    }

    /// <inheritdoc/>
    public async Task<string> CreateBackupAsync()
    {
        try
        {
            _logHelper?.Information("开始创建备份...");

            // 生成备份文件名
            var timestamp = DateTime.Now.ToString(Constants.BACKUP_TIMESTAMP_FORMAT);
            var backupFileName = $"{Constants.BACKUP_PREFIX}{timestamp}.zip";
            var backupFilePath = Path.Combine(BackupDirectory, backupFileName);

            // 创建临时目录用于收集备份文件
            var tempDir = Path.Combine(Path.GetTempPath(), $"BTFX_Backup_{timestamp}");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
            Directory.CreateDirectory(tempDir);

            try
            {
                // 1. 复制数据库文件
                if (File.Exists(DatabasePath))
                {
                    var dbDestPath = Path.Combine(tempDir, Constants.DATABASE_FILENAME);
                    File.Copy(DatabasePath, dbDestPath, true);
                    _logHelper?.Information($"已复制数据库文件: {Constants.DATABASE_FILENAME}");
                }

                // 2. 复制配置文件
                if (Directory.Exists(ConfigDirectory))
                {
                    var configDestDir = Path.Combine(tempDir, "Config");
                    Directory.CreateDirectory(configDestDir);

                    foreach (var file in Directory.GetFiles(ConfigDirectory, "*.json"))
                    {
                        var destFile = Path.Combine(configDestDir, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                    }
                    _logHelper?.Information("已复制配置文件");
                }

                // 3. 创建备份信息文件
                var backupInfo = new
                {
                    CreatedAt = DateTime.Now.ToString(Constants.DATETIME_FORMAT),
                    AppVersion = Constants.VERSION_FULL,
                    DatabaseVersion = DatabaseInitializer.CurrentDatabaseVersion,
                    MachineName = Environment.MachineName
                };
                var infoFilePath = Path.Combine(tempDir, "backup_info.json");
                var json = System.Text.Json.JsonSerializer.Serialize(backupInfo, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(infoFilePath, json);

                // 4. 压缩为ZIP文件
                await _zipHelper.CompressDirectoryAsync(tempDir, backupFilePath, false);

                var fileInfo = new FileInfo(backupFilePath);
                _logHelper?.Information($"备份创建成功: {backupFileName}, 大小: {fileInfo.Length / 1024.0:F2} KB");

                return backupFilePath;
            }
            finally
            {
                // 清理临时目录
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("创建备份失败", ex);
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RestoreBackupAsync(string backupFilePath)
    {
        try
        {
            if (!File.Exists(backupFilePath))
            {
                _logHelper?.Error($"备份文件不存在: {backupFilePath}");
                return false;
            }

            _logHelper?.Information($"开始恢复备份: {Path.GetFileName(backupFilePath)}");

            // 创建临时目录用于解压
            var tempDir = Path.Combine(Path.GetTempPath(), $"BTFX_Restore_{DateTime.Now:yyyyMMddHHmmss}");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }

            try
            {
                // 1. 解压备份文件
                await _zipHelper.ExtractAsync(backupFilePath, tempDir, true);

                // 2. 恢复数据库文件
                var dbSourcePath = Path.Combine(tempDir, Constants.DATABASE_FILENAME);
                if (File.Exists(dbSourcePath))
                {
                    // 先备份当前数据库
                    var currentBackup = DatabasePath + ".bak";
                    if (File.Exists(DatabasePath))
                    {
                        File.Copy(DatabasePath, currentBackup, true);
                    }

                    try
                    {
                        File.Copy(dbSourcePath, DatabasePath, true);
                        _logHelper?.Information("数据库文件已恢复");
                    }
                    catch
                    {
                        // 恢复失败，还原当前数据库
                        if (File.Exists(currentBackup))
                        {
                            File.Copy(currentBackup, DatabasePath, true);
                        }
                        throw;
                    }
                    finally
                    {
                        if (File.Exists(currentBackup))
                        {
                            File.Delete(currentBackup);
                        }
                    }
                }

                // 3. 恢复配置文件
                var configSourceDir = Path.Combine(tempDir, "Config");
                if (Directory.Exists(configSourceDir))
                {
                    if (!Directory.Exists(ConfigDirectory))
                    {
                        Directory.CreateDirectory(ConfigDirectory);
                    }

                    foreach (var file in Directory.GetFiles(configSourceDir, "*.json"))
                    {
                        var destFile = Path.Combine(ConfigDirectory, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                    }
                    _logHelper?.Information("配置文件已恢复");
                }

                _logHelper?.Information("备份恢复成功");
                return true;
            }
            finally
            {
                // 清理临时目录
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("恢复备份失败", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<List<(string FileName, DateTime CreatedAt, long FileSizeBytes)>> GetBackupFilesAsync()
    {
        var result = new List<(string FileName, DateTime CreatedAt, long FileSizeBytes)>();

        try
        {
            if (!Directory.Exists(BackupDirectory))
            {
                return Task.FromResult(result);
            }

            var files = Directory.GetFiles(BackupDirectory, $"{Constants.BACKUP_PREFIX}*.zip");

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                result.Add((fileInfo.Name, fileInfo.CreationTime, fileInfo.Length));
            }

            // 按创建时间降序排序
            result = result.OrderByDescending(x => x.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            _logHelper?.Error("获取备份文件列表失败", ex);
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteBackupAsync(string fileName)
    {
        try
        {
            var filePath = Path.Combine(BackupDirectory, fileName);

            if (!File.Exists(filePath))
            {
                _logHelper?.Warning($"备份文件不存在: {fileName}");
                return Task.FromResult(false);
            }

            File.Delete(filePath);
            _logHelper?.Information($"已删除备份文件: {fileName}");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除备份文件失败: {fileName}", ex);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public async Task<int> CleanupOldBackupsAsync(int retainCount)
    {
        try
        {
            if (retainCount < 1)
            {
                retainCount = 1;
            }

            var backups = await GetBackupFilesAsync();

            if (backups.Count <= retainCount)
            {
                return 0;
            }

            var toDelete = backups.Skip(retainCount).ToList();
            var deletedCount = 0;

            foreach (var backup in toDelete)
            {
                if (await DeleteBackupAsync(backup.FileName))
                {
                    deletedCount++;
                }
            }

            _logHelper?.Information($"已清理 {deletedCount} 个过期备份");
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logHelper?.Error("清理过期备份失败", ex);
            return 0;
        }
    }

    /// <inheritdoc/>
    public void StartAutoBackup()
    {
        try
        {
            StopAutoBackup();

            var settings = _settingsService?.CurrentSettings?.AutoBackup;
            if (settings == null || !settings.Enabled)
            {
                _logHelper?.Information("自动备份未启用");
                return;
            }

            // 计算下次备份时间
            var now = DateTime.Now;
            var backupTime = DateTime.Today.Add(TimeSpan.Parse(settings.Time));

            if (backupTime <= now)
            {
                // 如果今天的备份时间已过，设置为明天
                backupTime = backupTime.AddDays(1);
            }

            var interval = (backupTime - now).TotalMilliseconds;

            _autoBackupTimer = new Timer(interval);
            _autoBackupTimer.Elapsed += OnAutoBackupTimerElapsed;
            _autoBackupTimer.AutoReset = false;
            _autoBackupTimer.Start();

            _logHelper?.Information($"自动备份已启动，下次备份时间: {backupTime:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("启动自动备份失败", ex);
        }
    }

    /// <inheritdoc/>
    public void StopAutoBackup()
    {
        if (_autoBackupTimer != null)
        {
            _autoBackupTimer.Stop();
            _autoBackupTimer.Dispose();
            _autoBackupTimer = null;
            _logHelper?.Information("自动备份已停止");
        }
    }

    /// <summary>
    /// 自动备份定时器事件
    /// </summary>
    private async void OnAutoBackupTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            _logHelper?.Information("执行自动备份...");

            // 创建备份
            var backupPath = await CreateBackupAsync();

            if (!string.IsNullOrEmpty(backupPath))
            {
                // 清理旧备份
                var retainCount = _settingsService?.CurrentSettings?.AutoBackup?.RetainCount ?? 7;
                await CleanupOldBackupsAsync(retainCount);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("自动备份执行失败", ex);
        }
        finally
        {
            // 重新启动定时器（设置下次备份）
            StartAutoBackup();
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            StopAutoBackup();
            _disposed = true;
        }
    }
}
