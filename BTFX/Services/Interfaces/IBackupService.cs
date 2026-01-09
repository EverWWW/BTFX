namespace BTFX.Services.Interfaces;

/// <summary>
/// 备份服务接口
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// 创建备份
    /// </summary>
    /// <returns>备份文件路径</returns>
    Task<string> CreateBackupAsync();

    /// <summary>
    /// 恢复备份
    /// </summary>
    /// <param name="backupFilePath">备份文件路径</param>
    /// <returns>是否成功</returns>
    Task<bool> RestoreBackupAsync(string backupFilePath);

    /// <summary>
    /// 获取所有备份文件
    /// </summary>
    /// <returns>备份文件列表（文件名和创建时间）</returns>
    Task<List<(string FileName, DateTime CreatedAt, long FileSizeBytes)>> GetBackupFilesAsync();

    /// <summary>
    /// 删除备份文件
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteBackupAsync(string fileName);

    /// <summary>
    /// 清理旧备份（保留指定数量的最新备份）
    /// </summary>
    /// <param name="retainCount">保留数量</param>
    /// <returns>删除的文件数量</returns>
    Task<int> CleanupOldBackupsAsync(int retainCount);

    /// <summary>
    /// 启动自动备份
    /// </summary>
    void StartAutoBackup();

    /// <summary>
    /// 停止自动备份
    /// </summary>
    void StopAutoBackup();
}
