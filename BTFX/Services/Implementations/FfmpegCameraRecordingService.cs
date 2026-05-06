using System.Diagnostics;
using System.IO;
using BTFX.Models.Camera;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

public sealed class FfmpegCameraRecordingService : ICameraRecordingService
{
    public async Task<IReadOnlyList<CameraRecordingResult>> RecordAsync(
        CameraRecordingOptions options,
        IProgress<string>? logProgress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);

        var saveDirectory = Path.GetFullPath(options.SaveDirectory);
        Directory.CreateDirectory(saveDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var tasks = options.CameraNames
            .Select(cameraName =>
            {
                var safeTag = SafeCameraTag(cameraName);
                var aviFile = Path.Combine(saveDirectory, $"{timestamp}_{safeTag}.avi");
                var mp4File = Path.Combine(saveDirectory, $"{timestamp}_{safeTag}.mp4");
                return new CameraTask(cameraName, aviFile, mp4File);
            })
            .ToList();

        Report(logProgress, "准备录制");
        Report(logProgress, $"规格: {options.VideoSize} @ {options.FrameRate}fps, {options.DurationSeconds}s");
        foreach (var task in tasks)
        {
            Report(logProgress, $"相机: {task.CameraName}");
            Report(logProgress, $"AVI: {task.AviFile}");
            if (options.TranscodeToMp4)
            {
                Report(logProgress, $"MP4: {task.Mp4File}");
            }
        }

        var recordTasks = tasks
            .Select(task => RunRecordStageAsync(options, task, logProgress, cancellationToken))
            .ToArray();

        await Task.WhenAll(recordTasks);
        Report(logProgress, "录制阶段完成");

        if (options.TranscodeToMp4)
        {
            foreach (var task in tasks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RunTranscodeStageAsync(options, task, logProgress, cancellationToken);

                if (options.DeleteAviAfterMp4 && File.Exists(task.AviFile))
                {
                    File.Delete(task.AviFile);
                    Report(logProgress, $"已删除临时 AVI: {task.AviFile}");
                }
            }
        }

        Report(logProgress, "全部任务完成");
        return tasks
            .Select(task => new CameraRecordingResult(
                task.CameraName,
                task.AviFile,
                options.TranscodeToMp4 ? task.Mp4File : null))
            .ToList();
    }

    private static async Task RunRecordStageAsync(
        CameraRecordingOptions options,
        CameraTask task,
        IProgress<string>? logProgress,
        CancellationToken cancellationToken)
    {
        var arguments = BuildRecordArguments(options, task.AviFile, task.CameraName);
        Report(logProgress, $"开始录制: {task.CameraName}");
        Report(logProgress, $"{options.FfmpegPath} {arguments}");

        var exitCode = await RunProcessAsync(
            options.FfmpegPath,
            arguments,
            line => Report(logProgress, $"[{task.CameraName}] {line}"),
            cancellationToken);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"{task.CameraName} 录制失败，FFmpeg 退出码: {exitCode}");
        }

        if (!File.Exists(task.AviFile))
        {
            throw new FileNotFoundException($"{task.CameraName} 录制完成但未找到 AVI 文件", task.AviFile);
        }

        Report(logProgress, $"录制完成: {task.AviFile}");
    }

    private static async Task RunTranscodeStageAsync(
        CameraRecordingOptions options,
        CameraTask task,
        IProgress<string>? logProgress,
        CancellationToken cancellationToken)
    {
        var arguments = BuildTranscodeArguments(task.AviFile, task.Mp4File);
        Report(logProgress, $"开始转码: {task.CameraName}");
        Report(logProgress, $"{options.FfmpegPath} {arguments}");

        var exitCode = await RunProcessAsync(
            options.FfmpegPath,
            arguments,
            line => Report(logProgress, $"[{task.CameraName} 转码] {line}"),
            cancellationToken);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"{task.CameraName} 转码失败，FFmpeg 退出码: {exitCode}");
        }

        if (!File.Exists(task.Mp4File))
        {
            throw new FileNotFoundException($"{task.CameraName} 转码完成但未找到 MP4 文件", task.Mp4File);
        }

        Report(logProgress, $"转码完成: {task.Mp4File}");
    }

    private static async Task<int> RunProcessAsync(
        string fileName,
        string arguments,
        Action<string> logLine,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                logLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                logLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            throw;
        }
    }

    private static void ValidateOptions(CameraRecordingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.FfmpegPath) || !File.Exists(options.FfmpegPath))
        {
            throw new FileNotFoundException("找不到 ffmpeg.exe，请检查路径。", options.FfmpegPath);
        }

        if (string.IsNullOrWhiteSpace(options.SaveDirectory))
        {
            throw new ArgumentException("保存目录不能为空。", nameof(options));
        }

        if (options.CameraNames.Count == 0 || options.CameraNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("至少需要输入一个有效的相机名称。", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.VideoSize))
        {
            throw new ArgumentException("分辨率不能为空。", nameof(options));
        }

        if (options.FrameRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "帧率必须大于 0。");
        }

        if (options.DurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "录制时长必须大于 0。");
        }
    }

    private static string BuildRecordArguments(CameraRecordingOptions options, string aviFile, string cameraName)
    {
        return string.Join(
            " ",
            "-y",
            "-f dshow",
            "-rtbufsize 1024M",
            $"-video_size {Quote(options.VideoSize)}",
            $"-framerate {options.FrameRate}",
            "-vcodec mjpeg",
            $"-i {Quote($"video={cameraName}")}",
            $"-t {options.DurationSeconds}",
            "-c copy",
            Quote(aviFile));
    }

    private static string BuildTranscodeArguments(string aviFile, string mp4File)
    {
        return string.Join(
            " ",
            "-y",
            $"-i {Quote(aviFile)}",
            "-vf \"scale=in_range=pc:out_range=tv,format=yuv420p\"",
            "-c:v libx264",
            "-preset veryfast",
            "-crf 18",
            "-movflags +faststart",
            Quote(mp4File));
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private static string SafeCameraTag(string cameraName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = cameraName
            .Select(ch => invalidChars.Contains(ch) || ch is ' ' or '-' ? '_' : ch)
            .ToArray();
        return new string(chars);
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup when cancellation races process exit.
        }
    }

    private static void Report(IProgress<string>? progress, string message)
    {
        progress?.Report($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    private sealed record CameraTask(string CameraName, string AviFile, string Mp4File);
}
