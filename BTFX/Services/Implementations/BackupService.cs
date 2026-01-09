using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

/// <summary>
/// 备份服务实现（占位实现，第四阶段完善）
/// </summary>
public class BackupService : IBackupService
{
    // TODO: 注入配置和文件系统服务

    /// <inheritdoc/>
    public Task<string> CreateBackupAsync()
    {
        // TODO: 备份数据库文件
        return Task.FromResult(string.Empty);
    }

    /// <inheritdoc/>
    public Task<bool> RestoreBackupAsync(string backupFilePath)
    {
        // TODO: 恢复数据库
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<List<(string FileName, DateTime CreatedAt, long FileSizeBytes)>> GetBackupFilesAsync()
    {
        // TODO: 读取备份文件夹
        return Task.FromResult(new List<(string, DateTime, long)>());
    }

    /// <inheritdoc/>
    public Task<bool> DeleteBackupAsync(string fileName)
    {
        // TODO: 删除备份文件
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<int> CleanupOldBackupsAsync(int retainCount)
    {
        // TODO: 清理旧备份
        return Task.FromResult(0);
    }

    /// <inheritdoc/>
    public void StartAutoBackup()
    {
        // TODO: 启动定时备份任务
    }

    /// <inheritdoc/>
    public void StopAutoBackup()
    {
        // TODO: 停止定时备份任务
    }
}
