using BTFX.Services.Interfaces;

namespace BTFX.Helpers;

public static class RuntimeDependencyMessages
{
    public static string Format(
        RuntimeDependencyCheckResult result,
        ILocalizationService localizationService)
    {
        return string.Join(
            Environment.NewLine,
            result.Issues.Select(issue => localizationService.GetString(GetResourceKey(issue.Code), issue.Detail)));
    }

    private static string GetResourceKey(RuntimeDependencyIssueCode code) => code switch
    {
        RuntimeDependencyIssueCode.FfmpegMissing => "RuntimeDependency.FfmpegMissing",
        RuntimeDependencyIssueCode.FfprobeMissing => "RuntimeDependency.FfprobeMissing",
        RuntimeDependencyIssueCode.AlgorithmExecutableMissing => "RuntimeDependency.AlgorithmExecutableMissing",
        RuntimeDependencyIssueCode.AlgorithmRuntimeMissing => "RuntimeDependency.AlgorithmRuntimeMissing",
        RuntimeDependencyIssueCode.DahengRuntimeUnavailable => "RuntimeDependency.DahengRuntimeUnavailable",
        _ => "RuntimeDependency.Unknown"
    };
}
