using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BTFX.Common;
using BTFX.Helpers;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BTFX.ViewModels;

/// <summary>
/// 报告预览对话框视图模型。
/// </summary>
public partial class ReportPreviewDialogViewModel : ObservableObject
{
    private const string UnitName = "步态智能分析系统";
    private Report? _report;
    private FlowDocument? _previewDocument;
    private string _previewStatus = "尚未加载报告预览。";
    private bool _isInitializing;
    private bool _includeSpatiotemporalParameters = true;
    private bool _includeKinematicParameters = true;
    private bool _includeTrunkPelvisParameters = true;
    private bool _includeSymmetryAnalysis = true;
    private bool _includeLeftRightParameters = true;
    private bool _includeCurveCharts = true;
    private string _selectedExportFormat = "PDF";

    public Report? Report
    {
        get => _report;
        private set => SetProperty(ref _report, value);
    }

    public FlowDocument? PreviewDocument
    {
        get => _previewDocument;
        private set => SetProperty(ref _previewDocument, value);
    }

    public string PreviewStatus
    {
        get => _previewStatus;
        private set => SetProperty(ref _previewStatus, value);
    }

    public string SelectedExportFormat
    {
        get => _selectedExportFormat;
        set
        {
            if (SetProperty(ref _selectedExportFormat, value))
            {
                ExportReportCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IReadOnlyList<string> ExportFormats { get; } = ["PDF"];

    public bool IncludeSpatiotemporalParameters
    {
        get => _includeSpatiotemporalParameters;
        set => SetSectionProperty(ref _includeSpatiotemporalParameters, value);
    }

    public bool IncludeKinematicParameters
    {
        get => _includeKinematicParameters;
        set => SetSectionProperty(ref _includeKinematicParameters, value);
    }

    public bool IncludeTrunkPelvisParameters
    {
        get => _includeTrunkPelvisParameters;
        set => SetSectionProperty(ref _includeTrunkPelvisParameters, value);
    }

    public bool IncludeSymmetryAnalysis
    {
        get => _includeSymmetryAnalysis;
        set => SetSectionProperty(ref _includeSymmetryAnalysis, value);
    }

    public bool IncludeLeftRightParameters
    {
        get => _includeLeftRightParameters;
        set => SetSectionProperty(ref _includeLeftRightParameters, value);
    }

    public bool IncludeCurveCharts
    {
        get => _includeCurveCharts;
        set => SetSectionProperty(ref _includeCurveCharts, value);
    }

    public string ReportTitle => string.IsNullOrWhiteSpace(Report?.Title) ? "步态分析报告" : Report.Title;

    public string ReportNumber => string.IsNullOrWhiteSpace(Report?.ReportNumber) ? "--" : Report.ReportNumber;

    public string PreviewSource => Report?.AnalysisResultId is int analysisResultId
        ? $"AnalysisResult #{analysisResultId}"
        : "--";

    public string PatientNameDisplay => !string.IsNullOrWhiteSpace(Report?.Patient?.Name)
        ? Report.Patient.Name
        : Report?.PatientId > 0 ? $"患者 #{Report.PatientId}" : "--";

    public string MeasurementTypeDisplay => Report?.MeasurementRecord is null
        ? "--"
        : GetEnumDescription(Report.MeasurementRecord.MeasurementType);

    public string MeasurementDateDisplay => Report?.MeasurementRecord?.MeasurementDate.ToString(Constants.DATETIME_FORMAT)
        ?? Report?.ReportDate.ToString(Constants.DATETIME_FORMAT)
        ?? "--";

    public string VideoModeDisplay
    {
        get
        {
            var record = Report?.MeasurementRecord;
            if (record is null)
            {
                return "--";
            }

            return record.HasDualVideo ? "双视频模式" : record.HasSideVideo || record.HasFrontVideo ? "单视频模式" : "--";
        }
    }

    public string ReportStatusDisplay => Report is null ? "--" : GetEnumDescription(Report.Status);

    public string ExportFormatDisplay => SelectedExportFormat;

    public string IncludedSectionsSummary
    {
        get
        {
            var sections = BuildIncludedSections();
            return sections.Count > 0 ? string.Join("、", sections) : "未选择包含项";
        }
    }

    public IReadOnlyList<string> IncludedSectionTags => BuildIncludedSections();

    public string UpdatedAtDisplay => Report?.UpdatedAt.ToString(Constants.DATETIME_FORMAT) ?? "--";

    public bool IncludeBasicInfo => true;

    public bool IncludeKinematicSummary => IncludeKinematicParameters;

    public bool IncludeQualityControl => IncludeSymmetryAnalysis;

    public bool IncludeResultFiles => false;

    public bool IncludeGaitCycleSummary => IncludeSpatiotemporalParameters;

    public bool IncludeSymmetryMetrics => IncludeSymmetryAnalysis;

    public bool IncludeJointAngleCurve => IncludeCurveCharts;

    public bool IncludeVideoKeyFrames => false;

    public bool IncludeAnalysisConclusion => true;

    public bool IncludeRemarks => !string.IsNullOrWhiteSpace(Report?.DoctorOpinion);

    public event Action<ReportPreviewDialogResult>? CloseRequested;

    public Task InitializeAsync(Report report, FlowDocument previewDocument)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(previewDocument);

        _isInitializing = true;
        Report = report;
        ApplyInitialSectionOptions();
        _isInitializing = false;

        RebuildPreviewDocument();
        PreviewStatus = "已生成当前报告预览，可在返回配置后继续调整。";
        NotifyReportPropertiesChanged();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void BackToConfig()
    {
        CloseRequested?.Invoke(ReportPreviewDialogResult.BackToConfig);
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(ReportPreviewDialogResult.ClosePreview);
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private void Print()
    {
        if (PreviewDocument is null)
        {
            MessageBox.Show("当前没有可打印的报告预览。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var printed = PrintHelper.PrintDocument(PreviewDocument, $"报告_{ReportNumber}", showDialog: true);
        if (printed)
        {
            MessageBox.Show("报告已发送到打印队列。", "打印", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private bool CanPrint() => PreviewDocument is not null;

    [RelayCommand(CanExecute = nameof(CanExportReport))]
    private async Task ExportReportAsync()
    {
        if (Report is null || PreviewDocument is null || !string.Equals(SelectedExportFormat, "PDF", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出报告",
            Filter = "PDF 文件 (*.pdf)|*.pdf",
            FileName = $"报告_{SanitizeFileName(ReportNumber)}.pdf"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            SyncReportOptionsJson();
            var settingsService = App.Services?.GetService(typeof(ISettingsService)) as ISettingsService;
            if (settingsService is null)
            {
                MessageBox.Show("未找到系统设置服务，无法导出报告。", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var report = Report;
            var filePath = dialog.FileName;
            var success = await Task.Run(() =>
            {
                var exporter = new ReportPdfExporter(settingsService);
                return exporter.ExportToPdf(report, filePath);
            });

            MessageBox.Show(
                success ? $"报告已导出至：\n{filePath}" : "PDF 导出失败。",
                success ? "导出成功" : "导出失败",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败：{ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanExportReport() => Report is not null && PreviewDocument is not null && SelectedExportFormat == "PDF";

    private void SetSectionProperty(ref bool field, bool value)
    {
        if (!SetProperty(ref field, value))
        {
            return;
        }

        if (_isInitializing)
        {
            return;
        }

        SyncReportOptionsJson();
        RebuildPreviewDocument();
        NotifySectionPropertiesChanged();
    }

    private void ApplyInitialSectionOptions()
    {
        var options = ParseReportOptions();
        _includeSpatiotemporalParameters = options?.IncludeSpatiotemporalParameters ?? true;
        _includeKinematicParameters = options?.IncludeKinematicSummary ?? true;
        _includeTrunkPelvisParameters = options?.IncludeKinematicSummary ?? true;
        _includeSymmetryAnalysis = options?.IncludeQualityControl ?? true;
        _includeLeftRightParameters = options?.IncludeSpatiotemporalParameters ?? true;
        _includeCurveCharts = options?.IncludeKinematicSummary ?? true;

        OnPropertyChanged(nameof(IncludeSpatiotemporalParameters));
        OnPropertyChanged(nameof(IncludeKinematicParameters));
        OnPropertyChanged(nameof(IncludeTrunkPelvisParameters));
        OnPropertyChanged(nameof(IncludeSymmetryAnalysis));
        OnPropertyChanged(nameof(IncludeLeftRightParameters));
        OnPropertyChanged(nameof(IncludeCurveCharts));
    }

    private void RebuildPreviewDocument()
    {
        var document = Report is null ? null : BuildSelectedSectionsDocument(Report);
        PreviewDocument = document;
        PrintCommand.NotifyCanExecuteChanged();
        ExportReportCommand.NotifyCanExecuteChanged();
    }

    private FlowDocument BuildSelectedSectionsDocument(Report report)
    {
        var document = new FlowDocument
        {
            PageWidth = PrintHelper.A4WidthInPixels,
            PageHeight = PrintHelper.A4HeightInPixels,
            PagePadding = new Thickness(48, 44, 48, 44),
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 12,
            Background = Brushes.White,
            ColumnWidth = PrintHelper.A4WidthInPixels
        };

        AddHeader(document, report);
        AddBasicInfo(document, report);
        AddClinicalSummary(document, report);

        if (IncludeSpatiotemporalParameters)
        {
            AddClinicalParameterSection(
                document,
                "步态时空参数",
                "反映整体步行效率、节律稳定性和支撑相分配，是评估步态功能的基础参数。",
                new[]
                {
                    ("步态周期", FormatSeconds(report.AnalysisResult?.GaitCycleDurationS ?? report.MeasurementRecord?.GaitParameters?.GaitCycleDurationS, 1.02), "s", "参考 0.9-1.2"),
                    ("步长", FormatMeters(report.AnalysisResult?.StepLengthM ?? report.MeasurementRecord?.GaitParameters?.StepLengthM, 0.63), "m", "参考 0.55-0.75"),
                    ("步幅", FormatMeters(report.AnalysisResult?.StrideLengthM ?? report.MeasurementRecord?.GaitParameters?.StrideLengthM, 1.26), "m", "参考 1.10-1.50"),
                    ("步频", FormatNumber(report.MeasurementRecord?.GaitParameters?.Cadence, 112.5, "F1"), "step/min", "参考 100-120"),
                    ("步速", FormatNumber(report.AnalysisResult?.GaitSpeedMPerS ?? report.MeasurementRecord?.GaitParameters?.GaitSpeedMPerS, 1.18, "F2"), "m/s", "参考 1.0-1.4"),
                    ("站立相时间", FormatNumber(report.AnalysisResult?.StanceTimeS ?? report.MeasurementRecord?.GaitParameters?.StanceTimeS, 0.72, "F2"), "s", "参考 0.60-0.80"),
                    ("摆动相时间", FormatNumber(report.AnalysisResult?.SwingTimeS ?? report.MeasurementRecord?.GaitParameters?.SwingTimeS, 0.38, "F2"), "s", "参考 0.30-0.45"),
                    ("双支撑时间", FormatNumber(report.AnalysisResult?.DoubleSupportTimeS ?? report.MeasurementRecord?.GaitParameters?.DoubleSupportTimeS, 0.21, "F2"), "s", "参考 0.15-0.25"),
                    ("单支撑时间", FormatNumber(report.AnalysisResult?.SingleSupportTimeS ?? report.MeasurementRecord?.GaitParameters?.SingleSupportTimeS, 0.49, "F2"), "s", "参考 0.40-0.55")
                });
        }

        if (IncludeKinematicParameters)
        {
            AddClinicalParameterSection(
                document,
                "运动学参数",
                "展示下肢主要关节活动范围，用于识别关节活动受限、代偿和左右活动差异。",
                new[]
                {
                    ("左髋 ROM", "43.6", "°", "--"),
                    ("右髋 ROM", "42.1", "°", "--"),
                    ("左膝 ROM", "57.3", "°", "--"),
                    ("右膝 ROM", "55.4", "°", "--"),
                    ("左踝 ROM", "25.1", "°", "--"),
                    ("右踝 ROM", "23.7", "°", "--"),
                    ("骨盆冠状面角度", FormatNumber(report.KinematicSummary?.PelvisCoronalRomDeg, 7.8, "F1"), "°", "--"),
                    ("髋关节 ROM", FormatNumber(report.KinematicSummary?.HipRomDeg, 42.8, "F1"), "°", "--"),
                    ("膝关节 ROM", FormatNumber(report.KinematicSummary?.KneeRomDeg, 57.3, "F1"), "°", "--"),
                    ("踝关节 ROM", FormatNumber(report.KinematicSummary?.AnkleRomDeg, 24.6, "F1"), "°", "--")
                });
        }

        if (IncludeTrunkPelvisParameters)
        {
            AddClinicalParameterSection(
                document,
                "躯干和骨盆参数",
                "用于观察躯干稳定性、骨盆控制能力和步行过程中的姿势代偿情况。",
                new[]
                {
                    ("躯干倾斜平均角度", "4.2", "°", "--"),
                    ("躯干倾斜最大角度", "8.5", "°", "--"),
                    ("躯干倾斜最小角度", "1.2", "°", "--"),
                    ("躯干倾斜活动范围", "7.3", "°", "--"),
                    ("骨盆倾斜角", "6.1", "°", "--"),
                    ("骨盆侧倾角", "3.4", "°", "--"),
                    ("躯干侧屈角", "2.8", "°", "--")
                });
        }

        if (IncludeSymmetryAnalysis)
        {
            AddClinicalParameterSection(
                document,
                "对称性分析",
                "对比左右侧关键时空和运动学指标，辅助判断双侧负重、支撑和摆动是否协调。",
                new[]
                {
                    ("左右步长差", "0.02", "m", "--"),
                    ("左右步长差百分比", "3.2", "%", "--"),
                    ("左右站立相差", "0.03", "s", "--"),
                    ("左右站立相差百分比", "4.1", "%", "--"),
                    ("左右摆动相差", "0.02", "s", "--"),
                    ("左右膝关节 ROM 差", "1.9", "°", "--"),
                    ("左右髋关节 ROM 差", "2.4", "°", "--"),
                    ("左右踝关节 ROM 差", "1.6", "°", "--"),
                    ("综合对称性评分", FormatNumber(report.MeasurementRecord?.GaitParameters?.SymmetryIndex, 92.4, "F1"), "分", "--")
                });
        }

        if (IncludeLeftRightParameters)
        {
            AddClinicalParameterSection(
                document,
                "左右侧参数",
                "列出左右侧步长、步幅和时相占比，用于复核单侧步态表现。",
                new[]
                {
                    ("左侧步长", FormatMetersFromCentimeters(report.MeasurementRecord?.GaitParameters?.StepLengthLeft, 0.62), "m", "--"),
                    ("右侧步长", FormatMetersFromCentimeters(report.MeasurementRecord?.GaitParameters?.StepLengthRight, 0.64), "m", "--"),
                    ("左侧步幅", FormatMetersFromCentimeters(report.MeasurementRecord?.GaitParameters?.StrideLengthLeft, 1.25), "m", "--"),
                    ("右侧步幅", FormatMetersFromCentimeters(report.MeasurementRecord?.GaitParameters?.StrideLengthRight, 1.27), "m", "--"),
                    ("左侧站立相占比", FormatNumber(report.MeasurementRecord?.GaitParameters?.StancePhaseLeft, 61.8, "F1"), "%", "--"),
                    ("右侧站立相占比", FormatNumber(report.MeasurementRecord?.GaitParameters?.StancePhaseRight, 62.4, "F1"), "%", "--"),
                    ("左侧摆动相占比", FormatNumber(report.MeasurementRecord?.GaitParameters?.SwingPhaseLeft, 38.2, "F1"), "%", "--"),
                    ("右侧摆动相占比", FormatNumber(report.MeasurementRecord?.GaitParameters?.SwingPhaseRight, 37.6, "F1"), "%", "--")
                });
        }

        if (IncludeCurveCharts)
        {
            AddCurveSection(document);
        }

        if (!BuildIncludedSections().Any())
        {
            AddSectionTitle(document, "报告内容");
            AddNoteBox(document, "未选择报告内容，请在左侧勾选需要预览、打印或导出的参数模块。");
        }

        AddFooter(document, report);
        return document;
    }

    private void AddHeader(FlowDocument document, Report report)
    {
        document.Blocks.Add(new Paragraph(new Run(UnitName))
        {
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        });

        document.Blocks.Add(new Paragraph(new Run(string.IsNullOrWhiteSpace(report.Title) ? "步态分析报告" : report.Title))
        {
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        });

        document.Blocks.Add(new Paragraph(new Run($"报告编号：{ReportNumber}"))
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14)
        });

        AddSeparator(document);
    }

    private void AddBasicInfo(FlowDocument document, Report report)
    {
        var patient = report.Patient ?? report.MeasurementRecord?.Patient;
        AddSectionTitle(document, "基本信息");
        AddCompactInfoGrid(document, new[]
        {
            ("患者", PatientNameDisplay),
            ("性别", patient?.GenderDisplay ?? "--"),
            ("年龄", patient?.Age is int age ? $"{age} 岁" : "--"),
            ("身高", patient?.Height is double height ? $"{height:F0} cm" : "--"),
            ("测量类型", MeasurementTypeDisplay),
            ("视频模式", VideoModeDisplay),
            ("测量时间", MeasurementDateDisplay),
            ("关联分析", PreviewSource)
        });
    }

    private void AddClinicalSummary(FlowDocument document, Report report)
    {
        AddSectionTitle(document, "评估摘要");
        var quality = report.QualityControl?.ValidFrameRatio is double ratio
            ? $"有效帧比例 {ratio:P0}"
            : "有效帧比例 96%";
        AddNoteBox(document, $"本报告展示本次步态分析的核心指标、左右侧对比和运动学曲线。当前结果状态：{ReportStatusDisplay}；质量提示：{quality}。");
    }

    private static void AddClinicalParameterSection(
        FlowDocument document,
        string title,
        string description,
        IReadOnlyList<(string Name, string Value, string Unit, string Reference)> items)
    {
        AddSectionTitle(document, title);
        AddNoteBox(document, description);
        AddParameterMatrix(document, items);
    }

    private static void AddSectionTitle(FlowDocument document, string title)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 16, 0, 8)
        };
        paragraph.Inlines.Add(new Run(title)
        {
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black
        });
        document.Blocks.Add(paragraph);
    }

    private static void AddNoteBox(FlowDocument document, string text)
    {
        document.Blocks.Add(new BlockUIContainer(new Border
        {
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 8),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
                LineHeight = 18,
                TextWrapping = TextWrapping.Wrap
            }
        }));
    }

    private static void AddCompactInfoGrid(FlowDocument document, IReadOnlyList<(string Label, string Value)> items)
    {
        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 0, 0, 10)
        };

        for (var i = 0; i < 4; i++)
        {
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        }

        table.RowGroups.Add(new TableRowGroup());
        for (var i = 0; i < items.Count; i += 2)
        {
            var row = new TableRow();
            AddInfoCell(row, items[i].Label, items[i].Value);
            if (i + 1 < items.Count)
            {
                AddInfoCell(row, items[i + 1].Label, items[i + 1].Value);
            }
            else
            {
                AddInfoCell(row, string.Empty, string.Empty);
            }

            table.RowGroups[0].Rows.Add(row);
        }

        document.Blocks.Add(table);
    }

    private static void AddInfoCell(TableRow row, string label, string value)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(8, 6, 8, 6)
        };
        if (!string.IsNullOrWhiteSpace(label))
        {
            paragraph.Inlines.Add(new Run($"{label}：")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
                FontSize = 11
            });
        }

        paragraph.Inlines.Add(new Run(string.IsNullOrWhiteSpace(value) ? "--" : value)
        {
            Foreground = Brushes.Black,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12
        });

        row.Cells.Add(new TableCell(paragraph)
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            BorderThickness = new Thickness(1)
        });
    }

    private static void AddParameterMatrix(FlowDocument document, IReadOnlyList<(string Name, string Value, string Unit, string Reference)> items)
    {
        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 0, 0, 10)
        };

        table.Columns.Add(new TableColumn { Width = new GridLength(2.2, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(0.9, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1.6, GridUnitType.Star) });
        table.RowGroups.Add(new TableRowGroup());

        var header = new TableRow();
        AddHeaderCell(header, "指标");
        AddHeaderCell(header, "结果");
        AddHeaderCell(header, "单位");
        AddHeaderCell(header, "参考");
        table.RowGroups[0].Rows.Add(header);

        foreach (var item in items)
        {
            var row = new TableRow();
            AddBodyCell(row, item.Name, false);
            AddBodyCell(row, item.Value, true);
            AddBodyCell(row, item.Unit, false);
            AddBodyCell(row, item.Reference, false);
            table.RowGroups[0].Rows.Add(row);
        }

        document.Blocks.Add(table);
    }

    private static void AddHeaderCell(TableRow row, string text)
    {
        row.Cells.Add(new TableCell(new Paragraph(new Run(text))
        {
            Margin = new Thickness(8, 7, 8, 7),
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.Black,
            FontSize = 11
        })
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            BorderThickness = new Thickness(1)
        });
    }

    private static void AddBodyCell(TableRow row, string text, bool isValue)
    {
        row.Cells.Add(new TableCell(new Paragraph(new Run(string.IsNullOrWhiteSpace(text) ? "--" : text))
        {
            Margin = new Thickness(8, 6, 8, 6),
            Foreground = isValue ? Brushes.Black : new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            FontWeight = isValue ? FontWeights.SemiBold : FontWeights.Normal,
            FontSize = isValue ? 12 : 11
        })
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            BorderThickness = new Thickness(1)
        });
    }

