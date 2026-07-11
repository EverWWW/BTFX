using BTFX.Models.Camera;
using BTFX.Services.Implementations;
using BTFX.Services.Interfaces;
using Xunit;

namespace BTFX.Tests;

public sealed class RuntimeDependencyPreflightTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"btfx-preflight-{Guid.NewGuid():N}");

    [Fact]
    public void CheckAnalysis_SucceedsWhenAllRequiredFilesExist()
    {
        var exePath = CreateCompleteAnalysisEnvironment();
        var service = new RuntimeDependencyPreflightService(_root, () => { }, path => path!);

        var result = service.CheckAnalysis(exePath);

        Assert.True(result.IsReady);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void CheckAnalysis_ReportsEveryMissingDependency()
    {
        Directory.CreateDirectory(_root);
        var exePath = Path.Combine(_root, "gait_analysis", "custom_engine_1.2.exe");
        var service = new RuntimeDependencyPreflightService(_root, () => { }, path => path!);

        var result = service.CheckAnalysis(exePath);

        Assert.False(result.IsReady);
        Assert.Contains(result.Issues, issue => issue.Code == RuntimeDependencyIssueCode.FfmpegMissing);
        Assert.Contains(result.Issues, issue => issue.Code == RuntimeDependencyIssueCode.FfprobeMissing);
        Assert.Contains(result.Issues, issue => issue.Code == RuntimeDependencyIssueCode.AlgorithmExecutableMissing);
        Assert.Contains(result.Issues, issue => issue.Code == RuntimeDependencyIssueCode.AlgorithmRuntimeMissing);
    }

    [Fact]
    public void CheckCamera_OnlyRequiresDahengRuntimeForDahengBackend()
    {
        CreateFile(Path.Combine(_root, "ffmpeg", "ffmpeg.exe"));
        var service = new RuntimeDependencyPreflightService(
            _root,
            () => throw new DllNotFoundException("Galaxy SDK runtime missing"));

        var yunxi = service.CheckCamera(new CameraCaptureSettings { DeviceType = CameraCaptureSettings.DeviceTypeYunxi });
        var daheng = service.CheckCamera(new CameraCaptureSettings { DeviceType = CameraCaptureSettings.DeviceTypeDaheng });

        Assert.True(yunxi.IsReady);
        Assert.False(daheng.IsReady);
        var issue = Assert.Single(daheng.Issues);
        Assert.Equal(RuntimeDependencyIssueCode.DahengRuntimeUnavailable, issue.Code);
        Assert.Contains("Galaxy SDK runtime missing", issue.Detail, StringComparison.Ordinal);
    }

    private string CreateCompleteAnalysisEnvironment()
    {
        CreateFile(Path.Combine(_root, "ffmpeg", "ffmpeg.exe"));
        CreateFile(Path.Combine(_root, "ffmpeg", "ffprobe.exe"));
        var exePath = Path.Combine(_root, "gait_analysis", "custom_engine_1.2.exe");
        CreateFile(exePath);
        Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(exePath)!, "_internal"));
        return exePath;
    }

    private static void CreateFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
