using System.Text.Json.Serialization;

namespace BTFX.Models.Analysis;

/// <summary>
/// 算法输出汇总（对应 summary.json），使用 snake_case 命名
/// </summary>
public class AnalysisSummary
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("protocol_version")]
    public string ProtocolVersion { get; set; } = string.Empty;

    [JsonPropertyName("algorithm_version")]
    public string AlgorithmVersion { get; set; } = string.Empty;

    [JsonPropertyName("model_version")]
    public string ModelVersion { get; set; } = string.Empty;

    [JsonPropertyName("task_status")]
    public string TaskStatus { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error_code")]
    public int ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("generated_time")]
    public string? GeneratedTime { get; set; }

    [JsonPropertyName("output_dir")]
    public string? OutputDir { get; set; }

    [JsonPropertyName("annotated_video_path")]
    public string? AnnotatedVideoPath { get; set; }

    [JsonPropertyName("gait_event_parameters")]
    public GaitEventParametersDto? GaitEventParameters { get; set; }

    [JsonPropertyName("kinematic_summary")]
    public KinematicSummaryDto? KinematicSummary { get; set; }

    [JsonPropertyName("csv_files")]
    public CsvFilesDto? CsvFiles { get; set; }

    [JsonPropertyName("quality_control")]
    public QualityControlDto? QualityControl { get; set; }
}

/// <summary>
/// 步态事件参数 DTO（从 summary.json 读取）
/// </summary>
public class GaitEventParametersDto
{
    [JsonPropertyName("gait_cycle_duration_s")]
    public double? GaitCycleDurationS { get; set; }

    [JsonPropertyName("step_length_m")]
    public double? StepLengthM { get; set; }

    [JsonPropertyName("stride_length_m")]
    public double? StrideLengthM { get; set; }

    [JsonPropertyName("cadence_step_per_min")]
    public double? CadenceStepPerMin { get; set; }

    [JsonPropertyName("gait_speed_m_per_s")]
    public double? GaitSpeedMPerS { get; set; }

    [JsonPropertyName("stance_time_s")]
    public double? StanceTimeS { get; set; }

    [JsonPropertyName("swing_time_s")]
    public double? SwingTimeS { get; set; }

    [JsonPropertyName("double_support_time_s")]
    public double? DoubleSupportTimeS { get; set; }

    [JsonPropertyName("single_support_time_s")]
    public double? SingleSupportTimeS { get; set; }
}

/// <summary>
/// 运动学汇总 DTO（从 summary.json 读取）
/// </summary>
public class KinematicSummaryDto
{
    [JsonPropertyName("hip_rom_deg")]
    public double? HipRomDeg { get; set; }

    [JsonPropertyName("knee_rom_deg")]
    public double? KneeRomDeg { get; set; }

    [JsonPropertyName("ankle_rom_deg")]
    public double? AnkleRomDeg { get; set; }

    [JsonPropertyName("pelvis_coronal_rom_deg")]
    public double? PelvisCoronalRomDeg { get; set; }
}

/// <summary>
/// CSV 文件路径 DTO
/// </summary>
public class CsvFilesDto
{
    [JsonPropertyName("joint_angle_csv")]
    public string? JointAngleCsv { get; set; }

    [JsonPropertyName("keypoint_trajectory_csv")]
    public string? KeypointTrajectoryCsv { get; set; }

    [JsonPropertyName("keypoint_velocity_csv")]
    public string? KeypointVelocityCsv { get; set; }

    [JsonPropertyName("joint_angular_velocity_csv")]
    public string? JointAngularVelocityCsv { get; set; }
}

/// <summary>
/// 质量控制 DTO
/// </summary>
public class QualityControlDto
{
    [JsonPropertyName("mean_keypoint_confidence")]
    public double? MeanKeypointConfidence { get; set; }

    [JsonPropertyName("valid_frame_ratio")]
    public double? ValidFrameRatio { get; set; }

    [JsonPropertyName("occlusion_warning")]
    public bool OcclusionWarning { get; set; }

    [JsonPropertyName("missing_point_warning")]
    public bool MissingPointWarning { get; set; }
}
