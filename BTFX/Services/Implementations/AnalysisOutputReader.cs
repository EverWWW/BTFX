using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using BTFX.Helpers;
using BTFX.Models.Analysis;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

/// <summary>
/// Reads both the current summary.json contract and the expected algorithm result.json contract.
/// </summary>
public sealed class AnalysisOutputReader : IAnalysisOutputReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AnalysisOutputReadResult> ReadAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        }

        var resultPath = FindFirstFile(outputDirectory, "result.json");
        var summaryPath = FindFirstFile(outputDirectory, "summary.json");

        if (!string.IsNullOrWhiteSpace(resultPath) && File.Exists(resultPath))
        {
            var json = await File.ReadAllTextAsync(resultPath, cancellationToken);
            var summary = ConvertResultJson(json, outputDirectory);
            return new AnalysisOutputReadResult
            {
                SummaryPath = !string.IsNullOrWhiteSpace(summaryPath) && File.Exists(summaryPath) ? summaryPath : resultPath,
                ResultPath = resultPath,
                Summary = summary
            };
        }

        if (!string.IsNullOrWhiteSpace(summaryPath) && File.Exists(summaryPath))
        {
            var json = await File.ReadAllTextAsync(summaryPath, cancellationToken);
            var summary = JsonSerializer.Deserialize<AnalysisSummary>(json, JsonOptions)
                ?? throw new InvalidOperationException($"无法解析 summary.json: {summaryPath}");

            EnsureCsvFallbacks(summary, outputDirectory);

            return new AnalysisOutputReadResult
            {
                SummaryPath = summaryPath,
                ResultPath = null,
                Summary = summary
            };
        }

        throw new FileNotFoundException($"算法输出目录中未找到 result.json 或 summary.json: {outputDirectory}");
    }

    private static AnalysisSummary ConvertResultJson(string json, string outputDirectory)
    {
        var directSummary = JsonSerializer.Deserialize<AnalysisSummary>(json, JsonOptions);
        if (!string.IsNullOrWhiteSpace(directSummary?.RequestId) || directSummary?.GaitEventParameters is not null)
        {
            NormalizeCadence(directSummary);
            EnsureCsvFallbacks(directSummary, outputDirectory);
            return directSummary;
        }

        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException("result.json 格式无效。");

        var outputFiles = root["output_files"] as JsonObject;
        var gaitCycle = root["gait_cycle"] as JsonObject;
        var spatiotemporal = root["spatiotemporal_parameters"] as JsonObject;
        var jointAngles = root["joint_angles"] as JsonObject;
        var quality = root["quality_control"] as JsonObject;
        var status = ReadString(root, "status");
        var phaseMetrics = GaitPhaseMetricsCalculator.Calculate(gaitCycle);
        var fps = ReadDouble(root["video_info"] as JsonObject, "fps");
        var eventPhaseMetrics = GaitPhaseMetricsCalculator.CalculateFromEvents(root["gait_events"] as JsonObject, fps);
        var robustRom = RobustRomCalculator.Calculate(
            outputDirectory,
            root,
            fps,
            new RobustRomValues
            {
                LeftHipRomDeg = ReadJointRom(jointAngles, "left_hip", "left hip"),
                RightHipRomDeg = ReadJointRom(jointAngles, "right_hip", "right hip"),
                LeftKneeRomDeg = ReadJointRom(jointAngles, "left_knee", "left knee"),
                RightKneeRomDeg = ReadJointRom(jointAngles, "right_knee", "right knee"),
                LeftAnkleRomDeg = ReadJointRom(jointAngles, "left_ankle", "left ankle"),
                RightAnkleRomDeg = ReadJointRom(jointAngles, "right_ankle", "right ankle")
            });

        var frameCoverageRatio = AnalysisFrameCoverageHelper.FromResultJson(root, outputDirectory)?.Ratio;
        var meanCycleDuration = ReadDouble(gaitCycle, "mean_cycle_duration_sec")
            ?? ReadDouble(gaitCycle, "cycle_time_sec")
            ?? AverageCycleDuration(gaitCycle);
        var summary = new AnalysisSummary
        {
            RequestId = ReadString(root, "task_id") ?? ReadString(root, "request_id") ?? $"RESULT_{DateTime.Now:yyyyMMdd_HHmmss}",
            ProtocolVersion = ReadString(root, "protocol_version") ?? "result-json",
            AlgorithmVersion = ReadString(root, "algorithm_version") ?? "external",
            ModelVersion = ReadString(root, "model_version") ?? "external",
            TaskStatus = ReadString(root, "task_status") ?? status ?? "completed",
            Success = ReadBool(root, "success") ?? !string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase),
            ErrorCode = ReadInt(root, "error_code") ?? 0,
            ErrorMessage = ReadString(root, "error_message"),
            GeneratedTime = ReadString(root, "generated_time") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            OutputDir = ReadString(root, "output_dir") ?? outputDirectory,
            AnnotatedVideoPath = ReadString(outputFiles, "visualized_video")
                ?? ReadString(outputFiles, "annotated_video")
                ?? ReadString(root, "annotated_video_path")
                ?? FindAnnotatedVideo(outputDirectory),
            GaitEventParameters = new GaitEventParametersDto
            {
                GaitCycleDurationS = meanCycleDuration,
                StepLengthM = ReadDouble(spatiotemporal, "mean_step_length_m"),
                StrideLengthM = ReadDouble(spatiotemporal, "mean_stride_length_m"),
                CadenceStepPerMin = GaitCadenceCalculator.PreferCycleDerived(
                    meanCycleDuration,
                    ReadDouble(spatiotemporal, "cadence_step_per_min")),
                GaitSpeedMPerS = ReadDouble(spatiotemporal, "gait_velocity_m_per_sec") ?? ReadDouble(spatiotemporal, "gait_speed_m_per_s"),
                StanceTimeS = ReadDouble(spatiotemporal, "mean_stance_time_sec") ?? phaseMetrics.MeanStanceTimeSec ?? eventPhaseMetrics.MeanStanceTimeSec,
                SwingTimeS = ReadDouble(spatiotemporal, "mean_swing_time_sec") ?? phaseMetrics.MeanSwingTimeSec ?? eventPhaseMetrics.MeanSwingTimeSec,
                DoubleSupportTimeS = ReadDouble(spatiotemporal, "mean_double_support_time_sec") ?? phaseMetrics.MeanDoubleSupportTimeSec,
                SingleSupportTimeS = ReadDouble(spatiotemporal, "mean_single_support_time_sec") ?? phaseMetrics.MeanSingleSupportTimeSec,
                LeftStanceRatioPct = ReadDouble(root, "left_stance_ratio_pct") ?? eventPhaseMetrics.LeftStanceRatioPct,
                RightStanceRatioPct = ReadDouble(root, "right_stance_ratio_pct") ?? eventPhaseMetrics.RightStanceRatioPct,
                LeftSwingRatioPct = ReadDouble(root, "left_swing_ratio_pct") ?? eventPhaseMetrics.LeftSwingRatioPct,
                RightSwingRatioPct = ReadDouble(root, "right_swing_ratio_pct") ?? eventPhaseMetrics.RightSwingRatioPct
            },
            KinematicSummary = new KinematicSummaryDto
            {
                HipRomDeg = Average(robustRom.LeftHipRomDeg, robustRom.RightHipRomDeg),
                KneeRomDeg = Average(robustRom.LeftKneeRomDeg, robustRom.RightKneeRomDeg),
                AnkleRomDeg = Average(robustRom.LeftAnkleRomDeg, robustRom.RightAnkleRomDeg),
                PelvisCoronalRomDeg = ReadDouble(root["segment_angles"]?["pelvis_coronal_rom_deg"] as JsonObject, "rom")
                    ?? Difference(
                        ReadDouble(root["segment_angles"]?["pelvis_tilt_deg"] as JsonObject, "max"),
                        ReadDouble(root["segment_angles"]?["pelvis_tilt_deg"] as JsonObject, "min"))
            },
            CsvFiles = new CsvFilesDto
            {
                JointAngleCsv = ReadString(outputFiles, "joint_angle_csv") ?? ReadString(outputFiles, "angle_curve_csv"),
                KeypointTrajectoryCsv = ReadString(outputFiles, "keypoints_csv") ?? ReadString(outputFiles, "pose_csv"),
                KeypointVelocityCsv = ReadString(outputFiles, "trajectory_csv") ?? ReadString(root["trajectory_analysis"] as JsonObject, "toe_trajectory")
            },
            QualityControl = new QualityControlDto
            {
                MeanKeypointConfidence = ReadDouble(quality, "mean_keypoint_confidence") ?? ReadDouble(root, "keypoint_confidence"),
                ValidFrameRatio = frameCoverageRatio ?? ReadDouble(quality, "valid_frame_ratio") ?? ReadDouble(root, "valid_frame_ratio") ?? ReadDouble(root, "valid_frame_percent"),
                OcclusionWarning = ReadBool(quality, "occlusion_warning") ?? false,
                MissingPointWarning = ReadBool(quality, "missing_point_warning") ?? false
            }
        };

        EnsureCsvFallbacks(summary, outputDirectory);
        return summary;
    }

    private static void NormalizeCadence(AnalysisSummary summary)
    {
        if (summary.GaitEventParameters is not { } gait)
        {
            return;
        }

        gait.CadenceStepPerMin = GaitCadenceCalculator.PreferCycleDerived(
            gait.GaitCycleDurationS,
            gait.CadenceStepPerMin);
    }

    private static void EnsureCsvFallbacks(AnalysisSummary summary, string outputDirectory)
    {
        var dataFiles = Directory.Exists(outputDirectory)
            ? Directory.GetFiles(outputDirectory, "*.*", SearchOption.AllDirectories)
                .Where(path => IsAnalysisDataFile(path))
                .Select(path => ToOutputRelativePath(outputDirectory, path))
                .ToList()
            : [];

        summary.CsvFiles ??= new CsvFilesDto();

        summary.CsvFiles.JointAngleCsv ??= dataFiles.FirstOrDefault(path => Path.GetFileName(path).Equals("joint_angle.csv", StringComparison.OrdinalIgnoreCase))
            ?? dataFiles.FirstOrDefault(path => path.Contains("joint", StringComparison.OrdinalIgnoreCase) || path.Contains("angle", StringComparison.OrdinalIgnoreCase));
        summary.CsvFiles.KeypointTrajectoryCsv ??= dataFiles.FirstOrDefault(path => path.EndsWith(".trc", StringComparison.OrdinalIgnoreCase))
            ?? dataFiles.FirstOrDefault(path => path.Contains("keypoint", StringComparison.OrdinalIgnoreCase) || path.Contains("trajectory", StringComparison.OrdinalIgnoreCase));
        summary.CsvFiles.KeypointVelocityCsv ??= dataFiles.FirstOrDefault(path => path.Contains("velocity", StringComparison.OrdinalIgnoreCase))
            ?? dataFiles.FirstOrDefault(path => path.EndsWith(".mot", StringComparison.OrdinalIgnoreCase));

        var remaining = dataFiles
            .Where(name => name != summary.CsvFiles.JointAngleCsv
                           && name != summary.CsvFiles.KeypointTrajectoryCsv
                           && name != summary.CsvFiles.KeypointVelocityCsv)
            .ToList();

        summary.CsvFiles.JointAngleCsv ??= remaining.ElementAtOrDefault(0);
        summary.CsvFiles.KeypointTrajectoryCsv ??= remaining.ElementAtOrDefault(1);
        summary.CsvFiles.KeypointVelocityCsv ??= remaining.ElementAtOrDefault(2);
    }

    private static string? ReadString(JsonObject? obj, string name)
    {
        return obj is not null && obj.TryGetPropertyValue(name, out var node)
            ? node?.GetValue<string>()
            : null;
    }

    private static double? ReadDouble(JsonObject? obj, string name)
    {
        if (obj is null || !obj.TryGetPropertyValue(name, out var node) || node is null)
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

    private static JsonObject? ReadObject(JsonObject? obj, params string[] names)
    {
        if (obj is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (obj.TryGetPropertyValue(name, out var node) && node is JsonObject value)
            {
                return value;
            }
        }

        return null;
    }

    private static double? ReadJointRom(JsonObject? jointAngles, params string[] names)
    {
        var joint = ReadObject(jointAngles, names);
        return ReadDouble(joint, "rom_deg")
               ?? Difference(ReadDouble(joint, "max_flexion_deg"), ReadDouble(joint, "min_flexion_deg"))
               ?? Difference(ReadDouble(joint, "max"), ReadDouble(joint, "min"));
    }

    private static int? ReadInt(JsonObject? obj, string name)
    {
        if (obj is null || !obj.TryGetPropertyValue(name, out var node) || node is null)
        {
            return null;
        }

        try
        {
            return node.GetValue<int>();
        }
        catch
        {
            return null;
        }
    }

    private static bool? ReadBool(JsonObject? obj, string name)
    {
        if (obj is null || !obj.TryGetPropertyValue(name, out var node) || node is null)
        {
            return null;
        }

        try
        {
            return node.GetValue<bool>();
        }
        catch
        {
            return null;
        }
    }

    private static double? Average(double? left, double? right)
    {
        return (left, right) switch
        {
            ({ } l, { } r) => (l + r) / 2.0,
            ({ } l, null) => l,
            (null, { } r) => r,
            _ => null
        };
    }

    private static double? Difference(double? max, double? min)
    {
        return max.HasValue && min.HasValue ? Math.Abs(max.Value - min.Value) : null;
    }

    private static string? FindFirstFile(string outputDirectory, string fileName)
    {
        if (!Directory.Exists(outputDirectory))
        {
            return null;
        }

        var rootPath = Path.Combine(outputDirectory, fileName);
        if (File.Exists(rootPath))
        {
            return rootPath;
        }

        return Directory.GetFiles(outputDirectory, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }

    private static string? FindAnnotatedVideo(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            return null;
        }

        var previewVideo = Directory.GetFiles(outputDirectory, "analysis_preview.mp4", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(previewVideo))
        {
            return ToOutputRelativePath(outputDirectory, previewVideo);
        }

        var videos = Directory.GetFiles(outputDirectory, "*.mp4", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).Contains("Sports2D", StringComparison.OrdinalIgnoreCase)
                           || Path.GetFileName(path).Contains("annotated", StringComparison.OrdinalIgnoreCase)
                           || Path.GetFileName(path).Contains("visual", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var preferred = videos.FirstOrDefault(path => path.Contains("side", StringComparison.OrdinalIgnoreCase) || path.Contains("侧面", StringComparison.OrdinalIgnoreCase))
            ?? videos.FirstOrDefault();
        return preferred is null ? null : ToOutputRelativePath(outputDirectory, preferred);
    }

    private static bool IsSingleViewOutput(string outputDirectory)
    {
        var taskConfigPath = Directory.GetFiles(outputDirectory, "task_config.json", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(taskConfigPath))
        {
            return false;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(taskConfigPath))?.AsObject();
            return root is not null && root.TryGetPropertyValue("front_video", out var node) && node is null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAnalysisDataFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".mot", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".trc", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToOutputRelativePath(string outputDirectory, string path)
    {
        return Path.GetRelativePath(outputDirectory, path);
    }

    private static double? AverageCycleDuration(JsonObject? gaitCycle)
    {
        var cycles = EnumerateCycles(gaitCycle).ToArray();
        if (cycles.Length == 0)
        {
            return null;
        }

        var values = cycles
            .Select(cycle => ReadDouble(cycle, "duration_sec"))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();
        return values.Count == 0 ? null : values.Average();
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
}
