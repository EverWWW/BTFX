namespace BTFX.Models;

/// <summary>
/// 在线更新信息。
/// </summary>
public sealed record AppUpdateInfo(
    string Version,
    string PackageUrl,
    string Detail);
