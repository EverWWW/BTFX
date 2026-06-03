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
    private string _reportConfigTitle = "报告配置";

    /// <summary>
    /// 报告配置说明。
    /// </summary>
    private string _reportConfigMessage = "可基于当前分析结果生成报告草稿，并在此完善基础配置。";

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
    private string _reportPreviewMessage = "完成基础配置后，可进入报告预览检查内容排版。";

    /// <summary>
    /// 是否正在准备报告预览。
    /// </summary>
    private bool _isPreparingReportPreview;

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
    public ObservableCollection<AnalysisDetailNavigationItem> NavigationItems { get; } =
    [
        new("overview", "结果概览", "展示测量与分析摘要信息"),
        new("spatiotemporal", "时空参数", "展示步速、步频、步长等核心参数"),
        new("kinematics", "运动学参数", "展示髋膝踝等核心运动学摘要"),
        new("quality", "质量控制", "展示分析质量与风险提示"),
        new("files", "文件管理", "展示分析输出目录与结果文件"),
        new("report", "报告配置", "基于当前分析结果生成并完善报告草稿")
    ];

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
        ISessionService sessionService)
    {
        _gaitAnalysisService = gaitAnalysisService;
        _reportService = reportService;
        _exportImportService = exportImportService;
        _sessionService = sessionService;
        CanExport = _sessionService.HasPermission("export");

        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch
        {
        }

        BuildRealPlotModels();

        SelectedNavigationItem = NavigationItems.FirstOrDefault();
        SetEmptyState("暂无分析结果", "当前测量尚未生成可查看的分析结果，可先查看基础测量信息。", false);
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
                SetSuccessState();
                return;
            }

            if (HasAnalysisFailure(record))
            {
                SetFailedState();
                return;
            }

            SetEmptyState("暂无分析结果", "当前测量已保存，但还没有成功的分析结果。", false);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"加载分析详情失败：MeasurementId={record.Id}", ex);
            SetFailedState($"分析结果加载失败：{ex.Message}");
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
    public string MeasurementTypeDisplay => Record is null ? "自然步行" : GetEnumDescription(Record.MeasurementType);

    /// <summary>
    /// 分析模式。
    /// </summary>
    public string MeasurementVideoModeDisplay => Record?.HasDualVideo == true ? "双视角模式" : "单视角模式";

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
                return "已完成";
            }

            if (AnalysisResult.Success)
            {
                return "已完成";
            }

            return string.IsNullOrWhiteSpace(AnalysisResult.TaskStatus)
                ? "失败"
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
        AnalysisDetailState.Success => "分析成功",
        AnalysisDetailState.Failed => "分析失败",
        _ => "尚未分析"
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
            if (AnalysisResult?.QualityControl is null)
            {
                return _detailData.ValidFrameRatio is double ratio
                    ? $"有效帧比例 {ratio:P0}"
                    : "--";
            }

            if (AnalysisResult.QualityControl.ValidFrameRatio is double validFrameRatio)
            {
                return $"有效帧比例 {validFrameRatio:P0}";
            }

            return "已生成质量控制信息";
        }
    }

    /// <summary>
    /// 有效帧比例摘要。
    /// </summary>
    public string ValidFrameRatioDisplay => AnalysisResult?.QualityControl?.ValidFrameRatio is double validFrameRatio
        ? validFrameRatio.ToString("P0")
        : (_detailData.ValidFrameRatio is double ratio ? ratio.ToString("P0") : "--");

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
                return "髋关节 ROM -- / 膝关节 ROM -- / 踝关节 ROM --";
            }

            return $"髋关节 ROM {FormatNumber(summary.HipRomDeg, "F1", "°")} / 膝关节 ROM {FormatNumber(summary.KneeRomDeg, "F1", "°")} / 踝关节 ROM {FormatNumber(summary.AnkleRomDeg, "F1", "°")}";
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
        ? $"{AnalysisResult.CsvFiles.Count} 个结果文件"
        : (_detailData.ResultFileCount > 0 ? $"{_detailData.ResultFileCount} 个结果文件" : "--");

    /// <summary>
    /// 标注视频摘要。
    /// </summary>
    public string AnnotatedVideoDisplay => string.IsNullOrWhiteSpace(ResolveAnnotatedVideoPath())
        ? "--"
        : Path.GetFileName(ResolveAnnotatedVideoPath()) ?? "--";

    public bool HasAnnotatedVideo => !string.IsNullOrWhiteSpace(ResolveAnnotatedVideoPath())
        && File.Exists(ResolveAnnotatedVideoPath());

    public Uri? AnnotatedVideoUri => HasAnnotatedVideo
        ? new Uri(ResolveAnnotatedVideoPath()!, UriKind.Absolute)
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
    public string CycleCountDisplay => _detailData.CycleCount.HasValue ? $"{_detailData.CycleCount.Value} 个" : "--";

    /// <summary>
    /// 文件摘要。
    /// </summary>
    public string ResultFileSummaryDisplay => AnalysisResult?.CsvFiles?.Count > 0
        ? $"已生成 {AnalysisResult.CsvFiles.Count} 个结果文件"
        : (_detailData.ResultFileSummary ?? "--");

    /// <summary>
    /// 日志文件摘要。
    /// </summary>
    public string LogFileDisplay => _detailData.LogFileName ?? "--";

    /// <summary>
    /// CSV 文件数量。
    /// </summary>
    public string CsvFileCountDisplay => AnalysisResult?.CsvFiles?.Count is int count and > 0 ? $"{count} 个" : $"{_detailData.CsvFileCount} 个";

    /// <summary>
    /// 图片文件数量。
    /// </summary>
    public string ImageFileCountDisplay => $"{_detailData.ImageFileCount} 个";

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

    public string CurrentGaitCycleDisplay => _detailData.CycleCount.HasValue ? $"共 {_detailData.CycleCount.Value} 周期" : "--";

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
                return "当前没有可用于配置报告的分析结果。";
            }

            return ValidFrameRatioDisplay == "--"
                ? "当前分析结果已生成，暂未计算有效帧比例。"
                : $"当前分析结果已生成，有效帧比例 {ValidFrameRatioDisplay}，可继续配置报告内容。";
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
                sections.Add("时空参数");
            }

            if (IncludeKinematicSummary)
            {
                sections.Add("运动学摘要");
            }

            if (IncludeQualityControl)
            {
                sections.Add("质量控制");
            }

            if (IncludeResultFiles)
            {
                sections.Add("结果文件摘要");
            }

            return sections.Count > 0 ? string.Join("、", sections) : "尚未选择报告内容";
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
    public string CurrentSectionTitle => SelectedNavigationItem?.Title ?? "结果概览";

    /// <summary>
    /// 当前模块说明。
    /// </summary>
    public string CurrentSectionDescription => SelectedNavigationItem?.Description ?? "展示当前测量与分析结果的核心信息。";

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
        ? $"当前浏览第 {CurrentSectionIndex} / {NavigationSectionCount} 个模块"
        : "当前暂无可浏览模块";

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
                Title = "导出测量结果包",
                Filter = "BTFX测量结果包 (*.btfxpkg)|*.btfxpkg",
                FileName = $"测量结果包_{Record.Patient?.Name}_{Record.MeasurementDate:yyyyMMdd_HHmmss}.btfxpkg"
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
                System.Windows.MessageBox.Show(result.Message, "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                _logHelper?.Information($"导出测量结果包：MeasurementId={Record.Id}, 文件={dialog.FileName}");
                return;
            }

            System.Windows.MessageBox.Show(result.Message, "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"导出测量结果包失败：MeasurementId={Record.Id}", ex);
            System.Windows.MessageBox.Show($"导出失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
            ReportConfigTitle = "暂不可配置报告";
            ReportConfigMessage = AnalysisResult is null
                ? "需要先生成成功的分析结果，才能进入报告配置。"
                : "当前用户没有报告配置权限。";
            ReportPreviewMessage = AnalysisResult is null
                ? "当前没有可预览的分析结果。"
                : "当前用户没有报告预览权限。";
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
                ReportConfigTitle = "报告草稿初始化失败";
                ReportConfigMessage = "无法为当前分析结果准备报告草稿，请稍后重试。";
                ReportPreviewMessage = "报告草稿尚未准备完成，暂时无法进入预览。";
                return;
            }

            CurrentReportDraft = report;
            ApplyDraftToReportConfig(report);
            await PersistDraftSnapshotAsync();
            ReportConfigTitle = "报告配置";
            ReportConfigMessage = "已加载当前分析结果对应的报告草稿，可继续完善基础信息与包含项。";
            ReportPreviewMessage = "当前草稿已准备就绪，可进入报告预览检查版式与内容。";
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"加载报告配置失败：MeasurementId={Record.Id}", ex);
            ReportConfigTitle = "报告配置加载失败";
            ReportConfigMessage = $"初始化报告配置时发生错误：{ex.Message}";
            ReportPreviewMessage = "报告预览入口初始化失败，请先重试草稿加载。";
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
                ? $"步态分析报告 - {PatientName}"
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
            ? "当前配置已同步，可进入报告预览检查版式与内容。"
            : "请先补充报告标题并确认草稿已加载完成。";

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

        ReportConfigMessage = "报告草稿配置保存失败，请稍后重试。";
        ReportPreviewMessage = "当前配置已更新到界面，但尚未成功保存到草稿。";
    }

    private void ResetReportConfigState()
    {
        ReportConfigTitle = "报告配置";
        ReportConfigMessage = "可基于当前分析结果生成报告草稿，并在此完善基础配置。";
        ReportPreviewMessage = "完成基础配置后，可进入报告预览检查内容排版。";
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
            ReportPreviewMessage = "当前条件不足，暂时无法进入报告预览。";
            return;
        }

        if (CurrentReportDraft is null)
        {
            await EnsureReportDraftAsync(forceReload: false);
        }

        if (CurrentReportDraft is null)
        {
            ReportPreviewMessage = "报告草稿尚未准备完成，暂时无法进入预览。";
            return;
        }

        if (string.IsNullOrWhiteSpace(ReportTitle))
        {
            ReportPreviewMessage = "请先填写报告标题后再进入预览。";
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
                ? "已从预览返回配置，可继续调整后再次查看。"
                : "已关闭报告预览，可继续调整配置后再次查看。";
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"打开报告预览失败：MeasurementId={Record?.Id}", ex);
            ReportPreviewMessage = $"打开报告预览失败：{ex.Message}";
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
            System.Windows.MessageBox.Show("当前没有可打开的结果目录。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
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
            System.Windows.MessageBox.Show($"打开目录失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void SetSuccessState()
    {
        DetailState = AnalysisDetailState.Success;
        StateTitle = "分析结果详情";
        StateMessage = "已加载当前测量的最新成功分析结果。";
    }

    private void SetFailedState(string? message = null)
    {
        DetailState = AnalysisDetailState.Failed;
        StateTitle = "分析未成功完成";
        StateMessage = string.IsNullOrWhiteSpace(message)
            ? "当前测量存在分析流程，但尚未生成可用结果，请检查分析日志后重试。"
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
        CycleDetails.Clear();

        try
        {
            var resultPath = ResolveResultJsonPath(result);
            if (!string.IsNullOrWhiteSpace(resultPath) && File.Exists(resultPath))
            {
                LoadResultJson(resultPath);
            }

            ApplyPreferredVideoMetadata();

            _detailData.ResultFileCount = CountResultFiles(result.OutputDirectory);
            _detailData.CsvFileCount = CountFiles(result.OutputDirectory, "*.csv");
            _detailData.ImageFileCount = CountFiles(result.OutputDirectory, "*.png") + CountFiles(result.OutputDirectory, "*.jpg") + CountFiles(result.OutputDirectory, "*.jpeg");
            _detailData.LogFileName = ResolveLogFileName(result.OutputDirectory);
            _detailData.ResultFileSummary = BuildResultFileSummary(result.OutputDirectory);

            var jointAngleCsv = ResolveJointAngleCsvPath(result);
            if (!string.IsNullOrWhiteSpace(jointAngleCsv) && File.Exists(jointAngleCsv))
            {
                _angleFrames = ParseAngleCsv(jointAngleCsv, _detailData.VideoFps ?? 30d, _detailData.VideoDurationSec);
                _detailData.ValidFrameRatio ??= EstimateValidFrameRatio(_angleFrames, _detailData.VideoFps, _detailData.VideoDurationSec);
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
        _detailData.CycleCount = ReadInt(gaitCycle, "cycle_count");
        _detailData.MeanCycleDurationSec = AverageCycleDuration(gaitCycle);
        ApplyPreferredVideoMetadata();
        LoadCycleDetails(gaitCycle, gaitEvents, _detailData.VideoFps ?? 30d);
        _detailData.CadenceStepPerMin = ReadDouble(spatiotemporal, "cadence_step_per_min");
        _detailData.GaitSpeedMPerS = ReadDouble(spatiotemporal, "gait_velocity_m_per_sec");
        _detailData.MeanStepLengthM = ReadDouble(spatiotemporal, "mean_step_length_m");
        _detailData.MeanStrideLengthM = ReadDouble(spatiotemporal, "mean_stride_length_m");
        _detailData.MeanStanceTimeSec = ReadDouble(spatiotemporal, "mean_stance_time_sec");
        _detailData.MeanSwingTimeSec = ReadDouble(spatiotemporal, "mean_swing_time_sec");
        _detailData.MeanDoubleSupportTimeSec = ReadDouble(spatiotemporal, "mean_double_support_time_sec");
        _detailData.MeanSingleSupportTimeSec = ReadDouble(spatiotemporal, "mean_single_support_time_sec");

        _detailData.LeftHeelStrikeCount = ReadArrayCount(gaitEvents, "left_heel_strike_frames");
        _detailData.RightHeelStrikeCount = ReadArrayCount(gaitEvents, "right_heel_strike_frames");
        _detailData.LeftToeOffCount = ReadArrayCount(gaitEvents, "left_toe_off_frames");
        _detailData.RightToeOffCount = ReadArrayCount(gaitEvents, "right_toe_off_frames");

        _detailData.LeftHipRomDeg = ReadDouble(jointAngles?["left_hip"] as JsonObject, "rom_deg");
        _detailData.RightHipRomDeg = ReadDouble(jointAngles?["right_hip"] as JsonObject, "rom_deg");
        _detailData.LeftKneeRomDeg = ReadDouble(jointAngles?["left_knee"] as JsonObject, "rom_deg");
        _detailData.RightKneeRomDeg = ReadDouble(jointAngles?["right_knee"] as JsonObject, "rom_deg");
        _detailData.LeftAnkleRomDeg = ReadDouble(jointAngles?["left_ankle"] as JsonObject, "rom_deg");
        _detailData.RightAnkleRomDeg = ReadDouble(jointAngles?["right_ankle"] as JsonObject, "rom_deg");

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
        _detailData.LeftStanceRatioPct = ReadDouble(root, "left_stance_ratio_pct");
        _detailData.RightStanceRatioPct = ReadDouble(root, "right_stance_ratio_pct");
        _detailData.ValidFrameRatio = ReadDouble(root["quality_control"] as JsonObject, "valid_frame_ratio");
    }

    private void BuildRealPlotModels()
    {
        if (_angleFrames.Count == 0)
        {
            LeftHipAnglePlotModel = CreateEmptyPlot("左髋角度曲线");
            RightHipAnglePlotModel = CreateEmptyPlot("右髋角度曲线");
            LeftKneeAnglePlotModel = CreateEmptyPlot("左膝角度曲线");
            RightKneeAnglePlotModel = CreateEmptyPlot("右膝角度曲线");
            LeftAnkleAnglePlotModel = CreateEmptyPlot("左踝角度曲线");
            RightAnkleAnglePlotModel = CreateEmptyPlot("右踝角度曲线");
            PelvisAnglePlotModel = CreateEmptyPlot("骨盆角度曲线");
            TrunkAnglePlotModel = CreateEmptyPlot("躯干角度曲线");
            VideoKneeAnglePlotModel = CreateEmptyPlot("膝关节角度曲线", alignToPlaybackBar: true);
            VideoHipAnglePlotModel = CreateEmptyPlot("髋关节角度曲线", alignToPlaybackBar: true);
            VideoAnkleAnglePlotModel = CreateEmptyPlot("踝关节角度曲线", alignToPlaybackBar: true);
            VideoPelvisAnglePlotModel = CreateEmptyPlot("骨盆角度曲线", alignToPlaybackBar: true);
            VideoTrunkAnglePlotModel = CreateEmptyPlot("躯干角度曲线", alignToPlaybackBar: true);
        }
        else
        {
            var maxTime = Math.Max(_detailData.VideoDurationSec ?? 0d, _angleFrames[^1].TimeS);
            LeftHipAnglePlotModel = BuildSingleAnglePlot("左髋角度曲线", _angleFrames, f => f.LeftHip, OxyColors.SteelBlue, maxTime);
            RightHipAnglePlotModel = BuildSingleAnglePlot("右髋角度曲线", _angleFrames, f => f.RightHip, OxyColor.Parse("#F2306A"), maxTime);
            LeftKneeAnglePlotModel = BuildSingleAnglePlot("左膝角度曲线", _angleFrames, f => f.LeftKnee, OxyColors.ForestGreen, maxTime);
            RightKneeAnglePlotModel = BuildSingleAnglePlot("右膝角度曲线", _angleFrames, f => f.RightKnee, OxyColors.OrangeRed, maxTime);
            LeftAnkleAnglePlotModel = BuildSingleAnglePlot("左踝角度曲线", _angleFrames, f => f.LeftAnkle, OxyColors.MediumPurple, maxTime);
            RightAnkleAnglePlotModel = BuildSingleAnglePlot("右踝角度曲线", _angleFrames, f => f.RightAnkle, OxyColors.DarkCyan, maxTime);
            PelvisAnglePlotModel = BuildSingleAnglePlot("骨盆角度曲线", _angleFrames, f => f.Pelvis, OxyColor.Parse("#40385F"), maxTime);
            TrunkAnglePlotModel = BuildSingleAnglePlot("躯干角度曲线", _angleFrames, f => f.Trunk, OxyColor.Parse("#F2306A"), maxTime);
            VideoKneeAnglePlotModel = BuildDualAnglePlot("膝关节角度曲线", "左膝", "右膝", _angleFrames, f => f.LeftKnee, f => f.RightKnee, OxyColors.ForestGreen, OxyColor.Parse("#F2306A"), maxTime);
            VideoHipAnglePlotModel = BuildDualAnglePlot("髋关节角度曲线", "左髋", "右髋", _angleFrames, f => f.LeftHip, f => f.RightHip, OxyColors.SteelBlue, OxyColors.OrangeRed, maxTime);
            VideoAnkleAnglePlotModel = BuildDualAnglePlot("踝关节角度曲线", "左踝", "右踝", _angleFrames, f => f.LeftAnkle, f => f.RightAnkle, OxyColors.MediumPurple, OxyColors.DarkCyan, maxTime);
            VideoPelvisAnglePlotModel = BuildSinglePlaybackAnglePlot("骨盆角度曲线", "骨盆", _angleFrames, f => f.Pelvis, OxyColor.Parse("#40385F"), maxTime);
            VideoTrunkAnglePlotModel = BuildSinglePlaybackAnglePlot("躯干角度曲线", "躯干", _angleFrames, f => f.Trunk, OxyColor.Parse("#F2306A"), maxTime);
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
        var path = AnalysisResult?.AnnotatedVideoPath;
        if (!string.IsNullOrWhiteSpace(path)
            && File.Exists(path)
            && !(IsSingleViewOutput(AnalysisResult?.OutputDirectory) && Path.GetFileName(path).Equals("analysis_preview.mp4", StringComparison.OrdinalIgnoreCase)))
        {
            return path;
        }

        var outputDirectory = AnalysisResult?.OutputDirectory;
        if (!IsSingleViewOutput(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            return path;
        }

        return Directory.GetFiles(outputDirectory, "*.mp4", SearchOption.AllDirectories)
            .Where(file => Path.GetFileName(file).Contains("Sports2D", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(file => file.Contains("side", StringComparison.OrdinalIgnoreCase) || file.Contains("侧面", StringComparison.OrdinalIgnoreCase))
            ?? Directory.GetFiles(outputDirectory, "*.mp4", SearchOption.AllDirectories)
                .FirstOrDefault(file => Path.GetFileName(file).Contains("Sports2D", StringComparison.OrdinalIgnoreCase));
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
        if (gaitCycle?["cycles"] is not JsonArray cycles || cycles.Count == 0)
        {
            return;
        }

        var leftHeelStrikeFrames = ReadIntArray(gaitEvents, "left_heel_strike_frames").ToHashSet();
        var rightHeelStrikeFrames = ReadIntArray(gaitEvents, "right_heel_strike_frames").ToHashSet();
        var safeFps = fps > 0 ? fps : 30d;

        foreach (var node in cycles)
        {
            if (node is not JsonObject cycle)
            {
                continue;
            }

            var cycleId = ReadInt(cycle, "cycle_id");
            var startFrame = ReadInt(cycle, "start_frame");
            var endFrame = ReadInt(cycle, "end_frame");
            var duration = ReadDouble(cycle, "duration_sec");
            var side = "--";
            if (startFrame is int start)
            {
                if (leftHeelStrikeFrames.Contains(start))
                {
                    side = "左侧";
                }
                else if (rightHeelStrikeFrames.Contains(start))
                {
                    side = "右侧";
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

    private static double? EstimateValidFrameRatio(IReadOnlyCollection<AnalysisAngleFrame> angleFrames, double? fps, double? durationSec)
    {
        if (angleFrames.Count == 0 || fps is not > 0 || durationSec is not > 0)
        {
            return null;
        }

        var totalFrames = fps.Value * durationSec.Value;
        if (totalFrames <= 0)
        {
            return null;
        }

        return Math.Clamp(angleFrames.Count / totalFrames, 0d, 1d);
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

    private static string? BuildResultFileSummary(string? directory)
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
        return parts.Count > 0 ? $"已生成 {string.Join("、", parts)}" : null;
    }

    private static PlotModel BuildSingleAnglePlot(string title, List<AnalysisAngleFrame> frames, Func<AnalysisAngleFrame, double> valueSelector, OxyColor color, double maxTime)
    {
        var model = CreatePlotBase(title, "时间 (s)", "角度 (°)");
        model.Axes.OfType<LinearAxis>().First(axis => axis.Position == AxisPosition.Bottom).Maximum = Math.Max(1, maxTime);
        AddLineSeries(model, title.Replace("角度曲线", string.Empty, StringComparison.Ordinal), frames, valueSelector, color);
        ApplyValueAxisRange(model);
        return model;
    }

    private static PlotModel BuildDualAnglePlot(
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
        var model = CreatePlotBase($"{title}（{firstName} / {secondName}）", "时间 (s)", "角度 (°)", alignToPlaybackBar: true);
        model.Axes.OfType<LinearAxis>().First(axis => axis.Position == AxisPosition.Bottom).Maximum = Math.Max(1, maxTime);
        AddLineSeries(model, firstName, frames, firstSelector, firstColor);
        AddLineSeries(model, secondName, frames, secondSelector, secondColor);
        AddPlaybackCursor(model, 0);
        ApplyValueAxisRange(model);
        return model;
    }

    private static PlotModel BuildSinglePlaybackAnglePlot(
        string title,
        string seriesName,
        List<AnalysisAngleFrame> frames,
        Func<AnalysisAngleFrame, double> valueSelector,
        OxyColor color,
        double maxTime)
    {
        var model = CreatePlotBase(title, "时间 (s)", "角度 (°)", alignToPlaybackBar: true);
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

    private static PlotModel CreateEmptyPlot(string title, string message = "暂无曲线数据", bool alignToPlaybackBar = false)
    {
        var model = CreatePlotBase(title, "时间 (s)", "角度 (°)", alignToPlaybackBar);
        model.Subtitle = message;
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
        if (gaitCycle?["cycles"] is not JsonArray cycles || cycles.Count == 0)
        {
            return null;
        }

        var values = cycles
            .OfType<JsonObject>()
            .Select(cycle => ReadDouble(cycle, "duration_sec"))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();
        return values.Count == 0 ? null : values.Average();
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

    private static string FormatCount(int? value)
    {
        return value.HasValue ? $"{value.Value} 次" : "--";
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
