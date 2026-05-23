using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: gait_analysis <task_config.json>");
    return 2;
}

var configArg = args[0] == "--config" && args.Length >= 2 ? args[1] : args[0];
var configPath = Path.GetFullPath(configArg);
if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"Config file not found: {configPath}");
    return 3;
}

try
{
    var startedAt = DateTime.Now;
    var configJson = await File.ReadAllTextAsync(configPath, Encoding.UTF8);
    var config = JsonNode.Parse(configJson)?.AsObject()
        ?? throw new InvalidOperationException("Invalid task_config.json.");

    var outputDir = Path.GetDirectoryName(configPath)
        ?? AppContext.BaseDirectory;
    Directory.CreateDirectory(outputDir);

    var requestId = GetString(config, "request_id")
        ?? $"MOCK_{DateTime.Now:yyyyMMdd_HHmmss}";
    var protocolVersion = GetString(config, "protocol_version") ?? "V2.0";
    var algorithmVersion = "mock-gait-analysis-1.0";
    var modelVersion = "mock-model-1.0";
    var sagittalVideo = GetString(config["video_info"] as JsonObject, "sagittal_video_path");
    var coronalVideo = GetString(config["video_info"] as JsonObject, "coronal_video_path");
    var duration = GetDouble(config["video_info"] as JsonObject, "duration_s") ?? 6.0;
    duration = Math.Clamp(duration, 3.0, 20.0);

    var logLines = new List<string>();
    await RunStageAsync(10, "初始化", "读取任务配置", logLines);
    await RunStageAsync(30, "视频读取", "加载侧面/正面视频", logLines);
    await RunStageAsync(55, "姿态识别", "模拟关键点识别", logLines);
    await RunStageAsync(80, "步态参数", "模拟步态事件和参数计算", logLines);
    await RunStageAsync(100, "输出结果", "写入结果文件", logLines);

    var annotatedVideoPath = Path.Combine(outputDir, "annotated_video.mp4");
    CopyPreviewVideo(sagittalVideo, coronalVideo, annotatedVideoPath);

    var jointAnglePath = Path.Combine(outputDir, "joint_angle.csv");
    var keypointsPath = Path.Combine(outputDir, "keypoints.csv");
    var trajectoryPath = Path.Combine(outputDir, "trajectory.csv");

    await WriteJointAngleCsvAsync(jointAnglePath, duration);
    await WriteKeypointsCsvAsync(keypointsPath, duration);
    await WriteTrajectoryCsvAsync(trajectoryPath, duration);

    var elapsed = Math.Max(1.0, (DateTime.Now - startedAt).TotalSeconds);
    var summary = BuildSummary(
        requestId,
        protocolVersion,
        algorithmVersion,
        modelVersion,
        outputDir,
        annotatedVideoPath,
        duration,
        elapsed);

    await WriteJsonAsync(Path.Combine(outputDir, "summary.json"), summary);

    var richResult = BuildRichResult(
        requestId,
        outputDir,
        annotatedVideoPath,
        jointAnglePath,
        keypointsPath,
        trajectoryPath,
        duration,
        elapsed);

    await WriteJsonAsync(Path.Combine(outputDir, "result.json"), richResult);
    await File.WriteAllLinesAsync(Path.Combine(outputDir, "log.txt"), logLines, Encoding.UTF8);

    Console.WriteLine($"PROGRESS 100 completed 分析完成: {requestId}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
    return 10;
}

static async Task RunStageAsync(int progress, string stage, string message, List<string> logLines)
{
    var line = $"PROGRESS {progress} {stage} {message}";
    Console.WriteLine(line);
    logLines.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}");
    await Task.Delay(700);
}

static string? GetString(JsonObject? obj, string name)
{
    if (obj is null || !obj.TryGetPropertyValue(name, out var node))
    {
        return null;
    }

    return node?.GetValue<string>();
}

static double? GetDouble(JsonObject? obj, string name)
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

static void CopyPreviewVideo(string? sagittalVideo, string? coronalVideo, string target)
{
    var source = new[] { sagittalVideo, coronalVideo }
        .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));

    if (!string.IsNullOrWhiteSpace(source))
    {
        File.Copy(source, target, overwrite: true);
        return;
    }

    File.WriteAllBytes(target, []);
}

static async Task WriteJointAngleCsvAsync(string path, double duration)
{
    var lines = new List<string> { "time_sec,left_hip_angle_deg,right_hip_angle_deg,left_knee_angle_deg,right_knee_angle_deg,left_ankle_angle_deg,right_ankle_angle_deg" };
    for (var i = 0; i <= 120; i++)
    {
        var t = duration * i / 120.0;
        var phase = 2.0 * Math.PI * i / 60.0;
        lines.Add(string.Join(',',
            F(t),
            F(20 + 10 * Math.Sin(phase)),
            F(21 + 9 * Math.Cos(phase)),
            F(35 + 25 * Math.Sin(phase + 0.5)),
            F(34 + 24 * Math.Cos(phase + 0.4)),
            F(12 + 8 * Math.Sin(phase + 1.0)),
            F(11 + 7 * Math.Cos(phase + 0.8))));
    }

    await File.WriteAllLinesAsync(path, lines, Encoding.UTF8);
}

