using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Documents;
using BTFX.Common;
using BTFX.Helpers;
using BTFX.Models;
using BTFX.Models.Analysis;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using ToolHelper.LoggingDiagnostics.Abstractions;
using Constants = BTFX.Common.Constants;

namespace BTFX.ViewModels;

/// <summary>
/// 分析结果详情宿主视图模型。
/// </summary>
public partial class GaitAnalysisDetailViewModel : ObservableObject
{
    private readonly IGaitAnalysisService _gaitAnalysisService;
    private readonly IReportService _reportService;
    private readonly IExportImportService _exportImportService;
    private readonly ISessionService _sessionService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;

    /// <summary>
    /// 当前测量记录。
    /// </summary>
    [ObservableProperty]
    private MeasurementRecord? _record;

    /// <summary>
    /// 当前分析结果。
    /// </summary>
    [ObservableProperty]
    private AnalysisResult? _analysisResult;

    /// <summary>
    /// 当前详情状态。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyState))]
    [NotifyPropertyChangedFor(nameof(IsFailedState))]
    [NotifyPropertyChangedFor(nameof(IsSuccessState))]
    private AnalysisDetailState _detailState = AnalysisDetailState.Empty;

    /// <summary>
    /// 当前是否正在加载。
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 状态标题。
    /// </summary>
    [ObservableProperty]
    private string _stateTitle = string.Empty;

    /// <summary>
    /// 状态说明。
    /// </summary>
    [ObservableProperty]
    private string _stateMessage = string.Empty;

    /// <summary>
    /// 当前选中的导航项。
    /// </summary>
    [ObservableProperty]
    private AnalysisDetailNavigationItem? _selectedNavigationItem;

    /// <summary>
    /// 是否可导出。
    /// </summary>
    [ObservableProperty]
    private bool _canExport;

    /// <summary>
    /// 当前报告草稿。
    /// </summary>
    private Report? _currentReportDraft;

    /// <summary>
    /// 是否正在加载报告配置。
    /// </summary>
    private bool _isReportConfigLoading;

    /// <summary>
    /// 报告配置标题。
    /// </summary>
    private string _reportConfigTitle = string.Empty;

    /// <summary>
    /// 报告配置说明。
    /// </summary>
    private string _reportConfigMessage = string.Empty;

    /// <summary>
    /// 报告标题。
    /// </summary>
    private string _reportTitle = string.Empty;

    /// <summary>
    /// 医生意见。
    /// </summary>
    private string _reportDoctorOpinion = string.Empty;

    /// <summary>
    /// 是否包含时空参数。
    /// </summary>
    private bool _includeSpatiotemporalParameters = true;

    /// <summary>
    /// 是否包含运动学摘要。
    /// </summary>
    private bool _includeKinematicSummary = true;

    /// <summary>
    /// 是否包含质量控制。
    /// </summary>
    private bool _includeQualityControl = true;

    /// <summary>
    /// 是否包含结果文件摘要。
    /// </summary>
    private bool _includeResultFiles = false;

    /// <summary>
    /// 报告预览状态说明。
    /// </summary>
    private string _reportPreviewMessage = string.Empty;

    /// <summary>
    /// 是否正在准备报告预览。
    /// </summary>
    private bool _isPreparingReportPreview;
    private bool _isAnalysisPreviewGenerating;
    private bool _analysisPreviewGenerationRequested;
    private string _analysisPreviewStatusText = string.Empty;
    private bool _isSingleViewOutput;
    private string? _annotatedVideoPath;

    /// <summary>
    /// 是否正在回填草稿配置。
    /// </summary>
    private bool _isApplyingDraftConfig;

    /// <summary>
    /// 是否存在待持久化的草稿配置变更。
    /// </summary>
    private bool _hasPendingDraftSnapshotChanges;

    private AnalysisDetailData _detailData = new();
    private List<AnalysisAngleFrame> _angleFrames = [];
    private double _videoPlaybackSeconds;

    /// <summary>
    /// 当前报告草稿。
    /// </summary>
    public Report? CurrentReportDraft
    {
        get => _currentReportDraft;
        private set => SetProperty(ref _currentReportDraft, value);
    }

    /// <summary>
    /// 是否正在加载报告配置。
    /// </summary>
    public bool IsReportConfigLoading
    {
        get => _isReportConfigLoading;
        private set => SetProperty(ref _isReportConfigLoading, value);
    }

    /// <summary>
    /// 报告配置标题。
    /// </summary>
    public string ReportConfigTitle
    {
        get => _reportConfigTitle;
        private set => SetProperty(ref _reportConfigTitle, value);
    }

    /// <summary>
    /// 报告配置说明。
    /// </summary>
    public string ReportConfigMessage
    {
        get => _reportConfigMessage;
        private set => SetProperty(ref _reportConfigMessage, value);
    }

    /// <summary>
    /// 报告标题。
    /// </summary>
    public string ReportTitle
    {
        get => _reportTitle;
        set
        {
            if (SetProperty(ref _reportTitle, value))
            {
                SyncDraftOptionsToModel(markDirty: true);
            }
        }
    }

    /// <summary>
    /// 报告预览状态说明。
    /// </summary>
    public string ReportPreviewMessage
    {
        get => _reportPreviewMessage;
        private set => SetProperty(ref _reportPreviewMessage, value);
    }

    /// <summary>
    /// 是否正在准备报告预览。
    /// </summary>
    public bool IsPreparingReportPreview
    {
        get => _isPreparingReportPreview;
        private set => SetProperty(ref _isPreparingReportPreview, value);
    }

    /// <summary>
    /// 医生意见。
    /// </summary>
    public string ReportDoctorOpinion
    {
        get => _reportDoctorOpinion;
        set
        {
            if (SetProperty(ref _reportDoctorOpinion, value))
            {
                SyncDraftOptionsToModel(markDirty: true);
            }
        }
    }

    /// <summary>
    /// 是否包含时空参数。
    /// </summary>
    public bool IncludeSpatiotemporalParameters
    {
        get => _includeSpatiotemporalParameters;
        set
        {
            if (SetProperty(ref _includeSpatiotemporalParameters, value))
            {
                SyncDraftOptionsToModel(markDirty: true);
            }
        }
    }

    /// <summary>
    /// 是否包含运动学摘要。
    /// </summary>
    public bool IncludeKinematicSummary
    {
        get => _includeKinematicSummary;
        set
        {
            if (SetProperty(ref _includeKinematicSummary, value))
            {
                SyncDraftOptionsToModel(markDirty: true);
            }
        }
    }

    /// <summary>
    /// 是否包含质量控制。
    /// </summary>
    public bool IncludeQualityControl
    {
        get => _includeQualityControl;
        set
        {
            if (SetProperty(ref _includeQualityControl, value))
            {
                SyncDraftOptionsToModel(markDirty: true);
            }
        }
    }

    /// <summary>
    /// 是否包含结果文件摘要。
    /// </summary>
    public bool IncludeResultFiles
    {
        get => _includeResultFiles;
        set
        {
            if (SetProperty(ref _includeResultFiles, value))
            {
                SyncDraftOptionsToModel(markDirty: true);
            }
        }
    }

    /// <summary>
    /// 是否可以配置报告。
    /// </summary>
    public bool CanConfigureReport =>
        _sessionService.HasPermission("reportmanagement") &&
        AnalysisResult is { Success: true };

    /// <summary>
    /// 是否可以查看报告预览。
    /// </summary>
    public bool CanPreviewReport =>
        CanConfigureReport &&
        CurrentReportDraft is not null &&
        !string.IsNullOrWhiteSpace(ReportTitle) &&
        AnalysisResult is not null;

    /// <summary>
    /// 是否为空状态。
    /// </summary>
    public bool IsEmptyState => DetailState == AnalysisDetailState.Empty;

    /// <summary>
    /// 是否为失败状态。
    /// </summary>
    public bool IsFailedState => DetailState == AnalysisDetailState.Failed;

    /// <summary>
    /// 是否为成功状态。
    /// </summary>
    public bool IsSuccessState => DetailState == AnalysisDetailState.Success;

    /// <summary>
    /// 是否显示结果概览分区。
    /// </summary>
    public bool IsOverviewSectionSelected => SelectedNavigationItem?.Key is "overview";

    /// <summary>
    /// 是否显示时空参数分区。
    /// </summary>
    public bool IsSpatiotemporalSectionSelected => SelectedNavigationItem?.Key is "spatiotemporal";

    /// <summary>
    /// 是否显示运动学分区。
    /// </summary>
    public bool IsKinematicsSectionSelected => SelectedNavigationItem?.Key is "kinematics";

    /// <summary>
    /// 是否显示质量控制分区。
    /// </summary>
    public bool IsQualitySectionSelected => SelectedNavigationItem?.Key is "quality";

    /// <summary>
    /// 是否显示文件管理分区。
    /// </summary>
    public bool IsFilesSectionSelected => SelectedNavigationItem?.Key is "files";

    /// <summary>
    /// 是否显示报告配置分区。
    /// </summary>
    public bool IsReportSectionSelected => SelectedNavigationItem?.Key is "report";

    /// <summary>
    /// 左侧导航集合。
    /// </summary>
    public ObservableCollection<AnalysisDetailNavigationItem> NavigationItems { get; } = [];

    public PlotModel LeftHipAnglePlotModel { get; private set; } = new();

    public PlotModel RightHipAnglePlotModel { get; private set; } = new();

    public PlotModel LeftKneeAnglePlotModel { get; private set; } = new();

    public PlotModel RightKneeAnglePlotModel { get; private set; } = new();

    public PlotModel LeftAnkleAnglePlotModel { get; private set; } = new();

    public PlotModel RightAnkleAnglePlotModel { get; private set; } = new();

    public PlotModel PelvisAnglePlotModel { get; private set; } = new();

    public PlotModel TrunkAnglePlotModel { get; private set; } = new();

    public PlotModel VideoKneeAnglePlotModel { get; private set; } = new();

    public PlotModel VideoHipAnglePlotModel { get; private set; } = new();

    public PlotModel VideoAnkleAnglePlotModel { get; private set; } = new();

    public PlotModel VideoPelvisAnglePlotModel { get; private set; } = new();

    public PlotModel VideoTrunkAnglePlotModel { get; private set; } = new();

    public PlotModel VideoTrajectoryPlotModel { get; private set; } = new();

    public ObservableCollection<AnalysisCycleDetail> CycleDetails { get; } = [];

    /// <summary>
    /// 请求关闭对话框事件。
    /// </summary>
    public event Action? CloseRequested;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public GaitAnalysisDetailViewModel(
        IGaitAnalysisService gaitAnalysisService,
        IReportService reportService,
        IExportImportService exportImportService,
        ISessionService sessionService,
        ILocalizationService localizationService)
    {
        _gaitAnalysisService = gaitAnalysisService;
        _reportService = reportService;
        _exportImportService = exportImportService;
        _sessionService = sessionService;
        _localizationService = localizationService;
        CanExport = _sessionService.HasPermission("export");

        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch
        {
        }

        BuildRealPlotModels();
        ResetReportConfigState();
        InitializeNavigationItems();

        SelectedNavigationItem = NavigationItems.FirstOrDefault();
        SetEmptyState(L("AnalysisDetail.Empty.Title"), L("AnalysisDetail.Empty.Message"), false);
    }

    private string L(string key) => _localizationService.GetString(key);

    private string L(string key, params object[] args) => _localizationService.GetString(key, args);

    private void InitializeNavigationItems()
    {
        NavigationItems.Clear();
        NavigationItems.Add(new AnalysisDetailNavigationItem("overview", L("AnalysisDetail.Navigation.Overview"), L("AnalysisDetail.Navigation.OverviewDesc")));
        NavigationItems.Add(new AnalysisDetailNavigationItem("spatiotemporal", L("AnalysisDetail.Navigation.Spatiotemporal"), L("AnalysisDetail.Navigation.SpatiotemporalDesc")));
        NavigationItems.Add(new AnalysisDetailNavigationItem("kinematics", L("AnalysisDetail.Navigation.Kinematics"), L("AnalysisDetail.Navigation.KinematicsDesc")));
        NavigationItems.Add(new AnalysisDetailNavigationItem("quality", L("AnalysisDetail.Navigation.Quality"), L("AnalysisDetail.Navigation.QualityDesc")));
        NavigationItems.Add(new AnalysisDetailNavigationItem("files", L("AnalysisDetail.Navigation.Files"), L("AnalysisDetail.Navigation.FilesDesc")));
        NavigationItems.Add(new AnalysisDetailNavigationItem("report", L("AnalysisDetail.Navigation.Report"), L("AnalysisDetail.Navigation.ReportDesc")));
    }

