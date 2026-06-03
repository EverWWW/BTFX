using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;

namespace BTFX.Helpers;

public sealed record VideoProbeMetadata(double? FrameRate, double? DurationSeconds);

public static class VideoMetadataProbe
{
    public static VideoProbeMetadata? TryRead(string? videoPath, int timeoutMilliseconds = 3000)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
        {
            return null;
        }

        var ffprobePath = ResolveFfprobePath();
        if (string.IsNullOrWhiteSpace(ffprobePath))
        {
            return null;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-select_streams");
            startInfo.ArgumentList.Add("v:0");
            startInfo.ArgumentList.Add("-show_entries");
            startInfo.ArgumentList.Add("stream=avg_frame_rate,r_frame_rate,duration:format=duration");
            startInfo.ArgumentList.Add("-of");
            startInfo.ArgumentList.Add("json");
            startInfo.ArgumentList.Add(videoPath);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                TryKill(process);
                return null;
            }

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            var root = JsonNode.Parse(output)?.AsObject();
            var stream = root?["streams"]?.AsArray().FirstOrDefault()?.AsObject();
            var format = root?["format"] as JsonObject;
            var fps = ParseFrameRate(ReadString(stream, "avg_frame_rate"))
                ?? ParseFrameRate(ReadString(stream, "r_frame_rate"));
            var duration = ReadDouble(stream, "duration") ?? ReadDouble(format, "duration");
            return fps is > 0 || duration is > 0
                ? new VideoProbeMetadata(fps, duration)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveFfprobePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffprobe.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffprobe.exe"),
            @"D:\ffmpeg\bin\ffprobe.exe",
            @"C:\ffmpeg\bin\ffprobe.exe",
            "ffprobe.exe"
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private static string? ReadString(JsonObject? obj, string name)
    {
        return obj is not null && obj.TryGetPropertyValue(name, out var node)
            ? node?.ToString()
            : null;
    }

    private static double? ReadDouble(JsonObject? obj, string name)
    {
        return double.TryParse(
            ReadString(obj, name),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    private static double? ParseFrameRate(string? frameRateText)
    {
        if (string.IsNullOrWhiteSpace(frameRateText))
        {
            return null;
        }

        var parts = frameRateText.Split('/');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator)
            && denominator > 0)
        {
            return numerator / denominator;
        }

        return double.TryParse(frameRateText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
