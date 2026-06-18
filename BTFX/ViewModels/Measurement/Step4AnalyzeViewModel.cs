using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using BTFX.Common;
using BTFX.Helpers;
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
    private readonly IAnalysisPackageService _analysisPackageService;
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

    /// <summary>
    /// 请求回到新建测量并重新选择视频。用于已完成结果的重新分析入口。
    /// </summary>
    public event Action? ReanalysisSetupRequested;

    /// <summary>
    /// 分析取消完成后通知父级流程返回回放检查。
    /// </summary>
    public event Action? AnalysisCanceledRequested;

    #region 状态管理

    /// <summary>
    /// 当前分析状态
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(IsPreviewing))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    [NotifyPropertyChangedFor(nameof(ResultStatusDisplay))]
    [NotifyCanExecuteChangedFor(nameof(StartAnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelAnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryAnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(StepBackwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(StepForwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleParamsViewCommand))]
    [NotifyCanExecuteChangedFor(nameof(ViewReportCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateReportCommand))]
    [NotifyCanExecuteChangedFor(nameof(RerunAnalysisCommand))]
    [NotifyCanExecuteChangedFor(nameof(ViewLogCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoToStep2Command))]
    [NotifyCanExecuteChangedFor(nameof(ReturnCoverCommand))]
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
    public string ElapsedTimeDisplay => ElapsedTime.ToString(@"mm\:ss\.f");

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
    [NotifyPropertyChangedFor(nameof(ResultTaskIdDisplay))]
    [NotifyPropertyChangedFor(nameof(ResultAnalysisDurationDisplay))]
    [NotifyPropertyChangedFor(nameof(ResultCadenceDisplay))]
    [NotifyPropertyChangedFor(nameof(ResultStatusDisplay))]
    [NotifyPropertyChangedFor(nameof(ResultAnalysisModeDisplay))]
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

    public string ResultTaskIdDisplay => string.IsNullOrWhiteSpace(AnalysisResult?.RequestId)
        ? "--"
        : AnalysisResult.RequestId;

    public string ResultAnalysisModeDisplay
    {
        get
        {
            if (CurrentMeasurement?.HasDualVideo == true)
            {
                return L("MA.Step4.Mode.Dual");
            }

            if (CurrentMeasurement?.HasSideVideo == true || CurrentMeasurement?.HasFrontVideo == true)
            {
                return L("MA.Step4.Mode.Single");
            }

            return "--";
        }
    }

    public string ResultStatusDisplay
    {
        get
        {
            if (IsRunning)
            {
                return L("MA.Step4.Status.Running");
            }

            if (IsFailed)
            {
                return L("MA.Step4.Status.Failed");
            }

            if (IsPreviewing)
            {
                return AnalysisResult?.Success == true
                    ? L("MA.Step4.Status.Success")
                    : L("MA.Step4.Status.Completed");
            }

            return L("MA.Step4.Status.Waiting");
        }
    }

    public string ResultAnalysisDurationDisplay
    {
        get
        {
            if (AnalysisResult?.AnalysisDurationSeconds is double seconds)
            {
                return $"{seconds:F1} s";
            }

            return string.IsNullOrWhiteSpace(AnalysisDurationDisplay) ? "--" : AnalysisDurationDisplay;
        }
    }

    public string ResultCadenceDisplay
    {
        get
        {
            if (AnalysisResult?.GaitCycleDurationS is double cycleDuration && cycleDuration > 0)
            {
                return $"{120.0 / cycleDuration:F1} 步/min";
            }

            return "--";
        }
    }

    public string PatientHeightDisplay
    {
        get
        {
            if (CurrentPatient?.Height is not > 0)
            {
                return "--";
            }

            var height = CurrentPatient.Height.Value;
            return height > 3 ? $"{height:F0} cm" : $"{height:F2} m";
        }
    }

    public string PatientWeightDisplay => CurrentPatient?.Weight is > 0
        ? $"{CurrentPatient.Weight.Value:F1} kg"
        : "--";

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
        IAnalysisPackageService analysisPackageService,
        ISettingsService settingsService,
        ILocalizationService localizationService,
        ILogHelper? logHelper = null)
    {
        _analysisService = analysisService;
        _measurementService = measurementService;
        _analysisPackageService = analysisPackageService;
        _settingsService = settingsService;
        _localizationService = localizationService;
        _logHelper = logHelper;

        // 订阅分析服务事件
        _analysisService.ProgressChanged += OnAnalysisProgressChanged;
        _analysisService.LogReceived += OnAnalysisLogReceived;
        _localizationService.LanguageChanged += OnLanguageChanged;

        _logHelper?.Information("Step4AnalyzeViewModel 初始化完成");
    }

    private string L(string key) => _localizationService.GetString(key);

    private string L(string key, params object[] args) => _localizationService.GetString(key, args);

    private void OnLanguageChanged(object? sender, AppLanguage e)
    {
        OnPropertyChanged(nameof(ResultAnalysisModeDisplay));
        OnPropertyChanged(nameof(ResultStatusDisplay));
        RefreshPrerequisites();
        RebuildJointAnglePlots();
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
            Name = L("MA.Step4.Prereq.FrontVideo"),
            IsMet = hasFrontVideo,
            IsRequired = false,
            Icon = "VideoOutline"
        });

        // 侧面视频（可选）
        var hasSideVideo = !string.IsNullOrEmpty(CurrentMeasurement?.SideVideoPath)
                           && File.Exists(CurrentMeasurement?.SideVideoPath);
        items.Add(new PrerequisiteItem
        {
            Name = L("MA.Step4.Prereq.SideVideo"),
            IsMet = hasSideVideo,
            IsRequired = false,
            Icon = "VideoOutline"
        });

        // 至少一个视频可用（必需）
        items.Add(new PrerequisiteItem
        {
            Name = L("MA.Step4.Prereq.AnyVideo"),
            IsMet = hasFrontVideo || hasSideVideo,
            IsRequired = true,
            Icon = "VideoCheckOutline"
        });

        // 患者身高
        var hasHeight = CurrentPatient?.Height is > 0;
        items.Add(new PrerequisiteItem
        {
            Name = L("MA.Step4.Prereq.PatientHeight"),
            IsMet = hasHeight,
            IsRequired = true,
            Icon = "HumanMaleHeight"
        });

        // 患者体重（可选）
        var hasWeight = CurrentPatient?.Weight is > 0;
        items.Add(new PrerequisiteItem
        {
            Name = L("MA.Step4.Prereq.PatientWeight"),
            IsMet = hasWeight,
            IsRequired = false,
            Icon = "ScaleBathroom"
        });

        // 相机距离。未配置时算法配置使用 TOML 默认距离，后续可在设置中补充精确值。
        var hasCameraDistance = settings.Algorithm.SideCameraDistance > 0;
        items.Add(new PrerequisiteItem
        {
            Name = L("MA.Step4.Prereq.CameraDistance"),
            IsMet = hasCameraDistance,
            IsRequired = false,
            Icon = "CameraOutline"
        });

        // 算法程序
        var exePath = ResolveAlgorithmExePath(settings.Algorithm.ExePath);
        var hasExe = !string.IsNullOrEmpty(exePath) && File.Exists(exePath);
        items.Add(new PrerequisiteItem
        {
            Name = L("MA.Step4.Prereq.AlgorithmExe"),
            IsMet = hasExe,
            IsRequired = true,
            Icon = "CogOutline"
        });

        PrerequisiteItems = items;
        AllPrerequisitesMet = items.Where(i => i.IsRequired).All(i => i.IsMet);
        OnPropertyChanged(nameof(PatientHeightDisplay));
        OnPropertyChanged(nameof(PatientWeightDisplay));
        OnPropertyChanged(nameof(ResultAnalysisModeDisplay));
    }

    public async Task RestoreExistingMeasurementAsync(
        MeasurementRecord record,
        Patient? patient,
        MeasurementResumeDecision decision)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(decision);

        StopElapsedTimer(updateElapsedTime: false);
        _analysisCts?.Cancel();
        _analysisCts?.Dispose();
        _analysisCts = null;
        PlayStateChanged?.Invoke(this, false);

        CurrentMeasurement = record;
        CurrentPatient = patient;
        TaskLogs.Clear();
        AddLog(decision.Message);
        RefreshPrerequisites();

        ErrorCode = null;
        ErrorDescription = null;
        ErrorSuggestion = null;
        IsPlaying = false;
        HasVideoError = false;
        VideoErrorMessage = null;

        if (record.Status == MeasurementStatus.Completed)
        {
            var latestResult = await _analysisService.GetLatestAnalysisResultAsync(record.Id);
            if (latestResult is null)
            {
                AnalysisResult = null;
                AnalysisState = AnalysisState.Failed;
                Progress = 0;
                CurrentStage = L("MA.Step4.Stage.MissingResult");
                StatusMessage = L("MA.Step4.Message.MissingResult");
                ErrorDescription = StatusMessage;
                ErrorSuggestion = L("MA.Step4.Message.MissingResultSuggestion");
                AddLog(StatusMessage);
                return;
            }

            AnalysisResult = latestResult;
            PopulateResultDisplayData(latestResult);
            AnnotatedVideoPath = !string.IsNullOrWhiteSpace(latestResult.AnnotatedVideoPath) && File.Exists(latestResult.AnnotatedVideoPath)
                ? latestResult.AnnotatedVideoPath
                : null;
            HasVideoError = AnnotatedVideoPath is null;
            VideoErrorMessage = HasVideoError ? L("MA.Step4.Message.VideoMissingParamsOnly") : null;
            IsShowingParams = HasVideoError;
            LoadJointAngleDataAndBuildPlots(latestResult);
            HasChartData = HipAnglePlotModel is not null;
            Progress = 100;
            CurrentStage = L("MA.Step4.Status.Completed");
            StatusMessage = L("MA.Step4.Message.HistoryLoaded");
            AnalysisState = AnalysisState.Previewing;
            AddLog(StatusMessage);
            return;
        }

        if (record.Status == MeasurementStatus.InProgress && !decision.RequiresReanalysis)
        {
            _analysisStartTime = DateTime.Now;
            StartElapsedTimer();
            AnalysisResult = null;
            Progress = Math.Max(Progress, 5);
            CurrentStage = L("MA.Step4.Status.Running");
            StatusMessage = L("MA.Step4.Message.BackgroundRunning");
            AnalysisState = AnalysisState.Running;
            return;
        }

        if (decision.RequiresReanalysis)
        {
            AnalysisResult = null;
            Progress = 0;
            CurrentStage = L("MA.Step4.Stage.WaitingReanalysis");
            StatusMessage = decision.Message;
            ErrorDescription = decision.Message;
            ErrorSuggestion = L("MA.Step4.Message.ReanalysisSuggestion");
            AnalysisState = AnalysisState.Failed;
            return;
        }

        AnalysisResult = null;
        Progress = 0;
        CurrentStage = L("MA.Step4.Status.Waiting");
        StatusMessage = decision.Message;
        AnalysisState = AnalysisState.Ready;
    }

    private static string ResolveAlgorithmExePath(string? configuredPath)
    {
        var exePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Constants.ALGORITHM_DIRECTORY, Constants.ALGORITHM_EXE_FILENAME)
            : configuredPath;

        if (string.Equals(exePath, Path.Combine("Algorithm", Constants.ALGORITHM_EXE_FILENAME), StringComparison.OrdinalIgnoreCase))
        {
            exePath = Path.Combine(Constants.ALGORITHM_DIRECTORY, Constants.ALGORITHM_EXE_FILENAME);
        }

        if (string.Equals(exePath, Path.Combine("gait_analysis", "Gait_analysis.exe"), StringComparison.OrdinalIgnoreCase)
            || string.Equals(exePath, Path.Combine("gait_analysis", "gait_analysis.exe"), StringComparison.OrdinalIgnoreCase)
            || IsKnownCpuAlgorithmPath(exePath))
        {
            exePath = Path.Combine(Constants.ALGORITHM_DIRECTORY, Constants.ALGORITHM_EXE_FILENAME);
        }

        return Path.IsPathRooted(exePath)
            ? exePath
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exePath);
    }

    private static bool IsKnownCpuAlgorithmPath(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        var fileName = Path.GetFileName(exePath);
        if (!string.Equals(fileName, "Gait_analysis.exe", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fileName, "gait_analysis.exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var directoryName = Path.GetFileName(Path.GetDirectoryName(exePath)?.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar));
        return string.Equals(directoryName, "gait_analysis", StringComparison.OrdinalIgnoreCase)
               || string.Equals(directoryName, "Algorithm", StringComparison.OrdinalIgnoreCase);
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
            if (!DiskSpaceGuard.EnsureProgramDriveHasSpace("步态分析"))
            {
                return;
            }

            // 切换到运行状态
            AnalysisState = AnalysisState.Running;
            Progress = 0;
            CurrentStage = L("MA.Step4.Stage.Preparing");
            StatusMessage = string.Empty;
            TaskLogs.Clear();
            IsLogExpanded = true;
            AddLog("开始分析任务");
            await UpdateCurrentMeasurementStatusAsync(MeasurementStatus.InProgress, "测量状态已更新为分析中");

            // 启动计时
            _analysisStartTime = DateTime.Now;
            StartElapsedTimer();

            // 准备输出目录：固定在程序目录下，便于联调时直接查看输入、配置、日志和算法输出。
            var outputDir = CreateAnalysisOutputDirectory(CurrentMeasurement);
            Directory.CreateDirectory(outputDir);
            AddLog($"分析输出目录: {outputDir}");

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
                await OnAnalysisFailedAsync(result.ErrorCode, result.ErrorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            StopElapsedTimer();
            AnalysisState = AnalysisState.Ready;
            await UpdateCurrentMeasurementStatusAsync(MeasurementStatus.Pending, "分析已取消，测量状态已恢复为待处理");
            AddLog("分析已取消");
            _logHelper?.Information("分析已取消");
            AnalysisCanceledRequested?.Invoke();
        }
        catch (Exception ex)
        {
            StopElapsedTimer();
            await OnAnalysisFailedAsync(null, ex.Message);
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
        var result = await MaterialDesignThemes.Wpf.DialogHost.Show(
            new Views.Dialogs.ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel
                {
                    Title = L("MA.Step4.Dialog.StopTitle"),
                    Message = L("MA.Step4.Dialog.StopMessage"),
                    ConfirmText = L("Confirm"),
                    CancelText = L("Cancel"),
                    IsCancelVisible = true
                }
            },
            "RootDialog");

        if (result is not true)
        {
            return;
        }

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

    [RelayCommand(CanExecute = nameof(CanRerunAnalysis))]
    private async Task RerunAnalysisAsync()
    {
        if (IsPreviewing || CurrentMeasurement?.Status == MeasurementStatus.Completed)
        {
            var result = MessageBox.Show(
                "重新分析需要重新上传或重新采集视频，当前已完成结果会保留为历史记录。\n是否返回新建测量页面重新选择视频？",
                "重新分析",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.OK)
            {
                return;
            }

            ResetToReady();
            ReanalysisSetupRequested?.Invoke();
            return;
        }

        ResetToReady();
        RefreshPrerequisites();
        await StartAnalyzeAsync();
    }

    private bool CanRerunAnalysis() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanReturnCover))]
    private void ReturnCover()
    {
        ResetToReady();
        NavigateToStepRequested?.Invoke(0);
    }

    private bool CanReturnCover() => !IsRunning;

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
        VideoCurrentTime = position;
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
        VideoErrorMessage = L("MA.Step4.Message.VideoCannotPlay", errorMessage);
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

                AddLog("正在生成分析结果包...");
                var packageResult = await _analysisPackageService.CreatePackageAsync(result, CurrentMeasurement);
                AddLog(packageResult.Success
                    ? $"分析结果包已生成: {Path.GetFileName(packageResult.PackagePath)}"
                    : $"⚠ {packageResult.Message}");

                // 更新测量记录状态为已完成
                if (CurrentMeasurement is not null)
                {
                    await UpdateCurrentMeasurementStatusAsync(MeasurementStatus.Completed, "测量状态已更新为已完成");
                }
            }
            else
            {
                AddLog("⚠ 分析结果保存失败，数据仅在内存中");
                await UpdateCurrentMeasurementStatusAsync(MeasurementStatus.Failed, "分析结果保存失败，测量状态已更新为分析失败");
                _logHelper?.Warning("分析结果入库失败");
            }
        }
        catch (Exception ex)
        {
            AddLog($"⚠ 保存分析结果时出错: {ex.Message}");
            await UpdateCurrentMeasurementStatusAsync(MeasurementStatus.Failed, "分析结果保存异常，测量状态已更新为分析失败");
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
            VideoErrorMessage = L("MA.Step4.Message.VideoMissingParamsOnly");
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
    private async Task OnAnalysisFailedAsync(int? errorCode, string? errorMessage)
    {
        var friendlyMessage = ToUserFriendlyAnalysisError(errorMessage);
        ErrorCode = errorCode;
        ErrorDescription = friendlyMessage.Description;
        ErrorSuggestion = friendlyMessage.Suggestion ?? GetErrorSuggestion(errorCode);
        AnalysisState = AnalysisState.Failed;
        await UpdateCurrentMeasurementStatusAsync(MeasurementStatus.Failed, "测量状态已更新为分析失败");
        AddLog($"分析失败: [{errorCode}] {friendlyMessage.Description}");
    }

    private (string Description, string? Suggestion) ToUserFriendlyAnalysisError(string? rawMessage)
    {
        var message = NormalizeWhitespace(rawMessage);
        if (string.IsNullOrWhiteSpace(message))
        {
            return (L("MA.Step4.Error.AnalysisFailed"), L("MA.Step4.Error.CheckVideoAndLog"));
        }

        var lower = message.ToLowerInvariant();
        if (lower.Contains("未检测到完整人体关键点", StringComparison.Ordinal)
            || lower.Contains("at least one of the following markers is missing", StringComparison.Ordinal)
            || lower.Contains("marker is missing", StringComparison.Ordinal)
            || lower.Contains("markers is missing", StringComparison.Ordinal)
            || lower.Contains("person is entirely visible", StringComparison.Ordinal)
            || lower.Contains("person is entirely wisible", StringComparison.Ordinal))
        {
            return (L("MA.Step4.Error.MissingBodyKeypoints"), L("MA.Step4.Error.MissingBodyKeypointsSuggestion"));
        }

        var logIndex = message.IndexOf("详细日志:", StringComparison.Ordinal);
        if (logIndex >= 0)
        {
            message = message[..logIndex].TrimEnd('。', '，', ' ');
        }

        return (message, null);
    }

    private static string NormalizeWhitespace(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private async Task UpdateCurrentMeasurementStatusAsync(MeasurementStatus status, string logMessage)
    {
        if (CurrentMeasurement is null)
        {
            return;
        }

        try
        {
            var success = await _measurementService.UpdateMeasurementStatusAsync(CurrentMeasurement.Id, status);
            if (success)
            {
                CurrentMeasurement.Status = status;
                AddLog(logMessage);
            }
            else
            {
                AddLog("⚠ 测量状态更新失败");
            }
        }
        catch (Exception ex)
        {
            AddLog($"⚠ 测量状态更新异常: {ex.Message}");
            _logHelper?.Error($"测量状态更新异常: MeasurementId={CurrentMeasurement.Id}, Status={status}", ex);
        }
    }

    private static string GetAnalysisRootDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, "Data", "Analysis");
    }

    private static string CreateAnalysisOutputDirectory(MeasurementRecord measurement)
    {
        var measurementName = string.IsNullOrWhiteSpace(measurement.MeasurementName)
            ? $"measurement_{measurement.Id}"
            : SanitizePathSegment(measurement.MeasurementName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(GetAnalysisRootDirectory(), $"{measurement.Id}_{measurementName}_{timestamp}");
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray();
        var sanitized = new string(chars).Trim(' ', '.', '_');
        return string.IsNullOrWhiteSpace(sanitized) ? "measurement" : sanitized;
    }

    /// <summary>
    /// 填充结果展示数据
    /// </summary>
    private void PopulateResultDisplayData(AnalysisResult result)
    {
        EnrichResultFromResultJson(result);

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

        var validFrameRatio = result.QualityControl?.ValidFrameRatio ?? EstimateValidFrameRatio(result);
        QualityResult = new QualityControlDisplay
        {
            ValidFrameRatio = validFrameRatio ?? 0,
            ValidFrameRatioDisplay = validFrameRatio.HasValue ? $"{validFrameRatio.Value * 100:F0}%" : "--"
        };
        OnPropertyChanged(nameof(ResultCadenceDisplay));
    }

    private double? EstimateValidFrameRatio(AnalysisResult result)
    {
        try
        {
            var csvPath = result.CsvFiles?
                .FirstOrDefault(file => file.FileType == CsvFileType.JointAngle && File.Exists(file.FilePath))
                ?.FilePath;

            if (string.IsNullOrWhiteSpace(csvPath))
            {
                return null;
            }

            var validFrameCount = File.ReadLines(csvPath)
                .Skip(1)
                .Count(line => !string.IsNullOrWhiteSpace(line));
            if (validFrameCount <= 0)
            {
                return null;
            }

            var totalFrameCount = TryReadTotalFrameCount(result);
            return totalFrameCount is > 0
                ? Math.Clamp(validFrameCount / totalFrameCount.Value, 0d, 1d)
                : null;
        }
        catch (Exception ex)
        {
            _logHelper?.Warning($"估算有效帧比例失败：{ex.Message}");
            return null;
        }
    }

    private static void EnrichResultFromResultJson(AnalysisResult result)
    {
        var resultJsonPath = ResolveResultJsonPath(result);
        if (string.IsNullOrWhiteSpace(resultJsonPath))
        {
            return;
        }

        var root = JsonNode.Parse(File.ReadAllText(resultJsonPath))?.AsObject();
        if (root is null)
        {
            return;
        }

        var gaitCycle = root["gait_cycle"] as JsonObject;
        var sp = root["spatiotemporal_parameters"] as JsonObject;
        result.GaitCycleDurationS ??= ReadDouble(gaitCycle, "mean_cycle_duration_sec")
            ?? ReadDouble(gaitCycle, "cycle_time_sec")
            ?? AverageCycleDuration(gaitCycle);
        result.StanceTimeS ??= ReadDouble(sp, "mean_stance_time_sec");
        result.SwingTimeS ??= ReadDouble(sp, "mean_swing_time_sec");
        result.DoubleSupportTimeS ??= ReadDouble(sp, "mean_double_support_time_sec");
        result.SingleSupportTimeS ??= ReadDouble(sp, "mean_single_support_time_sec");
        result.StepLengthM ??= ReadDouble(sp, "mean_step_length_m");
        result.StrideLengthM ??= ReadDouble(sp, "mean_stride_length_m");
        result.GaitSpeedMPerS ??= ReadDouble(sp, "gait_velocity_m_per_sec") ?? ReadDouble(sp, "gait_speed_m_per_s");

        var jointAngles = root["joint_angles"] as JsonObject;
        var segmentAngles = root["segment_angles"] as JsonObject;
        result.KinematicSummary ??= new KinematicSummary();
        result.KinematicSummary.HipRomDeg ??= Average(
            ReadDouble(jointAngles?["left_hip"] as JsonObject, "rom_deg"),
            ReadDouble(jointAngles?["right_hip"] as JsonObject, "rom_deg"));
        result.KinematicSummary.KneeRomDeg ??= Average(
            ReadDouble(jointAngles?["left_knee"] as JsonObject, "rom_deg"),
            ReadDouble(jointAngles?["right_knee"] as JsonObject, "rom_deg"));
        result.KinematicSummary.AnkleRomDeg ??= Average(
            ReadDouble(jointAngles?["left_ankle"] as JsonObject, "rom_deg"),
            ReadDouble(jointAngles?["right_ankle"] as JsonObject, "rom_deg"));
        result.KinematicSummary.PelvisCoronalRomDeg ??= Difference(
            ReadDouble(segmentAngles?["pelvis_tilt_deg"] as JsonObject, "max"),
            ReadDouble(segmentAngles?["pelvis_tilt_deg"] as JsonObject, "min"));
    }

    private static string? ResolveResultJsonPath(AnalysisResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.SummaryFilePath) && File.Exists(result.SummaryFilePath))
        {
            return result.SummaryFilePath;
        }

        return Directory.Exists(result.OutputDirectory)
            ? Directory.GetFiles(result.OutputDirectory, "result.json", SearchOption.AllDirectories).FirstOrDefault()
            : null;
    }

    private static double? AverageCycleDuration(JsonObject? gaitCycle)
    {
        if (gaitCycle?["cycles"] is not JsonArray cycles)
        {
            return null;
        }

        var durations = cycles
            .OfType<JsonObject>()
            .Select(cycle => ReadDouble(cycle, "duration_sec"))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        return durations.Length == 0 ? null : durations.Average();
    }

    private static double? Average(double? left, double? right)
        => left.HasValue && right.HasValue ? (left.Value + right.Value) / 2d : left ?? right;

    private static double? Difference(double? left, double? right)
        => left.HasValue && right.HasValue ? Math.Abs(left.Value - right.Value) : null;

    private static double? TryReadTotalFrameCount(AnalysisResult result)
    {
        var resultJsonPath = ResolveResultJsonPath(result);

        if (string.IsNullOrWhiteSpace(resultJsonPath))
        {
            return null;
        }

        var root = JsonNode.Parse(File.ReadAllText(resultJsonPath))?.AsObject();
        var videoInfo = root?["video_info"] as JsonObject;
        var frameCount = ReadDouble(videoInfo, "frame_count");
        if (frameCount is > 0)
        {
            return frameCount;
        }

        var fps = ReadDouble(videoInfo, "fps");
        var durationSec = ReadDouble(videoInfo, "duration_sec");
        return fps is > 0 && durationSec is > 0 ? fps.Value * durationSec.Value : null;
    }

    private static double? ReadDouble(JsonObject? obj, string key)
    {
        if (obj is null || obj[key] is null)
        {
            return null;
        }

        return double.TryParse(
            obj[key]!.ToString(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
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

            RebuildJointAnglePlots();
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
        StopElapsedTimer(updateElapsedTime: false);
        ElapsedTime = TimeSpan.Zero;
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _elapsedTimer.Tick += ElapsedTimer_Tick;
        _elapsedTimer.Start();
    }

    private void ElapsedTimer_Tick(object? sender, EventArgs e)
    {
        ElapsedTime = DateTime.Now - _analysisStartTime;
    }

    private void StopElapsedTimer(bool updateElapsedTime = true)
    {
        if (_elapsedTimer is not null)
        {
            _elapsedTimer.Stop();
            _elapsedTimer.Tick -= ElapsedTimer_Tick;
        }

        _elapsedTimer = null;
        if (updateElapsedTime)
        {
            ElapsedTime = DateTime.Now - _analysisStartTime;
        }
    }

    private void ResetToReady()
    {
        StopElapsedTimer(updateElapsedTime: false);
        _analysisCts?.Cancel();
        _analysisCts?.Dispose();
        _analysisCts = null;
        PlayStateChanged?.Invoke(this, false);

        AnalysisState = AnalysisState.Ready;
        Progress = 0;
        CurrentStage = string.Empty;
        StatusMessage = string.Empty;
        ElapsedTime = TimeSpan.Zero;
        AnalysisDurationDisplay = string.Empty;
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
        TaskLogs.Clear();
        HipAnglePlotModel = null;
        KneeAnglePlotModel = null;
        AnkleAnglePlotModel = null;
        PelvisAnglePlotModel = null;
        _jointAngleFrames = [];
    }

    private void RebuildJointAnglePlots()
    {
        if (_jointAngleFrames.Count == 0)
        {
            return;
        }

        var maxTime = _jointAngleFrames[^1].TimeS;
        HipAnglePlotModel = BuildJointPlotModel(L("MA.Step4.Chart.HipAngleTitle"), _jointAngleFrames, f => f.HipAngleDeg, OxyColors.SteelBlue, maxTime);
        KneeAnglePlotModel = BuildJointPlotModel(L("MA.Step4.Chart.KneeAngleTitle"), _jointAngleFrames, f => f.KneeAngleDeg, OxyColors.ForestGreen, maxTime);
        AnkleAnglePlotModel = BuildJointPlotModel(L("MA.Step4.Chart.AnkleAngleTitle"), _jointAngleFrames, f => f.AnkleAngleDeg, OxyColors.OrangeRed, maxTime);
        PelvisAnglePlotModel = BuildJointPlotModel(L("MA.Step4.Chart.PelvisAngleTitle"), _jointAngleFrames, f => f.PelvisAngleDeg, OxyColors.MediumPurple, maxTime);
    }

    private string MapProgressToStage(int progress) => progress switch
    {
        < 10 => L("MA.Step4.Stage.Initializing"),
        < 35 => L("MA.Step4.Stage.KeypointDetection"),
        < 60 => L("MA.Step4.Stage.GaitEventDetection"),
        < 80 => L("MA.Step4.Stage.ParameterCalculation"),
        < 100 => L("MA.Step4.Stage.OutputResult"),
        _ => L("MA.Step4.Status.Completed")
    };

    private string GetErrorSuggestion(int? errorCode) => errorCode switch
    {
        1 => L("MA.Step4.Error.Suggestion1"),
        2 => L("MA.Step4.Error.Suggestion2"),
        3 => L("MA.Step4.Error.Suggestion3"),
        4 => L("MA.Step4.Error.Suggestion4"),
        5 => L("MA.Step4.Error.Suggestion5"),
        _ => L("MA.Step4.Error.SuggestionDefault")
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
