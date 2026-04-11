using System.Text.Json.Serialization;

namespace BTFX.Models.Analysis;

/// <summary>
/// 算法输入配置（对应 task_config.json），使用 snake_case 命名
/// </summary>
public class AnalysisTaskConfig
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("protocol_version")]
    public string ProtocolVersion { get; set; } = string.Empty;

    [JsonPropertyName("algorithm_version")]
    public string AlgorithmVersion { get; set; } = string.Empty;

    [JsonPropertyName("model_version")]
    public string ModelVersion { get; set; } = string.Empty;

    [JsonPropertyName("task_type")]
    public string TaskType { get; set; } = string.Empty;

    [JsonPropertyName("analysis_mode")]
    public string AnalysisMode { get; set; } = string.Empty;

    [JsonPropertyName("subject_info")]
    public SubjectInfo SubjectInfo { get; set; } = new();

    [JsonPropertyName("video_info")]
    public VideoInfo VideoInfo { get; set; } = new();

    [JsonPropertyName("device_info")]
    public DeviceInfo DeviceInfo { get; set; } = new();

    [JsonPropertyName("analysis_options")]
    public AnalysisOptionsConfig AnalysisOptions { get; set; } = new();
}

/// <summary>
/// 受试者信息
/// </summary>
public class SubjectInfo
{
    [JsonPropertyName("subject_id")]
    public string SubjectId { get; set; } = string.Empty;

    [JsonPropertyName("gender")]
    public string Gender { get; set; } = string.Empty;

    [JsonPropertyName("age")]
    public int Age { get; set; }

    [JsonPropertyName("height_m")]
    public double HeightM { get; set; }

    [JsonPropertyName("weight_kg")]
    public double WeightKg { get; set; }
}

/// <summary>
/// 视频信息
/// </summary>
public class VideoInfo
{
    [JsonPropertyName("sagittal_video_path")]
    public string? SagittalVideoPath { get; set; }

    [JsonPropertyName("coronal_video_path")]
    public string? CoronalVideoPath { get; set; }

    [JsonPropertyName("video_fps")]
    public int VideoFps { get; set; }

    [JsonPropertyName("video_resolution")]
    public string VideoResolution { get; set; } = string.Empty;

    [JsonPropertyName("start_time_s")]
    public double StartTimeS { get; set; }

    [JsonPropertyName("duration_s")]
    public double DurationS { get; set; }
}

/// <summary>
/// 设备信息
/// </summary>
public class DeviceInfo
{
    [JsonPropertyName("camera_id")]
    public string CameraId { get; set; } = string.Empty;

    [JsonPropertyName("camera_type")]
    public string CameraType { get; set; } = string.Empty;

    [JsonPropertyName("capture_fps")]
    public int CaptureFps { get; set; }

    [JsonPropertyName("side_camera_to_walkway_distance_m")]
    public double SideCameraToWalkwayDistanceM { get; set; }
}

/// <summary>
/// 分析选项（JSON 配置）
/// </summary>
public class AnalysisOptionsConfig
{
    [JsonPropertyName("calculate_gait_event_parameters")]
    public bool CalculateGaitEventParameters { get; set; } = true;

    [JsonPropertyName("calculate_kinematic_parameters")]
    public bool CalculateKinematicParameters { get; set; } = true;

    [JsonPropertyName("export_csv")]
    public bool ExportCsv { get; set; } = true;

    [JsonPropertyName("smooth_curve")]
    public bool SmoothCurve { get; set; } = true;
}
