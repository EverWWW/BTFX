using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
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

        var resultPath = Path.Combine(outputDirectory, "result.json");
        var summaryPath = Path.Combine(outputDirectory, "summary.json");

        if (File.Exists(resultPath))
        {
            var json = await File.ReadAllTextAsync(resultPath, cancellationToken);
            var summary = ConvertResultJson(json, outputDirectory);
            return new AnalysisOutputReadResult
            {
                SummaryPath = File.Exists(summaryPath) ? summaryPath : resultPath,
                ResultPath = resultPath,
                Summary = summary
            };
        }

        if (File.Exists(summaryPath))
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

        var summary = new AnalysisSummary
        {
            RequestId = ReadString(root, "task_id") ?? ReadString(root, "request_id") ?? $"RESULT_{DateTime.Now:yyyyMMdd_HHmmss}",
            ProtocolVersion = ReadString(root, "protocol_version") ?? "result-json",
            AlgorithmVersion = ReadString(root, "algorithm_version") ?? "external",
            ModelVersion = ReadString(root, "model_version") ?? "external",
            TaskStatus = ReadString(root, "task_status") ?? ReadString(root, "status") ?? "completed",
            Success = ReadBool(root, "success") ?? string.Equals(ReadString(root, "status"), "completed", StringComparison.OrdinalIgnoreCase),
            ErrorCode = ReadInt(root, "error_code") ?? 0,
            ErrorMessage = ReadString(root, "error_message"),
            GeneratedTime = ReadString(root, "generated_time") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            OutputDir = ReadString(root, "output_dir") ?? outputDirectory,
            AnnotatedVideoPath = ReadString(outputFiles, "visualized_video")
                ?? ReadString(outputFiles, "annotated_video")
                ?? ReadString(root, "annotated_video_path"),
            GaitEventParameters = new GaitEventParametersDto
            {
                GaitCycleDurationS = ReadDouble(gaitCycle, "mean_cycle_duration_sec") ?? ReadDouble(gaitCycle, "cycle_time_sec"),
                StepLengthM = ReadDouble(spatiotemporal, "mean_step_length_m"),
                StrideLengthM = ReadDouble(spatiotemporal, "mean_stride_length_m"),
                CadenceStepPerMin = ReadDouble(spatiotemporal, "cadence_step_per_min"),
                GaitSpeedMPerS = ReadDouble(spatiotemporal, "gait_velocity_m_per_sec") ?? ReadDouble(spatiotemporal, "gait_speed_m_per_s"),
                StanceTimeS = ReadDouble(spatiotemporal, "mean_stance_time_sec"),
                SwingTimeS = ReadDouble(spatiotemporal, "mean_swing_time_sec"),
                DoubleSupportTimeS = ReadDouble(spatiotemporal, "mean_double_support_time_sec"),
                SingleSupportTimeS = ReadDouble(spatiotemporal, "mean_single_support_time_sec")
            },
            KinematicSummary = new KinematicSummaryDto
            {
                HipRomDeg = Average(
                    ReadDouble(jointAngles?["left_hip"] as JsonObject, "rom_deg"),
                    ReadDouble(jointAngles?["right_hip"] as JsonObject, "rom_deg")),
                KneeRomDeg = Average(
                    ReadDouble(jointAngles?["left_knee"] as JsonObject, "rom_deg"),
                    ReadDouble(jointAngles?["right_knee"] as JsonObject, "rom_deg")),
                AnkleRomDeg = Average(
                    ReadDouble(jointAngles?["left_ankle"] as JsonObject, "rom_deg"),
                    ReadDouble(jointAngles?["right_ankle"] as JsonObject, "rom_deg")),
                PelvisCoronalRomDeg = ReadDouble(root["segment_angles"]?["pelvis_coronal_rom_deg"] as JsonObject, "rom")
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
                ValidFrameRatio = ReadDouble(quality, "valid_frame_ratio") ?? ReadDouble(root, "valid_frame_ratio") ?? ReadDouble(root, "valid_frame_percent"),
                OcclusionWarning = ReadBool(quality, "occlusion_warning") ?? false,
                MissingPointWarning = ReadBool(quality, "missing_point_warning") ?? false
            }
        };

        EnsureCsvFallbacks(summary, outputDirectory);
        return summary;
    }

    private static void EnsureCsvFallbacks(AnalysisSummary summary, string outputDirectory)
    {
        var csvFiles = Directory.Exists(outputDirectory)
            ? Directory.GetFiles(outputDirectory, "*.csv", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList()
            : [];

        summary.CsvFiles ??= new CsvFilesDto();

        summary.CsvFiles.JointAngleCsv ??= csvFiles.FirstOrDefault(name => name!.Contains("joint", StringComparison.OrdinalIgnoreCase));
        summary.CsvFiles.KeypointTrajectoryCsv ??= csvFiles.FirstOrDefault(name => name!.Contains("keypoint", StringComparison.OrdinalIgnoreCase));
        summary.CsvFiles.KeypointVelocityCsv ??= csvFiles.FirstOrDefault(name => name!.Contains("trajectory", StringComparison.OrdinalIgnoreCase));

        var remaining = csvFiles
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
}
