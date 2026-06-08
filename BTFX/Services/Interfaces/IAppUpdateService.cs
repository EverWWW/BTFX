namespace BTFX.Services.Interfaces;

/// <summary>
/// 在线更新检查服务。
/// </summary>
public interface IAppUpdateService
{
    Task CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}
