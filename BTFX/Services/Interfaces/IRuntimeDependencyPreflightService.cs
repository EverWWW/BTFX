using BTFX.Models.Camera;

namespace BTFX.Services.Interfaces;

public interface IRuntimeDependencyPreflightService
{
    RuntimeDependencyCheckResult CheckAnalysis(string? configuredAlgorithmPath);

    RuntimeDependencyCheckResult CheckCamera(CameraCaptureSettings settings);
}

public sealed record RuntimeDependencyCheckResult(IReadOnlyList<RuntimeDependencyIssue> Issues)
{
    public bool IsReady => Issues.Count == 0;
}

public sealed record RuntimeDependencyIssue(RuntimeDependencyIssueCode Code, string Detail);

public enum RuntimeDependencyIssueCode
{
    FfmpegMissing,
    FfprobeMissing,
    AlgorithmExecutableMissing,
    AlgorithmRuntimeMissing,
    DahengRuntimeUnavailable
}
