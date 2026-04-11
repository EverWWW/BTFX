using System.Diagnostics;
using System.Globalization;
using System.IO;
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
    private readonly ISettingsService _settingsService;
    private readonly ILogHelper? _logHelper;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private Process? _currentProcess;
    private CancellationTokenSource? _linkedCts;
    private volatile bool _isRunning;

    /// <inheritdoc/>
    public bool IsAnalysisRunning => _isRunning;

    /// <inheritdoc/>
    public event EventHandler<AnalysisProgressEventArgs>? ProgressChanged;

    /// <inheritdoc/>
    public event EventHandler<AnalysisLogEventArgs>? LogReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    public GaitAnalysisService(ISettingsService settingsService, ILogHelper? logHelper = null)
    {
        _settingsService = settingsService;
        _logHelper = logHelper;
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateEnvironmentAsync()
    {
        var exePath = GetAlgorithmExePath();
        var exists = File.Exists(exePath);

        if (!exists)
        {
            _logHelper?.Warning($"算法程序文件不存在: {exePath}");
        }

        return await Task.FromResult(exists);
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

            // [1] 验证输入
            ValidateRequest(request);

            // [2] 构建配置文件
            var requestId = GenerateRequestId();
            var outputDir = request.OutputDirectory;
            Directory.CreateDirectory(outputDir);

            var taskConfig = BuildTaskConfig(request, requestId);
            var configPath = Path.Combine(outputDir, Constants.TASK_CONFIG_FILENAME);
            await WriteTaskConfigAsync(taskConfig, configPath, ct);

            RaiseLog($"配置文件已生成: {configPath}");

            // [3] 设置超时+取消
            var settings = _settingsService.CurrentSettings.Algorithm;
            var timeoutMs = settings.TimeoutMinutes * 60 * 1000;

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            RaiseProgress(requestId, "pending", 0, "任务已接受");

            // [4] 启动算法进程
            var exePath = GetAlgorithmExePath();
            var exitCode = await RunProcessAsync(exePath, configPath, requestId, _linkedCts.Token);

            // [5] 判断结果
            stopwatch.Stop();
            var analysisDuration = stopwatch.Elapsed.TotalSeconds;

            if (exitCode != 0)
            {
                var errorCode = (AnalysisErrorCode)exitCode;
                var errorMessage = $"算法进程退出码: {exitCode} ({errorCode})";
                _logHelper?.Error(errorMessage);

                return BuildFailedResult(request, requestId, outputDir, configPath, exitCode, errorMessage, analysisDuration);
            }

            // [6] 解析结果（成功时）
            var summaryPath = Path.Combine(outputDir, Constants.SUMMARY_FILENAME);
            if (!File.Exists(summaryPath))
            {
                var errorMessage = $"算法输出文件不存在: {summaryPath}";
                _logHelper?.Error(errorMessage);
                return BuildFailedResult(request, requestId, outputDir, configPath, (int)AnalysisErrorCode.ExportFailed, errorMessage, analysisDuration);
            }

            var summary = await ReadSummaryAsync(summaryPath, ct);

            if (!summary.Success)
            {
                return BuildFailedResult(request, requestId, outputDir, configPath, summary.ErrorCode, summary.ErrorMessage ?? "算法返回失败", analysisDuration);
            }

            // [7] 构建成功结果
            var result = BuildSuccessResult(request, requestId, outputDir, configPath, summaryPath, summary, analysisDuration);

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

    #region 私有方法 — 配置构建

    /// <summary>
    /// 生成请求ID：GAIT_{yyyyMMdd}_{3位序号}
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

        // 若为相对路径，则基于应用程序目录解析
        if (!Path.IsPathRooted(exePath))
        {
            exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exePath);
        }

        return exePath;
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

        var cameraDistance = _settingsService.CurrentSettings.Algorithm.SideCameraDistance;
        if (cameraDistance is null or <= 0)
        {
            throw new InvalidOperationException("侧向相机距离未配置，请在设置页面中配置。");
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

        // Patient.Height 存 cm，需转换为 m
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

    #region 私有方法 — 进程管理

    /// <summary>
    /// 启动算法进程并等待完成
    /// </summary>
    private async Task<int> RunProcessAsync(string exePath, string configPath, string requestId, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"--config \"{configPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory
        };

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _currentProcess = process;

        var tcs = new TaskCompletionSource<int>();

        process.OutputDataReceived += (_, e) =>
        {
            if (App.IsShuttingDown || string.IsNullOrEmpty(e.Data)) return;
            HandleStdoutLine(e.Data, requestId);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (App.IsShuttingDown || string.IsNullOrEmpty(e.Data)) return;
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

            // 等待进程退出或取消
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
    /// 处理 stdout 单行输出
    /// </summary>
    private void HandleStdoutLine(string line, string requestId)
    {
        try
        {
            var message = JsonSerializer.Deserialize<TaskStatusMessage>(line);
            if (message is null || message.Type != Constants.STATUS_MESSAGE_TYPE)
            {
                // 非状态消息，作为普通日志
                RaiseLog(line);
                return;
            }

            RaiseProgress(requestId, message.TaskStatus, message.Progress, message.Message, message.ErrorCode);
            RaiseLog($"[{message.TaskStatus}] {message.Progress}% - {message.Message}");
        }
        catch (JsonException)
        {
            // 非 JSON 行，作为普通日志
            RaiseLog(line);
        }
    }

    #endregion

    #region 私有方法 — 结果解析

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

        // CSV 文件记录
        result.CsvFiles = BuildCsvFileRecords(outputDir, summary.CsvFiles);

        // 质量控制
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

    #region 私有方法 — 事件触发

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

    #region 数据持久化

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
                // [1] 插入主表
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

                // [3] 插入 CSV 文件记录
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

                // [5] 更新 GaitParameters 扩展字段（若有步态事件数据）
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
    /// 加载分析结果的子表数据（运动学汇总/CSV文件/质量控制）
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
