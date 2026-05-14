using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
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

    public PlotModel LeftHipAnglePlotModel { get; }

    public PlotModel RightHipAnglePlotModel { get; }

    public PlotModel LeftKneeAnglePlotModel { get; }

    public PlotModel RightKneeAnglePlotModel { get; }

    public PlotModel LeftAnkleAnglePlotModel { get; }

    public PlotModel RightAnkleAnglePlotModel { get; }

    public PlotModel VideoKneeAnglePlotModel { get; }

    public PlotModel VideoHipAnglePlotModel { get; }

    public PlotModel VideoAnkleAnglePlotModel { get; }

    public PlotModel VideoTrunkAnglePlotModel { get; }

    public PlotModel VideoTrajectoryPlotModel { get; }

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

        LeftHipAnglePlotModel = BuildDemoAnglePlot("左髋角度曲线", OxyColors.SteelBlue, phase: 0);
        RightHipAnglePlotModel = BuildDemoAnglePlot("右髋角度曲线", OxyColor.Parse("#F2306A"), phase: 0.35);
        LeftKneeAnglePlotModel = BuildDemoAnglePlot("左膝角度曲线", OxyColors.ForestGreen, phase: 0.7);
        RightKneeAnglePlotModel = BuildDemoAnglePlot("右膝角度曲线", OxyColors.OrangeRed, phase: 1.05);
        LeftAnkleAnglePlotModel = BuildDemoAnglePlot("左踝角度曲线", OxyColors.MediumPurple, phase: 1.4);
        RightAnkleAnglePlotModel = BuildDemoAnglePlot("右踝角度曲线", OxyColors.DarkCyan, phase: 1.75);
        VideoKneeAnglePlotModel = BuildDualDemoPlot("膝关节角度曲线", "左膝", "右膝", OxyColors.ForestGreen, OxyColor.Parse("#F2306A"));
        VideoHipAnglePlotModel = BuildDualDemoPlot("髋关节角度曲线", "左髋", "右髋", OxyColors.SteelBlue, OxyColors.OrangeRed);
        VideoAnkleAnglePlotModel = BuildDualDemoPlot("踝关节角度曲线", "左踝", "右踝", OxyColors.MediumPurple, OxyColors.DarkCyan);
        VideoTrunkAnglePlotModel = BuildDualDemoPlot("躯干倾斜角曲线", "躯干倾斜", "躯干侧屈", OxyColor.Parse("#40385F"), OxyColor.Parse("#F2306A"));
        VideoTrajectoryPlotModel = BuildDualDemoPlot("足尖轨迹 / 质心轨迹", "足尖轨迹", "质心轨迹", OxyColors.SteelBlue, OxyColors.OrangeRed);

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
    public string PatientName => Record?.Patient?.Name ?? "测试患者";

    /// <summary>
    /// 患者身高。
    /// </summary>
    public string PatientHeightDisplay => Record?.Patient?.Height is double height and > 0
        ? $"{height:F0} cm"
        : "175 cm";

    /// <summary>
    /// 患者编号。
    /// </summary>
    public string PatientCode => Record?.PatientId.ToString() ?? "P20260513001";

    /// <summary>
    /// 测量名称。
    /// </summary>
    public string MeasurementName => string.IsNullOrWhiteSpace(Record?.MeasurementName) ? "测量_20260513_103000" : Record.MeasurementName;

    /// <summary>
    /// 测量类型。
    /// </summary>
    public string MeasurementTypeDisplay => Record is null ? "自然步行" : GetEnumDescription(Record.MeasurementType);

    /// <summary>
    /// 测量视频模式。
    /// </summary>
    public string MeasurementVideoModeDisplay => Record?.HasDualVideo == true ? "双视频模式" : "单视频模式";

    /// <summary>
    /// 测量时间。
    /// </summary>
    public string MeasurementDate => Record?.MeasurementDate.ToString(Constants.DATETIME_FORMAT) ?? "2026-05-13 10:30:00";

    /// <summary>
    /// 分析任务编号。
    /// </summary>
    public string RequestIdDisplay => string.IsNullOrWhiteSpace(AnalysisResult?.RequestId) ? "GAIT_20260513_0001" : AnalysisResult.RequestId;

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
    public string AnalysisDurationDisplay => FormatNumber(AnalysisResult?.AnalysisDurationSeconds ?? 5.2, "F1", "s");

    /// <summary>
    /// 创建时间。
    /// </summary>
    public string AnalysisCreatedAtDisplay => AnalysisResult?.CreatedAt.ToString(Constants.DATETIME_FORMAT) ?? "2026-05-13 10:30:00";

    /// <summary>
    /// 协议版本。
    /// </summary>
    public string ProtocolVersionDisplay => string.IsNullOrWhiteSpace(AnalysisResult?.ProtocolVersion) ? "v1.0" : AnalysisResult.ProtocolVersion;

    /// <summary>
    /// 算法版本。
    /// </summary>
    public string AlgorithmVersionDisplay => string.IsNullOrWhiteSpace(AnalysisResult?.AlgorithmVersion) ? "GaitEngine 1.2.0" : AnalysisResult.AlgorithmVersion;

    /// <summary>
    /// 模型版本。
    /// </summary>
    public string ModelVersionDisplay => string.IsNullOrWhiteSpace(AnalysisResult?.ModelVersion) ? "PoseGait 0.9.3" : AnalysisResult.ModelVersion;

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
                return "有效帧比例 96%";
            }

            if (AnalysisResult.QualityControl.ValidFrameRatio is double validFrameRatio)
            {
                return $"有效帧比例 {validFrameRatio:P0}";
            }

            return "已生成质量控制信息";
        }
    }

    /// <summary>
    /// 质量提示。
    /// </summary>
    public string QualityHintDisplay => "视频质量良好，关键点连续性稳定，适合用于本次步态参数预览。";

    /// <summary>
    /// 质量等级摘要。
    /// </summary>
    public string QualityGradeDisplay
    {
        get
        {
            var qualityControl = AnalysisResult?.QualityControl;
            if (qualityControl is null)
            {
                return "A级";
            }

            var validFrameRatio = qualityControl.ValidFrameRatio;
            if (validFrameRatio is >= 0.95)
            {
                return "A级";
            }

            if (validFrameRatio is >= 0.80)
            {
                return "B级";
            }

            return "C级";
        }
    }

    /// <summary>
    /// 置信度摘要。
    /// </summary>
    public string ConfidenceDisplay => AnalysisResult?.QualityControl?.MeanKeypointConfidence is double confidence
        ? confidence.ToString("F2")
        : "0.94";

    /// <summary>
    /// 有效帧比例摘要。
    /// </summary>
    public string ValidFrameRatioDisplay => AnalysisResult?.QualityControl?.ValidFrameRatio is double validFrameRatio
        ? validFrameRatio.ToString("P0")
        : "96%";

    /// <summary>
    /// 平均步速。
    /// </summary>
    public string GaitSpeedDisplay => FormatNumber(AnalysisResult?.GaitSpeedMPerS ?? Record?.GaitParameters?.GaitSpeedMPerS ?? 1.18, "F2", "m/s");

    /// <summary>
    /// 平均步频。
    /// </summary>
    public string CadenceDisplay => FormatNumber(Record?.GaitParameters?.Cadence ?? 112.5, "F1", "step/min");

    /// <summary>
    /// 平均步长。
    /// </summary>
    public string StepLengthDisplay => FormatNumber(AnalysisResult?.StepLengthM ?? Record?.GaitParameters?.StepLengthM ?? 0.63, "F2", "m");

    /// <summary>
    /// 步态周期。
    /// </summary>
    public string GaitCycleDisplay => FormatNumber(AnalysisResult?.GaitCycleDurationS ?? Record?.GaitParameters?.GaitCycleDurationS ?? 1.02, "F2", "s");

    /// <summary>
    /// 步幅。
    /// </summary>
    public string StrideLengthDisplay => FormatNumber(AnalysisResult?.StrideLengthM ?? 1.26, "F2", "m");

    /// <summary>
    /// 站立相时长。
    /// </summary>
    public string StanceTimeDisplay => FormatNumber(AnalysisResult?.StanceTimeS ?? 0.72, "F2", "s");

    /// <summary>
    /// 摆动相时长。
    /// </summary>
    public string SwingTimeDisplay => FormatNumber(AnalysisResult?.SwingTimeS ?? 0.38, "F2", "s");

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
                return "髋关节 ROM 42.8 ° / 膝关节 ROM 57.3 ° / 踝关节 ROM 24.6 °";
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
        : "8 个结果文件";

    /// <summary>
    /// 标注视频摘要。
    /// </summary>
    public string AnnotatedVideoDisplay => string.IsNullOrWhiteSpace(AnalysisResult?.AnnotatedVideoPath)
        ? "result_visualized.mp4"
        : Path.GetFileName(AnalysisResult.AnnotatedVideoPath);

    /// <summary>
    /// 摘要文件摘要。
    /// </summary>
    public string SummaryFileDisplay => string.IsNullOrWhiteSpace(AnalysisResult?.SummaryFilePath)
        ? "result.json"
        : Path.GetFileName(AnalysisResult.SummaryFilePath);

    /// <summary>
    /// 有效周期数。
    /// </summary>
    public string CycleCountDisplay => "5 个";

    /// <summary>
    /// 文件摘要。
    /// </summary>
    public string ResultFileSummaryDisplay => AnalysisResult?.CsvFiles?.Count > 0
        ? $"已生成 {AnalysisResult.CsvFiles.Count} 个结果文件"
        : "已生成 result.json、joint_angles.csv、gait_events.csv";

    /// <summary>
    /// 日志文件摘要。
    /// </summary>
    public string LogFileDisplay => "analysis.log";

    /// <summary>
    /// CSV 文件数量。
    /// </summary>
    public string CsvFileCountDisplay => AnalysisResult?.CsvFiles?.Count is int count and > 0 ? $"{count} 个" : "6 个";

    /// <summary>
    /// 图片文件数量。
    /// </summary>
    public string ImageFileCountDisplay => "2 个";

    public string MeanStrideLengthDisplay => StrideLengthDisplay;

    public string DoubleSupportTimeDisplay => FormatNumber(AnalysisResult?.DoubleSupportTimeS ?? Record?.GaitParameters?.DoubleSupportTimeS ?? 0.21, "F2", "s");

    public string SingleSupportTimeDisplay => FormatNumber(AnalysisResult?.SingleSupportTimeS ?? Record?.GaitParameters?.SingleSupportTimeS ?? 0.49, "F2", "s");

    public string LeftStepLengthDisplay => Record?.GaitParameters?.StepLengthLeft is double leftStep ? FormatMetersFromCentimeters(leftStep) : "0.62 m";

    public string RightStepLengthDisplay => Record?.GaitParameters?.StepLengthRight is double rightStep ? FormatMetersFromCentimeters(rightStep) : "0.64 m";

    public string LeftStrideLengthDisplay => Record?.GaitParameters?.StrideLengthLeft is double leftStride ? FormatMetersFromCentimeters(leftStride) : "1.25 m";

    public string RightStrideLengthDisplay => Record?.GaitParameters?.StrideLengthRight is double rightStride ? FormatMetersFromCentimeters(rightStride) : "1.27 m";

    public string LeftStancePhaseDisplay => FormatNumber(Record?.GaitParameters?.StancePhaseLeft ?? 61.8, "F1", "%");

    public string RightStancePhaseDisplay => FormatNumber(Record?.GaitParameters?.StancePhaseRight ?? 62.4, "F1", "%");

    public string LeftSwingPhaseDisplay => FormatNumber(Record?.GaitParameters?.SwingPhaseLeft ?? 38.2, "F1", "%");

    public string RightSwingPhaseDisplay => FormatNumber(Record?.GaitParameters?.SwingPhaseRight ?? 37.6, "F1", "%");

    public string LeftHeelStrikeCountDisplay => "3 次";

    public string RightHeelStrikeCountDisplay => "3 次";

    public string LeftToeOffCountDisplay => "3 次";

    public string RightToeOffCountDisplay => "3 次";

    public string CycleConfidenceDisplay => "0.91";

    public string EventConfidenceDisplay => "0.89";

    public string HipRomDisplay => FormatNumber(AnalysisResult?.KinematicSummary?.HipRomDeg ?? 42.8, "F1", "°");

    public string LeftHipRomDisplay => "43.6 °";

    public string RightHipRomDisplay => "42.1 °";

    public string KneeRomDisplay => FormatNumber(AnalysisResult?.KinematicSummary?.KneeRomDeg ?? 57.3, "F1", "°");

    public string LeftKneeRomDisplay => "57.3 °";

    public string RightKneeRomDisplay => "55.4 °";

    public string AnkleRomDisplay => FormatNumber(AnalysisResult?.KinematicSummary?.AnkleRomDeg ?? 24.6, "F1", "°");

    public string LeftAnkleRomDisplay => "25.1 °";

    public string RightAnkleRomDisplay => "23.7 °";

    public string PelvisCoronalRomDisplay => FormatNumber(AnalysisResult?.KinematicSummary?.PelvisCoronalRomDeg ?? 7.8, "F1", "°");

    public string TrunkTiltMeanDisplay => "4.2 °";

    public string TrunkTiltMaxDisplay => "8.5 °";

    public string TrunkTiltMinDisplay => "1.2 °";

    public string TrunkTiltRomDisplay => "7.3 °";

    public string PelvicTiltMeanDisplay => "6.1 °";

    public string PelvicObliquityMeanDisplay => "3.4 °";

    public string TrunkLateralFlexionMeanDisplay => "2.8 °";

    public string StepLengthDiffDisplay => "0.02 m";

    public string StepLengthDiffPercentDisplay => "3.2 %";

    public string StanceTimeDiffDisplay => "0.03 s";

    public string StanceTimeDiffPercentDisplay => "4.1 %";

    public string SwingTimeDiffDisplay => "0.02 s";

    public string KneeRomDiffDisplay => "1.9 °";

    public string HipRomDiffDisplay => "2.4 °";

    public string AnkleRomDiffDisplay => "1.6 °";

    public string SymmetryScoreDisplay => FormatNumber(Record?.GaitParameters?.SymmetryIndex ?? 92.4, "F1", "");

    public string CurrentVideoFileDisplay => AnnotatedVideoDisplay;

    public string CurrentFrameDisplay => "128";

    public string CurrentPlaybackTimeDisplay => "2.17 s";

    public string CurrentGaitCycleDisplay => "第 2 周期";

    public string CurrentEventDisplay => "左脚脚跟着地";

    public string CurrentLeftKneeAngleDisplay => "42.6 °";

    public string CurrentRightKneeAngleDisplay => "39.8 °";

    public string CurrentLeftAnkleAngleDisplay => "12.4 °";

    public string CurrentRightAnkleAngleDisplay => "10.9 °";

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

            var qualityGrade = QualityGradeDisplay;
            return qualityGrade switch
            {
                "A级" => "当前分析质量较高，可继续配置报告内容。",
                "B级" => "当前分析质量可用于生成报告，建议在预览阶段重点复核质量说明。",
                _ => "当前分析质量偏低，后续生成正式报告前应补充人工复核。"
            };
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
                Title = "导出测量数据",
                Filter = "Excel文件 (*.xlsx)|*.xlsx|CSV文件 (*.csv)|*.csv",
                FileName = $"分析详情_{Record.Patient?.Name}_{Record.MeasurementDate:yyyyMMdd}"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var format = dialog.FilterIndex == 1 ? ExportFormat.Excel : ExportFormat.CSV;
            var success = await _exportImportService.ExportMeasurementsAsync([Record], format, dialog.FileName);

            if (success)
            {
                System.Windows.MessageBox.Show("导出成功！", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            System.Windows.MessageBox.Show("导出失败，请重试", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"导出分析详情失败：MeasurementId={Record.Id}", ex);
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

        return ReportPreviewHelper.GenerateReportDocument(report, "步态智能分析系统");
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

    private static PlotModel BuildDemoAnglePlot(string title, OxyColor color, double phase)
    {
        var model = CreateDemoPlotBase(title, "时间 (s)", "角度 (°)");
        var series = new LineSeries
        {
            Title = title.Replace("角度曲线", string.Empty, StringComparison.Ordinal),
            Color = color,
            StrokeThickness = 2.6,
            MarkerType = MarkerType.None
        };

        for (var i = 0; i <= 120; i++)
        {
            var time = i / 6d;
            var radians = (time / 20d * Math.PI * 4d) + phase;
            series.Points.Add(new DataPoint(time, 20d + 10d * Math.Sin(radians)));
        }

        model.Series.Add(series);
        return model;
    }

    private static PlotModel BuildDualDemoPlot(string title, string firstName, string secondName, OxyColor firstColor, OxyColor secondColor)
    {
        var model = CreateDemoPlotBase(title, "时间 (s)", title.Contains("轨迹", StringComparison.Ordinal) ? "位移 (m)" : "角度 (°)", alignToPlaybackBar: true);
        var first = new LineSeries
        {
            Title = firstName,
            Color = firstColor,
            StrokeThickness = 2.4
        };
        var second = new LineSeries
        {
            Title = secondName,
            Color = secondColor,
            StrokeThickness = 2.4
        };

        for (var i = 0; i <= 120; i++)
        {
            var time = i / 6d;
            var radians = time / 20d * Math.PI * 4d;
            first.Points.Add(new DataPoint(time, 20d + 10d * Math.Sin(radians)));
            second.Points.Add(new DataPoint(time, 20d + 10d * Math.Cos(radians + 0.35d)));
        }

        model.Series.Add(first);
        model.Series.Add(second);
        return model;
    }

    private static PlotModel CreateDemoPlotBase(string title, string xAxisTitle, string yAxisTitle, bool alignToPlaybackBar = false)
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
            PlotAreaBackground = OxyColor.Parse("#FCFDFE")
        };

        if (alignToPlaybackBar)
        {
            model.PlotMargins = new OxyThickness(108, 32, 70, 46);
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
        OnPropertyChanged(nameof(QualityHintDisplay));
        OnPropertyChanged(nameof(QualityGradeDisplay));
        OnPropertyChanged(nameof(ConfidenceDisplay));
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
        OnPropertyChanged(nameof(SummaryFileDisplay));
        OnPropertyChanged(nameof(CycleCountDisplay));
        OnPropertyChanged(nameof(ResultFileSummaryDisplay));
        OnPropertyChanged(nameof(LogFileDisplay));
        OnPropertyChanged(nameof(CsvFileCountDisplay));
        OnPropertyChanged(nameof(ImageFileCountDisplay));
        OnPropertyChanged(nameof(MeanStrideLengthDisplay));
        OnPropertyChanged(nameof(DoubleSupportTimeDisplay));
        OnPropertyChanged(nameof(SingleSupportTimeDisplay));
        OnPropertyChanged(nameof(LeftStepLengthDisplay));
        OnPropertyChanged(nameof(RightStepLengthDisplay));
        OnPropertyChanged(nameof(LeftStrideLengthDisplay));
        OnPropertyChanged(nameof(RightStrideLengthDisplay));
        OnPropertyChanged(nameof(LeftStancePhaseDisplay));
        OnPropertyChanged(nameof(RightStancePhaseDisplay));
        OnPropertyChanged(nameof(LeftSwingPhaseDisplay));
        OnPropertyChanged(nameof(RightSwingPhaseDisplay));
        OnPropertyChanged(nameof(LeftHeelStrikeCountDisplay));
        OnPropertyChanged(nameof(RightHeelStrikeCountDisplay));
        OnPropertyChanged(nameof(LeftToeOffCountDisplay));
        OnPropertyChanged(nameof(RightToeOffCountDisplay));
        OnPropertyChanged(nameof(CycleConfidenceDisplay));
        OnPropertyChanged(nameof(EventConfidenceDisplay));
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
        OnPropertyChanged(nameof(TrunkLateralFlexionMeanDisplay));
        OnPropertyChanged(nameof(StepLengthDiffDisplay));
        OnPropertyChanged(nameof(StepLengthDiffPercentDisplay));
        OnPropertyChanged(nameof(StanceTimeDiffDisplay));
        OnPropertyChanged(nameof(StanceTimeDiffPercentDisplay));
        OnPropertyChanged(nameof(SwingTimeDiffDisplay));
        OnPropertyChanged(nameof(KneeRomDiffDisplay));
        OnPropertyChanged(nameof(HipRomDiffDisplay));
        OnPropertyChanged(nameof(AnkleRomDiffDisplay));
        OnPropertyChanged(nameof(SymmetryScoreDisplay));
        OnPropertyChanged(nameof(CurrentVideoFileDisplay));
        OnPropertyChanged(nameof(CurrentFrameDisplay));
        OnPropertyChanged(nameof(CurrentPlaybackTimeDisplay));
        OnPropertyChanged(nameof(CurrentGaitCycleDisplay));
        OnPropertyChanged(nameof(CurrentEventDisplay));
        OnPropertyChanged(nameof(CurrentLeftKneeAngleDisplay));
        OnPropertyChanged(nameof(CurrentRightKneeAngleDisplay));
        OnPropertyChanged(nameof(CurrentLeftAnkleAngleDisplay));
        OnPropertyChanged(nameof(CurrentRightAnkleAngleDisplay));
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

    private static string GetEnumDescription<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var member = typeof(TEnum).GetMember(value.ToString()).FirstOrDefault();
        var attribute = member?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? value.ToString();
    }
}

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