    /// <summary>
    /// 初始化分析详情数据。
    /// </summary>
    /// <param name="record">测量记录。</param>
    public async Task InitializeAsync(MeasurementRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        Record = record;
        AnalysisResult = null;
        CurrentReportDraft = null;
        _isSingleViewOutput = false;
        _annotatedVideoPath = null;
        _analysisPreviewGenerationRequested = false;
        ResetReportConfigState();
        IsLoading = true;

        try
        {
            var latestAnalysisResult = await _gaitAnalysisService.GetLatestAnalysisResultAsync(record.Id);
            AnalysisResult = latestAnalysisResult;
            Record.LatestAnalysisResult = latestAnalysisResult;

            if (latestAnalysisResult is not null)
            {
                LoadAnalysisDetailFiles(latestAnalysisResult);
                UpdateAnalysisPreviewStatus();
                SetSuccessState();
                return;
            }

            if (HasAnalysisFailure(record))
            {
                SetFailedState();
                return;
            }

            SetEmptyState(L("AnalysisDetail.Empty.Title"), L("AnalysisDetail.Empty.NoSuccessResult"), false);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"加载分析详情失败：MeasurementId={record.Id}", ex);
            SetFailedState(L("AnalysisDetail.LoadFailedFormat", ex.Message));
        }
        finally
        {
            IsLoading = false;
            NotifyComputedPropertiesChanged();
        }
    }

    /// <summary>
    /// 患者姓名。
    /// </summary>
    public string PatientName => Record?.Patient?.Name ?? "--";

    /// <summary>
    /// 患者身高。
    /// </summary>
    public string PatientHeightDisplay => Record?.Patient?.Height is double height and > 0
        ? $"{height:F0} cm"
        : "--";

    /// <summary>
    /// 患者编号。
    /// </summary>
    public string PatientCode => Record?.PatientId.ToString() ?? "--";

    /// <summary>
    /// 测量名称。
    /// </summary>
    public string MeasurementName => string.IsNullOrWhiteSpace(Record?.MeasurementName) ? "--" : Record.MeasurementName;

    /// <summary>
    /// 测量类型。
    /// </summary>
    public string MeasurementTypeDisplay => Record is null ? L("AnalysisDetail.MeasurementType.NormalWalk") : GetEnumDescription(Record.MeasurementType);

    /// <summary>
    /// 分析模式。
    /// </summary>
    public string MeasurementVideoModeDisplay => Record?.HasDualVideo == true
        ? L("MA.Step4.Mode.Dual")
        : L("MA.Step4.Mode.Single");

    public bool IsDualVideoMode => Record?.HasDualVideo == true;

    public bool IsSingleVideoMode => !IsDualVideoMode;

    /// <summary>
    /// 测量时间。
    /// </summary>
    public string MeasurementDate => Record?.MeasurementDate.ToString(Constants.DATETIME_FORMAT) ?? "--";

    /// <summary>
    /// 分析任务编号。
    /// </summary>
    public string RequestIdDisplay => string.IsNullOrWhiteSpace(AnalysisResult?.RequestId) ? "--" : AnalysisResult.RequestId;

    /// <summary>
    /// 分析任务状态。
    /// </summary>
    public string TaskStatusDisplay
    {
        get
        {
            if (AnalysisResult is null)
            {
                return L("AnalysisDetail.Status.Completed");
            }

            if (AnalysisResult.Success)
            {
                return L("AnalysisDetail.Status.Completed");
            }

            return string.IsNullOrWhiteSpace(AnalysisResult.TaskStatus)
                ? L("AnalysisDetail.Status.ShortFailed")
                : AnalysisResult.TaskStatus;
        }
    }

    /// <summary>
    /// 分析耗时。
    /// </summary>
    public string AnalysisDurationDisplay => FormatNumber(AnalysisResult?.AnalysisDurationSeconds, "F1", "s");

    /// <summary>
    /// 创建时间。
    /// </summary>
    public string AnalysisCreatedAtDisplay => AnalysisResult?.CreatedAt.ToString(Constants.DATETIME_FORMAT) ?? "--";

    /// <summary>
    /// 协议版本。
    /// </summary>
    public string ProtocolVersionDisplay => string.IsNullOrWhiteSpace(AnalysisResult?.ProtocolVersion) ? "--" : AnalysisResult.ProtocolVersion;

    /// <summary>
    /// 算法版本。
    /// </summary>
    public string AlgorithmVersionDisplay => string.IsNullOrWhiteSpace(AnalysisResult?.AlgorithmVersion) ? "--" : AnalysisResult.AlgorithmVersion;

    /// <summary>
    /// 模型版本。
    /// </summary>
    public string ModelVersionDisplay => string.IsNullOrWhiteSpace(AnalysisResult?.ModelVersion) ? "--" : AnalysisResult.ModelVersion;

    /// <summary>
    /// 分析状态文本。
    /// </summary>
    public string AnalysisStatusText => DetailState switch
    {
        AnalysisDetailState.Success => L("AnalysisDetail.Status.Success"),
        AnalysisDetailState.Failed => L("AnalysisDetail.Status.Failed"),
        _ => L("AnalysisDetail.Status.NotAnalyzed")
    };

    /// <summary>
    /// 分析状态颜色。
    /// </summary>
    public string AnalysisStatusColor => DetailState switch
    {
        AnalysisDetailState.Success => "#4CAF50",
        AnalysisDetailState.Failed => "#F44336",
        _ => "#FF9800"
    };

    /// <summary>
    /// 质量结论。
    /// </summary>
    public string QualitySummary
    {
        get
        {
            if (_detailData.ValidFrameRatio is double ratio)
            {
                return L("AnalysisDetail.ValidFrameRatioText", ratio.ToString("P0", CultureInfo.CurrentCulture));
            }

            if (AnalysisResult?.QualityControl?.ValidFrameRatio is double validFrameRatio)
            {
                return L("AnalysisDetail.ValidFrameRatioText", validFrameRatio.ToString("P0", CultureInfo.CurrentCulture));
            }

            return AnalysisResult?.QualityControl is null ? "--" : L("AnalysisDetail.QualityGenerated");
        }
    }

    /// <summary>
    /// 有效帧比例摘要。
    /// </summary>
    public string ValidFrameRatioDisplay => _detailData.ValidFrameRatio is double ratio
        ? ratio.ToString("P0")
        : (AnalysisResult?.QualityControl?.ValidFrameRatio is double validFrameRatio ? validFrameRatio.ToString("P0") : "--");

    /// <summary>
    /// 平均步速。
    /// </summary>
    public string GaitSpeedDisplay => FormatNumber(AnalysisResult?.GaitSpeedMPerS ?? Record?.GaitParameters?.GaitSpeedMPerS ?? _detailData.GaitSpeedMPerS, "F2", "m/s");

    /// <summary>
    /// 平均步频。
    /// </summary>
    public string CadenceDisplay => FormatNumber(Record?.GaitParameters?.Cadence ?? _detailData.CadenceStepPerMin, "F1", "step/min");

    /// <summary>
    /// 平均步长。
    /// </summary>
    public string StepLengthDisplay => FormatNumber(AnalysisResult?.StepLengthM ?? Record?.GaitParameters?.StepLengthM ?? _detailData.MeanStepLengthM, "F2", "m");

    /// <summary>
    /// 步态周期。
    /// </summary>
    public string GaitCycleDisplay => FormatNumber(AnalysisResult?.GaitCycleDurationS ?? Record?.GaitParameters?.GaitCycleDurationS ?? _detailData.MeanCycleDurationSec, "F2", "s");

    /// <summary>
    /// 步幅。
    /// </summary>
    public string StrideLengthDisplay => FormatNumber(AnalysisResult?.StrideLengthM ?? _detailData.MeanStrideLengthM, "F2", "m");

    /// <summary>
    /// 站立相时长。
    /// </summary>
    public string StanceTimeDisplay => FormatNumber(AnalysisResult?.StanceTimeS ?? _detailData.MeanStanceTimeSec, "F2", "s");

    /// <summary>
    /// 摆动相时长。
    /// </summary>
    public string SwingTimeDisplay => FormatNumber(AnalysisResult?.SwingTimeS ?? _detailData.MeanSwingTimeSec, "F2", "s");

    /// <summary>
    /// 运动学摘要。
    /// </summary>
    public string KinematicSummaryDisplay
    {
        get
        {
            var summary = AnalysisResult?.KinematicSummary;
            if (summary is null)
            {
                return L("AnalysisDetail.KinematicsSummaryEmpty");
            }

            return L(
                "AnalysisDetail.KinematicsSummaryFormat",
                FormatNumber(summary.HipRomDeg, "F1", "°"),
                FormatNumber(summary.KneeRomDeg, "F1", "°"),
                FormatNumber(summary.AnkleRomDeg, "F1", "°"));
        }
    }

    /// <summary>
    /// 输出目录。
    /// </summary>
    public string OutputDirectory => !string.IsNullOrWhiteSpace(AnalysisResult?.OutputDirectory)
        ? AnalysisResult.OutputDirectory
        : (!string.IsNullOrWhiteSpace(Record?.MeasurementFolderPath) ? Record.MeasurementFolderPath : "output/demo_20260513_001");

    /// <summary>
    /// 结果文件数量。
    /// </summary>
    public string FileCountDisplay => AnalysisResult?.CsvFiles?.Count > 0
        ? L("AnalysisDetail.ResultFileCountFormat", AnalysisResult.CsvFiles.Count)
        : (_detailData.ResultFileCount > 0 ? L("AnalysisDetail.ResultFileCountFormat", _detailData.ResultFileCount) : "--");

    /// <summary>
    /// 标注视频摘要。
    /// </summary>
    public string AnnotatedVideoDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_annotatedVideoPath) && File.Exists(_annotatedVideoPath))
            {
                return Path.GetFileName(_annotatedVideoPath) ?? "--";
            }

            if (IsAnalysisPreviewGenerating)
            {
                return L("AnalysisDetail.PreviewGeneratingShort");
            }

            return HasAnalysisPreviewFailedMarker()
                ? L("AnalysisDetail.PreviewGenerateFailedShort")
                : "--";
        }
    }

    public bool IsAnalysisPreviewGenerating
    {
        get => _isAnalysisPreviewGenerating;
        private set
        {
            if (SetProperty(ref _isAnalysisPreviewGenerating, value))
            {
                OnPropertyChanged(nameof(AnnotatedVideoDisplay));
            }
        }
    }

    public string AnalysisPreviewStatusText
    {
        get => string.IsNullOrWhiteSpace(_analysisPreviewStatusText)
            ? L("AnalysisDetail.NoAnnotatedVideo")
            : _analysisPreviewStatusText;
        private set => SetProperty(ref _analysisPreviewStatusText, value);
    }

    public bool HasAnnotatedVideo => !string.IsNullOrWhiteSpace(_annotatedVideoPath)
        && File.Exists(_annotatedVideoPath);

    public Uri? AnnotatedVideoUri => HasAnnotatedVideo
        ? new Uri(_annotatedVideoPath!, UriKind.Absolute)
        : null;

    /// <summary>
    /// 摘要文件摘要。
    /// </summary>
    public string SummaryFileDisplay => string.IsNullOrWhiteSpace(AnalysisResult?.SummaryFilePath)
        ? "result.json"
        : Path.GetFileName(AnalysisResult.SummaryFilePath);

    /// <summary>
    /// 有效周期数。
    /// </summary>
    public string CycleCountDisplay => _detailData.CycleCount.HasValue
        ? L("AnalysisDetail.CycleCountFormat", _detailData.CycleCount.Value)
        : "--";

    /// <summary>
    /// 文件摘要。
    /// </summary>
    public string ResultFileSummaryDisplay => AnalysisResult?.CsvFiles?.Count > 0
        ? L("AnalysisDetail.FileSummaryFormat", AnalysisResult.CsvFiles.Count)
        : (_detailData.ResultFileSummary ?? "--");

    /// <summary>
    /// 日志文件摘要。
    /// </summary>
    public string LogFileDisplay => _detailData.LogFileName ?? "--";

    /// <summary>
    /// CSV 文件数量。
    /// </summary>
    public string CsvFileCountDisplay => AnalysisResult?.CsvFiles?.Count is int count and > 0
        ? L("AnalysisDetail.CountFormat", count)
        : L("AnalysisDetail.CountFormat", _detailData.CsvFileCount);

    /// <summary>
    /// 图片文件数量。
    /// </summary>
    public string ImageFileCountDisplay => L("AnalysisDetail.CountFormat", _detailData.ImageFileCount);

    public string MeanStrideLengthDisplay => StrideLengthDisplay;

    public string DoubleSupportTimeDisplay => FormatNumber(AnalysisResult?.DoubleSupportTimeS ?? Record?.GaitParameters?.DoubleSupportTimeS ?? _detailData.MeanDoubleSupportTimeSec, "F2", "s");

    public string SingleSupportTimeDisplay => FormatNumber(AnalysisResult?.SingleSupportTimeS ?? Record?.GaitParameters?.SingleSupportTimeS ?? _detailData.MeanSingleSupportTimeSec, "F2", "s");

    public string LeftStrideLengthDisplay => Record?.GaitParameters?.StrideLengthLeft is double leftStride ? FormatMetersFromCentimeters(leftStride) : FormatNumber(_detailData.LeftStrideMeanM, "F2", "m");

    public string RightStrideLengthDisplay => Record?.GaitParameters?.StrideLengthRight is double rightStride ? FormatMetersFromCentimeters(rightStride) : FormatNumber(_detailData.RightStrideMeanM, "F2", "m");

    public string StrideLengthDiffDisplay => FormatNumber(Difference(_detailData.LeftStrideMeanM, _detailData.RightStrideMeanM), "F2", "m");

    public string StrideLengthDiffPercentDisplay => FormatNumber(DifferencePercent(_detailData.LeftStrideMeanM, _detailData.RightStrideMeanM), "F1", "%");

    public string LeftStancePhaseDisplay => FormatNumber(Record?.GaitParameters?.StancePhaseLeft ?? _detailData.LeftStanceRatioPct, "F1", "%");

    public string RightStancePhaseDisplay => FormatNumber(Record?.GaitParameters?.StancePhaseRight ?? _detailData.RightStanceRatioPct, "F1", "%");

    public string LeftSwingPhaseDisplay => FormatNumber(Record?.GaitParameters?.SwingPhaseLeft ?? ComplementPercent(_detailData.LeftStanceRatioPct), "F1", "%");

    public string RightSwingPhaseDisplay => FormatNumber(Record?.GaitParameters?.SwingPhaseRight ?? ComplementPercent(_detailData.RightStanceRatioPct), "F1", "%");

    public string LeftHeelStrikeCountDisplay => FormatCount(_detailData.LeftHeelStrikeCount);

    public string RightHeelStrikeCountDisplay => FormatCount(_detailData.RightHeelStrikeCount);

    public string LeftToeOffCountDisplay => FormatCount(_detailData.LeftToeOffCount);

    public string RightToeOffCountDisplay => FormatCount(_detailData.RightToeOffCount);

    public string HipRomDisplay => FormatNumber(AnalysisResult?.KinematicSummary?.HipRomDeg ?? Average(_detailData.LeftHipRomDeg, _detailData.RightHipRomDeg), "F1", "°");

    public string LeftHipRomDisplay => FormatNumber(_detailData.LeftHipRomDeg, "F1", "°");

    public string RightHipRomDisplay => FormatNumber(_detailData.RightHipRomDeg, "F1", "°");

    public string KneeRomDisplay => FormatNumber(AnalysisResult?.KinematicSummary?.KneeRomDeg ?? Average(_detailData.LeftKneeRomDeg, _detailData.RightKneeRomDeg), "F1", "°");

    public string LeftKneeRomDisplay => FormatNumber(_detailData.LeftKneeRomDeg, "F1", "°");

    public string RightKneeRomDisplay => FormatNumber(_detailData.RightKneeRomDeg, "F1", "°");

    public string AnkleRomDisplay => FormatNumber(AnalysisResult?.KinematicSummary?.AnkleRomDeg ?? Average(_detailData.LeftAnkleRomDeg, _detailData.RightAnkleRomDeg), "F1", "°");

    public string LeftAnkleRomDisplay => FormatNumber(_detailData.LeftAnkleRomDeg, "F1", "°");

    public string RightAnkleRomDisplay => FormatNumber(_detailData.RightAnkleRomDeg, "F1", "°");

    public string PelvisCoronalRomDisplay => FormatNumber(AnalysisResult?.KinematicSummary?.PelvisCoronalRomDeg ?? _detailData.PelvisRomDeg, "F1", "°");

    public string TrunkTiltMeanDisplay => FormatNumber(_detailData.TrunkTiltMeanDeg, "F1", "°");

    public string TrunkTiltMaxDisplay => FormatNumber(_detailData.TrunkTiltMaxDeg, "F1", "°");

    public string TrunkTiltMinDisplay => FormatNumber(_detailData.TrunkTiltMinDeg, "F1", "°");

    public string TrunkTiltRomDisplay => FormatNumber(_detailData.TrunkTiltRomDeg, "F1", "°");

    public string PelvicTiltMeanDisplay => FormatNumber(_detailData.PelvisTiltMeanDeg, "F1", "°");

    public string PelvicObliquityMeanDisplay => "--";

    public string StepLengthDiffDisplay => FormatNumber(Difference(_detailData.LeftStrideMeanM, _detailData.RightStrideMeanM), "F2", "m");

    public string StepLengthDiffPercentDisplay => FormatNumber(DifferencePercent(_detailData.LeftStrideMeanM, _detailData.RightStrideMeanM), "F1", "%");

    public string StanceTimeDiffPercentDisplay => FormatNumber(Difference(_detailData.LeftStanceRatioPct, _detailData.RightStanceRatioPct), "F1", "%");

    public string KneeRomDiffDisplay => FormatNumber(Difference(_detailData.LeftKneeRomDeg, _detailData.RightKneeRomDeg), "F1", "°");

    public string HipRomDiffDisplay => FormatNumber(Difference(_detailData.LeftHipRomDeg, _detailData.RightHipRomDeg), "F1", "°");

    public string AnkleRomDiffDisplay => FormatNumber(Difference(_detailData.LeftAnkleRomDeg, _detailData.RightAnkleRomDeg), "F1", "°");

    public string SymmetryScoreDisplay => FormatNumber(Record?.GaitParameters?.SymmetryIndex ?? CalculateSymmetryScore(), "F1", "");

    public string CurrentVideoFileDisplay => AnnotatedVideoDisplay;

    public string CurrentFrameDisplay => _angleFrames.Count > 0 ? _angleFrames[0].FrameIndex.ToString(CultureInfo.InvariantCulture) : "--";

    public string CurrentPlaybackTimeDisplay => _angleFrames.Count > 0 ? $"{_angleFrames[0].TimeS:F2} s" : "--";

    public string VideoDurationDisplay => _detailData.VideoDurationSec.HasValue ? $"{_detailData.VideoDurationSec.Value:F2} s" : "--";

    public string VideoDurationPlaybackDisplay => FormatPlaybackTime(_detailData.VideoDurationSec);

    public string CurrentGaitCycleDisplay => _detailData.CycleCount.HasValue
        ? L("AnalysisDetail.CurrentGaitCycleFormat", _detailData.CycleCount.Value)
        : "--";

    public string CurrentEventDisplay => "--";

    public string CurrentLeftKneeAngleDisplay => _angleFrames.Count > 0 ? $"{_angleFrames[0].LeftKnee:F1} °" : "--";

    public string CurrentRightKneeAngleDisplay => _angleFrames.Count > 0 ? $"{_angleFrames[0].RightKnee:F1} °" : "--";

    public string CurrentLeftAnkleAngleDisplay => _angleFrames.Count > 0 ? $"{_angleFrames[0].LeftAnkle:F1} °" : "--";

    public string CurrentRightAnkleAngleDisplay => _angleFrames.Count > 0 ? $"{_angleFrames[0].RightAnkle:F1} °" : "--";

    /// <summary>
    /// 报告编号摘要。
    /// </summary>
    public string ReportNumberDisplay => string.IsNullOrWhiteSpace(CurrentReportDraft?.ReportNumber)
        ? "--"
        : CurrentReportDraft.ReportNumber;

    /// <summary>
    /// 报告分析源摘要。
    /// </summary>
    public string ReportAnalysisSourceDisplay => AnalysisResult is null
        ? "--"
        : $"AnalysisResult #{AnalysisResult.Id} / {TaskStatusDisplay}";

    /// <summary>
    /// 报告操作员。
    /// </summary>
    public string ReportOperatorDisplay => _sessionService.CurrentUser?.Name ?? Record?.Operator?.Name ?? "--";

    /// <summary>
    /// 报告草稿更新时间。
    /// </summary>
    public string ReportDraftUpdatedAtDisplay => CurrentReportDraft?.UpdatedAt.ToString(Constants.DATETIME_FORMAT) ?? "--";

    /// <summary>
    /// 报告配置质量提示。
    /// </summary>
    public string ReportQualityHint
    {
        get
        {
            if (AnalysisResult is null)
            {
                return L("AnalysisDetail.ReportConfig.Quality.NoResult");
            }

            return ValidFrameRatioDisplay == "--"
                ? L("AnalysisDetail.ReportConfig.Quality.NoValidRatio")
                : L("AnalysisDetail.ReportConfig.Quality.ValidRatioFormat", ValidFrameRatioDisplay);
        }
    }

    /// <summary>
    /// 报告配置摘要。
    /// </summary>
    public string ReportIncludedSectionsSummary
    {
        get
        {
            var sections = new List<string>();
            if (IncludeSpatiotemporalParameters)
            {
                sections.Add(L("AnalysisDetail.ReportPreview.Sections.Spatiotemporal"));
            }

            if (IncludeKinematicSummary)
            {
                sections.Add(L("AnalysisDetail.ReportPreview.Sections.KinematicSummary"));
            }

            if (IncludeQualityControl)
            {
                sections.Add(L("AnalysisDetail.ReportPreview.Sections.QualityControl"));
            }

            if (IncludeResultFiles)
            {
                sections.Add(L("AnalysisDetail.ReportPreview.Sections.ResultFiles"));
            }

            return sections.Count > 0
                ? string.Join(L("AnalysisDetail.ReportPreview.Sections.Separator"), sections)
                : L("AnalysisDetail.ReportConfig.Sections.None");
        }
    }

    partial void OnSelectedNavigationItemChanged(AnalysisDetailNavigationItem? value)
    {
        if (value?.Key is "report" && !IsReportConfigLoading && CurrentReportDraft is null)
        {
            _ = LoadReportConfigAsync(forceReload: false);
        }

        OnPropertyChanged(nameof(CurrentSectionTitle));
        OnPropertyChanged(nameof(CurrentSectionDescription));
        OnPropertyChanged(nameof(IsOverviewSectionSelected));
        OnPropertyChanged(nameof(IsSpatiotemporalSectionSelected));
        OnPropertyChanged(nameof(IsKinematicsSectionSelected));
        OnPropertyChanged(nameof(IsQualitySectionSelected));
        OnPropertyChanged(nameof(IsFilesSectionSelected));
        OnPropertyChanged(nameof(IsReportSectionSelected));
    }

    /// <summary>
    /// 当前模块标题。
    /// </summary>
    public string CurrentSectionTitle => SelectedNavigationItem?.Title ?? L("AnalysisDetail.Navigation.Overview");

    /// <summary>
    /// 当前模块说明。
    /// </summary>
    public string CurrentSectionDescription => SelectedNavigationItem?.Description ?? L("AnalysisDetail.Navigation.DefaultDesc");

    /// <summary>
    /// 导航模块总数。
    /// </summary>
    public int NavigationSectionCount => NavigationItems.Count;

    /// <summary>
    /// 当前模块序号。
    /// </summary>
    public int CurrentSectionIndex
    {
        get
        {
            if (SelectedNavigationItem is null)
            {
                return 0;
            }

            var index = NavigationItems.IndexOf(SelectedNavigationItem);
            return index >= 0 ? index + 1 : 0;
        }
    }

    /// <summary>
    /// 导航进度条当前值。
    /// </summary>
    public double NavigationProgressValue => CurrentSectionIndex;

    /// <summary>
    /// 导航进度条最大值。
    /// </summary>
    public double NavigationProgressMaximum => Math.Max(NavigationSectionCount, 1);

    /// <summary>
    /// 导航进度说明。
    /// </summary>
    public string NavigationProgressText => CurrentSectionIndex > 0
        ? L("AnalysisDetail.Navigation.ProgressFormat", CurrentSectionIndex, NavigationSectionCount)
        : L("AnalysisDetail.Navigation.Empty");

    /// <summary>
    /// 关闭命令。
    /// </summary>
    [RelayCommand]
    private async Task CloseAsync()
    {
        await PersistDraftSnapshotAsync();
        CloseRequested?.Invoke();
    }

    /// <summary>
    /// 导出命令。
    /// </summary>
    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Record is null || !CanExport)
        {
            return;
        }

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = L("AnalysisDetail.Export.PackageTitle"),
                Filter = L("AnalysisDetail.Export.PackageFilter"),
                FileName = L("AnalysisDetail.Export.PackageFileNameFormat", Record.Patient?.Name ?? "Patient", Record.MeasurementDate)
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var exportRecord = Record;
            var result = await Task.Run(() => _exportImportService.ExportMeasurementArchiveAsync(
                [exportRecord],
                dialog.FileName,
                progress: null,
                cancellationToken: CancellationToken.None)).ConfigureAwait(true);

            if (result.Success)
            {
                System.Windows.MessageBox.Show(result.Message, L("Tip"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                _logHelper?.Information($"导出测量结果包：MeasurementId={Record.Id}, 文件={dialog.FileName}");
                return;
            }

            System.Windows.MessageBox.Show(result.Message, L("Error"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"导出测量结果包失败：MeasurementId={Record.Id}", ex);
            System.Windows.MessageBox.Show(L("AnalysisDetail.Export.FailedFormat", ex.Message), L("Error"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 加载报告配置命令。
    /// </summary>
    [RelayCommand]
    private Task LoadReportConfigAsync(bool forceReload = true)
    {
        return EnsureReportDraftAsync(forceReload);
    }

    private async Task EnsureReportDraftAsync(bool forceReload)
    {
        if (Record is null)
        {
            return;
        }

        if (!CanConfigureReport)
        {
            ReportConfigTitle = L("AnalysisDetail.ReportConfig.UnavailableTitle");
            ReportConfigMessage = AnalysisResult is null
                ? L("AnalysisDetail.ReportConfig.NeedSuccessResult")
                : L("AnalysisDetail.ReportConfig.NoPermission");
            ReportPreviewMessage = AnalysisResult is null
                ? L("AnalysisDetail.ReportConfig.NoPreviewResult")
                : L("AnalysisDetail.ReportConfig.NoPreviewPermission");
            return;
        }

        if (!forceReload && CurrentReportDraft is not null)
        {
            return;
        }

        IsReportConfigLoading = true;
        try
        {
            var report = await _reportService.GetOrCreateDraftReportAsync(Record.Id, _sessionService.CurrentUser?.Id ?? Record.OperatorId);
            if (report is null)
            {
                ReportConfigTitle = L("AnalysisDetail.ReportConfig.InitFailedTitle");
                ReportConfigMessage = L("AnalysisDetail.ReportConfig.InitFailedMessage");
                ReportPreviewMessage = L("AnalysisDetail.ReportConfig.DraftNotReady");
                return;
            }

            CurrentReportDraft = report;
            ApplyDraftToReportConfig(report);
            await PersistDraftSnapshotAsync();
            ReportConfigTitle = L("AnalysisDetail.ReportConfig");
            ReportConfigMessage = L("AnalysisDetail.ReportConfig.LoadedMessage");
            ReportPreviewMessage = L("AnalysisDetail.ReportConfig.PreviewReady");
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"加载报告配置失败：MeasurementId={Record.Id}", ex);
            ReportConfigTitle = L("AnalysisDetail.ReportConfig.LoadFailedTitle");
            ReportConfigMessage = L("AnalysisDetail.ReportConfig.LoadFailedMessage", ex.Message);
            ReportPreviewMessage = L("AnalysisDetail.ReportConfig.PreviewInitFailed");
        }
        finally
        {
            IsReportConfigLoading = false;
            NotifyComputedPropertiesChanged();
        }
    }

    private void ApplyDraftToReportConfig(Report report)
    {
        var options = ParseReportOptions(report.ReportOptionsJson);
        var hasPersistedOptions = options is not null;

        _isApplyingDraftConfig = true;
        try
        {
            ReportTitle = string.IsNullOrWhiteSpace(report.Title)
                ? L("AnalysisDetail.ReportConfig.DefaultReportTitleFormat", PatientName)
                : report.Title;
            ReportDoctorOpinion = report.DoctorOpinion ?? string.Empty;
            IncludeSpatiotemporalParameters = options?.IncludeSpatiotemporalParameters ?? true;
            IncludeKinematicSummary = options?.IncludeKinematicSummary ?? true;
            IncludeQualityControl = options?.IncludeQualityControl ?? true;
            IncludeResultFiles = options?.IncludeResultFiles ?? false;
        }
        finally
        {
            _isApplyingDraftConfig = false;
        }

        var shouldPersistDefaults = !hasPersistedOptions
            || string.IsNullOrWhiteSpace(report.Title)
            || report.AnalysisResultId != AnalysisResult?.Id;

        SyncDraftOptionsToModel(markDirty: shouldPersistDefaults);
    }

    private void SyncDraftOptionsToModel(bool markDirty)
    {
        if (CurrentReportDraft is null || _isApplyingDraftConfig)
        {
            return;
        }

        CurrentReportDraft.Title = ReportTitle;
        CurrentReportDraft.DoctorOpinion = ReportDoctorOpinion;
        CurrentReportDraft.AnalysisResultId = AnalysisResult?.Id;
        CurrentReportDraft.ReportOptionsJson = JsonSerializer.Serialize(new ReportDraftOptions(
            IncludeSpatiotemporalParameters,
            IncludeKinematicSummary,
            IncludeQualityControl,
            IncludeResultFiles));
        CurrentReportDraft.UpdatedAt = DateTime.Now;
        CurrentReportDraft.AnalysisResult = AnalysisResult;
        CurrentReportDraft.KinematicSummary = AnalysisResult?.KinematicSummary;
        CurrentReportDraft.QualityControl = AnalysisResult?.QualityControl;
        CurrentReportDraft.MeasurementRecord = Record;
        CurrentReportDraft.Patient = Record?.Patient;

        if (markDirty)
        {
            _hasPendingDraftSnapshotChanges = true;
        }

        ReportPreviewMessage = CanPreviewReport
            ? L("AnalysisDetail.ReportConfig.SyncReady")
            : L("AnalysisDetail.ReportConfig.TitleRequired");

        OnPropertyChanged(nameof(ReportNumberDisplay));
        OnPropertyChanged(nameof(ReportDraftUpdatedAtDisplay));
        OnPropertyChanged(nameof(ReportIncludedSectionsSummary));
        OnPropertyChanged(nameof(CanPreviewReport));
    }

    private async Task PersistDraftSnapshotAsync()
    {
        if (!_hasPendingDraftSnapshotChanges || CurrentReportDraft is null)
        {
            return;
        }

        var success = await _reportService.SaveDraftSnapshotAsync(CurrentReportDraft);
        if (success)
        {
            _hasPendingDraftSnapshotChanges = false;
            OnPropertyChanged(nameof(ReportDraftUpdatedAtDisplay));
            return;
        }

        ReportConfigMessage = L("AnalysisDetail.ReportConfig.SaveFailed");
        ReportPreviewMessage = L("AnalysisDetail.ReportConfig.SavePartialFailed");
    }

    private void ResetReportConfigState()
    {
        ReportConfigTitle = L("AnalysisDetail.ReportConfig");
        ReportConfigMessage = L("AnalysisDetail.ReportConfig.Message.Default");
        ReportPreviewMessage = L("AnalysisDetail.ReportConfig.PreviewMessage.Default");
        ReportTitle = string.Empty;
        ReportDoctorOpinion = string.Empty;
        IncludeSpatiotemporalParameters = true;
        IncludeKinematicSummary = true;
        IncludeQualityControl = true;
        IncludeResultFiles = false;
        IsReportConfigLoading = false;
        IsPreparingReportPreview = false;
        _hasPendingDraftSnapshotChanges = false;
        _isApplyingDraftConfig = false;
    }

    /// <summary>
    /// 打开报告预览命令。
    /// </summary>
    [RelayCommand]
    private async Task OpenReportPreviewAsync()
    {
        if (!CanConfigureReport)
        {
            ReportPreviewMessage = L("AnalysisDetail.ReportConfig.PreviewUnavailable");
            return;
        }

        if (CurrentReportDraft is null)
        {
            await EnsureReportDraftAsync(forceReload: false);
        }

        if (CurrentReportDraft is null)
        {
            ReportPreviewMessage = L("AnalysisDetail.ReportConfig.DraftNotReady");
            return;
        }

        if (string.IsNullOrWhiteSpace(ReportTitle))
        {
            ReportPreviewMessage = L("AnalysisDetail.ReportConfig.FillTitleFirst");
            return;
        }

        try
        {
            IsPreparingReportPreview = true;
            SyncDraftOptionsToModel(markDirty: true);
            await PersistDraftSnapshotAsync();

            var previewViewModel = App.Services.GetRequiredService<ReportPreviewDialogViewModel>();
            await previewViewModel.InitializeAsync(CurrentReportDraft, BuildReportPreviewDocument(CurrentReportDraft));

            CloseRequested?.Invoke();
            await Task.Delay(180);

            var previewResult = await DialogHost.Show(
                new Views.Dialogs.ReportPreviewDialog
                {
                    DataContext = previewViewModel
                },
                "RootDialog").ConfigureAwait(true);

            ReportPreviewMessage = previewResult is ReportPreviewDialogResult.BackToConfig
                ? L("AnalysisDetail.ReportConfig.BackFromPreview")
                : L("AnalysisDetail.ReportConfig.ClosedPreview");
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"打开报告预览失败：MeasurementId={Record?.Id}", ex);
            ReportPreviewMessage = L("AnalysisDetail.ReportConfig.OpenPreviewFailed", ex.Message);
        }
        finally
        {
            IsPreparingReportPreview = false;
            NotifyComputedPropertiesChanged();
        }
    }

    private FlowDocument BuildReportPreviewDocument(Report report)
    {
        ArgumentNullException.ThrowIfNull(report);

        report.AnalysisResult ??= AnalysisResult;
        report.KinematicSummary ??= AnalysisResult?.KinematicSummary;
        report.QualityControl ??= AnalysisResult?.QualityControl;
        report.MeasurementRecord ??= Record;
        report.Patient ??= Record?.Patient;

        var settingsService = App.Services?.GetService(typeof(ISettingsService)) as ISettingsService;
        var unitName = settingsService?.CurrentSettings?.Unit?.Name ?? Constants.APP_DISPLAY_NAME;
        var logoPath = settingsService?.CurrentSettings?.Unit?.LogoPath;
        return ReportPreviewHelper.GenerateReportDocument(report, unitName, logoPath);
    }

    private static ReportDraftOptions? ParseReportOptions(string? reportOptionsJson)
    {
        if (string.IsNullOrWhiteSpace(reportOptionsJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ReportDraftOptions>(reportOptionsJson);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 打开结果目录命令。
    /// </summary>
    [RelayCommand]
    private void OpenOutputDirectory()
    {
        var path = ResolveOutputDirectory();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            System.Windows.MessageBox.Show(L("AnalysisDetail.OutputDirectory.Empty"), L("Tip"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"打开结果目录失败：Path={path}", ex);
            System.Windows.MessageBox.Show(L("AnalysisDetail.OutputDirectory.OpenFailedFormat", ex.Message), L("Error"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void SetSuccessState()
    {
        DetailState = AnalysisDetailState.Success;
        StateTitle = L("AnalysisDetail.State.SuccessTitle");
        StateMessage = L("AnalysisDetail.State.SuccessMessage");
    }

    private void SetFailedState(string? message = null)
    {
        DetailState = AnalysisDetailState.Failed;
        StateTitle = L("AnalysisDetail.State.FailedTitle");
        StateMessage = string.IsNullOrWhiteSpace(message)
            ? L("AnalysisDetail.State.FailedMessage")
            : message;
    }

    private void SetEmptyState(string title, string message, bool isFailed)
    {
        DetailState = isFailed ? AnalysisDetailState.Failed : AnalysisDetailState.Empty;
        StateTitle = title;
        StateMessage = message;
    }

    private static bool HasAnalysisFailure(MeasurementRecord record)
    {
        return record.CurrentAnalysisStage != AnalysisStage.None
            && !record.KinematicsCompleted
            && (record.KeypointsCompleted || record.EventsCompleted || record.Status == MeasurementStatus.Failed);
    }

    private void LoadAnalysisDetailFiles(AnalysisResult result)
    {
        _detailData = new AnalysisDetailData();
        _angleFrames = [];
        _isSingleViewOutput = IsSingleViewOutput(result.OutputDirectory);
        _annotatedVideoPath = ResolveAnnotatedVideoPath();
        CycleDetails.Clear();

        try
        {
            var resultPath = ResolveResultJsonPath(result);
            if (!string.IsNullOrWhiteSpace(resultPath) && File.Exists(resultPath))
            {
                LoadResultJson(resultPath);
            }

            ApplyPreferredVideoMetadata();
            _annotatedVideoPath = ResolveAnnotatedVideoPath();

            _detailData.ResultFileCount = CountResultFiles(result.OutputDirectory);
            _detailData.CsvFileCount = CountFiles(result.OutputDirectory, "*.csv");
            _detailData.ImageFileCount = CountFiles(result.OutputDirectory, "*.png") + CountFiles(result.OutputDirectory, "*.jpg") + CountFiles(result.OutputDirectory, "*.jpeg");
            _detailData.LogFileName = ResolveLogFileName(result.OutputDirectory);
            _detailData.ResultFileSummary = BuildResultFileSummary(result.OutputDirectory);

            var jointAngleCsv = ResolveJointAngleCsvPath(result);
            if (!string.IsNullOrWhiteSpace(jointAngleCsv) && File.Exists(jointAngleCsv))
            {
                _angleFrames = ParseAngleCsv(jointAngleCsv, _detailData.VideoFps ?? 30d, _detailData.VideoDurationSec);
            }

            BuildRealPlotModels();
        }
        catch (Exception ex)
        {
            _logHelper?.Warning($"加载分析详情真实数据失败: {ex.Message}");
            BuildRealPlotModels();
        }
    }

    private void LoadResultJson(string resultPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(resultPath))?.AsObject();
        if (root is null)
        {
            return;
        }

        var videoInfo = root["video_info"] as JsonObject;
        var gaitCycle = root["gait_cycle"] as JsonObject;
        var spatiotemporal = root["spatiotemporal_parameters"] as JsonObject;
        var gaitEvents = root["gait_events"] as JsonObject;
        var jointAngles = root["joint_angles"] as JsonObject;
        var segmentAngles = root["segment_angles"] as JsonObject;

        _detailData.VideoFps = ReadDouble(videoInfo, "fps");
        var reportedDuration = ReadDouble(videoInfo, "duration_sec");
        var frameCount = ReadInt(videoInfo, "frame_count");
        double? frameDuration = _detailData.VideoFps is > 0 && frameCount is > 0
            ? frameCount.Value / _detailData.VideoFps.Value
            : null;
        _detailData.VideoDurationSec = NormalizeVideoDuration(reportedDuration, frameDuration);
        _detailData.CycleCount = ReadInt(gaitCycle, "cycle_count") ?? ReadInt(gaitCycle, "total_cycle_count");
        _detailData.MeanCycleDurationSec = AverageCycleDuration(gaitCycle);
        ApplyPreferredVideoMetadata();
        LoadCycleDetails(gaitCycle, gaitEvents, _detailData.VideoFps ?? 30d);
        var phaseMetrics = GaitPhaseMetricsCalculator.Calculate(gaitCycle);
        var eventPhaseMetrics = GaitPhaseMetricsCalculator.CalculateFromEvents(gaitEvents, _detailData.VideoFps);
        _detailData.CadenceStepPerMin = ReadDouble(spatiotemporal, "cadence_step_per_min");
        _detailData.GaitSpeedMPerS = ReadDouble(spatiotemporal, "gait_velocity_m_per_sec");
        _detailData.MeanStepLengthM = ReadDouble(spatiotemporal, "mean_step_length_m");
        _detailData.MeanStrideLengthM = ReadDouble(spatiotemporal, "mean_stride_length_m");
        _detailData.MeanStanceTimeSec = ReadDouble(spatiotemporal, "mean_stance_time_sec") ?? phaseMetrics.MeanStanceTimeSec ?? eventPhaseMetrics.MeanStanceTimeSec;
        _detailData.MeanSwingTimeSec = ReadDouble(spatiotemporal, "mean_swing_time_sec") ?? phaseMetrics.MeanSwingTimeSec ?? eventPhaseMetrics.MeanSwingTimeSec;
        _detailData.MeanDoubleSupportTimeSec = ReadDouble(spatiotemporal, "mean_double_support_time_sec") ?? phaseMetrics.MeanDoubleSupportTimeSec;
        _detailData.MeanSingleSupportTimeSec = ReadDouble(spatiotemporal, "mean_single_support_time_sec") ?? phaseMetrics.MeanSingleSupportTimeSec;

        _detailData.LeftHeelStrikeCount = ReadArrayCount(gaitEvents, "left_heel_strike_frames");
        _detailData.RightHeelStrikeCount = ReadArrayCount(gaitEvents, "right_heel_strike_frames");
        _detailData.LeftToeOffCount = ReadArrayCount(gaitEvents, "left_toe_off_frames");
        _detailData.RightToeOffCount = ReadArrayCount(gaitEvents, "right_toe_off_frames");

        _detailData.LeftHipRomDeg = ReadJointRom(jointAngles, "left_hip", "left hip");
        _detailData.RightHipRomDeg = ReadJointRom(jointAngles, "right_hip", "right hip");
        _detailData.LeftKneeRomDeg = ReadJointRom(jointAngles, "left_knee", "left knee");
        _detailData.RightKneeRomDeg = ReadJointRom(jointAngles, "right_knee", "right knee");
        _detailData.LeftAnkleRomDeg = ReadJointRom(jointAngles, "left_ankle", "left ankle");
        _detailData.RightAnkleRomDeg = ReadJointRom(jointAngles, "right_ankle", "right ankle");

        var trunk = segmentAngles?["trunk_tilt_deg"] as JsonObject;
        _detailData.TrunkTiltMeanDeg = ReadDouble(trunk, "mean");
        _detailData.TrunkTiltMaxDeg = ReadDouble(trunk, "max");
        _detailData.TrunkTiltMinDeg = ReadDouble(trunk, "min");
        _detailData.TrunkTiltRomDeg = ReadDouble(trunk, "rom")
            ?? Difference(_detailData.TrunkTiltMaxDeg, _detailData.TrunkTiltMinDeg);

        var pelvis = segmentAngles?["pelvis_tilt_deg"] as JsonObject;
        _detailData.PelvisTiltMeanDeg = ReadDouble(pelvis, "mean");
        _detailData.PelvisTiltMaxDeg = ReadDouble(pelvis, "max");
        _detailData.PelvisTiltMinDeg = ReadDouble(pelvis, "min");
        _detailData.PelvisRomDeg = ReadDouble(pelvis, "rom")
            ?? Difference(_detailData.PelvisTiltMaxDeg, _detailData.PelvisTiltMinDeg);

        _detailData.LeftStrideMeanM = ReadDouble(root, "left_stride_mean_m");
        _detailData.RightStrideMeanM = ReadDouble(root, "right_stride_mean_m");
        _detailData.LeftStanceRatioPct = ReadDouble(root, "left_stance_ratio_pct") ?? eventPhaseMetrics.LeftStanceRatioPct;
        _detailData.RightStanceRatioPct = ReadDouble(root, "right_stance_ratio_pct") ?? eventPhaseMetrics.RightStanceRatioPct;
        _detailData.ValidFrameRatio = AnalysisFrameCoverageHelper.FromResultJson(root, AnalysisResult?.OutputDirectory)?.Ratio
            ?? ReadDouble(root["quality_control"] as JsonObject, "valid_frame_ratio");
    }

    private void BuildRealPlotModels()
    {
        if (_angleFrames.Count == 0)
        {
            LeftHipAnglePlotModel = CreateEmptyPlot(L("AnalysisDetail.Chart.LeftHipTitle"));
            RightHipAnglePlotModel = CreateEmptyPlot(L("AnalysisDetail.Chart.RightHipTitle"));
            LeftKneeAnglePlotModel = CreateEmptyPlot(L("AnalysisDetail.Chart.LeftKneeTitle"));
            RightKneeAnglePlotModel = CreateEmptyPlot(L("AnalysisDetail.Chart.RightKneeTitle"));
            LeftAnkleAnglePlotModel = CreateEmptyPlot(L("AnalysisDetail.Chart.LeftAnkleTitle"));
            RightAnkleAnglePlotModel = CreateEmptyPlot(L("AnalysisDetail.Chart.RightAnkleTitle"));
            PelvisAnglePlotModel = CreateEmptyPlot(L("AnalysisDetail.Chart.PelvisTitle"));
            TrunkAnglePlotModel = CreateEmptyPlot(L("AnalysisDetail.Chart.TrunkTitle"));
            VideoKneeAnglePlotModel = CreateEmptyPlot(L("AnalysisDetail.Chart.KneeTitle"), alignToPlaybackBar: true);
            VideoHipAnglePlotModel = CreateEmptyPlot(L("AnalysisDetail.Chart.HipTitle"), alignToPlaybackBar: true);
            VideoAnkleAnglePlotModel = CreateEmptyPlot(L("AnalysisDetail.Chart.AnkleTitle"), alignToPlaybackBar: true);
            VideoPelvisAnglePlotModel = CreateEmptyPlot(L("AnalysisDetail.Chart.PelvisTitle"), alignToPlaybackBar: true);
            VideoTrunkAnglePlotModel = CreateEmptyPlot(L("AnalysisDetail.Chart.TrunkTitle"), alignToPlaybackBar: true);
        }
        else
        {
            var maxTime = Math.Max(_detailData.VideoDurationSec ?? 0d, _angleFrames[^1].TimeS);
            LeftHipAnglePlotModel = BuildSingleAnglePlot(L("AnalysisDetail.Chart.LeftHipTitle"), L("AnalysisDetail.LeftHip"), _angleFrames, f => f.LeftHip, OxyColors.SteelBlue, maxTime);
            RightHipAnglePlotModel = BuildSingleAnglePlot(L("AnalysisDetail.Chart.RightHipTitle"), L("AnalysisDetail.RightHip"), _angleFrames, f => f.RightHip, OxyColor.Parse("#F2306A"), maxTime);
            LeftKneeAnglePlotModel = BuildSingleAnglePlot(L("AnalysisDetail.Chart.LeftKneeTitle"), L("AnalysisDetail.LeftKnee"), _angleFrames, f => f.LeftKnee, OxyColors.ForestGreen, maxTime);
            RightKneeAnglePlotModel = BuildSingleAnglePlot(L("AnalysisDetail.Chart.RightKneeTitle"), L("AnalysisDetail.RightKnee"), _angleFrames, f => f.RightKnee, OxyColors.OrangeRed, maxTime);
            LeftAnkleAnglePlotModel = BuildSingleAnglePlot(L("AnalysisDetail.Chart.LeftAnkleTitle"), L("AnalysisDetail.LeftAnkle"), _angleFrames, f => f.LeftAnkle, OxyColors.MediumPurple, maxTime);
            RightAnkleAnglePlotModel = BuildSingleAnglePlot(L("AnalysisDetail.Chart.RightAnkleTitle"), L("AnalysisDetail.RightAnkle"), _angleFrames, f => f.RightAnkle, OxyColors.DarkCyan, maxTime);
            PelvisAnglePlotModel = BuildSingleAnglePlot(L("AnalysisDetail.Chart.PelvisTitle"), L("AnalysisDetail.Pelvis"), _angleFrames, f => f.Pelvis, OxyColor.Parse("#40385F"), maxTime);
            TrunkAnglePlotModel = BuildSingleAnglePlot(L("AnalysisDetail.Chart.TrunkTitle"), L("AnalysisDetail.Trunk"), _angleFrames, f => f.Trunk, OxyColor.Parse("#F2306A"), maxTime);
            VideoKneeAnglePlotModel = BuildDualAnglePlot(L("AnalysisDetail.Chart.KneeTitle"), L("AnalysisDetail.LeftKnee"), L("AnalysisDetail.RightKnee"), _angleFrames, f => f.LeftKnee, f => f.RightKnee, OxyColors.ForestGreen, OxyColor.Parse("#F2306A"), maxTime);
            VideoHipAnglePlotModel = BuildDualAnglePlot(L("AnalysisDetail.Chart.HipTitle"), L("AnalysisDetail.LeftHip"), L("AnalysisDetail.RightHip"), _angleFrames, f => f.LeftHip, f => f.RightHip, OxyColors.SteelBlue, OxyColors.OrangeRed, maxTime);
            VideoAnkleAnglePlotModel = BuildDualAnglePlot(L("AnalysisDetail.Chart.AnkleTitle"), L("AnalysisDetail.LeftAnkle"), L("AnalysisDetail.RightAnkle"), _angleFrames, f => f.LeftAnkle, f => f.RightAnkle, OxyColors.MediumPurple, OxyColors.DarkCyan, maxTime);
            VideoPelvisAnglePlotModel = BuildSinglePlaybackAnglePlot(L("AnalysisDetail.Chart.PelvisTitle"), L("AnalysisDetail.Pelvis"), _angleFrames, f => f.Pelvis, OxyColor.Parse("#40385F"), maxTime);
            VideoTrunkAnglePlotModel = BuildSinglePlaybackAnglePlot(L("AnalysisDetail.Chart.TrunkTitle"), L("AnalysisDetail.Trunk"), _angleFrames, f => f.Trunk, OxyColor.Parse("#F2306A"), maxTime);
        }

        SetVideoPlaybackTime(_videoPlaybackSeconds);
        NotifyPlotModelsChanged();
    }


    private string? ResolveOutputDirectory()
    {
        if (!string.IsNullOrWhiteSpace(AnalysisResult?.OutputDirectory))
        {
            return AnalysisResult.OutputDirectory;
        }

        if (string.IsNullOrWhiteSpace(Record?.MeasurementFolderPath))
        {
            return null;
        }

        return Path.IsPathRooted(Record.MeasurementFolderPath)
            ? Record.MeasurementFolderPath
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Record.MeasurementFolderPath);
    }

    private string? ResolveAnnotatedVideoPath()
    {
        var outputDirectory = AnalysisResult?.OutputDirectory;
        if (!string.IsNullOrWhiteSpace(outputDirectory)
            && Directory.Exists(outputDirectory)
            && !_isSingleViewOutput)
        {
            var previewPath = GetAnalysisPreviewPath(outputDirectory);
            if (File.Exists(previewPath))
            {
                return previewPath;
            }

            return null;
        }

        var path = AnalysisResult?.AnnotatedVideoPath;
        if (!string.IsNullOrWhiteSpace(path)
            && File.Exists(path)
            && !(_isSingleViewOutput && Path.GetFileName(path).Equals("analysis_preview.mp4", StringComparison.OrdinalIgnoreCase)))
        {
            return path;
        }

        if (!_isSingleViewOutput || !Directory.Exists(outputDirectory))
        {
            return path;
        }

        return Directory.GetFiles(outputDirectory, "*.mp4", SearchOption.AllDirectories)
            .Where(file => Path.GetFileName(file).Contains("Sports2D", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(file => file.Contains("side", StringComparison.OrdinalIgnoreCase) || file.Contains("侧面", StringComparison.OrdinalIgnoreCase))
            ?? Directory.GetFiles(outputDirectory, "*.mp4", SearchOption.AllDirectories)
                .FirstOrDefault(file => Path.GetFileName(file).Contains("Sports2D", StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureAnalysisPreviewGenerationStarted()
    {
        var outputDirectory = AnalysisResult?.OutputDirectory;
        if (string.IsNullOrWhiteSpace(outputDirectory)
            || !Directory.Exists(outputDirectory)
            || _isSingleViewOutput
            || File.Exists(GetAnalysisPreviewPath(outputDirectory))
            || _analysisPreviewGenerationRequested)
        {
            _annotatedVideoPath = ResolveAnnotatedVideoPath();
            UpdateAnalysisPreviewStatus();
            return;
        }

        _analysisPreviewGenerationRequested = true;
        IsAnalysisPreviewGenerating = true;
        AnalysisPreviewStatusText = L("AnalysisDetail.PreviewGenerating");
        _ = _gaitAnalysisService.EnsureAnalysisPreviewVideoAsync(outputDirectory).ContinueWith(task =>
        {
            var previewPath = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
            var failed = string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath);
            void UpdateUi()
            {
                IsAnalysisPreviewGenerating = false;
                _annotatedVideoPath = failed ? ResolveAnnotatedVideoPath() : previewPath;
                AnalysisPreviewStatusText = failed
                    ? L("AnalysisDetail.PreviewGenerateFailed")
                    : string.Empty;
                OnPropertyChanged(nameof(AnnotatedVideoDisplay));
                OnPropertyChanged(nameof(HasAnnotatedVideo));
                OnPropertyChanged(nameof(AnnotatedVideoUri));
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is { HasShutdownStarted: false })
            {
                dispatcher.BeginInvoke(UpdateUi);
            }
        }, TaskScheduler.Default);
    }

    public void EnsureAnalysisPreviewReady()
    {
        EnsureAnalysisPreviewGenerationStarted();
    }

    private void UpdateAnalysisPreviewStatus()
    {
        var outputDirectory = AnalysisResult?.OutputDirectory;
        if (string.IsNullOrWhiteSpace(outputDirectory) || _isSingleViewOutput)
        {
            IsAnalysisPreviewGenerating = false;
            AnalysisPreviewStatusText = string.Empty;
            return;
        }

        var previewPath = GetAnalysisPreviewPath(outputDirectory);
        if (File.Exists(previewPath))
        {
            IsAnalysisPreviewGenerating = false;
            _annotatedVideoPath = previewPath;
            AnalysisPreviewStatusText = string.Empty;
            OnPropertyChanged(nameof(AnnotatedVideoDisplay));
            OnPropertyChanged(nameof(HasAnnotatedVideo));
            OnPropertyChanged(nameof(AnnotatedVideoUri));
            return;
        }

        var generatingPath = Path.Combine(outputDirectory, "preview", "analysis_preview.generating");
        IsAnalysisPreviewGenerating = File.Exists(generatingPath);
        AnalysisPreviewStatusText = IsAnalysisPreviewGenerating
            ? L("AnalysisDetail.PreviewGenerating")
            : HasAnalysisPreviewFailedMarker()
                ? L("AnalysisDetail.PreviewGenerateFailed")
                : L("AnalysisDetail.PreviewGeneratePending");
        OnPropertyChanged(nameof(AnnotatedVideoDisplay));
    }

    private static string GetAnalysisPreviewPath(string outputDirectory)
    {
        return Path.Combine(outputDirectory, "preview", "analysis_preview.mp4");
    }

    private bool HasAnalysisPreviewFailedMarker()
    {
        var outputDirectory = AnalysisResult?.OutputDirectory;
        return !string.IsNullOrWhiteSpace(outputDirectory)
               && File.Exists(Path.Combine(outputDirectory, "preview", "analysis_preview.failed"));
    }

    private static bool IsSingleViewOutput(string? outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            return false;
        }

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

    private static string? ResolveJointAngleCsvPath(AnalysisResult result)
    {
        var csvPath = result.CsvFiles?.FirstOrDefault(file =>
            file.FileType == (int)CsvFileType.JointAngle && File.Exists(file.FilePath))?.FilePath;
        if (!string.IsNullOrWhiteSpace(csvPath))
        {
            return csvPath;
        }

        return Directory.Exists(result.OutputDirectory)
            ? Directory.GetFiles(result.OutputDirectory, "joint_angle.csv", SearchOption.AllDirectories).FirstOrDefault()
            : null;
    }

    private static List<AnalysisAngleFrame> ParseAngleCsv(string path, double fps, double? videoDurationSec)
    {
        var frames = new List<AnalysisAngleFrame>();
        var lines = File.ReadLines(path).Skip(1);
        foreach (var line in lines)
        {
            var parts = line.Split(',');
            if (parts.Length < 7 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var frameIndex))
            {
                continue;
            }

            var computedTime = fps > 0 ? frameIndex / fps : frames.Count / 30d;
            var csvTime = ParseCsvNullableDouble(parts, 9);
            var time = csvTime is >= 0
                       && (videoDurationSec is not > 0 || csvTime.Value <= videoDurationSec.Value + 0.5d)
                ? csvTime.Value
                : computedTime;

            frames.Add(new AnalysisAngleFrame(
                frameIndex,
                time,
                ParseCsvDouble(parts, 1),
                ParseCsvDouble(parts, 2),
                ParseCsvDouble(parts, 3),
                ParseCsvDouble(parts, 4),
                ParseCsvDouble(parts, 5),
                ParseCsvDouble(parts, 6),
                ParseCsvDouble(parts, 7),
                ParseCsvDouble(parts, 8)));
        }

        return frames;
    }

    private static double? ParseCsvNullableDouble(string[] parts, int index)
    {
        return index < parts.Length && double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private void ApplyPreferredVideoMetadata()
    {
        var metadata = ResolveInputVideoMetadata() ?? VideoMetadataProbe.TryRead(ResolveAnnotatedVideoPath());
        if (metadata is null)
        {
            return;
        }

        if (metadata.FrameRate is > 0
            && (_detailData.VideoFps is not > 0 || Math.Abs(_detailData.VideoFps.Value - metadata.FrameRate.Value) > 0.5d))
        {
            _detailData.VideoFps = metadata.FrameRate;
        }

        if (metadata.DurationSeconds is > 0
            && (_detailData.VideoDurationSec is not > 0 || Math.Abs(_detailData.VideoDurationSec.Value - metadata.DurationSeconds.Value) > 0.5d))
        {
            _detailData.VideoDurationSec = metadata.DurationSeconds;
        }
    }

    private VideoProbeMetadata? ResolveInputVideoMetadata()
    {
        var videoPaths = new[]
        {
            Record?.SideVideoPath,
            Record?.FrontVideoPath,
            Record?.VideoFilePath
        };

        foreach (var path in videoPaths)
        {
            var metadata = VideoMetadataProbe.TryRead(path);
            if (metadata is not null)
            {
                return metadata;
            }
        }

        return null;
    }

    private void LoadCycleDetails(JsonObject? gaitCycle, JsonObject? gaitEvents, double fps)
    {
        var cycles = EnumerateCycles(gaitCycle).ToArray();
        if (cycles.Length == 0)
        {
            return;
        }

        var leftHeelStrikeFrames = ReadIntArray(gaitEvents, "left_heel_strike_frames").ToHashSet();
        var rightHeelStrikeFrames = ReadIntArray(gaitEvents, "right_heel_strike_frames").ToHashSet();
        var safeFps = fps > 0 ? fps : 30d;

        foreach (var cycle in cycles)
        {
            var cycleId = ReadInt(cycle, "cycle_id");
            var startFrame = ReadInt(cycle, "start_frame");
            var endFrame = ReadInt(cycle, "end_frame");
            var duration = ReadDouble(cycle, "duration_sec");
            var side = ReadCycleSide(cycle);
            if (startFrame is int start)
            {
                if (side == "--" && leftHeelStrikeFrames.Contains(start))
                {
                    side = L("AnalysisDetail.Side.Left");
                }
                else if (side == "--" && rightHeelStrikeFrames.Contains(start))
                {
                    side = L("AnalysisDetail.Side.Right");
                }
            }

            CycleDetails.Add(new AnalysisCycleDetail(
                cycleId?.ToString(CultureInfo.InvariantCulture) ?? "--",
                side,
                startFrame?.ToString(CultureInfo.InvariantCulture) ?? "--",
                endFrame?.ToString(CultureInfo.InvariantCulture) ?? "--",
                FormatSeconds(startFrame.HasValue ? startFrame.Value / safeFps : null),
                FormatSeconds(endFrame.HasValue ? endFrame.Value / safeFps : null),
                FormatSeconds(duration)));
        }
    }

    private static double ParseCsvDouble(string[] parts, int index)
    {
        return index < parts.Length && double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : double.NaN;
    }

    private static int CountResultFiles(string? directory)
    {
        return Directory.Exists(directory) ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length : 0;
    }

    private static int CountFiles(string? directory, string pattern)
    {
        return Directory.Exists(directory) ? Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).Length : 0;
    }

    private static string? ResolveLogFileName(string? directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var log = Directory.GetFiles(directory, "logs.txt", SearchOption.TopDirectoryOnly).FirstOrDefault()
            ?? Directory.GetFiles(directory, "*.log", SearchOption.AllDirectories).FirstOrDefault();
        return string.IsNullOrWhiteSpace(log) ? null : Path.GetFileName(log);
    }

    private string? BuildResultFileSummary(string? directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var hasResult = Directory.GetFiles(directory, "result.json", SearchOption.AllDirectories).Any();
        var hasJoint = Directory.GetFiles(directory, "joint_angle.csv", SearchOption.AllDirectories).Any();
        var hasPreview = Directory.GetFiles(directory, "analysis_preview.mp4", SearchOption.AllDirectories).Any();
        var parts = new List<string>();
        if (hasResult) parts.Add("result.json");
        if (hasJoint) parts.Add("joint_angle.csv");
        if (hasPreview) parts.Add("analysis_preview.mp4");
        return parts.Count > 0 ? L("AnalysisDetail.FileSummaryNamesFormat", string.Join(", ", parts)) : null;
    }

    private PlotModel BuildSingleAnglePlot(string title, string seriesName, List<AnalysisAngleFrame> frames, Func<AnalysisAngleFrame, double> valueSelector, OxyColor color, double maxTime)
    {
        var model = CreatePlotBase(title, L("AnalysisDetail.Chart.TimeAxis"), L("AnalysisDetail.Chart.AngleAxis"));
        model.Axes.OfType<LinearAxis>().First(axis => axis.Position == AxisPosition.Bottom).Maximum = Math.Max(1, maxTime);
        AddLineSeries(model, seriesName, frames, valueSelector, color);
        ApplyValueAxisRange(model);
        return model;
    }

    private PlotModel BuildDualAnglePlot(
        string title,
        string firstName,
        string secondName,
        List<AnalysisAngleFrame> frames,
        Func<AnalysisAngleFrame, double> firstSelector,
        Func<AnalysisAngleFrame, double> secondSelector,
        OxyColor firstColor,
        OxyColor secondColor,
        double maxTime)
    {
        var model = CreatePlotBase($"{title} ({firstName} / {secondName})", L("AnalysisDetail.Chart.TimeAxis"), L("AnalysisDetail.Chart.AngleAxis"), alignToPlaybackBar: true);
        model.Axes.OfType<LinearAxis>().First(axis => axis.Position == AxisPosition.Bottom).Maximum = Math.Max(1, maxTime);
        AddLineSeries(model, firstName, frames, firstSelector, firstColor);
        AddLineSeries(model, secondName, frames, secondSelector, secondColor);
        AddPlaybackCursor(model, 0);
        ApplyValueAxisRange(model);
        return model;
    }

    private PlotModel BuildSinglePlaybackAnglePlot(
        string title,
        string seriesName,
        List<AnalysisAngleFrame> frames,
        Func<AnalysisAngleFrame, double> valueSelector,
        OxyColor color,
        double maxTime)
    {
        var model = CreatePlotBase(title, L("AnalysisDetail.Chart.TimeAxis"), L("AnalysisDetail.Chart.AngleAxis"), alignToPlaybackBar: true);
        model.Axes.OfType<LinearAxis>().First(axis => axis.Position == AxisPosition.Bottom).Maximum = Math.Max(1, maxTime);
        AddLineSeries(model, seriesName, frames, valueSelector, color);
        AddPlaybackCursor(model, 0);
        ApplyValueAxisRange(model);
        return model;
    }

    public void SetVideoPlaybackTime(double seconds)
    {
        _videoPlaybackSeconds = Math.Max(0, seconds);
        UpdatePlaybackCursor(VideoKneeAnglePlotModel, _videoPlaybackSeconds);
        UpdatePlaybackCursor(VideoHipAnglePlotModel, _videoPlaybackSeconds);
        UpdatePlaybackCursor(VideoAnkleAnglePlotModel, _videoPlaybackSeconds);
        UpdatePlaybackCursor(VideoPelvisAnglePlotModel, _videoPlaybackSeconds);
        UpdatePlaybackCursor(VideoTrunkAnglePlotModel, _videoPlaybackSeconds);
    }

    public void SetVideoPreviewDuration(double seconds)
    {
        if (seconds <= 0)
        {
            return;
        }

        var maximum = Math.Max(1, seconds);
        UpdateVideoPlotMaximum(VideoKneeAnglePlotModel, maximum);
        UpdateVideoPlotMaximum(VideoHipAnglePlotModel, maximum);
        UpdateVideoPlotMaximum(VideoAnkleAnglePlotModel, maximum);
        UpdateVideoPlotMaximum(VideoPelvisAnglePlotModel, maximum);
        UpdateVideoPlotMaximum(VideoTrunkAnglePlotModel, maximum);
        SetVideoPlaybackTime(Math.Min(_videoPlaybackSeconds, maximum));
    }

    private static void UpdateVideoPlotMaximum(PlotModel? model, double maximum)
    {
        if (model is null)
        {
            return;
        }

        var xAxis = model.Axes.OfType<LinearAxis>().FirstOrDefault(axis => axis.Position == AxisPosition.Bottom);
        if (xAxis is null)
        {
            return;
        }

        xAxis.Maximum = maximum;
        xAxis.MajorStep = CalculateMajorStep(maximum);
        xAxis.MinorStep = Math.Max(0.5d, xAxis.MajorStep / 2d);
        model.InvalidatePlot(false);
    }

    private static void UpdatePlaybackCursor(PlotModel? model, double seconds)
    {
        if (model is null)
        {
            return;
        }

        var cursor = model.Annotations
            .OfType<LineAnnotation>()
            .FirstOrDefault(annotation => Equals(annotation.Tag, "PlaybackCursor"));
        if (cursor is null)
        {
            AddPlaybackCursor(model, seconds);
        }
        else
        {
            cursor.X = seconds;
        }

        model.InvalidatePlot(false);
    }

    private static void AddPlaybackCursor(PlotModel model, double seconds)
    {
        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Vertical,
            X = Math.Max(0, seconds),
            Color = OxyColor.Parse("#E4004A"),
            StrokeThickness = 2,
            LineStyle = LineStyle.Solid,
            Tag = "PlaybackCursor"
        });
    }

    private static void AddLineSeries(PlotModel model, string title, List<AnalysisAngleFrame> frames, Func<AnalysisAngleFrame, double> valueSelector, OxyColor color)
    {
        var series = new LineSeries
        {
            Title = title,
            Color = color,
            StrokeThickness = 2.4,
            MarkerType = MarkerType.None
        };

        foreach (var frame in frames)
        {
            var value = valueSelector(frame);
            if (!double.IsNaN(value) && !double.IsInfinity(value))
            {
                series.Points.Add(new DataPoint(frame.TimeS, value));
            }
        }

        model.Series.Add(series);
    }

    private static void ApplyValueAxisRange(PlotModel model)
    {
        var values = model.Series
            .OfType<LineSeries>()
            .SelectMany(series => series.Points)
            .Select(point => point.Y)
            .Where(value => !double.IsNaN(value) && !double.IsInfinity(value))
            .ToList();
        var yAxis = model.Axes.OfType<LinearAxis>().FirstOrDefault(axis => axis.Position == AxisPosition.Left);
        if (yAxis is null || values.Count == 0)
        {
            return;
        }

        var min = values.Min();
        var max = values.Max();
        var padding = Math.Max(5d, (max - min) * 0.12d);
        yAxis.Minimum = Math.Floor(min - padding);
        yAxis.Maximum = Math.Ceiling(max + padding);
        yAxis.MajorStep = CalculateMajorStep(yAxis.Maximum - yAxis.Minimum);
        yAxis.MinorStep = yAxis.MajorStep / 2d;
    }

    private static double CalculateMajorStep(double range)
    {
        if (range <= 20) return 5;
        if (range <= 60) return 10;
        if (range <= 120) return 20;
        return 50;
    }

    private PlotModel CreateEmptyPlot(string title, string? message = null, bool alignToPlaybackBar = false)
    {
        var model = CreatePlotBase(title, L("AnalysisDetail.Chart.TimeAxis"), L("AnalysisDetail.Chart.AngleAxis"), alignToPlaybackBar);
        model.Subtitle = message ?? L("AnalysisDetail.Chart.NoData");
        model.SubtitleColor = OxyColor.Parse("#999999");
        return model;
    }


    private static PlotModel CreatePlotBase(string title, string xAxisTitle, string yAxisTitle, bool alignToPlaybackBar = false)
    {
        var model = new PlotModel
        {
            Title = title,
            TitleFont = "Microsoft YaHei",
            TitleFontSize = 14,
            TitleFontWeight = OxyPlot.FontWeights.Bold,
            TextColor = OxyColor.Parse("#333333"),
            PlotAreaBorderColor = OxyColor.Parse("#DCE3EC"),
            PlotAreaBorderThickness = new OxyThickness(1),
            Background = OxyColors.White,
            PlotAreaBackground = OxyColor.Parse("#FCFDFE"),
            IsLegendVisible = true
        };

        if (alignToPlaybackBar)
        {
            model.PlotMargins = new OxyThickness(108, 24, 70, 46);
        }

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = xAxisTitle,
            Minimum = 0,
            Maximum = 20,
            MajorStep = 5,
            MinorStep = 1,
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.Parse("#E8EDF3"),
            MinorGridlineColor = OxyColor.Parse("#F1F4F8"),
            AxislineColor = OxyColor.Parse("#DCE3EC"),
            TextColor = OxyColor.Parse("#666666"),
            TitleColor = OxyColor.Parse("#666666"),
            IsPanEnabled = false,
            IsZoomEnabled = false
        });

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = yAxisTitle,
            Minimum = 0,
            Maximum = 40,
            MajorStep = 10,
            MinorStep = 5,
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.Parse("#E8EDF3"),
            MinorGridlineColor = OxyColor.Parse("#F1F4F8"),
            AxislineColor = OxyColor.Parse("#DCE3EC"),
            TextColor = OxyColor.Parse("#666666"),
            TitleColor = OxyColor.Parse("#666666"),
            IsPanEnabled = false,
            IsZoomEnabled = false
        });

        return model;
    }

    private void NotifyComputedPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsEmptyState));
        OnPropertyChanged(nameof(IsFailedState));
        OnPropertyChanged(nameof(IsSuccessState));
        OnPropertyChanged(nameof(IsOverviewSectionSelected));
        OnPropertyChanged(nameof(IsSpatiotemporalSectionSelected));
        OnPropertyChanged(nameof(IsKinematicsSectionSelected));
        OnPropertyChanged(nameof(IsQualitySectionSelected));
        OnPropertyChanged(nameof(IsFilesSectionSelected));
        OnPropertyChanged(nameof(IsReportSectionSelected));
        OnPropertyChanged(nameof(CurrentSectionIndex));
        OnPropertyChanged(nameof(NavigationProgressValue));
        OnPropertyChanged(nameof(NavigationProgressMaximum));
        OnPropertyChanged(nameof(NavigationProgressText));
        OnPropertyChanged(nameof(PatientName));
        OnPropertyChanged(nameof(PatientHeightDisplay));
        OnPropertyChanged(nameof(PatientCode));
        OnPropertyChanged(nameof(MeasurementName));
        OnPropertyChanged(nameof(MeasurementTypeDisplay));
        OnPropertyChanged(nameof(MeasurementVideoModeDisplay));
        OnPropertyChanged(nameof(IsDualVideoMode));
        OnPropertyChanged(nameof(IsSingleVideoMode));
        OnPropertyChanged(nameof(MeasurementDate));
        OnPropertyChanged(nameof(RequestIdDisplay));
        OnPropertyChanged(nameof(TaskStatusDisplay));
        OnPropertyChanged(nameof(AnalysisDurationDisplay));
        OnPropertyChanged(nameof(AnalysisCreatedAtDisplay));
        OnPropertyChanged(nameof(ProtocolVersionDisplay));
        OnPropertyChanged(nameof(AlgorithmVersionDisplay));
        OnPropertyChanged(nameof(ModelVersionDisplay));
        OnPropertyChanged(nameof(AnalysisStatusText));
        OnPropertyChanged(nameof(AnalysisStatusColor));
        OnPropertyChanged(nameof(QualitySummary));
        OnPropertyChanged(nameof(ValidFrameRatioDisplay));
        OnPropertyChanged(nameof(GaitSpeedDisplay));
        OnPropertyChanged(nameof(CadenceDisplay));
        OnPropertyChanged(nameof(StepLengthDisplay));
        OnPropertyChanged(nameof(GaitCycleDisplay));
        OnPropertyChanged(nameof(StrideLengthDisplay));
        OnPropertyChanged(nameof(StanceTimeDisplay));
        OnPropertyChanged(nameof(SwingTimeDisplay));
        OnPropertyChanged(nameof(KinematicSummaryDisplay));
        OnPropertyChanged(nameof(OutputDirectory));
        OnPropertyChanged(nameof(FileCountDisplay));
        OnPropertyChanged(nameof(AnnotatedVideoDisplay));
        OnPropertyChanged(nameof(HasAnnotatedVideo));
        OnPropertyChanged(nameof(AnnotatedVideoUri));
        OnPropertyChanged(nameof(IsAnalysisPreviewGenerating));
        OnPropertyChanged(nameof(AnalysisPreviewStatusText));
        OnPropertyChanged(nameof(SummaryFileDisplay));
        OnPropertyChanged(nameof(CycleCountDisplay));
        OnPropertyChanged(nameof(ResultFileSummaryDisplay));
        OnPropertyChanged(nameof(LogFileDisplay));
        OnPropertyChanged(nameof(CsvFileCountDisplay));
        OnPropertyChanged(nameof(ImageFileCountDisplay));
        OnPropertyChanged(nameof(MeanStrideLengthDisplay));
        OnPropertyChanged(nameof(DoubleSupportTimeDisplay));
        OnPropertyChanged(nameof(SingleSupportTimeDisplay));
        OnPropertyChanged(nameof(LeftStrideLengthDisplay));
        OnPropertyChanged(nameof(RightStrideLengthDisplay));
        OnPropertyChanged(nameof(StrideLengthDiffDisplay));
        OnPropertyChanged(nameof(StrideLengthDiffPercentDisplay));
        OnPropertyChanged(nameof(LeftStancePhaseDisplay));
        OnPropertyChanged(nameof(RightStancePhaseDisplay));
        OnPropertyChanged(nameof(LeftSwingPhaseDisplay));
        OnPropertyChanged(nameof(RightSwingPhaseDisplay));
        OnPropertyChanged(nameof(LeftHeelStrikeCountDisplay));
        OnPropertyChanged(nameof(RightHeelStrikeCountDisplay));
        OnPropertyChanged(nameof(LeftToeOffCountDisplay));
        OnPropertyChanged(nameof(RightToeOffCountDisplay));
        OnPropertyChanged(nameof(HipRomDisplay));
        OnPropertyChanged(nameof(LeftHipRomDisplay));
        OnPropertyChanged(nameof(RightHipRomDisplay));
        OnPropertyChanged(nameof(KneeRomDisplay));
        OnPropertyChanged(nameof(LeftKneeRomDisplay));
        OnPropertyChanged(nameof(RightKneeRomDisplay));
        OnPropertyChanged(nameof(AnkleRomDisplay));
        OnPropertyChanged(nameof(LeftAnkleRomDisplay));
        OnPropertyChanged(nameof(RightAnkleRomDisplay));
        OnPropertyChanged(nameof(PelvisCoronalRomDisplay));
        OnPropertyChanged(nameof(TrunkTiltMeanDisplay));
        OnPropertyChanged(nameof(TrunkTiltMaxDisplay));
        OnPropertyChanged(nameof(TrunkTiltMinDisplay));
        OnPropertyChanged(nameof(TrunkTiltRomDisplay));
        OnPropertyChanged(nameof(PelvicTiltMeanDisplay));
        OnPropertyChanged(nameof(PelvicObliquityMeanDisplay));
        OnPropertyChanged(nameof(StepLengthDiffDisplay));
        OnPropertyChanged(nameof(StepLengthDiffPercentDisplay));
        OnPropertyChanged(nameof(StanceTimeDiffPercentDisplay));
        OnPropertyChanged(nameof(KneeRomDiffDisplay));
        OnPropertyChanged(nameof(HipRomDiffDisplay));
        OnPropertyChanged(nameof(AnkleRomDiffDisplay));
        OnPropertyChanged(nameof(SymmetryScoreDisplay));
        OnPropertyChanged(nameof(CurrentVideoFileDisplay));
        OnPropertyChanged(nameof(CurrentFrameDisplay));
        OnPropertyChanged(nameof(CurrentPlaybackTimeDisplay));
        OnPropertyChanged(nameof(VideoDurationDisplay));
        OnPropertyChanged(nameof(VideoDurationPlaybackDisplay));
        OnPropertyChanged(nameof(CurrentGaitCycleDisplay));
        OnPropertyChanged(nameof(CurrentEventDisplay));
        OnPropertyChanged(nameof(CurrentLeftKneeAngleDisplay));
        OnPropertyChanged(nameof(CurrentRightKneeAngleDisplay));
        OnPropertyChanged(nameof(CurrentLeftAnkleAngleDisplay));
        OnPropertyChanged(nameof(CurrentRightAnkleAngleDisplay));
        OnPropertyChanged(nameof(CycleDetails));
        OnPropertyChanged(nameof(CanConfigureReport));
        OnPropertyChanged(nameof(CanPreviewReport));
        OnPropertyChanged(nameof(ReportNumberDisplay));
        OnPropertyChanged(nameof(ReportAnalysisSourceDisplay));
        OnPropertyChanged(nameof(ReportOperatorDisplay));
        OnPropertyChanged(nameof(ReportDraftUpdatedAtDisplay));
        OnPropertyChanged(nameof(ReportQualityHint));
        OnPropertyChanged(nameof(ReportIncludedSectionsSummary));
        OnPropertyChanged(nameof(ReportPreviewMessage));
    }

    private void NotifyPlotModelsChanged()
    {
        OnPropertyChanged(nameof(LeftHipAnglePlotModel));
        OnPropertyChanged(nameof(RightHipAnglePlotModel));
        OnPropertyChanged(nameof(LeftKneeAnglePlotModel));
        OnPropertyChanged(nameof(RightKneeAnglePlotModel));
        OnPropertyChanged(nameof(LeftAnkleAnglePlotModel));
        OnPropertyChanged(nameof(RightAnkleAnglePlotModel));
        OnPropertyChanged(nameof(PelvisAnglePlotModel));
        OnPropertyChanged(nameof(TrunkAnglePlotModel));
        OnPropertyChanged(nameof(VideoKneeAnglePlotModel));
        OnPropertyChanged(nameof(VideoHipAnglePlotModel));
        OnPropertyChanged(nameof(VideoAnkleAnglePlotModel));
        OnPropertyChanged(nameof(VideoPelvisAnglePlotModel));
        OnPropertyChanged(nameof(VideoTrunkAnglePlotModel));
        OnPropertyChanged(nameof(VideoTrajectoryPlotModel));
    }

    private static double? ReadDouble(JsonObject? obj, string name)
    {
        if (obj is null || obj[name] is null)
        {
            return null;
        }

        if (obj[name] is JsonValue value)
        {
            if (value.TryGetValue<double>(out var doubleValue))
            {
                return doubleValue;
            }

            if (value.TryGetValue<string>(out var stringValue)
                && double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static JsonObject? ReadObject(JsonObject? obj, params string[] names)
    {
        if (obj is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (obj[name] is JsonObject value)
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

    private static IEnumerable<int> ReadIntArray(JsonObject? obj, string name)
    {
        if (obj?[name] is not JsonArray array)
        {
            return [];
        }

        return array
            .Select(node =>
            {
                if (node is JsonValue value && value.TryGetValue<int>(out var intValue))
                {
                    return intValue;
                }

                return (int?)null;
            })
            .Where(value => value.HasValue)
            .Select(value => value!.Value);
    }

    private static string FormatSeconds(double? seconds)
    {
        return seconds.HasValue ? $"{seconds.Value:F2} s" : "--";
    }

    private static string FormatPlaybackTime(double? seconds)
    {
        if (seconds is not > 0)
        {
            return "0:00";
        }

        var rounded = TimeSpan.FromSeconds(Math.Max(0, (int)Math.Round(seconds.Value, MidpointRounding.AwayFromZero)));
        return rounded.TotalHours >= 1
            ? $"{(int)rounded.TotalHours}:{rounded.Minutes:00}:{rounded.Seconds:00}"
            : $"{(int)rounded.TotalMinutes}:{rounded.Seconds:00}";
    }

    private static int? ReadInt(JsonObject? obj, string name)
    {
        if (obj is null || obj[name] is null)
        {
            return null;
        }

        if (obj[name] is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (value.TryGetValue<string>(out var stringValue)
                && int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int? ReadArrayCount(JsonObject? obj, string name)
    {
        return obj?[name] is JsonArray array ? array.Count : null;
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

    private string ReadCycleSide(JsonObject cycle)
    {
        var side = cycle["side"] is JsonValue value && value.TryGetValue<string>(out var rawSide)
            ? rawSide.Trim().ToLowerInvariant()
            : null;
        return side switch
        {
            "left" => L("AnalysisDetail.Side.Left"),
            "right" => L("AnalysisDetail.Side.Right"),
            _ => "--"
        };
    }

    private static string FormatNumber(double? value, string format, string unit)
    {
        if (!value.HasValue)
        {
            return "--";
        }

        var number = value.Value.ToString(format);
        return string.IsNullOrWhiteSpace(unit) ? number : $"{number} {unit}";
    }

    private static string FormatMetersFromCentimeters(double? value)
    {
        return value.HasValue ? $"{(value.Value / 100d):F2} m" : "--";
    }

    private string FormatCount(int? value)
    {
        return value.HasValue ? L("AnalysisDetail.TimesFormat", value.Value) : "--";
    }

    private static double? ComplementPercent(double? value)
    {
        return value.HasValue ? 100d - value.Value : null;
    }

    private static double? Average(double? left, double? right)
    {
        return left.HasValue && right.HasValue
            ? (left.Value + right.Value) / 2d
            : left ?? right;
    }

    private static double? Difference(double? left, double? right)
    {
        return left.HasValue && right.HasValue ? Math.Abs(left.Value - right.Value) : null;
    }

    private static double? DifferencePercent(double? left, double? right)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return null;
        }

        var average = (Math.Abs(left.Value) + Math.Abs(right.Value)) / 2d;
        return average <= 0 ? null : Math.Abs(left.Value - right.Value) / average * 100d;
    }

    private double? CalculateSymmetryScore()
    {
        var differences = new[]
        {
            DifferencePercent(_detailData.LeftStrideMeanM, _detailData.RightStrideMeanM),
            Difference(_detailData.LeftStanceRatioPct, _detailData.RightStanceRatioPct),
            DifferencePercent(_detailData.LeftKneeRomDeg, _detailData.RightKneeRomDeg),
            DifferencePercent(_detailData.LeftHipRomDeg, _detailData.RightHipRomDeg),
            DifferencePercent(_detailData.LeftAnkleRomDeg, _detailData.RightAnkleRomDeg)
        }.Where(value => value.HasValue).Select(value => value!.Value).ToList();

        if (differences.Count == 0)
        {
            return null;
        }

        return Math.Clamp(100d - differences.Average(), 0d, 100d);
    }

    private static double? NormalizeVideoDuration(double? reportedDuration, double? frameDuration)
    {
        if (reportedDuration is > 0 && frameDuration is > 0)
        {
            return Math.Abs(reportedDuration.Value - frameDuration.Value) > 0.5d
                ? frameDuration
                : reportedDuration;
        }

        return reportedDuration is > 0 ? reportedDuration : frameDuration;
    }

    private static string GetEnumDescription<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var member = typeof(TEnum).GetMember(value.ToString()).FirstOrDefault();
        var attribute = member?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? value.ToString();
    }
}

internal sealed class AnalysisDetailData
{
    public double? VideoFps { get; set; }
    public double? VideoDurationSec { get; set; }
    public int? CycleCount { get; set; }
    public double? MeanCycleDurationSec { get; set; }
    public double? CadenceStepPerMin { get; set; }
    public double? GaitSpeedMPerS { get; set; }
    public double? MeanStepLengthM { get; set; }
    public double? MeanStrideLengthM { get; set; }
    public double? MeanStanceTimeSec { get; set; }
    public double? MeanSwingTimeSec { get; set; }
    public double? MeanDoubleSupportTimeSec { get; set; }
    public double? MeanSingleSupportTimeSec { get; set; }
    public int? LeftHeelStrikeCount { get; set; }
    public int? RightHeelStrikeCount { get; set; }
    public int? LeftToeOffCount { get; set; }
    public int? RightToeOffCount { get; set; }
    public double? LeftHipRomDeg { get; set; }
    public double? RightHipRomDeg { get; set; }
    public double? LeftKneeRomDeg { get; set; }
    public double? RightKneeRomDeg { get; set; }
    public double? LeftAnkleRomDeg { get; set; }
    public double? RightAnkleRomDeg { get; set; }
    public double? TrunkTiltMeanDeg { get; set; }
    public double? TrunkTiltMaxDeg { get; set; }
    public double? TrunkTiltMinDeg { get; set; }
    public double? TrunkTiltRomDeg { get; set; }
    public double? PelvisTiltMeanDeg { get; set; }
    public double? PelvisTiltMaxDeg { get; set; }
    public double? PelvisTiltMinDeg { get; set; }
    public double? PelvisRomDeg { get; set; }
    public double? LeftStrideMeanM { get; set; }
    public double? RightStrideMeanM { get; set; }
    public double? LeftStanceRatioPct { get; set; }
    public double? RightStanceRatioPct { get; set; }
    public double? ValidFrameRatio { get; set; }
    public string? QualityLevel { get; set; }
    public string? QualityHint { get; set; }
    public int ResultFileCount { get; set; }
    public int CsvFileCount { get; set; }
    public int ImageFileCount { get; set; }
    public string? LogFileName { get; set; }
    public string? ResultFileSummary { get; set; }
}

internal sealed record AnalysisAngleFrame(
    int FrameIndex,
    double TimeS,
    double RightAnkle,
    double LeftAnkle,
    double RightKnee,
    double LeftKnee,
    double RightHip,
    double LeftHip,
    double Pelvis,
    double Trunk);

public sealed record AnalysisCycleDetail(
    string CycleId,
    string Side,
    string StartFrame,
    string EndFrame,
    string StartTime,
    string EndTime,
    string Duration);

/// <summary>
/// 分析详情状态。
/// </summary>
public enum AnalysisDetailState
{
    /// <summary>
    /// 暂无分析结果。
    /// </summary>
    Empty,

    /// <summary>
    /// 分析失败。
    /// </summary>
    Failed,

    /// <summary>
    /// 分析成功。
    /// </summary>
    Success
}

/// <summary>
/// 分析详情导航项。
/// </summary>
/// <param name="Key">导航键。</param>
/// <param name="Title">导航标题。</param>
/// <param name="Description">导航说明。</param>
public sealed record AnalysisDetailNavigationItem(string Key, string Title, string Description);

/// <summary>
/// 报告草稿配置项。
/// </summary>
/// <param name="IncludeSpatiotemporalParameters">是否包含时空参数。</param>
/// <param name="IncludeKinematicSummary">是否包含运动学摘要。</param>
/// <param name="IncludeQualityControl">是否包含质量控制。</param>
/// <param name="IncludeResultFiles">是否包含结果文件摘要。</param>
public sealed record ReportDraftOptions(
    bool IncludeSpatiotemporalParameters,
    bool IncludeKinematicSummary,
    bool IncludeQualityControl,
    bool IncludeResultFiles);
