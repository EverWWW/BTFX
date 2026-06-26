using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;

namespace BTFX.Helpers;

public sealed record AnalysisFrameCoverage(
    double Ratio,
    bool IsComplete,
    int? SideFirst,
    int? SideLast,
    int? FrontFirst,
    int? FrontLast);

public static class AnalysisFrameCoverageHelper
{
    public static AnalysisFrameCoverage? FromResultJson(JsonObject? root, string? outputDirectory)
    {
        if (root is null)
        {
            return null;
        }

        var side = ReadRange(root, "side");
        var front = ReadRange(root, "front");
        if (side.First is null && front.First is null)
        {
            return null;
        }

        var videoInfo = root["video_info"] as JsonObject;
        var fallbackTotalFrames = ReadInt(videoInfo, "frame_count")
            ?? EstimateFrameCount(ReadDouble(videoInfo, "fps"), ReadDouble(videoInfo, "duration_sec"));
        var sideTotal = ResolveInputFrameCount(outputDirectory, "side.mp4") ?? fallbackTotalFrames;
        var frontTotal = ResolveInputFrameCount(outputDirectory, "front.mp4") ?? fallbackTotalFrames;

        var ratios = new List<double>();
        if (CalculateRatio(side, sideTotal) is double sideRatio)
        {
            ratios.Add(sideRatio);
        }

        if (CalculateRatio(front, frontTotal) is double frontRatio)
        {
            ratios.Add(frontRatio);
        }

        if (ratios.Count == 0)
        {
            return null;
        }

        var ratio = ratios.Min();
        var isComplete = IsRangeComplete(side, sideTotal) && IsRangeComplete(front, frontTotal);
        return new AnalysisFrameCoverage(ratio, isComplete, side.First, side.Last, front.First, front.Last);
    }

    private static (int? First, int? Last) ReadRange(JsonObject root, string view)
    {
        return view == "side"
            ? (ReadInt(root, "side_data_first_valid_frame") ?? ReadInt(root, "side_trc_first_valid_frame"),
                ReadInt(root, "side_data_last_valid_frame") ?? ReadInt(root, "side_trc_last_valid_frame"))
            : (ReadInt(root, "front_data_first_valid_any_frame")
               ?? ReadInt(root, "front_data_first_valid_frame")
               ?? ReadInt(root, "front_trc_first_valid_any_frame")
               ?? ReadInt(root, "front_trc_first_valid_frame"),
                ReadInt(root, "front_data_last_valid_any_frame")
                ?? ReadInt(root, "front_data_last_valid_frame")
                ?? ReadInt(root, "front_trc_last_valid_any_frame")
                ?? ReadInt(root, "front_trc_last_valid_frame"));
    }

    private static double? CalculateRatio((int? First, int? Last) range, int? totalFrames)
    {
        if (range.First is not int first || range.Last is not int last || totalFrames is not > 0 || last < first)
        {
            return null;
        }

        var validFrames = Math.Clamp(last - first + 1, 0, totalFrames.Value);
        return Math.Clamp(validFrames / (double)totalFrames.Value, 0d, 1d);
    }

    private static bool IsRangeComplete((int? First, int? Last) range, int? totalFrames)
    {
        if (range.First is null && range.Last is null)
        {
            return true;
        }

        return range.First is <= 0
               && range.Last is int last
               && totalFrames is > 0
               && last >= totalFrames.Value - 1;
    }

    private static int? ResolveInputFrameCount(string? outputDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return null;
        }

        var path = Path.Combine(outputDirectory, "input", fileName);
        var metadata = VideoMetadataProbe.TryRead(path);
        return EstimateFrameCount(metadata?.FrameRate, metadata?.DurationSeconds);
    }

    private static int? EstimateFrameCount(double? fps, double? durationSec)
    {
        return fps is > 0 && durationSec is > 0
            ? (int)Math.Round(fps.Value * durationSec.Value, MidpointRounding.AwayFromZero)
            : null;
    }

    private static int? ReadInt(JsonObject? obj, string key)
    {
        if (obj is null || obj[key] is null)
        {
            return null;
        }

        return int.TryParse(obj[key]!.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static double? ReadDouble(JsonObject? obj, string key)
    {
        if (obj is null || obj[key] is null)
        {
            return null;
        }

        return double.TryParse(obj[key]!.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
