using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BTFX.Common;
using BTFX.Data;
using BTFX.Models;
using BTFX.Models.Analysis;
using BTFX.Services.Interfaces;
using ToolHelper.Database.Sqlite;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 步态分析算法调用服务实现
/// </summary>
public class GaitAnalysisService : IGaitAnalysisService
{
    private const string SideConfigFileName = "Config_side.toml";
    private const string FrontConfigFileName = "Config_front.toml";
    private const string InternalRuntimeZipFileName = "_internal.zip";
    private const string InternalRuntimeDirectoryName = "_internal";
    private const string InputDirectoryName = "input";
    private const string ConfigSnapshotDirectoryName = "config_snapshot";
    private const string LogDirectoryName = "logs";
    private const string SideInputFileName = "side.mp4";
    private const string FrontInputFileName = "front.mp4";
    private const string PreferredAlgorithmExeFileName = "Gait_analysis.exe";
    private const string LegacyAlgorithmExeFileName = "gait_analysis.exe";
    private static readonly string[] AlgorithmStatusFiles =
    [
        "status.json",
        "status_history.json",
        "logs.txt"
    ];
    private static readonly string[] SideJointAngles =
    [
        "Right ankle",
        "Left ankle",
        "Right knee",
        "Left knee",
        "Right hip",
        "Left hip"
    ];
    private static readonly string[] FrontSegmentAngles =
    [
        "Pelvis",
        "Trunk"
    ];

    private readonly ISettingsService _settingsService;
    private readonly IAnalysisOutputReader _analysisOutputReader;
    private readonly ILogHelper? _logHelper;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly object _stdoutStatusLock = new();

    private Process? _currentProcess;
    private CancellationTokenSource? _linkedCts;
    private TaskStatusMessage? _lastStdoutStatus;
    private TaskStatusMessage? _stdoutFailureStatus;
    private volatile bool _isRunning;

    private sealed record PreparedAnalysisInput(string? SideVideoPath, string? FrontVideoPath);

    /// <inheritdoc/>
    public bool IsAnalysisRunning => _isRunning;

    /// <inheritdoc/>
    public event EventHandler<AnalysisProgressEventArgs>? ProgressChanged;