static async Task WriteKeypointsCsvAsync(string path, double duration)
{
    var lines = new List<string> { "frame,time_sec,point,x,y,confidence" };
    for (var i = 0; i <= 120; i++)
    {
        var t = duration * i / 120.0;
        lines.Add($"{i},{F(t)},left_ankle,{F(0.2 + i * 0.006)},{F(0.7 + 0.04 * Math.Sin(i / 10.0))},0.92");
        lines.Add($"{i},{F(t)},right_ankle,{F(0.25 + i * 0.006)},{F(0.72 + 0.04 * Math.Cos(i / 10.0))},0.91");
    }

    await File.WriteAllLinesAsync(path, lines, Encoding.UTF8);
}

static async Task WriteTrajectoryCsvAsync(string path, double duration)
{
    var lines = new List<string> { "time_sec,toe_x_m,toe_y_m,com_x_m,com_y_m" };
    for (var i = 0; i <= 120; i++)
    {
        var t = duration * i / 120.0;
        lines.Add(string.Join(',',
            F(t),
            F(i * 0.012),
            F(0.04 * Math.Abs(Math.Sin(i / 8.0))),
            F(i * 0.010),
            F(0.95 + 0.015 * Math.Sin(i / 12.0))));
    }

    await File.WriteAllLinesAsync(path, lines, Encoding.UTF8);
}

static object BuildSummary(
    string requestId,
    string protocolVersion,
    string algorithmVersion,
    string modelVersion,
    string outputDir,
    string annotatedVideoPath,
    double duration,
    double elapsed)
{
    return new
    {
        request_id = requestId,
        protocol_version = protocolVersion,
        algorithm_version = algorithmVersion,
        model_version = modelVersion,
        task_status = "completed",
        success = true,
        error_code = 0,
        error_message = "",
        generated_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        output_dir = outputDir,
        annotated_video_path = Path.GetFileName(annotatedVideoPath),
        gait_event_parameters = new
        {
            gait_cycle_duration_s = 1.08,
            step_length_m = 0.63,
            stride_length_m = 1.26,
            cadence_step_per_min = 112.5,
            gait_speed_m_per_s = 1.18,
            stance_time_s = 0.72,
            swing_time_s = 0.38,
            double_support_time_s = 0.21,
            single_support_time_s = 0.49
        },
        kinematic_summary = new
        {
            hip_rom_deg = 38.5,
            knee_rom_deg = 57.3,
            ankle_rom_deg = 24.6,
            pelvis_coronal_rom_deg = 7.8
        },
        csv_files = new
        {
            joint_angle_csv = "joint_angle.csv",
            keypoint_trajectory_csv = "keypoints.csv",
            keypoint_velocity_csv = "trajectory.csv"
        },
        quality_control = new
        {
            mean_keypoint_confidence = 0.92,
            valid_frame_ratio = 0.96,
            occlusion_warning = false,
            missing_point_warning = false
        },
        video_info = new
        {
            duration_sec = duration,
            frame_count = (int)Math.Round(duration * 30),
            fps = 30
        },
        analysis_duration_sec = elapsed
    };
}

static object BuildRichResult(
    string requestId,
    string outputDir,
    string annotatedVideoPath,
    string jointAnglePath,
    string keypointsPath,
    string trajectoryPath,
    double duration,
    double elapsed)
{
    return new
    {
        task_id = requestId,
        status = "completed",
        success = true,
        analysis_duration_sec = elapsed,
        video_info = new { fps = 30, duration_sec = duration, frame_count = (int)Math.Round(duration * 30) },
        gait_cycle = new { cycle_count = 5, mean_cycle_duration_sec = 1.08 },
        spatiotemporal_parameters = new
        {
            cadence_step_per_min = 112.5,
            gait_velocity_m_per_sec = 1.18,
            mean_step_length_m = 0.63,
            mean_stride_length_m = 1.26,
            mean_stance_time_sec = 0.72,
            mean_swing_time_sec = 0.38,
            mean_double_support_time_sec = 0.21,
            mean_single_support_time_sec = 0.49
        },
        joint_angles = new
        {
            left_hip = new { rom_deg = 38.5 },
            right_hip = new { rom_deg = 37.9 },
            left_knee = new { max_flexion_deg = 62.4, min_flexion_deg = 5.1, rom_deg = 57.3 },
            right_knee = new { max_flexion_deg = 60.2, min_flexion_deg = 4.8, rom_deg = 55.4 },
            left_ankle = new { rom_deg = 24.6 },
            right_ankle = new { rom_deg = 23.8 }
        },
        quality_control = new { mean_keypoint_confidence = 0.92, valid_frame_ratio = 0.96 },
        output_files = new
        {
            visualized_video = Path.GetRelativePath(outputDir, annotatedVideoPath),
            joint_angle_csv = Path.GetRelativePath(outputDir, jointAnglePath),
            keypoints_csv = Path.GetRelativePath(outputDir, keypointsPath),
            trajectory_csv = Path.GetRelativePath(outputDir, trajectoryPath)
        }
    };
}

static async Task WriteJsonAsync(string path, object value)
{
    var options = new JsonSerializerOptions { WriteIndented = true };
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, options), Encoding.UTF8);
}

static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
