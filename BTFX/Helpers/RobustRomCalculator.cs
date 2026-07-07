using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;

namespace BTFX.Helpers;

public sealed class RobustRomValues
{
    public double? LeftHipRomDeg { get; set; }

    public double? RightHipRomDeg { get; set; }

    public double? LeftKneeRomDeg { get; set; }

    public double? RightKneeRomDeg { get; set; }

    public double? LeftAnkleRomDeg { get; set; }

    public double? RightAnkleRomDeg { get; set; }

    public bool HasCorrections { get; set; }
}

public static class RobustRomCalculator
{
    private const double MinimumAbsoluteDifferenceDeg = 15d;
    private const double MinimumRatioDifference = 1.25d;
    private const double LowerPercentile = 0.05d;
    private const double UpperPercentile = 0.95d;

    public static RobustRomValues Calculate(
        string? outputDirectory,
        JsonObject? resultRoot,
        double? fps,
        RobustRomValues reported)
    {
        var csvPath = ResolveJointAngleCsvPath(outputDirectory);
        if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
        {
            return reported;
        }

        var samples = ReadSamples(csvPath);
        if (samples.Count == 0)
        {
            return reported;
        }

        var stableRanges = ResolveStableRanges(resultRoot, samples);
        var robust = new RobustRomValues
        {
            LeftHipRomDeg = CalculateRom(samples, stableRanges, "left hip"),
            RightHipRomDeg = CalculateRom(samples, stableRanges, "right hip"),
            LeftKneeRomDeg = CalculateRom(samples, stableRanges, "left knee"),
            RightKneeRomDeg = CalculateRom(samples, stableRanges, "right knee"),
            LeftAnkleRomDeg = CalculateRom(samples, stableRanges, "left ankle"),
            RightAnkleRomDeg = CalculateRom(samples, stableRanges, "right ankle")
        };

        return new RobustRomValues
        {
            LeftHipRomDeg = ChooseRom(reported.LeftHipRomDeg, robust.LeftHipRomDeg, reported),
            RightHipRomDeg = ChooseRom(reported.RightHipRomDeg, robust.RightHipRomDeg, reported),
            LeftKneeRomDeg = ChooseRom(reported.LeftKneeRomDeg, robust.LeftKneeRomDeg, reported),
            RightKneeRomDeg = ChooseRom(reported.RightKneeRomDeg, robust.RightKneeRomDeg, reported),
            LeftAnkleRomDeg = ChooseRom(reported.LeftAnkleRomDeg, robust.LeftAnkleRomDeg, reported),
            RightAnkleRomDeg = ChooseRom(reported.RightAnkleRomDeg, robust.RightAnkleRomDeg, reported),
            HasCorrections = reported.HasCorrections
        };
    }

    private static double? ChooseRom(double? reported, double? robust, RobustRomValues result)
    {
        if (robust is not > 0)
        {
            return reported;
        }

        if (reported is not > 0)
        {
            result.HasCorrections = true;
            return robust;
        }

        var difference = reported.Value - robust.Value;
        if (difference >= MinimumAbsoluteDifferenceDeg
            && reported.Value / robust.Value >= MinimumRatioDifference)
        {
            result.HasCorrections = true;
            return robust;
        }

        return reported;
    }

    private static List<AngleSample> ReadSamples(string csvPath)
    {
        var lines = File.ReadLines(csvPath).ToList();
        if (lines.Count < 2)
        {
            return [];
        }

        var headers = lines[0]
            .Split(',')
            .Select((name, index) => new { Name = NormalizeHeader(name), Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.OrdinalIgnoreCase);

        var samples = new List<AngleSample>();
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(',');
            var frame = ReadInt(parts, headers, "frame")
                ?? ReadInt(parts, headers, "frame number")
                ?? ReadInt(parts, headers, "帧号")
                ?? ReadIntAt(parts, 0);
            if (frame is null)
            {
                continue;
            }

            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in new[] { "left hip", "right hip", "left knee", "right knee", "left ankle", "right ankle" })
            {
                var value = ReadDouble(parts, headers, name);
                if (value.HasValue && !IsMissingValue(value.Value))
                {
                    values[name] = value.Value;
                }
            }

            if (values.Count > 0)
            {
                samples.Add(new AngleSample(frame.Value, values));
            }
        }

