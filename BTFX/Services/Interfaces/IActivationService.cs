using BTFX.Models.Activation;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 软件激活服务。
/// </summary>
public interface IActivationService
{
    bool IsActivated { get; }

    SoftKey GetCurrentMachineInfo();

    string GenerateLicenseKey(SoftKey softKey);

    Task<ActivationResult> ActivateOnlineAsync(string productCode, CancellationToken cancellationToken = default);

    ActivationResult ActivateOffline(string productCode, string licenseKey);
}
