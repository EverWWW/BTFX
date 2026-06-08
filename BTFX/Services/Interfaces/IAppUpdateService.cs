using BTFX.Models;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 在线更新检查服务。
/// </summary>
public interface IAppUpdateService
{
    Task CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task<AppUpdateInfo?> CheckForUpdatesAsync(bool force, CancellationToken cancellationToken = default);

    Task<string> DownloadUpdatePackageAsync(
        AppUpdateInfo updateInfo,
        IProgress<OperationProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    void StartInstallerAndShutdown(string installerPath);
}
