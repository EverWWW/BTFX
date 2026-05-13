using System.Windows;
using System.Windows.Documents;
using System.ComponentModel;
using BTFX.Common;
using BTFX.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTFX.ViewModels;

/// <summary>
/// 报告预览对话框视图模型。
/// </summary>
public partial class ReportPreviewDialogViewModel : ObservableObject
{
    private Report? _report;
    private FlowDocument? _previewDocument;
    private string _previewStatus = GetResourceString("AnalysisDetail.ReportPreview.Status.NotLoaded", "尚未加载报告预览。");

    /// <summary>
    /// 当前报告。
    /// </summary>
    public Report? Report
    {
        get => _report;
        private set => SetProperty(ref _report, value);
    }

    /// <summary>
    /// 预览文档。
    /// </summary>
    public FlowDocument? PreviewDocument
    {
        get => _previewDocument;
        private set => SetProperty(ref _previewDocument, value);
    }

    /// <summary>
    /// 预览状态。
    /// </summary>
    public string PreviewStatus
    {
        get => _previewStatus;
        private set => SetProperty(ref _previewStatus, value);
    }

    /// <summary>
    /// 报告标题。
    /// </summary>
    public string ReportTitle => string.IsNullOrWhiteSpace(Report?.Title) ? "--" : Report.Title;

    /// <summary>
    /// 报告编号。
    /// </summary>
    public string ReportNumber => string.IsNullOrWhiteSpace(Report?.ReportNumber) ? "--" : Report.ReportNumber;

    /// <summary>
    /// 预览来源。
    /// </summary>
    public string PreviewSource => Report?.AnalysisResultId is int analysisResultId
        ? string.Format(GetResourceString("AnalysisDetail.ReportPreview.SourceValue", "AnalysisResult #{0}"), analysisResultId)
        : GetResourceString("AnalysisDetail.ReportPreview.EmptyValue", "--");

    /// <summary>
    /// 患者姓名。
    /// </summary>
    public string PatientNameDisplay => !string.IsNullOrWhiteSpace(Report?.Patient?.Name)
        ? Report.Patient.Name
        : Report?.PatientId > 0 ? $"患者 #{Report.PatientId}" : GetResourceString("AnalysisDetail.ReportPreview.EmptyValue", "--");

    /// <summary>
    /// 测量类型。
    /// </summary>
    public string MeasurementTypeDisplay => Report?.MeasurementRecord is null
        ? GetResourceString("AnalysisDetail.ReportPreview.EmptyValue", "--")
        : GetEnumDescription(Report.MeasurementRecord.MeasurementType);

    /// <summary>
    /// 测量时间。
    /// </summary>
    public string MeasurementDateDisplay => Report?.MeasurementRecord?.MeasurementDate.ToString(Constants.DATETIME_FORMAT)
        ?? Report?.ReportDate.ToString(Constants.DATETIME_FORMAT)
        ?? GetResourceString("AnalysisDetail.ReportPreview.EmptyValue", "--");

    /// <summary>
    /// 视频模式。
    /// </summary>
    public string VideoModeDisplay
    {
        get
        {
            var record = Report?.MeasurementRecord;
            if (record is null)
            {
                return GetResourceString("AnalysisDetail.ReportPreview.EmptyValue", "--");
            }

            return record.HasDualVideo ? "双视频模式" : record.HasSideVideo || record.HasFrontVideo ? "单视频模式" : "--";
        }
    }

    /// <summary>
    /// 报告状态。
    /// </summary>
    public string ReportStatusDisplay => Report is null
        ? GetResourceString("AnalysisDetail.ReportPreview.EmptyValue", "--")
        : GetEnumDescription(Report.Status);

    /// <summary>
    /// 当前预览模式。
    /// </summary>
    public string PreviewModeDisplay => "FlowDocument 预览";

    /// <summary>
    /// 当前支持的导出格式。
    /// </summary>
    public string ExportFormatDisplay => "PDF";

    /// <summary>
    /// 包含项摘要。
    /// </summary>
    public string IncludedSectionsSummary
    {
        get
        {
            var sections = BuildIncludedSections();
            return sections.Count > 0
                ? string.Join(GetResourceString("AnalysisDetail.ReportPreview.Sections.Separator", "、"), sections)
                : GetResourceString("AnalysisDetail.ReportPreview.Sections.NoneSelected", "未选择包含项");
        }
    }

    /// <summary>
    /// 包含项标签。
    /// </summary>
    public IReadOnlyList<string> IncludedSectionTags => BuildIncludedSections();

    /// <summary>
    /// 最近更新时间。
    /// </summary>
    public string UpdatedAtDisplay => Report?.UpdatedAt.ToString(Constants.DATETIME_FORMAT)
        ?? GetResourceString("AnalysisDetail.ReportPreview.EmptyValue", "--");

    /// <summary>
    /// 带标签的最近更新时间。
    /// </summary>
    public string UpdatedAtDisplayWithLabel => string.Concat(
        GetResourceString("AnalysisDetail.ReportPreview.UpdatedAt", "最近更新时间："),
        UpdatedAtDisplay);

    public bool IncludeBasicInfo => true;

    public bool IncludeSpatiotemporalParameters => ParseReportOptions()?.IncludeSpatiotemporalParameters ?? false;

    public bool IncludeKinematicSummary => ParseReportOptions()?.IncludeKinematicSummary ?? false;

    public bool IncludeQualityControl => ParseReportOptions()?.IncludeQualityControl ?? false;

    public bool IncludeResultFiles => ParseReportOptions()?.IncludeResultFiles ?? false;

