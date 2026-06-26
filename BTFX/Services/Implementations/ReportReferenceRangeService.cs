using System.Globalization;
using System.IO;
using System.Text.Json;
using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

public sealed class ReportReferenceRangeService : IReportReferenceRangeService
{
    private const string FileName = "report-reference-ranges.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _configPath;
    private Dictionary<string, ReportReferenceRange>? _ranges;

    public ReportReferenceRangeService()
    {
        _configPath = Path.Combine(AppContext.BaseDirectory, "Data", "Config", FileName);
    }

    public string GetReferenceText(string key)
    {
        var ranges = EnsureLoaded();
        if (!ranges.TryGetValue(key, out var range) || range is null)
        {
            return "--";
        }

        return FormatRange(range);
    }

    private Dictionary<string, ReportReferenceRange> EnsureLoaded()
    {
        if (_ranges is not null)
        {
            return _ranges;
        }

        var defaults = CreateDefaultRanges();
        var loaded = ReadRanges();
        var changed = loaded.Count == 0;

        foreach (var (key, value) in defaults)
        {
            if (!loaded.ContainsKey(key))
            {
                loaded[key] = value;
                changed = true;
            }
        }

        if (changed)
        {
            SaveRanges(loaded);
        }

        _ranges = loaded;
        return _ranges;
    }

    private Dictionary<string, ReportReferenceRange> ReadRanges()
    {
        if (!File.Exists(_configPath))
        {
            return new Dictionary<string, ReportReferenceRange>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var ranges = JsonSerializer.Deserialize<Dictionary<string, ReportReferenceRange>>(json, JsonOptions);
            return ranges is null
                ? new Dictionary<string, ReportReferenceRange>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ReportReferenceRange>(ranges, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, ReportReferenceRange>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveRanges(Dictionary<string, ReportReferenceRange> ranges)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var ordered = ranges
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        File.WriteAllText(_configPath, JsonSerializer.Serialize(ordered, JsonOptions));
    }

    private static string FormatRange(ReportReferenceRange range)
    {
        return (range.Min, range.Max) switch
        {
            (double min, double max) => $"{FormatNumber(min)}-{FormatNumber(max)}",
            (double min, null) => $">={FormatNumber(min)}",
            (null, double max) => $"<={FormatNumber(max)}",
            _ => "--"
        };
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private static Dictionary<string, ReportReferenceRange> CreateDefaultRanges()
    {
        return new Dictionary<string, ReportReferenceRange>(StringComparer.OrdinalIgnoreCase)
        {
            ["GaitCycle"] = Range(0.9, 1.2),
            ["MeanStepLength"] = Range(0.55, 0.75),
            ["MeanStrideLength"] = Range(1.10, 1.50),
            ["MeanCadence"] = Range(100, 120),
            ["MeanGaitSpeed"] = Range(1.0, 1.4),
            ["StanceTime"] = Range(0.60, 0.80),
            ["SwingTime"] = Range(0.30, 0.45),
            ["DoubleSupportTime"] = Range(0.15, 0.25),
            ["SingleSupportTime"] = Range(0.40, 0.55),
            ["LeftHipRom"] = Empty(),
            ["RightHipRom"] = Empty(),
            ["LeftKneeRom"] = Empty(),
            ["RightKneeRom"] = Empty(),
            ["LeftAnkleRom"] = Empty(),
            ["RightAnkleRom"] = Empty(),
            ["HipAverageRom"] = Empty(),
            ["KneeAverageRom"] = Empty(),
            ["AnkleAverageRom"] = Empty(),
            ["PelvisCoronalAngle"] = Empty(),
            ["TrunkTiltMean"] = Empty(),
            ["TrunkTiltMax"] = Empty(),
            ["TrunkTiltMin"] = Empty(),
            ["TrunkTiltRom"] = Empty(),
            ["PelvisTiltMean"] = Empty(),
            ["PelvisTiltMax"] = Empty(),
            ["PelvisRom"] = Empty(),
            ["StrideDiff"] = Empty(),
            ["StrideDiffPercent"] = Empty(),
            ["StanceRatioDiff"] = Empty(),
            ["StanceRatioDiffPercent"] = Empty(),
            ["KneeRomDiff"] = Empty(),
            ["HipRomDiff"] = Empty(),
            ["AnkleRomDiff"] = Empty(),
            ["SymmetryScore"] = Empty(),
            ["LeftStride"] = Empty(),
            ["RightStride"] = Empty(),
            ["LeftStanceRatio"] = Range(60, 70),
            ["RightStanceRatio"] = Range(60, 70),
            ["LeftSwingRatio"] = Range(30, 40),
            ["RightSwingRatio"] = Range(30, 40)
        };
    }

    private static ReportReferenceRange Range(double min, double max)
    {
        return new ReportReferenceRange { Min = min, Max = max };
    }

    private static ReportReferenceRange Empty()
    {
        return new ReportReferenceRange();
    }
}
