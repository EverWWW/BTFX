using System.IO;
using BTFX.Helpers;
using BTFX.Models.Camera;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

public sealed class RuntimeDependencyPreflightService : IRuntimeDependencyPreflightService
{
    private readonly string _baseDirectory;
    private readonly Action _probeDahengRuntime;
    private readonly Func<string?, string> _resolveAlgorithmExecutable;

    public RuntimeDependencyPreflightService(DahengCameraRuntime dahengRuntime)
        : this(AppContext.BaseDirectory, () => dahengRuntime.GetInitializedFactory(), AlgorithmExecutableResolver.Resolve)
    {
    }

    internal RuntimeDependencyPreflightService(
        string baseDirectory,
        Action probeDahengRuntime,
        Func<string?, string>? resolveAlgorithmExecutable = null)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory);
        _probeDahengRuntime = probeDahengRuntime;
        _resolveAlgorithmExecutable = resolveAlgorithmExecutable ?? AlgorithmExecutableResolver.Resolve;
    }

    public RuntimeDependencyCheckResult CheckAnalysis(string? configuredAlgorithmPath)
    {
        var issues = new List<RuntimeDependencyIssue>();
        AddFfmpegIssueIfMissing(issues);

        var ffprobePath = Path.Combine(_baseDirectory, "ffmpeg", "ffprobe.exe");
        if (!File.Exists(ffprobePath))
        {
            issues.Add(new(RuntimeDependencyIssueCode.FfprobeMissing, ffprobePath));
        }

        var exePath = _resolveAlgorithmExecutable(configuredAlgorithmPath);
        if (!File.Exists(exePath))
        {
            issues.Add(new(RuntimeDependencyIssueCode.AlgorithmExecutableMissing, exePath));
        }

        var algorithmDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;
        var runtimeDirectory = Path.Combine(algorithmDirectory, "_internal");
        if (!Directory.Exists(runtimeDirectory))
        {
            issues.Add(new(RuntimeDependencyIssueCode.AlgorithmRuntimeMissing, runtimeDirectory));
        }

        return new RuntimeDependencyCheckResult(issues);
    }

    public RuntimeDependencyCheckResult CheckCamera(CameraCaptureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var issues = new List<RuntimeDependencyIssue>();
        AddFfmpegIssueIfMissing(issues);

        if (settings.ResolveBackend() == CameraCaptureBackend.Daheng)
        {
            try
            {
                _probeDahengRuntime();
            }
            catch (Exception ex)
            {
                issues.Add(new(RuntimeDependencyIssueCode.DahengRuntimeUnavailable, GetRootMessage(ex)));
            }
        }

        return new RuntimeDependencyCheckResult(issues);
    }

    private void AddFfmpegIssueIfMissing(ICollection<RuntimeDependencyIssue> issues)
    {
        var ffmpegPath = Path.Combine(_baseDirectory, "ffmpeg", "ffmpeg.exe");
        if (!File.Exists(ffmpegPath))
        {
            issues.Add(new(RuntimeDependencyIssueCode.FfmpegMissing, ffmpegPath));
        }
    }

    private static string GetRootMessage(Exception exception)
    {
        while (exception.InnerException != null)
        {
            exception = exception.InnerException;
        }

        return exception.Message;
    }
}
