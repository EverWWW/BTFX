using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using BTFX.Common;
using BTFX.Models;
using BTFX.Models.Analysis;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.ViewModels.Measurement;

/// <summary>
/// Step4 分析评估 ViewModel
/// 管理分析流程的 4 个 UI 状态：Ready → Running → Previewing / Failed
/// </summary>
public partial class Step4AnalyzeViewModel : ObservableObject
{
    private readonly IGaitAnalysisService _analysisService;
    private readonly IMeasurementService _measurementService;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;

    private CancellationTokenSource? _analysisCts;
    private DispatcherTimer? _elapsedTimer;
    private DateTime _analysisStartTime;

    /// <summary>
    /// 请求跳转到指定步骤的事件（由 MeasurementViewModel 订阅）
    /// </summary>
    public event Action<int>? NavigateToStepRequested;

    /// <summary>
    /// 请求查看分析详情的事件（由 MeasurementViewModel 订阅）。
    /// </summary>
    public event Action? ViewReportRequested;

    /// <summary>
    /// 请求生成报告。
    /// </summary>
    public event Action? GenerateReportRequested;

    #region 状态管理

    /// <summary>
    /// 当前分析状态
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(IsPreviewing))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    [NotifyCanExecuteChangedFor(nameof(StartAnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelAnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryAnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(StepBackwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(StepForwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleParamsViewCommand))]
    [NotifyCanExecuteChangedFor(nameof(ViewReportCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateReportCommand))]
    [NotifyCanExecuteChangedFor(nameof(ViewLogCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoToStep2Command))]
    private AnalysisState _analysisState = AnalysisState.Ready;

    /// <summary>
    /// 是否就绪
    /// </summary>
    public bool IsReady => AnalysisState == AnalysisState.Ready;

    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning => AnalysisState == AnalysisState.Running;

    /// <summary>
    /// 是否在预览状态
    /// </summary>
    public bool IsPreviewing => AnalysisState == AnalysisState.Previewing;

    /// <summary>
    /// 是否失败
    /// </summary>
    public bool IsFailed => AnalysisState == AnalysisState.Failed;

    /// <summary>
    /// 是否正在显示参数概览（预览状态下的子视图切换）
    /// </summary>
    [ObservableProperty]
    private bool _isShowingParams;

    #endregion

    #region 进度信息

    /// <summary>
    /// 进度百分比 0-100
    /// </summary>
    [ObservableProperty]
    private int _progress;

    /// <summary>
    /// 当前阶段描述
    /// </summary>
    [ObservableProperty]
    private string _currentStage = string.Empty;

    /// <summary>
    /// 状态消息
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// 已用时间
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElapsedTimeDisplay))]
    private TimeSpan _elapsedTime;

    /// <summary>
    /// 已用时间格式化显示
    /// </summary>
    public string ElapsedTimeDisplay => ElapsedTime.ToString(@"mm\:ss");

    #endregion

    #region 前置条件检查

    /// <summary>
    /// 前置条件列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PrerequisiteItem> _prerequisiteItems = [];

    /// <summary>
    /// 所有必选前置条件是否满足
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartAnalyzeCommand))]
    private bool _allPrerequisitesMet;

    #endregion

    #region 分析选项

    /// <summary>
    /// 计算步态事件参数
    /// </summary>
    [ObservableProperty]
    private bool _calculateGaitEvents = true;

    /// <summary>
    /// 计算运动学参数
    /// </summary>
    [ObservableProperty]
    private bool _calculateKinematics = true;

    /// <summary>
    /// 导出 CSV
    /// </summary>
    [ObservableProperty]
    private bool _exportCsv = true;

    /// <summary>
    /// 曲线平滑
    /// </summary>
    [ObservableProperty]
    private bool _smoothCurve = true;

    #endregion

    #region 结果数据

    /// <summary>
    /// 分析结果
    /// </summary>
    [ObservableProperty]
    private AnalysisResult? _analysisResult;

    /// <summary>
    /// 步态事件参数展示模型
    /// </summary>
    [ObservableProperty]
    private GaitEventParametersDisplay? _gaitEventResult;

    /// <summary>
    /// 运动学参数展示模型
    /// </summary>
    [ObservableProperty]
    private KinematicSummaryDisplay? _kinematicResult;

    /// <summary>
    /// 质量控制展示模型
    /// </summary>
    [ObservableProperty]
    private QualityControlDisplay? _qualityResult;

    /// <summary>
    /// 分析耗时文本
    /// </summary>
    [ObservableProperty]
    private string _analysisDurationDisplay = string.Empty;

    #endregion

    #region 视频预览

    /// <summary>
    /// 标注视频文件路径
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    private string? _annotatedVideoPath;

    /// <summary>
    /// 视频总时长
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoDurationSeconds))]
    private TimeSpan _videoDuration;

    /// <summary>
    /// 当前播放时间（核心同步属性）
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoCurrentTimeSeconds))]
    [NotifyPropertyChangedFor(nameof(CurrentTimeDisplay))]
    private TimeSpan _videoCurrentTime;

    /// <summary>
    /// 当前时间(秒)，供 Slider 绑定
    /// </summary>
    public double VideoCurrentTimeSeconds
    {
        get => VideoCurrentTime.TotalSeconds;
        set
        {
            if (Math.Abs(VideoCurrentTime.TotalSeconds - value) > 0.01)
            {
                VideoCurrentTime = TimeSpan.FromSeconds(value);
                UpdateTrackerLinePosition(value);
                VideoPositionChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// 总时长(秒)
    /// </summary>
    public double VideoDurationSeconds => VideoDuration.TotalSeconds;

    /// <summary>
    /// 是否正在播放
    /// </summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// 播放速度
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSpeed025x))]
    [NotifyPropertyChangedFor(nameof(IsSpeed05x))]
    [NotifyPropertyChangedFor(nameof(IsSpeed1x))]
    [NotifyPropertyChangedFor(nameof(IsSpeed15x))]
    [NotifyPropertyChangedFor(nameof(IsSpeed2x))]
    private double _playbackSpeed = 1.0;

    /// <summary>
    /// 播放速度选中标识
    /// </summary>
    public bool IsSpeed025x => Math.Abs(PlaybackSpeed - 0.25) < 0.01;
    public bool IsSpeed05x => Math.Abs(PlaybackSpeed - 0.5) < 0.01;
    public bool IsSpeed1x => Math.Abs(PlaybackSpeed - 1.0) < 0.01;
    public bool IsSpeed15x => Math.Abs(PlaybackSpeed - 1.5) < 0.01;
    public bool IsSpeed2x => Math.Abs(PlaybackSpeed - 2.0) < 0.01;

    /// <summary>
    /// 时间显示格式
    /// </summary>
    public string CurrentTimeDisplay
    {
        get
        {
            var current = VideoCurrentTime.ToString(@"mm\:ss\.ff");
            var total = VideoDuration.ToString(@"mm\:ss\.ff");
            return $"{current} / {total}";
        }
    }

    /// <summary>
    /// 视频位置变更事件（View Code-Behind 订阅以执行 MediaElement.Seek）
    /// </summary>
    public event EventHandler<double>? VideoPositionChanged;

    /// <summary>
    /// 播放状态变更事件（View Code-Behind 订阅以执行 Play/Pause）
    /// </summary>
    public event EventHandler<bool>? PlayStateChanged;

    /// <summary>
    /// 播放速度变更事件（View Code-Behind 订阅以设置 SpeedRatio）
    /// </summary>
    public event EventHandler<double>? SpeedChanged;

    #endregion

    #region 曲线图

    /// <summary>
    /// 髋关节角度曲线
    /// </summary>
    [ObservableProperty]
    private PlotModel? _hipAnglePlotModel;

    /// <summary>
    /// 膝关节角度曲线
    /// </summary>
    [ObservableProperty]
    private PlotModel? _kneeAnglePlotModel;

    /// <summary>
    /// 踝关节角度曲线
    /// </summary>
    [ObservableProperty]
    private PlotModel? _ankleAnglePlotModel;

    /// <summary>
    /// 骨盆角度曲线
    /// </summary>
    [ObservableProperty]
    private PlotModel? _pelvisAnglePlotModel;

    /// <summary>
    /// 关节角度帧数据
    /// </summary>
    private List<JointAngleFrame> _jointAngleFrames = [];

    #endregion

    #region 错误信息

    /// <summary>
    /// 错误码
    /// </summary>
    [ObservableProperty]
    private int? _errorCode;

    /// <summary>
    /// 错误描述
    /// </summary>
    [ObservableProperty]
    private string? _errorDescription;

    /// <summary>
    /// 修复建议
    /// </summary>
    [ObservableProperty]
    private string? _errorSuggestion;

    /// <summary>
    /// 视频是否加载失败（降级显示用）
    /// </summary>
    [ObservableProperty]
    private bool _hasVideoError;

    /// <summary>
    /// 视频错误信息
    /// </summary>
    [ObservableProperty]
    private string? _videoErrorMessage;

    /// <summary>
    /// 是否有曲线图数据
    /// </summary>
    [ObservableProperty]
    private bool _hasChartData;

    #endregion

    #region 任务日志

    /// <summary>
    /// 任务日志
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _taskLogs = [];

    /// <summary>
    /// 是否展开日志面板
    /// </summary>
    [ObservableProperty]
    private bool _isLogExpanded;

    #endregion

    #region 上下文引用

    /// <summary>
    /// 当前测量记录（由 MeasurementViewModel 设置）
    /// </summary>
    public MeasurementRecord? CurrentMeasurement { get; set; }

    /// <summary>
    /// 当前患者（由 MeasurementViewModel 设置）
    /// </summary>
    public Patient? CurrentPatient { get; set; }

    #endregion

    /// <summary>
    /// 构造函数
    /// </summary>
    public Step4AnalyzeViewModel(
        IGaitAnalysisService analysisService,
        IMeasurementService measurementService,
        ISettingsService settingsService,
        ILocalizationService localizationService,
        ILogHelper? logHelper = null)
    {
        _analysisService = analysisService;
        _measurementService = measurementService;
        _settingsService = settingsService;
        _localizationService = localizationService;
        _logHelper = logHelper;

        // 订阅分析服务事件
        _analysisService.ProgressChanged += OnAnalysisProgressChanged;
        _analysisService.LogReceived += OnAnalysisLogReceived;

        _logHelper?.Information("Step4AnalyzeViewModel 初始化完成");
    }

    /// <summary>
    /// 初始化/刷新前置条件检查（进入 Step4 时调用）
    /// </summary>
    public void RefreshPrerequisites()
    {
        var items = new ObservableCollection<PrerequisiteItem>();
        var settings = _settingsService.CurrentSettings;

        // 正面视频（可选）
        var hasFrontVideo = !string.IsNullOrEmpty(CurrentMeasurement?.FrontVideoPath)
                            && File.Exists(CurrentMeasurement?.FrontVideoPath);
        items.Add(new PrerequisiteItem
        {
            Name = "正面视频已就绪",
            IsMet = hasFrontVideo,
            IsRequired = false,
            Icon = "VideoOutline"
        });

        // 侧面视频（可选）
        var hasSideVideo = !string.IsNullOrEmpty(CurrentMeasurement?.SideVideoPath)
                           && File.Exists(CurrentMeasurement?.SideVideoPath);
        items.Add(new PrerequisiteItem
        {
            Name = "侧面视频已就绪",
            IsMet = hasSideVideo,
            IsRequired = false,
            Icon = "VideoOutline"
        });

        // 至少一个视频可用（必需）
        items.Add(new PrerequisiteItem
        {
            Name = "至少一个视频可用",
            IsMet = hasFrontVideo || hasSideVideo,
            IsRequired = true,
            Icon = "VideoCheckOutline"
        });

        // 患者身高
        var hasHeight = CurrentPatient?.Height is > 0;
        items.Add(new PrerequisiteItem
        {
            Name = "患者身高已填写",
            IsMet = hasHeight,
            IsRequired = true,
            Icon = "HumanMaleHeight"
        });

        // 患者体重（可选）
        var hasWeight = CurrentPatient?.Weight is > 0;
        items.Add(new PrerequisiteItem
        {
            Name = "患者体重已填写",
            IsMet = hasWeight,
            IsRequired = false,
            Icon = "ScaleBathroom"
        });

        // 相机距离
        var hasCameraDistance = settings.Algorithm.SideCameraDistance > 0;
        items.Add(new PrerequisiteItem
        {
            Name = "相机距离已配置",
            IsMet = hasCameraDistance,
            IsRequired = true,
            Icon = "CameraOutline"
        });

        // 算法程序
        var hasExe = !string.IsNullOrEmpty(settings.Algorithm.ExePath)
                     && File.Exists(settings.Algorithm.ExePath);
        items.Add(new PrerequisiteItem
        {
            Name = "算法程序可用",
            IsMet = hasExe,
            IsRequired = true,
            Icon = "CogOutline"
        });

        PrerequisiteItems = items;
        AllPrerequisitesMet = items.Where(i => i.IsRequired).All(i => i.IsMet);
    }

    #region 命令

    /// <summary>
    /// 开始分析
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartAnalyze))]
    private async Task StartAnalyzeAsync()
    {
        if (CurrentMeasurement == null || CurrentPatient == null)
        {
            _logHelper?.Warning("开始分析失败：缺少测量记录或患者信息");
            return;
        }

        try
        {
            // 切换到运行状态
            AnalysisState = AnalysisState.Running;
            Progress = 0;
            CurrentStage = "准备中...";
            StatusMessage = string.Empty;
            TaskLogs.Clear();
            IsLogExpanded = true;
            AddLog("开始分析任务");

            // 启动计时
            _analysisStartTime = DateTime.Now;
            StartElapsedTimer();

            // 准备输出目录
            var outputDir = Path.Combine(
                CurrentMeasurement.MeasurementFolderPath ?? Path.GetTempPath(),
                "analysis_output");
            Directory.CreateDirectory(outputDir);

            // 构建请求
            var request = new AnalysisRequest
            {
                Record = CurrentMeasurement,
                Patient = CurrentPatient,
                OutputDirectory = outputDir,
                Options = new AnalysisOptions
                {
                    CalculateGaitEvents = CalculateGaitEvents,
                    CalculateKinematics = CalculateKinematics,
                    ExportCsv = ExportCsv,
                    SmoothCurve = SmoothCurve
                }
            };

            // 执行分析
            _analysisCts = new CancellationTokenSource();
            var result = await _analysisService.RunAnalysisAsync(request, _analysisCts.Token);

            // 停止计时
            StopElapsedTimer();

            if (result.Success)
            {
                await OnAnalysisCompletedAsync(result);
            }
            else
            {
                OnAnalysisFailed(result.ErrorCode, result.ErrorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            StopElapsedTimer();
            AnalysisState = AnalysisState.Ready;
            AddLog("分析已取消");
            _logHelper?.Information("分析已取消");
        }
        catch (Exception ex)
        {
            StopElapsedTimer();
            OnAnalysisFailed(null, ex.Message);
            _logHelper?.Error("分析过程异常", ex);
        }
        finally
        {
            _analysisCts?.Dispose();
            _analysisCts = null;
        }
    }

    private bool CanStartAnalyze() => IsReady && AllPrerequisitesMet;

    /// <summary>
    /// 取消分析
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancelAnalyze))]
    private async Task CancelAnalyzeAsync()
    {
        _analysisCts?.Cancel();
        await _analysisService.CancelCurrentAnalysisAsync();
        AddLog("正在取消分析...");
    }

    private bool CanCancelAnalyze() => IsRunning;

    /// <summary>
    /// 重新分析
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRetryAnalyze))]
    private void RetryAnalyze()
    {
        ResetToReady();
        RefreshPrerequisites();
    }

    private bool CanRetryAnalyze() => IsPreviewing || IsFailed;

    /// <summary>
    /// 查看分析详情。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanViewReport))]
    private void ViewReport()
    {
        ViewReportRequested?.Invoke();
    }

    private bool CanViewReport() => IsPreviewing;

    /// <summary>
    /// 生成报告。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanViewReport))]
    private void GenerateReport()
    {
        GenerateReportRequested?.Invoke();
    }

    /// <summary>
    /// 播放/暂停
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPlayPause))]
    private void PlayPause()
    {
        IsPlaying = !IsPlaying;
        PlayStateChanged?.Invoke(this, IsPlaying);
    }

    private bool CanPlayPause() => IsPreviewing && !string.IsNullOrEmpty(AnnotatedVideoPath);

    /// <summary>
    /// 快退 1 秒
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStepBackward))]
    private void StepBackward()
    {
        var newTime = Math.Max(0, VideoCurrentTimeSeconds - 1.0);
        VideoCurrentTimeSeconds = newTime;
    }

    private bool CanStepBackward() => IsPreviewing;

    /// <summary>
    /// 快进 1 秒
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStepForward))]
    private void StepForward()
    {
        var newTime = Math.Min(VideoDurationSeconds, VideoCurrentTimeSeconds + 1.0);
        VideoCurrentTimeSeconds = newTime;
    }

    private bool CanStepForward() => IsPreviewing;

    /// <summary>
    /// 设置播放速度
    /// </summary>
    [RelayCommand]
    private void SetPlaybackSpeed(string speedText)
    {
        if (double.TryParse(speedText, System.Globalization.CultureInfo.InvariantCulture, out var speed))
        {
            PlaybackSpeed = speed;
            SpeedChanged?.Invoke(this, speed);
        }
    }

    /// <summary>
    /// 切换参数概览 / 视频预览
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanToggleParamsView))]
    private void ToggleParamsView()
    {
        IsShowingParams = !IsShowingParams;
    }

    private bool CanToggleParamsView() => IsPreviewing;

    /// <summary>
    /// 查看日志
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanViewLog))]
    private void ViewLog()
    {
        IsLogExpanded = !IsLogExpanded;
    }

    private bool CanViewLog() => IsFailed;

    /// <summary>
    /// 返回步骤 2
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoToStep2))]
    private void GoToStep2()
    {
        ResetToReady();
        NavigateToStepRequested?.Invoke(1);
    }

    private bool CanGoToStep2() => IsFailed;

    #endregion

    #region 由 View Code-Behind 调用的方法

    /// <summary>
    /// 从 MediaElement 更新当前播放时间（DispatcherTimer 调用）
    /// </summary>
    public void UpdateVideoTimeFromPlayer(TimeSpan position)
    {
        _videoCurrentTime = position;
        OnPropertyChanged(nameof(VideoCurrentTime));
        OnPropertyChanged(nameof(VideoCurrentTimeSeconds));
        OnPropertyChanged(nameof(CurrentTimeDisplay));
        UpdateTrackerLinePosition(position.TotalSeconds);
    }

    /// <summary>
    /// 设置视频总时长（MediaOpened 时调用）
    /// </summary>
    public void SetVideoDuration(TimeSpan duration)
    {
        VideoDuration = duration;
        UpdatePlotXRange(duration.TotalSeconds);
    }

    /// <summary>
    /// 视频播放结束
    /// </summary>
    public void OnVideoEnded()
    {
        IsPlaying = false;
    }

    /// <summary>
    /// 视频加载/播放失败（由 View MediaFailed 事件调用）
    /// </summary>
    public void OnVideoFailed(string errorMessage)
    {
        HasVideoError = true;
        VideoErrorMessage = $"视频无法播放: {errorMessage}";
        IsPlaying = false;
        IsShowingParams = true;
        _logHelper?.Error($"标注视频播放失败: {errorMessage}");
    }

    /// <summary>
    /// 曲线图被点击，跳转到对应时间
    /// </summary>
    public void OnChartClicked(double timeSeconds)
    {
        IsPlaying = false;
        PlayStateChanged?.Invoke(this, false);
        VideoCurrentTimeSeconds = Math.Clamp(timeSeconds, 0, VideoDurationSeconds);
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 分析完成处理（含数据入库）
    /// </summary>
    private async Task OnAnalysisCompletedAsync(AnalysisResult result)
    {
        AnalysisResult = result;
        AnalysisDurationDisplay = ElapsedTime.ToString(@"mm\:ss");
        AddLog("分析完成");

        // 数据入库
        try
        {
            AddLog("正在保存分析结果...");
            var savedId = await _analysisService.SaveAnalysisResultAsync(result);

            if (savedId > 0)
            {
                result.Id = savedId;
                AddLog($"分析结果已保存 (ID: {savedId})");

                // 更新测量记录状态为已完成
                if (CurrentMeasurement is not null)
                {
                    await _measurementService.UpdateMeasurementStatusAsync(
                        CurrentMeasurement.Id, MeasurementStatus.Completed);
                    AddLog("测量状态已更新为已完成");
                }
            }
            else
            {
                AddLog("⚠ 分析结果保存失败，数据仅在内存中");
                _logHelper?.Warning("分析结果入库失败");
            }
        }
        catch (Exception ex)
        {
            AddLog($"⚠ 保存分析结果时出错: {ex.Message}");
            _logHelper?.Error("分析结果入库异常", ex);
        }

        PopulateResultDisplayData(result);

        // 视频文件降级检查
        if (!string.IsNullOrEmpty(result.AnnotatedVideoPath) && File.Exists(result.AnnotatedVideoPath))
        {
            AnnotatedVideoPath = result.AnnotatedVideoPath;
            HasVideoError = false;
            VideoErrorMessage = null;
            AddLog($"标注视频已加载: {Path.GetFileName(result.AnnotatedVideoPath)}");
        }
        else
        {
            AnnotatedVideoPath = null;
            HasVideoError = true;
            VideoErrorMessage = "标注视频文件缺失，仅展示参数数据";
            IsShowingParams = true;
            AddLog("⚠ 标注视频文件不存在，降级为参数概览");
            _logHelper?.Warning($"标注视频文件不存在: {result.AnnotatedVideoPath}");
        }

        // 曲线图数据降级检查
        LoadJointAngleDataAndBuildPlots(result);
        HasChartData = HipAnglePlotModel is not null;

        if (!HasChartData)
        {
            AddLog("⚠ 关节角度数据不可用，曲线图未加载");
        }

        AnalysisState = AnalysisState.Previewing;
        if (!HasVideoError)
        {
            IsShowingParams = false;
        }

        _logHelper?.Information($"分析完成，结果 ID: {result.Id}，视频可用: {!HasVideoError}，曲线可用: {HasChartData}");
    }

    /// <summary>
    /// 分析失败处理
    /// </summary>
    private void OnAnalysisFailed(int? errorCode, string? errorMessage)
    {
        ErrorCode = errorCode;
        ErrorDescription = errorMessage ?? "未知错误";
        ErrorSuggestion = GetErrorSuggestion(errorCode);
        AnalysisState = AnalysisState.Failed;
        AddLog($"分析失败: [{errorCode}] {errorMessage}");
    }

    /// <summary>
    /// 填充结果展示数据
    /// </summary>
    private void PopulateResultDisplayData(AnalysisResult result)
    {
        if (result.GaitCycleDurationS.HasValue)
        {
            GaitEventResult = new GaitEventParametersDisplay
            {
                GaitCycleDuration = FormatValue(result.GaitCycleDurationS, "s"),
                StanceTime = FormatValue(result.StanceTimeS, "s"),
                SwingTime = FormatValue(result.SwingTimeS, "s"),
                DoubleSupportTime = FormatValue(result.DoubleSupportTimeS, "s"),
                SingleSupportTime = FormatValue(result.SingleSupportTimeS, "s"),
                StepLength = FormatLengthCm(result.StepLengthM),
                StrideLength = FormatLengthCm(result.StrideLengthM),
                GaitSpeed = FormatValue(result.GaitSpeedMPerS, "m/s")
            };
        }

        if (result.KinematicSummary != null)
        {
            var ks = result.KinematicSummary;
            KinematicResult = new KinematicSummaryDisplay
            {
                HipRom = FormatValue(ks.HipRomDeg, "°"),
                KneeRom = FormatValue(ks.KneeRomDeg, "°"),
                AnkleRom = FormatValue(ks.AnkleRomDeg, "°"),
                PelvisRom = FormatValue(ks.PelvisCoronalRomDeg, "°")
            };
        }

        if (result.QualityControl != null)
        {
            var qc = result.QualityControl;

            // 按设计文档阈值计算各项等级
            var confidenceGrade = qc.MeanKeypointConfidence switch
            {
                >= 0.85 => QualityGrade.Excellent,
                >= 0.70 => QualityGrade.Good,
                _ => QualityGrade.Poor
            };

            var frameRatioGrade = qc.ValidFrameRatio switch
            {
                >= 0.95 => QualityGrade.Excellent,
                >= 0.80 => QualityGrade.Good,
                _ => QualityGrade.Poor
            };

            // 遮挡和丢点：有则为差，无则为优秀
            var occlusionGrade = qc.OcclusionWarning ? QualityGrade.Poor : QualityGrade.Excellent;
            var missingGrade = qc.MissingPointWarning ? QualityGrade.Poor : QualityGrade.Excellent;

            // 综合等级判定
            var worstGrade = (QualityGrade)Math.Max(
                Math.Max((int)confidenceGrade, (int)frameRatioGrade),
                Math.Max((int)occlusionGrade, (int)missingGrade));

            var (gradeDisplay, gradeColor, gradeDesc) = worstGrade switch
            {
                QualityGrade.Excellent => ("A", "#4CAF50", _localizationService.GetString("MA.Step4.Quality.GradeExcellent")),
                QualityGrade.Good => ("B", "#FF9800", _localizationService.GetString("MA.Step4.Quality.GradeGood")),
                _ => ("C", "#F44336", _localizationService.GetString("MA.Step4.Quality.GradePoor"))
            };

            QualityResult = new QualityControlDisplay
            {
                Confidence = qc.MeanKeypointConfidence ?? 0,
                ConfidenceDisplay = qc.MeanKeypointConfidence?.ToString("F2") ?? "--",
                ConfidenceOk = confidenceGrade != QualityGrade.Poor,
                ConfidenceGrade = confidenceGrade,
                ValidFrameRatio = qc.ValidFrameRatio ?? 0,
                ValidFrameRatioDisplay = qc.ValidFrameRatio.HasValue ? $"{qc.ValidFrameRatio.Value * 100:F0}%" : "--",
                ValidFrameRatioOk = frameRatioGrade != QualityGrade.Poor,
                ValidFrameRatioGrade = frameRatioGrade,
                HasOcclusion = qc.OcclusionWarning,
                HasMissingPoints = qc.MissingPointWarning,
                OverallGrade = worstGrade,
                GradeDisplay = gradeDisplay,
                GradeColor = gradeColor,
                GradeDescription = gradeDesc
            };
        }
    }

    /// <summary>
    /// 加载关节角度 CSV 数据并构建曲线图
    /// </summary>
    private void LoadJointAngleDataAndBuildPlots(AnalysisResult result)
    {
        var csvFile = result.CsvFiles?.FirstOrDefault(f =>
            f.FileType == (int)CsvFileType.JointAngle);

        if (csvFile == null || !File.Exists(csvFile.FilePath))
        {
            _logHelper?.Warning("未找到关节角度 CSV 文件，跳过曲线图构建");
            return;
        }

        try
        {
            _jointAngleFrames = ParseJointAngleCsv(csvFile.FilePath);
            if (_jointAngleFrames.Count == 0) return;

            var maxTime = _jointAngleFrames[^1].TimeS;

            HipAnglePlotModel = BuildJointPlotModel("髋关节角度 (°)", _jointAngleFrames, f => f.HipAngleDeg, OxyColors.SteelBlue, maxTime);
            KneeAnglePlotModel = BuildJointPlotModel("膝关节角度 (°)", _jointAngleFrames, f => f.KneeAngleDeg, OxyColors.ForestGreen, maxTime);
            AnkleAnglePlotModel = BuildJointPlotModel("踝关节角度 (°)", _jointAngleFrames, f => f.AnkleAngleDeg, OxyColors.OrangeRed, maxTime);
            PelvisAnglePlotModel = BuildJointPlotModel("骨盆角度 (°)", _jointAngleFrames, f => f.PelvisAngleDeg, OxyColors.MediumPurple, maxTime);
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载关节角度数据失败", ex);
        }
    }

    /// <summary>
    /// 解析关节角度 CSV 文件
    /// </summary>
    private static List<JointAngleFrame> ParseJointAngleCsv(string filePath)
    {
        var frames = new List<JointAngleFrame>();
        var lines = File.ReadAllLines(filePath);

        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length < 6) continue;

            if (int.TryParse(parts[0], out var frameIndex)
                && double.TryParse(parts[1], out var time)
                && double.TryParse(parts[2], out var hip)
                && double.TryParse(parts[3], out var knee)
                && double.TryParse(parts[4], out var ankle)
                && double.TryParse(parts[5], out var pelvis))
            {
                frames.Add(new JointAngleFrame
                {
                    FrameIndex = frameIndex,
                    TimeS = time,
                    HipAngleDeg = hip,
                    KneeAngleDeg = knee,
                    AnkleAngleDeg = ankle,
                    PelvisAngleDeg = pelvis
                });
            }
        }

        return frames;
    }

    /// <summary>
    /// 构建单个关节的曲线图模型
    /// </summary>
    private static PlotModel BuildJointPlotModel(
        string title,
        List<JointAngleFrame> frames,
        Func<JointAngleFrame, double> valueSelector,
        OxyColor color,
        double maxTime)
    {
        var model = new PlotModel
        {
            Title = title,
            TitleFontSize = 12,
            TitleFontWeight = 400,
            PlotMargins = new OxyThickness(40, 4, 8, 24),
            Padding = new OxyThickness(0),
            Background = OxyColors.Transparent,
            PlotAreaBorderThickness = new OxyThickness(1),
            PlotAreaBorderColor = OxyColor.FromRgb(200, 200, 200),
            IsLegendVisible = false
        };

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = 0,
            Maximum = maxTime,
            Title = "t (s)",
            TitleFontSize = 10,
            FontSize = 9,
            IsPanEnabled = false,
            IsZoomEnabled = false,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(230, 230, 230)
        });

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "°",
            TitleFontSize = 10,
            FontSize = 9,
            IsPanEnabled = false,
            IsZoomEnabled = false,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(230, 230, 230)
        });

        var series = new LineSeries
        {
            Color = color,
            StrokeThickness = 1.5,
            MarkerType = MarkerType.None
        };

        foreach (var frame in frames)
        {
            series.Points.Add(new DataPoint(frame.TimeS, valueSelector(frame)));
        }

        model.Series.Add(series);

        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Vertical,
            X = 0,
            Color = OxyColors.Red,
            StrokeThickness = 1.5,
            LineStyle = LineStyle.Solid,
            Tag = "TrackerLine"
        });

        return model;
    }

    /// <summary>
    /// 更新所有曲线图的 Tracker 垂直线位置
    /// </summary>
    private void UpdateTrackerLinePosition(double timeSeconds)
    {
        UpdateSinglePlotTracker(HipAnglePlotModel, timeSeconds);
        UpdateSinglePlotTracker(KneeAnglePlotModel, timeSeconds);
        UpdateSinglePlotTracker(AnkleAnglePlotModel, timeSeconds);
        UpdateSinglePlotTracker(PelvisAnglePlotModel, timeSeconds);
    }

    private static void UpdateSinglePlotTracker(PlotModel? model, double x)
    {
        if (model == null) return;

        var tracker = model.Annotations.OfType<LineAnnotation>()
            .FirstOrDefault(a => a.Tag is "TrackerLine");

        if (tracker != null)
        {
            tracker.X = x;
            model.InvalidatePlot(false);
        }
    }

    /// <summary>
    /// 更新曲线图 X 轴范围
    /// </summary>
    private void UpdatePlotXRange(double maxSeconds)
    {
        UpdateSinglePlotXRange(HipAnglePlotModel, maxSeconds);
        UpdateSinglePlotXRange(KneeAnglePlotModel, maxSeconds);
        UpdateSinglePlotXRange(AnkleAnglePlotModel, maxSeconds);
        UpdateSinglePlotXRange(PelvisAnglePlotModel, maxSeconds);
    }

    private static void UpdateSinglePlotXRange(PlotModel? model, double max)
    {
        if (model == null) return;

        var xAxis = model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
        if (xAxis != null)
        {
            xAxis.Maximum = max;
            model.InvalidatePlot(false);
        }
    }

    private void OnAnalysisProgressChanged(object? sender, AnalysisProgressEventArgs e)
    {
        Progress = e.Progress;
        CurrentStage = MapProgressToStage(e.Progress);
        StatusMessage = e.Message ?? string.Empty;
    }

    private void OnAnalysisLogReceived(object? sender, AnalysisLogEventArgs e)
    {
        AddLog(e.Message);
    }

    private void AddLog(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => TaskLogs.Add(entry));
        }
        else
        {
            TaskLogs.Add(entry);
        }
    }

    private void StartElapsedTimer()
    {
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _elapsedTimer.Tick += (_, _) =>
        {
            ElapsedTime = DateTime.Now - _analysisStartTime;
        };
        _elapsedTimer.Start();
    }

    private void StopElapsedTimer()
    {
        _elapsedTimer?.Stop();
        _elapsedTimer = null;
        ElapsedTime = DateTime.Now - _analysisStartTime;
    }

    private void ResetToReady()
    {
        AnalysisState = AnalysisState.Ready;
        Progress = 0;
        CurrentStage = string.Empty;
        StatusMessage = string.Empty;
        ElapsedTime = TimeSpan.Zero;
        ErrorCode = null;
        ErrorDescription = null;
        ErrorSuggestion = null;
        AnalysisResult = null;
        GaitEventResult = null;
        KinematicResult = null;
        QualityResult = null;
        AnnotatedVideoPath = null;
        IsPlaying = false;
        IsShowingParams = false;
        HasVideoError = false;
        VideoErrorMessage = null;
        HasChartData = false;
        HipAnglePlotModel = null;
        KneeAnglePlotModel = null;
        AnkleAnglePlotModel = null;
        PelvisAnglePlotModel = null;
        _jointAngleFrames = [];
    }

    private static string MapProgressToStage(int progress) => progress switch
    {
        < 10 => "任务初始化",
        < 35 => "检测关键点",
        < 60 => "检测步态事件",
        < 80 => "计算步态参数",
        < 100 => "输出结果",
        _ => "分析完成"
    };

    private static string GetErrorSuggestion(int? errorCode) => errorCode switch
    {
        1 => "请重新分析或联系技术支持",
        2 => "请返回步骤2检查视频文件是否存在",
        3 => "视频格式可能不受支持，请重新导入视频",
        4 => "算法内部错误，请重新分析或联系技术支持",
        5 => "请检查磁盘空间后重新分析",
        _ => "请查看日志获取详细信息或联系技术支持"
    };

    private static string FormatValue(double? value, string unit)
    {
        return value.HasValue ? $"{value.Value:F2}{unit}" : "--";
    }

    /// <summary>
    /// 将米转换为厘米后格式化（步长/步幅等更适合以 cm 展示）
    /// </summary>
    private static string FormatLengthCm(double? valueInMeters)
    {
        return valueInMeters.HasValue ? $"{valueInMeters.Value * 100:F1}cm" : "--";
    }

    #endregion
}