    /// <inheritdoc/>
    public event EventHandler<AnalysisLogEventArgs>? LogReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    public GaitAnalysisService(
        ISettingsService settingsService,
        IAnalysisOutputReader analysisOutputReader,
        ILogHelper? logHelper = null)
    {
        _settingsService = settingsService;
        _analysisOutputReader = analysisOutputReader;
        _logHelper = logHelper;
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateEnvironmentAsync()
    {
        try
        {
            EnsureAlgorithmRuntimeReady();
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logHelper?.Warning($"算法运行环境校验失败: {ex.Message}");
            return await Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public async Task<AnalysisResult> RunAnalysisAsync(AnalysisRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await _semaphore.WaitAsync(0, ct))
        {
            throw new InvalidOperationException("已有分析任务正在运行，请等待完成或取消后再试。");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _isRunning = true;
            ResetStdoutStatus();
            ValidateRequest(request);

            var requestId = GenerateRequestId();
            var outputDir = request.OutputDirectory;
            Directory.CreateDirectory(outputDir);
            var algorithmDirectory = EnsureAlgorithmRuntimeReady();
            var inputDir = Path.Combine(outputDir, InputDirectoryName);
            var configSnapshotDir = Path.Combine(outputDir, ConfigSnapshotDirectoryName);
            var logDir = Path.Combine(outputDir, LogDirectoryName);

            Directory.CreateDirectory(inputDir);
            Directory.CreateDirectory(configSnapshotDir);
            Directory.CreateDirectory(logDir);
            CleanupAlgorithmStatusFiles(algorithmDirectory, outputDir);

            var preparedInput = PrepareInputVideos(request, inputDir);
            var configPath = PrepareAlgorithmTomlConfigs(
                request,
                algorithmDirectory,
                configSnapshotDir,
                inputDir,
                outputDir,
                preparedInput);
            var settings = _settingsService.CurrentSettings.Algorithm;
            var timeoutMs = settings.TimeoutMinutes * 60 * 1000;

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            RaiseProgress(requestId, "pending", 0, "任务已接收");

            var exePath = GetAlgorithmExePath();
            RaiseLog($"算法程序路径: {exePath}");
            RaiseLog($"算法工作目录: {outputDir}");
            WriteRunInfo(outputDir, logDir, request, requestId, exePath, configPath, inputDir, outputDir);
            var exitCode = await RunProcessAsync(exePath, requestId, logDir, outputDir, _linkedCts.Token);

            stopwatch.Stop();
            var analysisDuration = stopwatch.Elapsed.TotalSeconds;
            var stdoutFailure = GetStdoutFailureStatus();
            if (stdoutFailure is not null)
            {
                var errorMessage = BuildStdoutFailureMessage(stdoutFailure, logDir);
                _logHelper?.Error(errorMessage);
                return BuildFailedResult(
                    request,
                    string.IsNullOrWhiteSpace(stdoutFailure.EffectiveRequestId) ? requestId : stdoutFailure.EffectiveRequestId,
                    outputDir,
                    configPath,
                    stdoutFailure.ErrorCode ?? (int)AnalysisErrorCode.AnalysisFailed,
                    errorMessage,
                    analysisDuration);
            }

            if (exitCode != 0)
            {
                var stderrTail = ReadLastLogLines(Path.Combine(logDir, "stderr.log"), 12);
                if (IsStdoutCompleted())
                {
                    var warningMessage = string.IsNullOrWhiteSpace(stderrTail)
                        ? $"算法进程退出码非 0: {exitCode}，但 stdout 已返回 completed，继续读取结果。日志目录: {logDir}"
                        : $"算法进程退出码非 0: {exitCode}，但 stdout 已返回 completed，继续读取结果。日志目录: {logDir}\n{stderrTail}";
                    RaiseLog(warningMessage, isError: true);
                    _logHelper?.Warning(warningMessage);
                }
                else
                {
                    var errorCode = (AnalysisErrorCode)exitCode;
                    var errorMessage = string.IsNullOrWhiteSpace(stderrTail)
                        ? $"算法进程退出码: {exitCode} ({errorCode})，日志目录: {logDir}"
                        : $"算法进程退出码: {exitCode} ({errorCode})，日志目录: {logDir}\n{stderrTail}";
                    _logHelper?.Error(errorMessage);
                    return BuildFailedResult(request, requestId, outputDir, configPath, exitCode, errorMessage, analysisDuration);
                }
            }

            AnalysisOutputReadResult outputReadResult;
            try
            {
                outputReadResult = await _analysisOutputReader.ReadAsync(outputDir, ct);
            }
            catch (Exception ex)
            {
                var errorMessage = $"算法输出读取失败: {ex.Message}";
                _logHelper?.Error(errorMessage, ex);
                return BuildFailedResult(request, requestId, outputDir, configPath, (int)AnalysisErrorCode.ExportFailed, errorMessage, analysisDuration);
            }

            var summary = outputReadResult.Summary;
            if (!summary.Success)
            {
                return BuildFailedResult(
                    request,
                    requestId,
                    outputDir,
                    configPath,
                    summary.ErrorCode,
                    summary.ErrorMessage ?? "算法返回失败",
                    analysisDuration);
            }

            var result = BuildSuccessResult(
                request,
                requestId,
                outputDir,
                configPath,
                outputReadResult.SummaryPath,
                summary,
                analysisDuration);

            RaiseProgress(requestId, "completed", 100, "分析完成");
            RaiseLog($"分析完成，耗时: {analysisDuration:F1}s");

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logHelper?.Information("分析任务已取消");
            RaiseLog("分析任务已取消");
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logHelper?.Error("分析任务异常", ex);
            RaiseLog($"分析异常: {ex.Message}", isError: true);
            throw;
        }
        finally
        {
            _isRunning = false;
            _linkedCts?.Dispose();
            _linkedCts = null;
            _currentProcess = null;
            _semaphore.Release();
        }
    }
    /// <inheritdoc/>
    public Task CancelCurrentAnalysisAsync()
    {
        if (_linkedCts is { IsCancellationRequested: false })
        {
            _logHelper?.Information("正在取消分析任务...");
            _linkedCts.Cancel();
        }

        return Task.CompletedTask;
    }

    #region 私有方法 - 配置构建

    /// <summary>
    /// 生成请求ID：GAIT_{yyyyMMdd}_{HHmmss}
    /// </summary>
    private static string GenerateRequestId()
    {
        var date = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var seq = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
        return $"{Constants.REQUEST_ID_PREFIX}_{date}_{seq}";
    }

    /// <summary>
    /// 获取算法 exe 完整路径
    /// </summary>
    private string GetAlgorithmExePath()
    {
        var settings = _settingsService.CurrentSettings.Algorithm;
        var exePath = settings.ExePath;
        var oldDefaultPath = Path.Combine("Algorithm", Constants.ALGORITHM_EXE_FILENAME);
        if (string.Equals(exePath, oldDefaultPath, StringComparison.OrdinalIgnoreCase))
        {
            exePath = Path.Combine(Constants.ALGORITHM_DIRECTORY, Constants.ALGORITHM_EXE_FILENAME);
            settings.ExePath = exePath;
            _settingsService.SaveSettings();
        }

        // 若为相对路径，则基于应用程序目录解析
        if (!Path.IsPathRooted(exePath))
        {
            exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exePath);
        }

        if (File.Exists(exePath))
        {
            return exePath;
        }

        var directory = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        var preferredPath = Path.Combine(directory, PreferredAlgorithmExeFileName);
        if (File.Exists(preferredPath))
        {
            settings.ExePath = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, preferredPath);
            _settingsService.SaveSettings();
            return preferredPath;
        }

        var legacyPath = Path.Combine(directory, LegacyAlgorithmExeFileName);
        if (File.Exists(legacyPath))
        {
            return legacyPath;
        }

        var algorithmDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.ALGORITHM_DIRECTORY);
        var fallbackExe = Directory.Exists(algorithmDirectory)
            ? Directory.GetFiles(algorithmDirectory, "*.exe", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path => string.Equals(Path.GetFileName(path), PreferredAlgorithmExeFileName, StringComparison.OrdinalIgnoreCase))
                ?? Directory.GetFiles(algorithmDirectory, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault()
            : null;
        if (!string.IsNullOrWhiteSpace(fallbackExe) && File.Exists(fallbackExe))
        {
            settings.ExePath = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, fallbackExe);
            _settingsService.SaveSettings();
            return fallbackExe;
        }

