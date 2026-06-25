using System.Text.Json.Nodes;

namespace BTFX.Helpers;

public sealed record GaitPhaseMetrics(
    double? MeanStanceTimeSec,
    double? MeanSwingTimeSec,
    double? MeanDoubleSupportTimeSec,
    double? MeanSingleSupportTimeSec);

public sealed record GaitEventPhaseMetrics(
    double? LeftStanceTimeSec,
    double? RightStanceTimeSec,
    double? LeftSwingTimeSec,
    double? RightSwingTimeSec,
    double? LeftStanceRatioPct,
    double? RightStanceRatioPct,
    double? LeftSwingRatioPct,
    double? RightSwingRatioPct)
{
    public double? MeanStanceTimeSec => Average(LeftStanceTimeSec, RightStanceTimeSec);
    public double? MeanSwingTimeSec => Average(LeftSwingTimeSec, RightSwingTimeSec);

    private static double? Average(double? left, double? right)
    {
        var values = new[] { left, right }
            .Where(value => value is > 0)
            .Select(value => value!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Average();
    }
}

public static class GaitPhaseMetricsCalculator
{
    public static GaitPhaseMetrics Calculate(JsonObject? gaitCycle)
    {
        if (gaitCycle is null)
        {
            return new(null, null, null, null);
        }

        var stanceTimes = new List<double>();
        var swingTimes = new List<double>();
        var doubleSupportTimes = new List<double>();
        var singleSupportTimes = new List<double>();

        foreach (var cycle in EnumerateCycles(gaitCycle))
        {
            var side = ReadString(cycle, "side")?.Trim().ToLowerInvariant();
            if (cycle["phases"] is not JsonArray phases)
            {
                continue;
            }

            var leftSingle = 0d;
            var rightSingle = 0d;
            var doubleSupportTotal = 0d;

            foreach (var phase in phases.OfType<JsonObject>())
            {
                var name = ReadString(phase, "name")?.Trim().ToLowerInvariant();
                var duration = ReadDouble(phase, "duration_sec");
                if (string.IsNullOrWhiteSpace(name) || duration is not > 0)
                {
                    continue;
                }

                if (name.Contains("double_support", StringComparison.OrdinalIgnoreCase))
                {
                    doubleSupportTotal += duration.Value;
                    doubleSupportTimes.Add(duration.Value);
                }
                else if (name.Contains("left_single_support", StringComparison.OrdinalIgnoreCase))
                {
                    leftSingle += duration.Value;
                    singleSupportTimes.Add(duration.Value);
                }
                else if (name.Contains("right_single_support", StringComparison.OrdinalIgnoreCase))
                {
                    rightSingle += duration.Value;
                    singleSupportTimes.Add(duration.Value);
                }
            }

            if (side == "left")
            {
                AddIfPositive(stanceTimes, doubleSupportTotal + leftSingle);
                AddIfPositive(swingTimes, rightSingle);
            }
            else if (side == "right")
            {
                AddIfPositive(stanceTimes, doubleSupportTotal + rightSingle);
                AddIfPositive(swingTimes, leftSingle);
            }
        }

        return new(
            AverageOrNull(stanceTimes),
            AverageOrNull(swingTimes),
            AverageOrNull(doubleSupportTimes),
            AverageOrNull(singleSupportTimes));
    }

    public static GaitEventPhaseMetrics CalculateFromEvents(JsonObject? gaitEvents, double? fps)
    {
        if (gaitEvents is null || fps is not > 0)
        {
            return new(null, null, null, null, null, null, null, null);
        }

        var left = CalculateSideFromEvents(
            ReadIntArray(gaitEvents, "left_heel_strike_frames"),
            ReadIntArray(gaitEvents, "left_toe_off_frames"),
            fps.Value);
        var right = CalculateSideFromEvents(
            ReadIntArray(gaitEvents, "right_heel_strike_frames"),
            ReadIntArray(gaitEvents, "right_toe_off_frames"),
            fps.Value);

        return new(
            left.StanceTimeSec,
            right.StanceTimeSec,
            left.SwingTimeSec,
            right.SwingTimeSec,
            left.StanceRatioPct,
            right.StanceRatioPct,
            left.SwingRatioPct,
            right.SwingRatioPct);
    }

    private sealed record SideEventPhaseMetrics(
        double? StanceTimeSec,
        double? SwingTimeSec,
        double? StanceRatioPct,
        double? SwingRatioPct);

    private static SideEventPhaseMetrics CalculateSideFromEvents(IReadOnlyList<int> heelStrikes, IReadOnlyList<int> toeOffs, double fps)
    {
        if (heelStrikes.Count < 2 || toeOffs.Count == 0 || fps <= 0)
        {
            return new(null, null, null, null);
        }

        var stanceTimes = new List<double>();
        var swingTimes = new List<double>();
        var stanceRatios = new List<double>();
        var swingRatios = new List<double>();

        for (var i = 0; i < heelStrikes.Count - 1; i++)
        {
            var start = heelStrikes[i];
            var end = heelStrikes[i + 1];
            var cycleFrames = end - start;
            if (cycleFrames <= 0)
            {
                continue;
            }

            var toeOff = toeOffs.FirstOrDefault(frame => frame > start && frame < end);
            if (toeOff <= start)
            {
                continue;
            }

            var stanceFrames = toeOff - start;
            var swingFrames = end - toeOff;
            if (stanceFrames <= 0 || swingFrames < 0)
            {
                continue;
            }

            stanceTimes.Add(stanceFrames / fps);
            swingTimes.Add(swingFrames / fps);
            stanceRatios.Add(stanceFrames * 100d / cycleFrames);
            swingRatios.Add(swingFrames * 100d / cycleFrames);
        }

        return new(
            AverageOrNull(stanceTimes),
            AverageOrNull(swingTimes),
            AverageOrNull(stanceRatios),
            AverageOrNull(swingRatios));
    }

    private static IEnumerable<JsonObject> EnumerateCycles(JsonObject gaitCycle)
    {
        foreach (var key in new[] { "cycles", "left_cycles", "right_cycles" })
        {
            if (gaitCycle[key] is not JsonArray cycles)
            {
                continue;
            }

            foreach (var cycle in cycles.OfType<JsonObject>())
            {
                yield return cycle;
            }
        }
    }

    private static string? ReadString(JsonObject obj, string name)
    {
        return obj.TryGetPropertyValue(name, out var node)
            ? node?.GetValue<string>()
            : null;
    }

    private static double? ReadDouble(JsonObject obj, string name)
    {
        if (!obj.TryGetPropertyValue(name, out var node) || node is null)
        {
            return null;
        }

        try
        {
            return node.GetValue<double>();
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<int> ReadIntArray(JsonObject obj, string name)
    {
        if (!obj.TryGetPropertyValue(name, out var node) || node is not JsonArray array)
        {
            return [];
        }

        var values = new List<int>();
        foreach (var item in array)
        {
            try
            {
                if (item is not null)
                {
                    values.Add(item.GetValue<int>());
                }
            }
            catch
            {
                // 忽略单个异常值，避免一个坏事件帧导致整组参数无法计算。
            }
        }

        values.Sort();
        return values;
    }

    private static void AddIfPositive(List<double> values, double value)
    {
        if (value > 0)
        {
            values.Add(value);
        }
    }

    private static double? AverageOrNull(List<double> values)
    {
        return values.Count == 0 ? null : values.Average();
    }
}