    public bool IncludeGaitCycleSummary => IncludeSpatiotemporalParameters;

    public bool IncludeSymmetryMetrics => false;

    public bool IncludeJointAngleCurve => false;

    public bool IncludeVideoKeyFrames => false;

    public bool IncludeAnalysisConclusion => true;

    public bool IncludeRemarks => !string.IsNullOrWhiteSpace(Report?.DoctorOpinion);

    /// <summary>
    /// 关闭请求事件。
    /// </summary>
    public event Action<ReportPreviewDialogResult>? CloseRequested;

    /// <summary>
    /// 初始化预览内容。
    /// </summary>
    /// <param name="report">报告草稿。</param>
    /// <param name="previewDocument">预览文档。</param>
    public Task InitializeAsync(Report report, FlowDocument previewDocument)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(previewDocument);

        Report = report;
        PreviewDocument = previewDocument;
        PreviewStatus = GetResourceString(
            "AnalysisDetail.ReportPreview.Status.Ready",
            "已生成当前草稿的报告预览，可在返回配置后继续调整。");

        OnPropertyChanged(nameof(ReportTitle));
        OnPropertyChanged(nameof(ReportNumber));
        OnPropertyChanged(nameof(PreviewSource));
        OnPropertyChanged(nameof(PatientNameDisplay));
        OnPropertyChanged(nameof(MeasurementTypeDisplay));
        OnPropertyChanged(nameof(VideoModeDisplay));
        OnPropertyChanged(nameof(MeasurementDateDisplay));
        OnPropertyChanged(nameof(ReportStatusDisplay));
        OnPropertyChanged(nameof(PreviewModeDisplay));
        OnPropertyChanged(nameof(ExportFormatDisplay));
        OnPropertyChanged(nameof(IncludedSectionsSummary));
        OnPropertyChanged(nameof(IncludedSectionTags));
        OnPropertyChanged(nameof(UpdatedAtDisplay));
        OnPropertyChanged(nameof(UpdatedAtDisplayWithLabel));
        OnPropertyChanged(nameof(IncludeBasicInfo));
        OnPropertyChanged(nameof(IncludeSpatiotemporalParameters));
        OnPropertyChanged(nameof(IncludeKinematicSummary));
        OnPropertyChanged(nameof(IncludeQualityControl));
        OnPropertyChanged(nameof(IncludeResultFiles));
        OnPropertyChanged(nameof(IncludeGaitCycleSummary));
        OnPropertyChanged(nameof(IncludeSymmetryMetrics));
        OnPropertyChanged(nameof(IncludeJointAngleCurve));
        OnPropertyChanged(nameof(IncludeVideoKeyFrames));
        OnPropertyChanged(nameof(IncludeAnalysisConclusion));
        OnPropertyChanged(nameof(IncludeRemarks));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 关闭命令。
    /// </summary>
    [RelayCommand]
    private void BackToConfig()
    {
        CloseRequested?.Invoke(ReportPreviewDialogResult.BackToConfig);
    }

    /// <summary>
    /// 关闭命令。
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(ReportPreviewDialogResult.ClosePreview);
    }

    private static string GetResourceString(string key, string fallback)
    {
        return Application.Current.TryFindResource(key) as string ?? fallback;
    }

    private ReportDraftOptions? ParseReportOptions()
    {
        if (string.IsNullOrWhiteSpace(Report?.ReportOptionsJson))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<ReportDraftOptions>(Report.ReportOptionsJson);
        }
        catch
        {
            return null;
        }
    }

    private List<string> BuildIncludedSections()
    {
        if (string.IsNullOrWhiteSpace(Report?.ReportOptionsJson))
        {
            return [GetResourceString("AnalysisDetail.ReportPreview.Sections.EmptySummary", "未提供配置摘要")];
        }

        var options = ParseReportOptions();
        if (options is null)
        {
            return [GetResourceString("AnalysisDetail.ReportPreview.Sections.ParseFailed", "配置摘要解析失败")];
        }

        var sections = new List<string>();
        if (options.IncludeSpatiotemporalParameters)
        {
            sections.Add(GetResourceString("AnalysisDetail.ReportPreview.Sections.Spatiotemporal", "时空参数"));
        }

        if (options.IncludeKinematicSummary)
        {
            sections.Add(GetResourceString("AnalysisDetail.ReportPreview.Sections.KinematicSummary", "运动学摘要"));
        }

        if (options.IncludeQualityControl)
        {
            sections.Add(GetResourceString("AnalysisDetail.ReportPreview.Sections.QualityControl", "质量控制"));
        }

        if (options.IncludeResultFiles)
        {
            sections.Add(GetResourceString("AnalysisDetail.ReportPreview.Sections.ResultFiles", "结果文件摘要"));
        }

        return sections.Count > 0
            ? sections
            : [GetResourceString("AnalysisDetail.ReportPreview.Sections.NoneSelected", "未选择包含项")];
    }

    private static string GetEnumDescription<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var member = typeof(TEnum).GetMember(value.ToString()).FirstOrDefault();
        var attribute = member?.GetCustomAttributes(typeof(DescriptionAttribute), false).OfType<DescriptionAttribute>().FirstOrDefault();
        return attribute?.Description ?? value.ToString();
    }
}

/// <summary>
/// 报告预览对话框关闭结果。
/// </summary>
public enum ReportPreviewDialogResult
{
    /// <summary>
    /// 返回报告配置。
    /// </summary>
    BackToConfig,

    /// <summary>
    /// 关闭预览。
    /// </summary>
    ClosePreview
}