        return exePath;
    }

    private string EnsureAlgorithmRuntimeReady()
    {
        var exePath = GetAlgorithmExePath();
        var algorithmDirectory = Path.GetDirectoryName(exePath)
            ?? throw new InvalidOperationException("无法解析算法程序目录。");

        if (!Directory.Exists(algorithmDirectory))
        {
            throw new DirectoryNotFoundException($"算法目录不存在: {algorithmDirectory}");
        }

        var runtimeDirectory = Path.Combine(algorithmDirectory, InternalRuntimeDirectoryName);
        if (!Directory.Exists(runtimeDirectory))
        {
            var zipPath = Path.Combine(algorithmDirectory, InternalRuntimeZipFileName);
            if (!File.Exists(zipPath))
            {
                throw new FileNotFoundException($"算法运行库不存在，请确认已随程序复制 {InternalRuntimeZipFileName}。", runtimeDirectory);
            }

            RaiseLog("正在解压算法运行库，首次运行可能需要等待一段时间。");
            ZipFile.ExtractToDirectory(zipPath, algorithmDirectory, overwriteFiles: true);
        }

        if (!Directory.Exists(runtimeDirectory))
        {
            throw new DirectoryNotFoundException($"算法运行库解压后仍未找到: {runtimeDirectory}");
        }

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"算法程序文件不存在: {exePath}", exePath);
        }

        var sideConfigPath = Path.Combine(algorithmDirectory, SideConfigFileName);
        var frontConfigPath = Path.Combine(algorithmDirectory, FrontConfigFileName);
        if (!File.Exists(sideConfigPath))
        {
            throw new FileNotFoundException($"侧面算法配置文件不存在: {sideConfigPath}", sideConfigPath);
        }

        if (!File.Exists(frontConfigPath))
        {
            throw new FileNotFoundException($"正面算法配置文件不存在: {frontConfigPath}", frontConfigPath);
        }

        return algorithmDirectory;
    }

    /// <summary>
    /// 验证分析请求
    /// </summary>
    private void ValidateRequest(AnalysisRequest request)
    {
        if (request.Patient.Height is null or <= 0)
        {
            throw new InvalidOperationException("患者身高未填写，算法必填参数。");
        }

        if (string.IsNullOrWhiteSpace(request.Record.SideVideoPath))
        {
            throw new InvalidOperationException("侧面视频未选择，无法启动步态分析。");
        }

        if (!string.IsNullOrEmpty(request.Record.SideVideoPath) && !File.Exists(request.Record.SideVideoPath))
        {
            throw new InvalidOperationException($"侧面视频文件不存在: {request.Record.SideVideoPath}");
        }

        if (!string.IsNullOrEmpty(request.Record.FrontVideoPath) && !File.Exists(request.Record.FrontVideoPath))
        {
            throw new InvalidOperationException($"正面视频文件不存在: {request.Record.FrontVideoPath}");
        }
    }

    private static PreparedAnalysisInput PrepareInputVideos(AnalysisRequest request, string inputDir)
    {
        string? sideInputPath = null;
        string? frontInputPath = null;

        if (!string.IsNullOrWhiteSpace(request.Record.SideVideoPath))
        {
            sideInputPath = CopyInputVideo(request.Record.SideVideoPath, Path.Combine(inputDir, SideInputFileName));
        }

        if (!string.IsNullOrWhiteSpace(request.Record.FrontVideoPath))
        {
            frontInputPath = CopyInputVideo(request.Record.FrontVideoPath, Path.Combine(inputDir, FrontInputFileName));
        }

        return new PreparedAnalysisInput(sideInputPath, frontInputPath);
    }

    private static string CopyInputVideo(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"输入视频不存在: {sourcePath}", sourcePath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        var sourceFullPath = Path.GetFullPath(sourcePath);
        var destinationFullPath = Path.GetFullPath(destinationPath);
        if (!string.Equals(sourceFullPath, destinationFullPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourceFullPath, destinationFullPath, overwrite: true);
        }

        return destinationFullPath;
    }

    private string PrepareAlgorithmTomlConfigs(
        AnalysisRequest request,
        string algorithmDirectory,
        string configSnapshotDir,
        string inputDir,
        string outputDir,
        PreparedAnalysisInput preparedInput)
    {
        var heightM = (request.Patient.Height ?? 0) / 100.0;
        var cameraDistance = _settingsService.CurrentSettings.Algorithm.SideCameraDistance ?? 10.0;

        var sideConfigTemplatePath = Path.Combine(algorithmDirectory, SideConfigFileName);
        var frontConfigTemplatePath = Path.Combine(algorithmDirectory, FrontConfigFileName);
        var sideConfigPath = Path.Combine(outputDir, SideConfigFileName);
        var frontConfigPath = Path.Combine(outputDir, FrontConfigFileName);

        File.Copy(sideConfigTemplatePath, sideConfigPath, overwrite: true);
        File.Copy(frontConfigTemplatePath, frontConfigPath, overwrite: true);

        WriteAlgorithmToml(
            sideConfigPath,
            preparedInput.SideVideoPath is null ? "[]" : $"['{SideInputFileName}']",
            inputDir,
            outputDir,
            heightM,
            cameraDistance,
            isSideConfig: true);

        WriteAlgorithmToml(
            frontConfigPath,
            preparedInput.FrontVideoPath is null ? "[]" : $"['{FrontInputFileName}']",
            inputDir,
            outputDir,
            heightM,
            cameraDistance,
            isSideConfig: false);

        Directory.CreateDirectory(configSnapshotDir);
        File.Copy(sideConfigPath, Path.Combine(configSnapshotDir, SideConfigFileName), overwrite: true);
        File.Copy(frontConfigPath, Path.Combine(configSnapshotDir, FrontConfigFileName), overwrite: true);

        var manifestPath = Path.Combine(configSnapshotDir, Constants.TASK_CONFIG_FILENAME);
        var manifest = new
        {
            request_id = request.Record.Id,
            side_video = preparedInput.SideVideoPath,
            front_video = preparedInput.FrontVideoPath,
            input_dir = inputDir,
            result_dir = outputDir,
            side_config = sideConfigPath,
            front_config = frontConfigPath,
            side_config_snapshot = Path.Combine(configSnapshotDir, SideConfigFileName),
            front_config_snapshot = Path.Combine(configSnapshotDir, FrontConfigFileName),
            height_m = heightM,
            perspective_value = cameraDistance,
            generated_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        RaiseLog($"算法配置已写入本次输出目录，输入目录: {inputDir}，输出目录: {outputDir}，快照目录: {configSnapshotDir}");
        return manifestPath;
    }

    private static void WriteAlgorithmToml(
        string configPath,
        string videoInputValue,
        string inputDir,
        string outputDir,
        double heightM,
        double perspectiveValue,
        bool isSideConfig)
    {
        var lines = File.ReadAllLines(configPath).ToList();
        UpsertTomlValue(lines, "base", "video_input", videoInputValue);
        UpsertTomlValue(lines, "base", "video_dir", ToTomlString(inputDir));
        UpsertTomlValue(lines, "base", "first_person_height", heightM.ToString("0.###", CultureInfo.InvariantCulture));
        UpsertTomlValue(lines, "base", "time_range", "[]");
        UpsertTomlValue(lines, "base", "cut_off_frequency", "6");
        UpsertTomlValue(lines, "base", "show_realtime_results", "false");
        UpsertTomlValue(lines, "base", "save_vid", "true");
        UpsertTomlValue(lines, "base", "save_img", "false");
        UpsertTomlValue(lines, "base", "save_graphs", "false");
        UpsertTomlValue(lines, "base", "result_dir", ToTomlString(outputDir));
        UpsertTomlValue(lines, "base", "joint_angles", ToTomlStringArray(isSideConfig ? SideJointAngles : []));
        UpsertTomlValue(lines, "base", "segment_angles", ToTomlStringArray(isSideConfig ? [] : FrontSegmentAngles));
        UpsertTomlValue(lines, "px_to_meters_conversion", "perspective_value", perspectiveValue.ToString("0.###", CultureInfo.InvariantCulture));
        UpsertTomlValue(lines, "post-processing.butterworth", "cut_off_frequency", "6");
        File.WriteAllLines(configPath, lines);
    }

    private static void UpsertTomlValue(List<string> lines, string section, string key, string value)
    {
        var sectionHeader = $"[{section}]";
        var sectionStart = lines.FindIndex(line => string.Equals(line.Trim(), sectionHeader, StringComparison.OrdinalIgnoreCase));
        if (sectionStart < 0)
        {
            lines.Add(string.Empty);
            lines.Add(sectionHeader);
            lines.Add($"{key} = {value}");
            return;
        }

        var insertIndex = lines.Count;
        for (var i = sectionStart + 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('['))
            {
                insertIndex = i;
                break;
            }

            if (trimmed.StartsWith($"{key} ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase))
            {
                var commentIndex = lines[i].IndexOf('#');
                var comment = commentIndex >= 0 ? " " + lines[i][commentIndex..].Trim() : string.Empty;
                lines[i] = $"{key} = {value}{comment}";
                return;
            }
        }

        lines.Insert(insertIndex, $"{key} = {value}");
    }

    private static string ToTomlString(string path)
    {
        return $"'{path.Replace("\\", "/", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal)}'";
    }

    private static string ToTomlStringArray(IEnumerable<string> values)
    {
        return $"[{string.Join(", ", values.Select(value => $"'{value.Replace("'", "\\'", StringComparison.Ordinal)}'"))}]";
    }

    private static void CleanupAlgorithmStatusFiles(params string[] directories)
    {
        foreach (var directory in directories.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var fileName in AlgorithmStatusFiles)
            {
                var path = Path.Combine(directory, fileName);
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                    // 状态文件清理失败不应阻断分析，新的 stdout 状态仍然是主通道。
                }
            }
        }
    }

    private static void WriteRunInfo(
        string outputDir,
        string logDir,
        AnalysisRequest request,
        string requestId,
        string exePath,
        string configPath,
        string inputDir,
        string workingDirectory)
    {
        try
        {
            Directory.CreateDirectory(logDir);
            var runInfo = new
            {
                request_id = requestId,
                measurement_id = request.Record.Id,
                measurement_name = request.Record.MeasurementName,
                patient_id = request.Patient.Id,
                patient_name = request.Patient.Name,
                exe_path = exePath,
                working_directory = workingDirectory,
                config_manifest = configPath,
                input_dir = inputDir,
                output_dir = outputDir,
                side_video_source = request.Record.SideVideoPath,
                front_video_source = request.Record.FrontVideoPath,
                started_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            };

            var json = JsonSerializer.Serialize(runInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(logDir, "run_info.json"), json, Encoding.UTF8);
        }
        catch
        {
            // run_info 仅用于联调定位，写入失败不能影响分析。
        }
    }

    /// <summary>
    /// 构建算法输入配置
    /// </summary>
    private AnalysisTaskConfig BuildTaskConfig(AnalysisRequest request, string requestId)
    {
        var patient = request.Patient;
        var record = request.Record;
        var settings = _settingsService.CurrentSettings.Algorithm;

        var age = patient.BirthDate.HasValue
            ? (int)((DateTime.Now - patient.BirthDate.Value).TotalDays / 365.25)
            : 0;

        // Patient.Height 瀛?cm锛岄渶杞崲涓?m
        var heightM = (patient.Height ?? 0) / 100.0;

        // 根据 VideoSpec 推算分辨率和帧率
        var (fps, resolution) = record.VideoSpec switch
        {
            VideoSpec.P1080_30fps => (30, "1920x1080"),
            VideoSpec.P1440_30fps => (30, "2560x1440"),
            _ => (30, "1920x1080")
        };

        return new AnalysisTaskConfig
        {
            RequestId = requestId,
            ProtocolVersion = Constants.PROTOCOL_VERSION,
            AlgorithmVersion = settings.AlgorithmVersion,
            ModelVersion = settings.ModelVersion,
            TaskType = Constants.TASK_TYPE,
            AnalysisMode = Constants.ANALYSIS_MODE,
            SubjectInfo = new SubjectInfo
            {
                SubjectId = $"P{patient.Id:D4}",
                Gender = patient.Gender == Gender.Male ? "male" : "female",
                Age = age,
                HeightM = heightM,
                WeightKg = patient.Weight ?? 0
            },
            VideoInfo = new VideoInfo
            {
                SagittalVideoPath = record.SideVideoPath,
                CoronalVideoPath = record.FrontVideoPath,
                VideoFps = fps,
                VideoResolution = resolution,
                StartTimeS = 0,
                DurationS = record.DurationSeconds ?? 0
            },
            DeviceInfo = new DeviceInfo
            {
                CameraId = "default",
                CameraType = "webcam",
                CaptureFps = fps,
                SideCameraToWalkwayDistanceM = settings.SideCameraDistance ?? 0
            },
            AnalysisOptions = new AnalysisOptionsConfig
            {
                CalculateGaitEventParameters = request.Options.CalculateGaitEvents,
                CalculateKinematicParameters = request.Options.CalculateKinematics,
                ExportCsv = request.Options.ExportCsv,
                SmoothCurve = request.Options.SmoothCurve
            }
        };
    }

    /// <summary>
    /// 将配置写入 JSON 文件
    /// </summary>
    private static async Task WriteTaskConfigAsync(AnalysisTaskConfig config, string path, CancellationToken ct)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null // 已使用 JsonPropertyName 标注
        };

        var json = JsonSerializer.Serialize(config, options);
        await File.WriteAllTextAsync(path, json, ct);
    }

    #endregion

    #region 私有方法 - 进程管理

    /// <summary>
    /// 启动算法进程并等待完成
    /// </summary>
    private async Task<int> RunProcessAsync(string exePath, string requestId, string logDir, string workingDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(logDir);
        var stdoutLogPath = Path.Combine(logDir, "stdout.log");
        var stderrLogPath = Path.Combine(logDir, "stderr.log");
        ResetProcessLog(stdoutLogPath);
        ResetProcessLog(stderrLogPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = workingDirectory
        };

        startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _currentProcess = process;

        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (App.IsShuttingDown || string.IsNullOrEmpty(e.Data)) return;
            AppendProcessLog(stdoutLogPath, e.Data);
            HandleStdoutLine(e.Data, requestId);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (App.IsShuttingDown || string.IsNullOrEmpty(e.Data)) return;
            AppendProcessLog(stderrLogPath, e.Data);
            RaiseLog($"[stderr] {e.Data}", isError: true);
            _logHelper?.Warning($"算法 stderr: {e.Data}");
        };

        process.Exited += (_, _) =>
        {
            tcs.TrySetResult(process.ExitCode);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            RaiseLog($"算法进程已启动 (PID: {process.Id})");
            _logHelper?.Information($"算法进程已启动: PID={process.Id}, exe={exePath}");

            using var registration = ct.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        _logHelper?.Information("正在终止算法进程...");
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception ex)
                {
                    _logHelper?.Warning($"终止进程异常: {ex.Message}");
                }

                tcs.TrySetCanceled(ct);
            });

            return await tcs.Task;
        }
        finally
        {
            process.Dispose();
        }
    }

    /// <summary>
    /// 澶勭悊 stdout 鍗曡杈撳嚭
    /// </summary>
    private void HandleStdoutLine(string line, string requestId)
    {
        try
        {
            if (TryHandlePlainProgressLine(line, requestId))
            {
                return;
            }

            var message = JsonSerializer.Deserialize<TaskStatusMessage>(line);
            if (message is null || !message.IsStatusMessage)
            {
                // 非状态消息，作为普通日志
                RaiseLog(line);
                return;
            }

            var effectiveRequestId = string.IsNullOrWhiteSpace(message.EffectiveRequestId)
                ? requestId
                : message.EffectiveRequestId;
            var status = string.IsNullOrWhiteSpace(message.EffectiveStatus)
                ? "processing"
                : message.EffectiveStatus;
            var progress = Math.Clamp(message.EffectiveProgress, 0, 100);
            var text = !string.IsNullOrWhiteSpace(message.Message)
                ? message.Message
                : (!string.IsNullOrWhiteSpace(message.CurrentStage) ? message.CurrentStage : status);
            var errorCode = message.ErrorCode ?? (string.IsNullOrWhiteSpace(message.Error) ? null : (int?)AnalysisErrorCode.Unknown);

            CaptureStdoutStatus(message, status);

            RaiseProgress(effectiveRequestId, status, progress, text, errorCode);
            RaiseLog($"[{status}] {progress}% - {text}", errorCode.HasValue);
            if (!string.IsNullOrWhiteSpace(message.Error))
            {
                RaiseLog($"算法错误: {message.Error}", isError: true);
            }
        }
        catch (JsonException)
        {
            // 非 JSON 行，作为普通日志
            RaiseLog(line);
        }
    }

    private void ResetStdoutStatus()
    {
        lock (_stdoutStatusLock)
        {
            _lastStdoutStatus = null;
            _stdoutFailureStatus = null;
        }
    }

    private void CaptureStdoutStatus(TaskStatusMessage message, string status)
    {
        lock (_stdoutStatusLock)
        {
            _lastStdoutStatus = message;
            if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                _stdoutFailureStatus = message;
            }
        }
    }

    private TaskStatusMessage? GetStdoutFailureStatus()
    {
        lock (_stdoutStatusLock)
        {
            return _stdoutFailureStatus;
        }
    }

    private bool IsStdoutCompleted()
    {
        lock (_stdoutStatusLock)
        {
            return string.Equals(_lastStdoutStatus?.EffectiveStatus, "completed", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string BuildStdoutFailureMessage(TaskStatusMessage status, string logDir)
    {
        var message = !string.IsNullOrWhiteSpace(status.Error)
            ? status.Error
            : (!string.IsNullOrWhiteSpace(status.Message) ? status.Message : "算法返回失败状态。");
        var stage = string.IsNullOrWhiteSpace(status.CurrentStage) ? string.Empty : $"阶段: {status.CurrentStage}，";
        return $"算法执行失败，{stage}原因: {message}，日志目录: {logDir}";
    }

    private static void AppendProcessLog(string path, string line)
    {
        try
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch
        {
            // 日志写入不能影响算法主流程。
        }
    }

    private static void ResetProcessLog(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty, Encoding.UTF8);
        }
        catch
        {
            // 日志初始化失败不能影响算法主流程。
        }
    }

    private static string ReadLastLogLines(string path, int maxLines)
    {
        try
        {
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            var lines = File.ReadLines(path).TakeLast(maxLines);
            return string.Join(Environment.NewLine, lines);
        }
        catch
        {
            return string.Empty;
        }
    }

    private bool TryHandlePlainProgressLine(string line, string requestId)
    {
        if (!line.StartsWith("PROGRESS ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = line.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !int.TryParse(parts[1], out var progress))
        {
            RaiseLog(line);
            return true;
        }

        var status = parts[2];
        var message = parts.Length >= 4 ? parts[3] : status;
        RaiseProgress(requestId, status, Math.Clamp(progress, 0, 100), message);
        RaiseLog($"[{status}] {progress}% - {message}");
        return true;
    }

    #endregion

    #region 私有方法 - 结果解析

    /// <summary>
    /// 读取 summary.json
    /// </summary>
    private static async Task<AnalysisSummary> ReadSummaryAsync(string path, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(path, ct);

        return JsonSerializer.Deserialize<AnalysisSummary>(json)
               ?? throw new InvalidOperationException($"无法解析 summary.json: {path}");
    }

    /// <summary>
    /// 构建成功的分析结果
    /// </summary>
    private AnalysisResult BuildSuccessResult(
        AnalysisRequest request,
        string requestId,
        string outputDir,
        string configPath,
        string summaryPath,
        AnalysisSummary summary,
        double analysisDuration)
    {
        var settings = _settingsService.CurrentSettings.Algorithm;

        // 解析标注视频路径
        string? annotatedVideoPath = null;
        if (!string.IsNullOrEmpty(summary.AnnotatedVideoPath))
        {
            annotatedVideoPath = Path.IsPathRooted(summary.AnnotatedVideoPath)
                ? summary.AnnotatedVideoPath
                : Path.Combine(outputDir, summary.AnnotatedVideoPath);
        }
        else
        {
            // 尝试默认路径
            var defaultPath = Path.Combine(outputDir, Constants.ANNOTATED_VIDEO_FILENAME);
            if (File.Exists(defaultPath))
            {
                annotatedVideoPath = defaultPath;
            }
        }

        var result = new AnalysisResult
        {
            MeasurementId = request.Record.Id,
            RequestId = requestId,
            ProtocolVersion = summary.ProtocolVersion,
            AlgorithmVersion = summary.AlgorithmVersion,
            ModelVersion = summary.ModelVersion,
            TaskStatus = summary.TaskStatus,
            Success = true,
            OutputDirectory = outputDir,
            ConfigFilePath = configPath,
            SummaryFilePath = summaryPath,
            AnnotatedVideoPath = annotatedVideoPath,
            AnalysisDurationSeconds = analysisDuration
        };

        // 运动学汇总
        if (summary.KinematicSummary is not null)
        {
            result.KinematicSummary = new KinematicSummary
            {
                HipRomDeg = summary.KinematicSummary.HipRomDeg,
                KneeRomDeg = summary.KinematicSummary.KneeRomDeg,
                AnkleRomDeg = summary.KinematicSummary.AnkleRomDeg,
                PelvisCoronalRomDeg = summary.KinematicSummary.PelvisCoronalRomDeg,
                RawDataJson = JsonSerializer.Serialize(summary.KinematicSummary)
            };
        }

        // CSV 鏂囦欢璁板綍
        result.CsvFiles = BuildCsvFileRecords(outputDir, summary.CsvFiles);

        // 璐ㄩ噺鎺у埗
        if (summary.QualityControl is not null)
        {
            result.QualityControl = new QualityControlInfo
            {
                MeanKeypointConfidence = summary.QualityControl.MeanKeypointConfidence,
                ValidFrameRatio = summary.QualityControl.ValidFrameRatio,
                OcclusionWarning = summary.QualityControl.OcclusionWarning,
                MissingPointWarning = summary.QualityControl.MissingPointWarning,
                RawDataJson = JsonSerializer.Serialize(summary.QualityControl)
            };
        }

        // 步态事件参数
        if (summary.GaitEventParameters is not null)
        {
            var gep = summary.GaitEventParameters;
            result.GaitCycleDurationS = gep.GaitCycleDurationS;
            result.StanceTimeS = gep.StanceTimeS;
            result.SwingTimeS = gep.SwingTimeS;
            result.DoubleSupportTimeS = gep.DoubleSupportTimeS;
            result.SingleSupportTimeS = gep.SingleSupportTimeS;
            result.StepLengthM = gep.StepLengthM;
            result.StrideLengthM = gep.StrideLengthM;
            result.GaitSpeedMPerS = gep.GaitSpeedMPerS;
        }

        return result;
    }

    /// <summary>
    /// 构建失败的分析结果
    /// </summary>
    private AnalysisResult BuildFailedResult(
        AnalysisRequest request,
        string requestId,
        string outputDir,
        string configPath,
        int errorCode,
        string? errorMessage,
        double analysisDuration)
    {
        var settings = _settingsService.CurrentSettings.Algorithm;

        return new AnalysisResult
        {
            MeasurementId = request.Record.Id,
            RequestId = requestId,
            ProtocolVersion = Constants.PROTOCOL_VERSION,
            AlgorithmVersion = settings.AlgorithmVersion,
            ModelVersion = settings.ModelVersion,
            TaskStatus = "failed",
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            OutputDirectory = outputDir,
            ConfigFilePath = configPath,
            AnalysisDurationSeconds = analysisDuration
        };
    }

    /// <summary>
    /// 构建 CSV 文件记录列表
    /// </summary>
    private static List<AnalysisCsvFile> BuildCsvFileRecords(string outputDir, CsvFilesDto? csvFiles)
    {
        var files = new List<AnalysisCsvFile>();

        if (csvFiles is null) return files;

        void AddFile(string? relativePath, CsvFileType type, string defaultFilename)
        {
            var path = !string.IsNullOrEmpty(relativePath)
                ? (Path.IsPathRooted(relativePath) ? relativePath : Path.Combine(outputDir, relativePath))
                : Path.Combine(outputDir, defaultFilename);

            files.Add(new AnalysisCsvFile
            {
                FileType = type,
                FilePath = path,
                FileExists = File.Exists(path)
            });
        }

        AddFile(csvFiles.JointAngleCsv, CsvFileType.JointAngle, Constants.JOINT_ANGLE_CSV_FILENAME);
        AddFile(csvFiles.KeypointTrajectoryCsv, CsvFileType.KeypointTrajectory, Constants.KEYPOINT_TRAJECTORY_CSV_FILENAME);
        AddFile(csvFiles.KeypointVelocityCsv, CsvFileType.KeypointVelocity, Constants.KEYPOINT_VELOCITY_CSV_FILENAME);
        AddFile(csvFiles.JointAngularVelocityCsv, CsvFileType.JointAngularVelocity, Constants.JOINT_ANGULAR_VELOCITY_CSV_FILENAME);

        return files;
    }

    #endregion

    #region 私有方法 - 事件触发

    /// <summary>
    /// 触发进度事件
    /// </summary>
    private void RaiseProgress(string requestId, string status, int progress, string? message, int? errorCode = null)
    {
        if (App.IsShuttingDown) return;

        var args = new AnalysisProgressEventArgs
        {
            RequestId = requestId,
            TaskStatus = status,
            Progress = progress,
            Message = message,
            ErrorCode = errorCode
        };

        try
        {
            // 确保在 UI 线程触发
            if (System.Windows.Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
            {
                dispatcher.BeginInvoke(() => ProgressChanged?.Invoke(this, args));
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Warning($"触发进度事件异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 触发日志事件
    /// </summary>
    private void RaiseLog(string message, bool isError = false)
    {
        if (App.IsShuttingDown) return;

        var args = new AnalysisLogEventArgs
        {
            Message = message,
            IsError = isError
        };

        try
        {
            if (System.Windows.Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher)
            {
                dispatcher.BeginInvoke(() => LogReceived?.Invoke(this, args));
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Warning($"触发日志事件异常: {ex.Message}");
        }
    }

    #endregion

    #region 鏁版嵁鎸佷箙鍖?

    /// <inheritdoc/>
    public async Task<int> SaveAnalysisResultAsync(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var resultId = 0;

            await db.ExecuteInTransactionAsync(async () =>
            {
                // [1] 鎻掑叆涓昏〃
                result.CreatedAt = DateTime.Now;
                var id = await db.InsertReturnIdentityAsync(result);
                resultId = (int)id;
                result.Id = resultId;

                // [2] 插入运动学汇总
                if (result.KinematicSummary is not null)
                {
                    result.KinematicSummary.AnalysisResultId = resultId;
                    result.KinematicSummary.CreatedAt = DateTime.Now;
                    await db.InsertAsync(result.KinematicSummary);
                }

                // [3] 鎻掑叆 CSV 鏂囦欢璁板綍
                if (result.CsvFiles is { Count: > 0 })
                {
                    foreach (var csv in result.CsvFiles)
                    {
                        csv.AnalysisResultId = resultId;
                        csv.CreatedAt = DateTime.Now;
                    }

                    await db.InsertRangeAsync(result.CsvFiles);
                }

                // [4] 插入质量控制信息
                if (result.QualityControl is not null)
                {
                    result.QualityControl.AnalysisResultId = resultId;
                    result.QualityControl.CreatedAt = DateTime.Now;
                    await db.InsertAsync(result.QualityControl);
                }

                // [5] 鏇存柊 GaitParameters 鎵╁睍瀛楁锛堣嫢鏈夋鎬佷簨浠舵暟鎹級
                if (result.GaitCycleDurationS.HasValue)
                {
                    var gaitParams = await db.GetFirstAsync<GaitParameters>(
                        g => g.MeasurementRecordId == result.MeasurementId);

                    if (gaitParams is not null)
                    {
                        gaitParams.AnalysisResultId = resultId;
                        gaitParams.GaitCycleDurationS = result.GaitCycleDurationS;
                        gaitParams.StanceTimeS = result.StanceTimeS;
                        gaitParams.SwingTimeS = result.SwingTimeS;
                        gaitParams.DoubleSupportTimeS = result.DoubleSupportTimeS;
                        gaitParams.SingleSupportTimeS = result.SingleSupportTimeS;
                        gaitParams.StepLengthM = result.StepLengthM;
                        gaitParams.StrideLengthM = result.StrideLengthM;
                        gaitParams.GaitSpeedMPerS = result.GaitSpeedMPerS;
                        await db.UpdateAsync(gaitParams);
                    }
                }
            });

            _logHelper?.Information($"分析结果已保存: Id={resultId}, MeasurementId={result.MeasurementId}");
            return resultId;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"保存分析结果失败: MeasurementId={result.MeasurementId}", ex);
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<AnalysisResult?> GetLatestAnalysisResultAsync(int measurementId)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var result = await db.Queryable<AnalysisResult>()
                .Where(r => r.MeasurementId == measurementId && r.Success)
                .OrderByDescending(r => r.CreatedAt)
                .FirstAsync();

            if (result is not null)
            {
                await LoadAnalysisResultChildrenAsync(db, result);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"获取最新分析结果失败: MeasurementId={measurementId}", ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<AnalysisResult?> GetAnalysisResultByIdAsync(int analysisResultId)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var result = await db.GetByIdAsync<AnalysisResult>(analysisResultId);

            if (result is not null)
            {
                await LoadAnalysisResultChildrenAsync(db, result);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"获取分析结果失败: Id={analysisResultId}", ex);
            return null;
        }
    }

    /// <summary>
    /// 加载分析结果的子表数据（运动学汇总/CSV 文件/质量控制）
    /// </summary>
    private static async Task LoadAnalysisResultChildrenAsync(SqliteSugarHelper db, AnalysisResult result)
    {
        result.KinematicSummary = await db.GetFirstAsync<KinematicSummary>(
            k => k.AnalysisResultId == result.Id);

        result.CsvFiles = await db.Queryable<AnalysisCsvFile>()
            .Where(c => c.AnalysisResultId == result.Id)
            .ToListAsync();

        result.QualityControl = await db.GetFirstAsync<QualityControlInfo>(
            q => q.AnalysisResultId == result.Id);
    }

    #endregion
}
