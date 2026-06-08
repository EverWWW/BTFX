namespace BTFX.Models.Activation;

/// <summary>
/// 激活操作结果。
/// </summary>
public sealed record ActivationResult(bool IsSuccess, string Message)
{
    public static ActivationResult Success(string message) => new(true, message);

    public static ActivationResult Failed(string message) => new(false, message);
}