        return samples;
    }

    private static IReadOnlyList<FrameRange> ResolveStableRanges(JsonObject? root, IReadOnlyList<AngleSample> samples)
    {
        var cycles = EnumerateCycles(root?["gait_cycle"] as JsonObject)
            .Select(cycle => new FrameRange(ReadInt(cycle, "start_frame") ?? -1, ReadInt(cycle, "end_frame") ?? -1))
            .Where(range => range.StartFrame >= 0 && range.EndFrame > range.StartFrame)
            .OrderBy(range => range.StartFrame)
            .ToList();

        if (cycles.Count > 0)
        {
            var candidates = cycles.Count > 2
                ? cycles.Skip(1).Take(cycles.Count - 2).ToList()
                : cycles;
            if (candidates.Count > 3)
            {
                var start = Math.Max(0, (candidates.Count - 3) / 2);
                candidates = candidates.Skip(start).Take(3).ToList();
            }

            return candidates;
        }

        var firstFrame = samples.Min(sample => sample.Frame);
        var lastFrame = samples.Max(sample => sample.Frame);
        var total = lastFrame - firstFrame + 1;
        if (total <= 0)
        {
            return [];
        }

        var startFrame = firstFrame + (int)Math.Round(total * 0.15d);
        var endFrame = firstFrame + (int)Math.Round(total * 0.85d);
        return [new FrameRange(startFrame, Math.Max(startFrame + 1, endFrame))];
    }

    private static double? CalculateRom(
        IReadOnlyList<AngleSample> samples,
        IReadOnlyList<FrameRange> ranges,
        string valueName)
    {
        var values = samples
            .Where(sample => ranges.Count == 0 || ranges.Any(range => sample.Frame >= range.StartFrame && sample.Frame < range.EndFrame))
            .Select(sample => sample.Values.TryGetValue(valueName, out var value) ? value : double.NaN)
            .Where(value => !double.IsNaN(value) && !double.IsInfinity(value))
            .OrderBy(value => value)
            .ToList();

        if (values.Count < 5)
        {
            return null;
        }

        var low = Percentile(values, LowerPercentile);
        var high = Percentile(values, UpperPercentile);
        return high > low ? high - low : null;
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var position = (sortedValues.Count - 1) * percentile;
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        var weight = position - lowerIndex;
        return sortedValues[lowerIndex] * (1d - weight) + sortedValues[upperIndex] * weight;
    }

    private static IEnumerable<JsonObject> EnumerateCycles(JsonObject? gaitCycle)
    {
        if (gaitCycle is null)
        {
            yield break;
        }

        foreach (var key in new[] { "cycles", "left_cycles", "right_cycles" })
        {
            if (gaitCycle[key] is not JsonArray cycles)
            {
                continue;
            }

            foreach (var node in cycles.OfType<JsonObject>())
            {
                yield return node;
            }
        }
    }

    private static string? ResolveJointAngleCsvPath(string? outputDirectory)
    {
        return !string.IsNullOrWhiteSpace(outputDirectory) && Directory.Exists(outputDirectory)
            ? Directory.GetFiles(outputDirectory, "joint_angle.csv", SearchOption.AllDirectories).FirstOrDefault()
            : null;
    }

    private static string NormalizeHeader(string value)
    {
        return value.Trim().Trim('\uFEFF').Replace('_', ' ').ToLowerInvariant();
    }

    private static int? ReadInt(string[] parts, IReadOnlyDictionary<string, int> headers, string name)
    {
        return headers.TryGetValue(name, out var index)
               && index >= 0
               && index < parts.Length
               && int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static int? ReadIntAt(string[] parts, int index)
    {
        return index >= 0
               && index < parts.Length
               && int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static int? ReadInt(JsonObject? obj, string name)
    {
        if (obj?[name] is JsonValue value && value.TryGetValue<int>(out var intValue))
        {
            return intValue;
        }

        return null;
    }

    private static double? ReadDouble(string[] parts, IReadOnlyDictionary<string, int> headers, string name)
    {
        return headers.TryGetValue(name, out var index)
               && index >= 0
               && index < parts.Length
               && double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static bool IsMissingValue(double value)
    {
        return Math.Abs(value) < 0.0000001d;
    }

    private sealed record AngleSample(int Frame, IReadOnlyDictionary<string, double> Values);

    private sealed record FrameRange(int StartFrame, int EndFrame);
}