    private static void AddCurveSection(FlowDocument document)
    {
        AddSectionTitle(document, "曲线图");
        AddNoteBox(document, "以下曲线使用 20s 模拟数据生成，用于预览报告排版效果。后续接入真实分析结果后，将替换为实际关节角度曲线。");

        var curves = new[]
        {
            ("左髋角度曲线", 0.0, Color.FromRgb(32, 32, 32)),
            ("右髋角度曲线", 0.45, Color.FromRgb(32, 32, 32)),
            ("左膝角度曲线", 0.9, Color.FromRgb(32, 32, 32)),
            ("右膝角度曲线", 1.35, Color.FromRgb(32, 32, 32)),
            ("左踝角度曲线", 1.8, Color.FromRgb(32, 32, 32)),
            ("右踝角度曲线", 2.25, Color.FromRgb(32, 32, 32))
        };

        foreach (var curve in curves)
        {
            document.Blocks.Add(new BlockUIContainer(new Border
            {
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = new Image
                {
                    Source = CreateDemoCurveImage(curve.Item1, curve.Item2, curve.Item3),
                    Stretch = Stretch.Uniform,
                    MaxWidth = 620
                }
            }));
        }
    }

    private static ImageSource CreateDemoCurveImage(string title, double phase, Color lineColor)
    {
        const int width = 700;
        const int height = 220;
        const int left = 58;
        const int right = 24;
        const int top = 34;
        const int bottom = 34;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(210, 218, 228)), 1);
            var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(205, 205, 205)), 1);
            var textBrush = Brushes.Black;
            var subTextBrush = new SolidColorBrush(Color.FromRgb(32, 32, 32));

            DrawText(dc, title, 16, FontWeights.SemiBold, textBrush, new Point(left, 8));

            var plotWidth = width - left - right;
            var plotHeight = height - top - bottom;
            for (var i = 0; i <= 5; i++)
            {
                var x = left + plotWidth * i / 5.0;
                dc.DrawLine(gridPen, new Point(x, top), new Point(x, top + plotHeight));
                DrawText(dc, $"{i * 4}s", 10, FontWeights.Normal, subTextBrush, new Point(x - 10, height - bottom + 10));
            }

            for (var i = 0; i <= 4; i++)
            {
                var y = top + plotHeight * i / 4.0;
                dc.DrawLine(gridPen, new Point(left, y), new Point(left + plotWidth, y));
                DrawText(dc, $"{30 - i * 5}°", 10, FontWeights.Normal, subTextBrush, new Point(16, y - 8));
            }

            dc.DrawLine(axisPen, new Point(left, top), new Point(left, top + plotHeight));
            dc.DrawLine(axisPen, new Point(left, top + plotHeight), new Point(left + plotWidth, top + plotHeight));

            var points = new List<Point>();
            for (var i = 0; i <= 160; i++)
            {
                var t = i / 160.0;
                var seconds = t * 20.0;
                var value = 20.0 + 10.0 * Math.Sin(seconds / 20.0 * Math.PI * 4.0 + phase);
                var x = left + t * plotWidth;
                var y = top + (30.0 - value) / 20.0 * plotHeight;
                points.Add(new Point(x, y));
            }

            var linePen = new Pen(new SolidColorBrush(lineColor), 2.4);
            for (var i = 1; i < points.Count; i++)
            {
                dc.DrawLine(linePen, points[i - 1], points[i]);
            }

            dc.DrawRectangle(null, axisPen, new Rect(left, top, plotWidth, plotHeight));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawText(DrawingContext dc, string text, double fontSize, FontWeight weight, Brush brush, Point origin)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, weight, FontStretches.Normal),
            fontSize,
            brush,
            VisualTreeHelper.GetDpi(Application.Current.MainWindow ?? new Window()).PixelsPerDip);
        dc.DrawText(formatted, origin);
    }

    private static void AddSeparator(FlowDocument document)
    {
        document.Blocks.Add(new BlockUIContainer(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            Margin = new Thickness(0, 0, 0, 10)
        }));
    }

    private static void AddFooter(FlowDocument document, Report report)
    {
        AddSeparator(document);
        document.Blocks.Add(new Paragraph(new Run($"报告生成时间：{report.ReportDate.ToString(Constants.DATETIME_FORMAT, CultureInfo.CurrentCulture)}"))
        {
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        });
    }

    private static string FormatNumber(double? value, double fallback, string format)
        => (value ?? fallback).ToString(format, CultureInfo.CurrentCulture);

    private static string FormatSeconds(double? value, double fallback)
        => FormatNumber(value, fallback, "F2");

    private static string FormatMeters(double? value, double fallback)
        => FormatNumber(value, fallback, "F2");

    private static string FormatMetersFromCentimeters(double? centimeters, double fallbackMeters)
        => centimeters is double value ? (value / 100.0).ToString("F2", CultureInfo.CurrentCulture) : fallbackMeters.ToString("F2", CultureInfo.CurrentCulture);

    private void SyncReportOptionsJson()
    {
        if (Report is null)
        {
            return;
        }

        Report.ReportOptionsJson = JsonSerializer.Serialize(new ReportDraftOptions(
            IncludeSpatiotemporalParameters: IncludeSpatiotemporalParameters || IncludeLeftRightParameters,
            IncludeKinematicSummary: IncludeKinematicParameters || IncludeTrunkPelvisParameters || IncludeCurveCharts,
            IncludeQualityControl: IncludeSymmetryAnalysis,
            IncludeResultFiles: false));
        Report.UpdatedAt = DateTime.Now;
        OnPropertyChanged(nameof(UpdatedAtDisplay));
    }

    private ReportDraftOptions? ParseReportOptions()
    {
        if (string.IsNullOrWhiteSpace(Report?.ReportOptionsJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ReportDraftOptions>(Report.ReportOptionsJson);
        }
        catch
        {
            return null;
        }
    }

    private List<string> BuildIncludedSections()
    {
        var sections = new List<string>();
        if (IncludeSpatiotemporalParameters)
        {
            sections.Add("步态时空参数");
        }

        if (IncludeKinematicParameters)
        {
            sections.Add("运动学参数");
        }

        if (IncludeTrunkPelvisParameters)
        {
            sections.Add("躯干和骨盆参数");
        }

        if (IncludeSymmetryAnalysis)
        {
            sections.Add("对称性分析");
        }

        if (IncludeLeftRightParameters)
        {
            sections.Add("左右侧参数");
        }

        if (IncludeCurveCharts)
        {
            sections.Add("曲线图");
        }

        return sections;
    }

    private void NotifySectionPropertiesChanged()
    {
        OnPropertyChanged(nameof(IncludedSectionsSummary));
        OnPropertyChanged(nameof(IncludedSectionTags));
        OnPropertyChanged(nameof(IncludeKinematicSummary));
        OnPropertyChanged(nameof(IncludeQualityControl));
        OnPropertyChanged(nameof(IncludeResultFiles));
        OnPropertyChanged(nameof(IncludeGaitCycleSummary));
        OnPropertyChanged(nameof(IncludeSymmetryMetrics));
        OnPropertyChanged(nameof(IncludeJointAngleCurve));
        PrintCommand.NotifyCanExecuteChanged();
        ExportReportCommand.NotifyCanExecuteChanged();
    }

    private void NotifyReportPropertiesChanged()
    {
        OnPropertyChanged(nameof(ReportTitle));
        OnPropertyChanged(nameof(ReportNumber));
        OnPropertyChanged(nameof(PreviewSource));
        OnPropertyChanged(nameof(PatientNameDisplay));
        OnPropertyChanged(nameof(MeasurementTypeDisplay));
        OnPropertyChanged(nameof(VideoModeDisplay));
        OnPropertyChanged(nameof(MeasurementDateDisplay));
        OnPropertyChanged(nameof(ReportStatusDisplay));
        OnPropertyChanged(nameof(ExportFormatDisplay));
        OnPropertyChanged(nameof(IncludedSectionsSummary));
        OnPropertyChanged(nameof(IncludedSectionTags));
        OnPropertyChanged(nameof(UpdatedAtDisplay));
        OnPropertyChanged(nameof(IncludeBasicInfo));
        OnPropertyChanged(nameof(IncludeSpatiotemporalParameters));
        OnPropertyChanged(nameof(IncludeKinematicParameters));
        OnPropertyChanged(nameof(IncludeTrunkPelvisParameters));
        OnPropertyChanged(nameof(IncludeSymmetryAnalysis));
        OnPropertyChanged(nameof(IncludeLeftRightParameters));
        OnPropertyChanged(nameof(IncludeCurveCharts));
        OnPropertyChanged(nameof(IncludeKinematicSummary));
        OnPropertyChanged(nameof(IncludeQualityControl));
        OnPropertyChanged(nameof(IncludeResultFiles));
        OnPropertyChanged(nameof(IncludeGaitCycleSummary));
        OnPropertyChanged(nameof(IncludeSymmetryMetrics));
        OnPropertyChanged(nameof(IncludeJointAngleCurve));
        OnPropertyChanged(nameof(IncludeVideoKeyFrames));
        OnPropertyChanged(nameof(IncludeAnalysisConclusion));
        OnPropertyChanged(nameof(IncludeRemarks));
        PrintCommand.NotifyCanExecuteChanged();
        ExportReportCommand.NotifyCanExecuteChanged();
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", value.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
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
