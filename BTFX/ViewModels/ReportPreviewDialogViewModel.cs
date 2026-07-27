using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BTFX.Common;
using BTFX.Helpers;
using BTFX.Models;
using BTFX.Services.Implementations;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BTFX.ViewModels;

/// <summary>
/// 报告预览对话框视图模型。
/// </summary>
public partial class ReportPreviewDialogViewModel : ObservableObject, IDisposable
{
    private readonly ILocalizationService _localizationService;
    private readonly IReportReferenceRangeService _referenceRangeService;
    private readonly LanguageChangeSubscription _languageChangeSubscription;
    private Report? _report;
    private FlowDocument? _previewDocument;
    private string _previewStatus;
    private bool _isInitializing;
    private bool _includeSpatiotemporalParameters = true;
    private bool _includeKinematicParameters = true;
    private bool _includeTrunkPelvisParameters = true;
    private bool _includeSymmetryAnalysis = true;
    private bool _includeLeftRightParameters = true;
    private bool _includeCurveCharts = true;
    private string _selectedExportFormat = "PDF";
    private bool _disposed;

    public ReportPreviewDialogViewModel(
        ILocalizationService localizationService,
        IReportReferenceRangeService referenceRangeService)
    {
        _localizationService = localizationService;
        _referenceRangeService = referenceRangeService;
        _previewStatus = L("AnalysisDetail.ReportPreview.Status.NotLoaded");
        _languageChangeSubscription = new LanguageChangeSubscription(_localizationService, OnLanguageChanged);
    }

    private string L(string key) => _localizationService.GetString(key);

    private string L(string key, params object[] args) => _localizationService.GetString(key, args);

    private string Reference(string key) => _referenceRangeService.GetReferenceText(key);

    private void OnLanguageChanged(object? sender, AppLanguage language)
    {
        RebuildPreviewDocument();
        NotifyReportPropertiesChanged();
        NotifySectionPropertiesChanged();
        OnPropertyChanged(nameof(PreviewStatus));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _languageChangeSubscription.Dispose();
        _disposed = true;
    }

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
        get => _includeTrunkPelvisParameters && CanIncludeTrunkPelvisParameters;
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

    public string ReportTitle => NormalizeReportTitle(Report?.Title);

    public string ReportNumber => string.IsNullOrWhiteSpace(Report?.ReportNumber) ? "--" : Report.ReportNumber;

    public string PreviewSource => Report?.AnalysisResultId is int analysisResultId
        ? $"AnalysisResult #{analysisResultId}"
        : "--";

    public string PatientNameDisplay => !string.IsNullOrWhiteSpace(Report?.Patient?.Name)
        ? Report.Patient.Name
        : Report?.PatientId > 0 ? L("ReportPreview.PatientIdFormat", Report.PatientId) : "--";

    public string MeasurementTypeDisplay => Report?.MeasurementRecord is null
        ? "--"
        : GetMeasurementTypeText(Report.MeasurementRecord.MeasurementType);

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

            return record.HasDualVideo
                ? L("Report.Mode.Dual")
                : record.HasSideVideo || record.HasFrontVideo ? L("Report.Mode.Single") : "--";
        }
    }

    public string AnalysisModeDisplay => VideoModeDisplay;

    public bool IsDualVideoMode => Report?.MeasurementRecord?.HasDualVideo == true;

    public bool CanIncludeTrunkPelvisParameters => IsDualVideoMode;

    public string ReportStatusDisplay => Report is null ? "--" : GetReportStatusText(Report.Status);

    public string ExportFormatDisplay => SelectedExportFormat;

    public string IncludedSectionsSummary
    {
        get
        {
            var sections = BuildIncludedSections();
            return sections.Count > 0
                ? string.Join(L("AnalysisDetail.ReportPreview.Sections.Separator"), sections)
                : L("AnalysisDetail.ReportPreview.Sections.NoneSelected");
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
        PreviewStatus = L("ReportPreview.Status.Ready");
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
            AppDialog.Show(L("ReportPreview.Print.NoPreview"), L("Tip"), AppDialogButtons.Ok, AppDialogIcon.Information);
            return;
        }

        var printed = PrintHelper.PrintDocument(PreviewDocument, $"{L("ReportPreview.FilePrefix")}_{ReportNumber}", showDialog: true);
        if (printed)
        {
            AppDialog.Show(L("ReportPreview.Print.Sent"), L("Print"), AppDialogButtons.Ok, AppDialogIcon.Information);
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
            Title = L("Report.ExportReport"),
            Filter = L("ReportPreview.Export.PdfFilter"),
            FileName = $"{L("ReportPreview.FilePrefix")}_{SanitizeFileName(ReportNumber)}.pdf"
        };

        if (dialog.ShowDialog(Application.Current.MainWindow) != true)
        {
            return;
        }

        try
        {
            await Task.Yield();
            SyncReportOptionsJson();
            var report = Report;
            var filePath = dialog.FileName;
            var originalStatus = report.Status;
            report.Status = ReportStatus.Completed;
            var success = PrintHelper.ExportDocumentToPdf(PreviewDocument, filePath);

            if (success)
            {
                report.PdfFilePath = filePath;

                if (App.Services?.GetService(typeof(IReportService)) is IReportService reportService)
                {
                    await reportService.UpdateReportAsync(report);
                }

                OnPropertyChanged(nameof(ReportStatusDisplay));
            }
            else
            {
                report.Status = originalStatus;
            }

            AppDialog.Show(
                success ? L("ReportPreview.Export.SuccessFormat", filePath) : L("ReportPreview.Export.Failed"),
                success ? L("ReportPreview.Export.SuccessTitle") : L("ReportPreview.Export.FailedTitle"),
                AppDialogButtons.Ok,
                success ? AppDialogIcon.Information : AppDialogIcon.Error);
        }
        catch (Exception ex)
        {
            AppDialog.Show(L("ReportPreview.Export.FailedFormat", ex.Message), L("ReportPreview.Export.FailedTitle"), AppDialogButtons.Ok, AppDialogIcon.Error);
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
        var document = Report is null ? null : BuildRealSelectedSectionsDocument(Report);
        PreviewDocument = document;
        PrintCommand.NotifyCanExecuteChanged();
        ExportReportCommand.NotifyCanExecuteChanged();
    }

    private FlowDocument BuildRealSelectedSectionsDocument(Report report)
    {
        var data = ReportAnalysisSnapshot.From(report);
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
        AddComputedClinicalSummary(document, data);

        if (IncludeSpatiotemporalParameters)
        {
            AddClinicalParameterSection(document, L("Report.Section.Spatiotemporal"), L("ReportPreview.Description.Spatiotemporal"),
            [
                (L("ReportPreview.Param.GaitCycle"), FormatSeconds(data.MeanCycleDurationSec), "s", Reference("GaitCycle")),
                (L("ReportPreview.Param.MeanStepLength"), FormatMeters(data.MeanStepLengthM), "m", Reference("MeanStepLength")),
                (L("ReportPreview.Param.MeanStrideLength"), FormatMeters(data.MeanStrideLengthM), "m", Reference("MeanStrideLength")),
                (L("ReportPreview.Param.MeanCadence"), FormatNumber(data.CadenceStepPerMin, "F1"), L("CadenceUnit"), Reference("MeanCadence")),
                (L("ReportPreview.Param.MeanGaitSpeed"), FormatNumber(data.GaitSpeedMPerS, "F2"), "m/s", Reference("MeanGaitSpeed")),
                (L("ReportPreview.Param.StanceTime"), FormatNumber(data.MeanStanceTimeSec, "F2"), "s", Reference("StanceTime")),
                (L("ReportPreview.Param.SwingTime"), FormatNumber(data.MeanSwingTimeSec, "F2"), "s", Reference("SwingTime")),
                (L("ReportPreview.Param.DoubleSupportTime"), FormatNumber(data.MeanDoubleSupportTimeSec, "F2"), "s", Reference("DoubleSupportTime")),
                (L("ReportPreview.Param.SingleSupportTime"), FormatNumber(data.MeanSingleSupportTimeSec, "F2"), "s", Reference("SingleSupportTime"))
            ]);
        }

        if (IncludeKinematicParameters)
        {
            var kinematicItems = new List<(string Name, string Value, string Unit, string Reference)>
            {
                (L("ReportPreview.Param.LeftHipRom"), FormatNumber(data.LeftHipRomDeg, "F1"), "°", Reference("LeftHipRom")),
                (L("ReportPreview.Param.RightHipRom"), FormatNumber(data.RightHipRomDeg, "F1"), "°", Reference("RightHipRom")),
                (L("ReportPreview.Param.LeftKneeRom"), FormatNumber(data.LeftKneeRomDeg, "F1"), "°", Reference("LeftKneeRom")),
                (L("ReportPreview.Param.RightKneeRom"), FormatNumber(data.RightKneeRomDeg, "F1"), "°", Reference("RightKneeRom")),
                (L("ReportPreview.Param.LeftAnkleRom"), FormatNumber(data.LeftAnkleRomDeg, "F1"), "°", Reference("LeftAnkleRom")),
                (L("ReportPreview.Param.RightAnkleRom"), FormatNumber(data.RightAnkleRomDeg, "F1"), "°", Reference("RightAnkleRom")),
                (L("ReportPreview.Param.HipAverageRom"), FormatNumber(data.HipRomDeg, "F1"), "°", Reference("HipAverageRom")),
                (L("ReportPreview.Param.KneeAverageRom"), FormatNumber(data.KneeRomDeg, "F1"), "°", Reference("KneeAverageRom")),
                (L("ReportPreview.Param.AnkleAverageRom"), FormatNumber(data.AnkleRomDeg, "F1"), "°", Reference("AnkleAverageRom"))
            };

            if (data.IsDualVideoMode)
            {
                kinematicItems.Add((L("ReportPreview.Param.PelvisCoronalAngle"), FormatNumber(data.PelvisCoronalRomDeg, "F1"), "°", Reference("PelvisCoronalAngle")));
            }

            AddClinicalParameterSection(document, L("Report.Section.Kinematic"), L("ReportPreview.Description.Kinematic"),
                kinematicItems);
        }

        if (IncludeTrunkPelvisParameters && data.IsDualVideoMode)
        {
            AddClinicalParameterSection(document, L("Report.Section.TrunkPelvis"), L("ReportPreview.Description.TrunkPelvis"),
            [
                (L("ReportPreview.Param.TrunkTiltMean"), FormatNumber(data.TrunkTiltMeanDeg, "F1"), "°", Reference("TrunkTiltMean")),
                (L("ReportPreview.Param.TrunkTiltMax"), FormatNumber(data.TrunkTiltMaxDeg, "F1"), "°", Reference("TrunkTiltMax")),
                (L("ReportPreview.Param.TrunkTiltMin"), FormatNumber(data.TrunkTiltMinDeg, "F1"), "°", Reference("TrunkTiltMin")),
                (L("ReportPreview.Param.TrunkTiltRom"), FormatNumber(data.TrunkTiltRomDeg, "F1"), "°", Reference("TrunkTiltRom")),
                (L("ReportPreview.Param.PelvisTiltMean"), FormatNumber(data.PelvisTiltMeanDeg, "F1"), "°", Reference("PelvisTiltMean")),
                (L("ReportPreview.Param.PelvisTiltMax"), FormatNumber(data.PelvisTiltMaxDeg, "F1"), "°", Reference("PelvisTiltMax")),
                (L("ReportPreview.Param.PelvisRom"), FormatNumber(data.PelvisRomDeg, "F1"), "°", Reference("PelvisRom"))
            ]);
        }

        if (IncludeSymmetryAnalysis)
        {
            AddClinicalParameterSection(document, L("Report.Section.Symmetry"), L("ReportPreview.Description.Symmetry"),
            [
                (L("ReportPreview.Param.StrideDiff"), FormatNumber(AbsDiff(data.LeftStrideMeanM, data.RightStrideMeanM), "F2"), "m", Reference("StrideDiff")),
                (L("ReportPreview.Param.StrideDiffPercent"), FormatNumber(DiffPercent(data.LeftStrideMeanM, data.RightStrideMeanM), "F1"), "%", Reference("StrideDiffPercent")),
                (L("ReportPreview.Param.StanceRatioDiff"), FormatNumber(AbsDiff(data.LeftStanceRatioPct, data.RightStanceRatioPct), "F1"), "%", Reference("StanceRatioDiff")),
                (L("ReportPreview.Param.StanceRatioDiffPercent"), FormatNumber(DiffPercent(data.LeftStanceRatioPct, data.RightStanceRatioPct), "F1"), "%", Reference("StanceRatioDiffPercent")),
                (L("ReportPreview.Param.KneeRomDiff"), FormatNumber(AbsDiff(data.LeftKneeRomDeg, data.RightKneeRomDeg), "F1"), "°", Reference("KneeRomDiff")),
                (L("ReportPreview.Param.HipRomDiff"), FormatNumber(AbsDiff(data.LeftHipRomDeg, data.RightHipRomDeg), "F1"), "°", Reference("HipRomDiff")),
                (L("ReportPreview.Param.AnkleRomDiff"), FormatNumber(AbsDiff(data.LeftAnkleRomDeg, data.RightAnkleRomDeg), "F1"), "°", Reference("AnkleRomDiff")),
                (L("ReportPreview.Param.SymmetryScore"), FormatNumber(data.SymmetryScore, "F1"), L("ReportPreview.Unit.Score"), Reference("SymmetryScore"))
            ]);
        }

        if (IncludeLeftRightParameters)
        {
            AddClinicalParameterSection(document, L("Report.Section.SideParameters"), L("ReportPreview.Description.SideParameters"),
            [
                (L("ReportPreview.Param.LeftStride"), FormatMeters(data.LeftStrideMeanM), "m", Reference("LeftStride")),
                (L("ReportPreview.Param.RightStride"), FormatMeters(data.RightStrideMeanM), "m", Reference("RightStride")),
                (L("ReportPreview.Param.LeftStanceRatio"), FormatNumber(data.LeftStanceRatioPct, "F1"), "%", Reference("LeftStanceRatio")),
                (L("ReportPreview.Param.RightStanceRatio"), FormatNumber(data.RightStanceRatioPct, "F1"), "%", Reference("RightStanceRatio")),
                (L("ReportPreview.Param.LeftSwingRatio"), FormatNumber(ComplementPercent(data.LeftStanceRatioPct), "F1"), "%", Reference("LeftSwingRatio")),
                (L("ReportPreview.Param.RightSwingRatio"), FormatNumber(ComplementPercent(data.RightStanceRatioPct), "F1"), "%", Reference("RightSwingRatio"))
            ]);
        }

        if (IncludeCurveCharts)
        {
            AddRealCurveSection(document, data);
        }

        if (!BuildIncludedSections().Any())
        {
            AddSectionTitle(document, L("ReportPreview.ReportContent"));
            AddNoteBox(document, L("ReportPreview.NoContentSelected"));
        }

        AddFooter(document, report);
        return document;
    }

    private void AddHeader(FlowDocument document, Report report)
    {
        var settingsService = App.Services?.GetService(typeof(ISettingsService)) as ISettingsService;
        var unitName = settingsService?.CurrentSettings?.Unit?.Name ?? L("AppName");
        var logoPath = settingsService?.CurrentSettings?.Unit?.LogoPath;

        var logo = TryCreateReportLogo(logoPath);
        if (logo != null)
        {
            document.Blocks.Add(new Paragraph(new InlineUIContainer(logo))
            {
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        document.Blocks.Add(new Paragraph(new Run(unitName))
        {
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        });

        document.Blocks.Add(new Paragraph(new Run(NormalizeReportTitle(report.Title)))
        {
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        });

        document.Blocks.Add(new Paragraph(new Run(L("ReportPreview.ReportNumberLineFormat", ReportNumber)))
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14)
        });

        AddSeparator(document);
    }

    private static Image? TryCreateReportLogo(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
            bitmap.DecodePixelHeight = 72;
            bitmap.EndInit();
            bitmap.Freeze();

            return new Image
            {
                Source = bitmap,
                Width = 72,
                Height = 72,
                Stretch = Stretch.Uniform
            };
        }
        catch
        {
            return null;
        }
    }

    private void AddBasicInfo(FlowDocument document, Report report)
    {
        var patient = report.Patient ?? report.MeasurementRecord?.Patient;
        AddSectionTitle(document, L("BasicInformation"));
        AddCompactInfoGrid(document, new[]
        {
            (L("Patient"), PatientNameDisplay),
            (L("Gender"), patient is null ? "--" : GetGenderText(patient.Gender)),
            (L("Age"), patient?.Age is int age ? L("ReportPreview.AgeFormat", age) : "--"),
            (L("Height"), patient?.Height is double height ? $"{height:F0} cm" : "--"),
            (L("Report.Field.MeasurementType"), MeasurementTypeDisplay),
            (L("Report.Field.AnalysisMode"), AnalysisModeDisplay),
            (L("Report.Field.MeasurementTime"), MeasurementDateDisplay)
        });
    }

    private void AddComputedClinicalSummary(FlowDocument document, ReportAnalysisSnapshot data)
    {
        AddSectionTitle(document, L("ReportPreview.EvaluationSummary"));
        var validFrameRatio = data.ValidFrameRatio is double ratio
            ? L("ReportPreview.ValidFrameRatioFormat", ratio.ToString("P0", CultureInfo.CurrentCulture))
            : L("ReportPreview.ValidFrameRatioEmpty");
        var cycleCount = data.CycleCount.HasValue
            ? L("ReportPreview.ValidCycleCountFormat", data.CycleCount.Value)
            : L("ReportPreview.ValidCycleCountEmpty");
        AddNoteBox(document, L("ReportPreview.EvaluationSummaryText", ReportStatusDisplay, validFrameRatio, cycleCount));
    }

    private void AddClinicalParameterSection(
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

    private void AddParameterMatrix(FlowDocument document, IReadOnlyList<(string Name, string Value, string Unit, string Reference)> items)
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
        AddHeaderCell(header, L("ReportPreview.Table.Metric"));
        AddHeaderCell(header, L("ReportPreview.Table.Result"));
        AddHeaderCell(header, L("ReportPreview.Table.Unit"));
        AddHeaderCell(header, L("ReportPreview.Table.Reference"));
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

    private void AddRealCurveSection(FlowDocument document, ReportAnalysisSnapshot data)
    {
        AddSectionTitle(document, L("Report.Section.Charts"));
        if (data.AngleFrames.Count == 0)
        {
            AddNoteBox(document, L("ReportPreview.Chart.NoJointAngleCsv"));
            return;
        }

        AddNoteBox(document, L("ReportPreview.Chart.Description"));
        var curveDefinitions = new List<(string Title, Func<ReportAngleFrame, double> First, Func<ReportAngleFrame, double>? Second, string FirstName, string? SecondName)>
        {
            (L("AnalysisDetail.Chart.HipTitle"), f => f.LeftHip, f => f.RightHip, L("ReportPreview.Legend.LeftHip"), L("ReportPreview.Legend.RightHip")),
            (L("AnalysisDetail.Chart.KneeTitle"), f => f.LeftKnee, f => f.RightKnee, L("ReportPreview.Legend.LeftKnee"), L("ReportPreview.Legend.RightKnee")),
            (L("AnalysisDetail.Chart.AnkleTitle"), f => f.LeftAnkle, f => f.RightAnkle, L("ReportPreview.Legend.LeftAnkle"), L("ReportPreview.Legend.RightAnkle"))
        };

        if (data.IsDualVideoMode)
        {
            curveDefinitions.Add((L("AnalysisDetail.Chart.PelvisTitle"), f => f.Pelvis, null, L("ReportPreview.Legend.Pelvis"), null));
            curveDefinitions.Add((L("AnalysisDetail.Chart.TrunkTitle"), f => f.Trunk, null, L("ReportPreview.Legend.Trunk"), null));
        }

        foreach (var curve in curveDefinitions)
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
                    Source = CreateRealCurveImage(curve.Title, data.AngleFrames, curve.First, curve.Second, curve.FirstName, curve.SecondName, data.VideoDurationSec),
                    Stretch = Stretch.Uniform,
                    MaxWidth = 620
                }
            }));
        }
    }

    private static ImageSource CreateRealCurveImage(
        string title,
        IReadOnlyList<ReportAngleFrame> frames,
        Func<ReportAngleFrame, double> firstSelector,
        Func<ReportAngleFrame, double>? secondSelector,
        string firstName,
        string? secondName,
        double? videoDurationSec)
    {
        const int width = 700;
        const int height = 240;
        const int left = 58;
        const int right = 24;
        const int top = 44;
        const int bottom = 36;

        var values = frames
            .SelectMany(frame => secondSelector is null
                ? new[] { firstSelector(frame) }
                : new[] { firstSelector(frame), secondSelector(frame) })
            .Where(value => !double.IsNaN(value) && !double.IsInfinity(value))
            .ToArray();
        var maxTime = Math.Max(1d, videoDurationSec ?? frames.Max(frame => frame.TimeS));
        var minValue = values.Length == 0 ? 0d : values.Min();
        var maxValue = values.Length == 0 ? 40d : values.Max();
        var padding = Math.Max(5d, (maxValue - minValue) * 0.12d);
        minValue = Math.Floor(minValue - padding);
        maxValue = Math.Ceiling(maxValue + padding);
        if (Math.Abs(maxValue - minValue) < 0.01)
        {
            maxValue = minValue + 1;
        }

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(210, 218, 228)), 1);
            var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(205, 205, 205)), 1);
            var leftPen = new Pen(new SolidColorBrush(Color.FromRgb(32, 32, 32)), 2.2);
            var rightPen = new Pen(new SolidColorBrush(Color.FromRgb(228, 0, 74)), 2.2);
            var textBrush = Brushes.Black;
            var subTextBrush = new SolidColorBrush(Color.FromRgb(32, 32, 32));

            var titleText = string.IsNullOrWhiteSpace(secondName)
                ? $"{title}（{firstName}）"
                : $"{title}（{firstName} / {secondName}）";
            DrawText(dc, titleText, 15, FontWeights.SemiBold, textBrush, new Point(left, 8));
            DrawLegend(dc, firstName, leftPen.Brush, new Point(width - 170, 12));
            if (!string.IsNullOrWhiteSpace(secondName))
            {
                DrawLegend(dc, secondName, rightPen.Brush, new Point(width - 95, 12));
            }

            var plotWidth = width - left - right;
            var plotHeight = height - top - bottom;
            for (var i = 0; i <= 5; i++)
            {
                var x = left + plotWidth * i / 5.0;
                dc.DrawLine(gridPen, new Point(x, top), new Point(x, top + plotHeight));
                DrawText(dc, $"{maxTime * i / 5.0:F1}s", 10, FontWeights.Normal, subTextBrush, new Point(x - 12, height - bottom + 10));
            }

            for (var i = 0; i <= 4; i++)
            {
                var y = top + plotHeight * i / 4.0;
                var label = maxValue - (maxValue - minValue) * i / 4.0;
                dc.DrawLine(gridPen, new Point(left, y), new Point(left + plotWidth, y));
                DrawText(dc, $"{label:F0}°", 10, FontWeights.Normal, subTextBrush, new Point(12, y - 8));
            }

            dc.DrawLine(axisPen, new Point(left, top), new Point(left, top + plotHeight));
            dc.DrawLine(axisPen, new Point(left, top + plotHeight), new Point(left + plotWidth, top + plotHeight));
            DrawCurve(dc, frames, firstSelector, leftPen, left, top, plotWidth, plotHeight, maxTime, minValue, maxValue);
            if (secondSelector is not null)
            {
                DrawCurve(dc, frames, secondSelector, rightPen, left, top, plotWidth, plotHeight, maxTime, minValue, maxValue);
            }
            dc.DrawRectangle(null, axisPen, new Rect(left, top, plotWidth, plotHeight));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawCurve(
        DrawingContext dc,
        IReadOnlyList<ReportAngleFrame> frames,
        Func<ReportAngleFrame, double> selector,
        Pen pen,
        int left,
        int top,
        double plotWidth,
        double plotHeight,
        double maxTime,
        double minValue,
        double maxValue)
    {
        Point? previous = null;
        foreach (var frame in frames)
        {
            var value = selector(frame);
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                previous = null;
                continue;
            }

            var x = left + Math.Clamp(frame.TimeS / maxTime, 0d, 1d) * plotWidth;
            var y = top + (maxValue - value) / (maxValue - minValue) * plotHeight;
            var current = new Point(x, y);
            if (previous is Point last)
            {
                dc.DrawLine(pen, last, current);
            }

            previous = current;
        }
    }

    private static void DrawLegend(DrawingContext dc, string text, Brush brush, Point origin)
    {
        var pen = new Pen(brush, 2.2);
        dc.DrawLine(pen, origin, new Point(origin.X + 22, origin.Y));
        DrawText(dc, text, 10, FontWeights.Normal, Brushes.Black, new Point(origin.X + 28, origin.Y - 7));
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

    private void AddFooter(FlowDocument document, Report report)
    {
        AddSeparator(document);
        document.Blocks.Add(new Paragraph(new Run(L("ReportPreview.GeneratedTimeFormat", report.ReportDate.ToString(Constants.DATETIME_FORMAT, CultureInfo.CurrentCulture))))
        {
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        });
    }

    private static string FormatNumber(double? value, double fallback, string format)
        => (value ?? fallback).ToString(format, CultureInfo.CurrentCulture);

    private static string FormatNumber(double? value, string format)
        => value.HasValue ? value.Value.ToString(format, CultureInfo.CurrentCulture) : "--";

    private static string FormatSeconds(double? value, double fallback)
        => FormatNumber(value, fallback, "F2");

    private static string FormatSeconds(double? value)
        => FormatNumber(value, "F2");

    private static string FormatMeters(double? value, double fallback)
        => FormatNumber(value, fallback, "F2");

    private static string FormatMeters(double? value)
        => FormatNumber(value, "F2");

    private static string FormatMetersFromCentimeters(double? centimeters, double fallbackMeters)
        => GaitLengthUnitConverter.ToMeters(centimeters) is double meters
            ? meters.ToString("F2", CultureInfo.CurrentCulture)
            : fallbackMeters.ToString("F2", CultureInfo.CurrentCulture);

    private static double? AbsDiff(double? left, double? right)
        => left.HasValue && right.HasValue ? Math.Abs(left.Value - right.Value) : null;

    private static double? DiffPercent(double? left, double? right)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return null;
        }

        var denominator = (Math.Abs(left.Value) + Math.Abs(right.Value)) / 2d;
        return denominator <= 0.000001d ? null : Math.Abs(left.Value - right.Value) / denominator * 100d;
    }

    private static double? ComplementPercent(double? value)
        => value.HasValue ? Math.Clamp(100d - value.Value, 0d, 100d) : null;

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
            sections.Add(L("Report.Section.Spatiotemporal"));
        }

        if (IncludeKinematicParameters)
        {
            sections.Add(L("Report.Section.Kinematic"));
        }

        if (IncludeTrunkPelvisParameters)
        {
            sections.Add(L("Report.Section.TrunkPelvis"));
        }

        if (IncludeSymmetryAnalysis)
        {
            sections.Add(L("Report.Section.Symmetry"));
        }

        if (IncludeLeftRightParameters)
        {
            sections.Add(L("Report.Section.SideParameters"));
        }

        if (IncludeCurveCharts)
        {
            sections.Add(L("Report.Section.Charts"));
        }

        return sections;
    }

    private string NormalizeReportTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return L("Report.DefaultTitle");
        }

        const string legacyDefaultTitle = "步态分析报告";
        const string legacyDefaultTitlePrefix = "步态分析报告 - ";

        if (string.Equals(title, legacyDefaultTitle, StringComparison.Ordinal))
        {
            return L("Report.DefaultTitle");
        }

        if (title.StartsWith(legacyDefaultTitlePrefix, StringComparison.Ordinal))
        {
            var suffix = title[legacyDefaultTitlePrefix.Length..].Trim();
            return string.IsNullOrWhiteSpace(suffix)
                ? L("Report.DefaultTitle")
                : L("AnalysisDetail.ReportConfig.DefaultReportTitleFormat", suffix);
        }

        return title;
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
        OnPropertyChanged(nameof(AnalysisModeDisplay));
        OnPropertyChanged(nameof(IsDualVideoMode));
        OnPropertyChanged(nameof(CanIncludeTrunkPelvisParameters));
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

    private string GetMeasurementTypeText(MeasurementType type)
    {
        return type switch
        {
            MeasurementType.NormalWalk => L("MeasurementType.NormalWalk"),
            MeasurementType.FastWalk => L("MeasurementType.FastWalk"),
            MeasurementType.SlowWalk => L("MeasurementType.SlowWalk"),
            MeasurementType.Other => L("MeasurementType.Other"),
            _ => "--"
        };
    }

    private string GetReportStatusText(ReportStatus status)
    {
        return status switch
        {
            ReportStatus.Draft or ReportStatus.Completed or ReportStatus.Printed or ReportStatus.Outdated => L("Report.Status.Viewable"),
            _ => L("Report.Status.Unknown")
        };
    }

    private string GetGenderText(Gender gender)
    {
        return gender switch
        {
            Gender.Male => L("Male"),
            Gender.Female => L("Female"),
            _ => "--"
        };
    }

}

internal sealed class ReportAnalysisSnapshot
{
    public double? VideoFps { get; private set; }
    public double? VideoDurationSec { get; private set; }
    public int? VideoFrameCount { get; private set; }
    public int? CycleCount { get; private set; }
    public double? MeanCycleDurationSec { get; private set; }
    public double? CadenceStepPerMin { get; private set; }
    public double? GaitSpeedMPerS { get; private set; }
    public double? MeanStepLengthM { get; private set; }
    public double? MeanStrideLengthM { get; private set; }
    public double? MeanStanceTimeSec { get; private set; }
    public double? MeanSwingTimeSec { get; private set; }
    public double? MeanDoubleSupportTimeSec { get; private set; }
    public double? MeanSingleSupportTimeSec { get; private set; }
    public double? LeftHipRomDeg { get; private set; }
    public double? RightHipRomDeg { get; private set; }
    public double? LeftKneeRomDeg { get; private set; }
    public double? RightKneeRomDeg { get; private set; }
    public double? LeftAnkleRomDeg { get; private set; }
    public double? RightAnkleRomDeg { get; private set; }
    public double? HipRomDeg => Average(LeftHipRomDeg, RightHipRomDeg);
    public double? KneeRomDeg => Average(LeftKneeRomDeg, RightKneeRomDeg);
    public double? AnkleRomDeg => Average(LeftAnkleRomDeg, RightAnkleRomDeg);
    public double? TrunkTiltMeanDeg { get; private set; }
    public double? TrunkTiltMaxDeg { get; private set; }
    public double? TrunkTiltMinDeg { get; private set; }
    public double? TrunkTiltRomDeg => Diff(TrunkTiltMinDeg, TrunkTiltMaxDeg);
    public double? PelvisTiltMeanDeg { get; private set; }
    public double? PelvisTiltMaxDeg { get; private set; }
    public double? PelvisTiltMinDeg { get; private set; }
    public double? PelvisRomDeg => Diff(PelvisTiltMinDeg, PelvisTiltMaxDeg);
    public double? PelvisCoronalRomDeg => PelvisRomDeg ?? PelvisTiltMaxDeg;
    public double? LeftStrideMeanM { get; private set; }
    public double? RightStrideMeanM { get; private set; }
    public double? LeftStanceRatioPct { get; private set; }
    public double? RightStanceRatioPct { get; private set; }
    public double? ValidFrameRatio { get; private set; }
    public double? SymmetryScore { get; private set; }
    public List<ReportAngleFrame> AngleFrames { get; } = [];
    public bool IsDualVideoMode { get; private set; }

    public static ReportAnalysisSnapshot From(Report report)
    {
        var snapshot = new ReportAnalysisSnapshot();
        snapshot.IsDualVideoMode = report.MeasurementRecord?.HasDualVideo == true;
        snapshot.LoadFromReportNavigation(report);
        var resultPath = ResolveResultJsonPath(report.AnalysisResult);
        if (!string.IsNullOrWhiteSpace(resultPath) && File.Exists(resultPath))
        {
            snapshot.LoadResultJson(resultPath);
            snapshot.ValidFrameRatio = ReadFrameCoverageRatio(resultPath, report.AnalysisResult?.OutputDirectory) ?? snapshot.ValidFrameRatio;
        }

        snapshot.ApplyPreferredVideoMetadata(report);

        var csvPath = ResolveJointAngleCsvPath(report.AnalysisResult);
        if (!string.IsNullOrWhiteSpace(csvPath) && File.Exists(csvPath))
        {
            snapshot.AngleFrames.AddRange(ParseAngleCsv(csvPath, snapshot.VideoFps ?? 30d, snapshot.VideoDurationSec));
        }

        snapshot.SymmetryScore = CalculateSymmetryScore(snapshot);
        return snapshot;
    }

    private static double? ReadFrameCoverageRatio(string resultPath, string? outputDirectory)
    {
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(resultPath))?.AsObject();
            return AnalysisFrameCoverageHelper.FromResultJson(root, outputDirectory)?.Ratio;
        }
        catch
        {
            return null;
        }
    }

    private void LoadFromReportNavigation(Report report)
    {
        var analysis = report.AnalysisResult;
        var gait = report.MeasurementRecord?.GaitParameters;
        MeanCycleDurationSec = analysis?.GaitCycleDurationS ?? gait?.GaitCycleDurationS;
        MeanStanceTimeSec = analysis?.StanceTimeS ?? gait?.StanceTimeS;
        MeanSwingTimeSec = analysis?.SwingTimeS ?? gait?.SwingTimeS;
        MeanDoubleSupportTimeSec = analysis?.DoubleSupportTimeS ?? gait?.DoubleSupportTimeS;
        MeanSingleSupportTimeSec = analysis?.SingleSupportTimeS;
        MeanStepLengthM = analysis?.StepLengthM ?? gait?.StepLengthM;
        MeanStrideLengthM = analysis?.StrideLengthM ?? gait?.StrideLengthM;
        GaitSpeedMPerS = analysis?.GaitSpeedMPerS ?? gait?.GaitSpeedMPerS ?? gait?.Velocity;
        CadenceStepPerMin = GaitCadenceCalculator.PreferCycleDerived(MeanCycleDurationSec, gait?.Cadence);
        LeftStrideMeanM = MetersFromCentimeters(gait?.StrideLengthLeft);
        RightStrideMeanM = MetersFromCentimeters(gait?.StrideLengthRight);
        LeftStanceRatioPct = gait?.StancePhaseLeft;
        RightStanceRatioPct = gait?.StancePhaseRight;
        ValidFrameRatio = report.QualityControl?.ValidFrameRatio ?? analysis?.QualityControl?.ValidFrameRatio;

        LeftHipRomDeg = report.KinematicSummary?.HipRomDeg ?? analysis?.KinematicSummary?.HipRomDeg;
        RightHipRomDeg = report.KinematicSummary?.HipRomDeg ?? analysis?.KinematicSummary?.HipRomDeg;
        LeftKneeRomDeg = report.KinematicSummary?.KneeRomDeg ?? analysis?.KinematicSummary?.KneeRomDeg;
        RightKneeRomDeg = report.KinematicSummary?.KneeRomDeg ?? analysis?.KinematicSummary?.KneeRomDeg;
        LeftAnkleRomDeg = report.KinematicSummary?.AnkleRomDeg ?? analysis?.KinematicSummary?.AnkleRomDeg;
        RightAnkleRomDeg = report.KinematicSummary?.AnkleRomDeg ?? analysis?.KinematicSummary?.AnkleRomDeg;
    }

    private void LoadResultJson(string path)
    {
        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
        if (root is null)
        {
            return;
        }

        var videoInfo = root["video_info"] as JsonObject;
        VideoFps = ReadDouble(videoInfo, "fps") ?? VideoFps;
        var frameCount = ReadInt(videoInfo, "frame_count");
        VideoFrameCount = frameCount ?? VideoFrameCount;
        VideoDurationSec = VideoFps is > 0 && frameCount is > 0
            ? frameCount.Value / VideoFps.Value
            : ReadDouble(videoInfo, "duration_sec") ?? VideoDurationSec;

        var gaitCycle = root["gait_cycle"] as JsonObject;
        CycleCount = ReadInt(gaitCycle, "cycle_count") ?? ReadInt(gaitCycle, "total_cycle_count") ?? CycleCount;
        MeanCycleDurationSec = AverageCycleDuration(gaitCycle) ?? MeanCycleDurationSec;

        var sp = root["spatiotemporal_parameters"] as JsonObject;
        var phaseMetrics = GaitPhaseMetricsCalculator.Calculate(gaitCycle);
        var eventPhaseMetrics = GaitPhaseMetricsCalculator.CalculateFromEvents(root["gait_events"] as JsonObject, VideoFps);
        CadenceStepPerMin = GaitCadenceCalculator.PreferCycleDerived(
            MeanCycleDurationSec,
            ReadDouble(sp, "cadence_step_per_min") ?? CadenceStepPerMin);
        GaitSpeedMPerS = ReadDouble(sp, "gait_velocity_m_per_sec") ?? GaitSpeedMPerS;
        MeanStepLengthM = ReadDouble(sp, "mean_step_length_m") ?? MeanStepLengthM;
        MeanStrideLengthM = ReadDouble(sp, "mean_stride_length_m") ?? MeanStrideLengthM;
        MeanStanceTimeSec = ReadDouble(sp, "mean_stance_time_sec") ?? phaseMetrics.MeanStanceTimeSec ?? eventPhaseMetrics.MeanStanceTimeSec ?? MeanStanceTimeSec;
        MeanSwingTimeSec = ReadDouble(sp, "mean_swing_time_sec") ?? phaseMetrics.MeanSwingTimeSec ?? eventPhaseMetrics.MeanSwingTimeSec ?? MeanSwingTimeSec;
        MeanDoubleSupportTimeSec = ReadDouble(sp, "mean_double_support_time_sec") ?? phaseMetrics.MeanDoubleSupportTimeSec ?? MeanDoubleSupportTimeSec;
        MeanSingleSupportTimeSec = ReadDouble(sp, "mean_single_support_time_sec") ?? phaseMetrics.MeanSingleSupportTimeSec ?? MeanSingleSupportTimeSec;

        var joint = root["joint_angles"] as JsonObject;
        var robustRom = RobustRomCalculator.Calculate(
            Path.GetDirectoryName(path),
            root,
            VideoFps,
            new RobustRomValues
            {
                LeftHipRomDeg = ReadJointRom(joint, "left_hip", "left hip") ?? LeftHipRomDeg,
                RightHipRomDeg = ReadJointRom(joint, "right_hip", "right hip") ?? RightHipRomDeg,
                LeftKneeRomDeg = ReadJointRom(joint, "left_knee", "left knee") ?? LeftKneeRomDeg,
                RightKneeRomDeg = ReadJointRom(joint, "right_knee", "right knee") ?? RightKneeRomDeg,
                LeftAnkleRomDeg = ReadJointRom(joint, "left_ankle", "left ankle") ?? LeftAnkleRomDeg,
                RightAnkleRomDeg = ReadJointRom(joint, "right_ankle", "right ankle") ?? RightAnkleRomDeg
            });
        LeftHipRomDeg = robustRom.LeftHipRomDeg ?? LeftHipRomDeg;
        RightHipRomDeg = robustRom.RightHipRomDeg ?? RightHipRomDeg;
        LeftKneeRomDeg = robustRom.LeftKneeRomDeg ?? LeftKneeRomDeg;
        RightKneeRomDeg = robustRom.RightKneeRomDeg ?? RightKneeRomDeg;
        LeftAnkleRomDeg = robustRom.LeftAnkleRomDeg ?? LeftAnkleRomDeg;
        RightAnkleRomDeg = robustRom.RightAnkleRomDeg ?? RightAnkleRomDeg;

        var segment = root["segment_angles"] as JsonObject;
        var trunk = segment?["trunk_tilt_deg"] as JsonObject;
        TrunkTiltMeanDeg = ReadDouble(trunk, "mean") ?? TrunkTiltMeanDeg;
        TrunkTiltMaxDeg = ReadDouble(trunk, "max") ?? TrunkTiltMaxDeg;
        TrunkTiltMinDeg = ReadDouble(trunk, "min") ?? TrunkTiltMinDeg;
        var pelvis = segment?["pelvis_tilt_deg"] as JsonObject;
        PelvisTiltMeanDeg = ReadDouble(pelvis, "mean") ?? PelvisTiltMeanDeg;
        PelvisTiltMaxDeg = ReadDouble(pelvis, "max") ?? PelvisTiltMaxDeg;
        PelvisTiltMinDeg = ReadDouble(pelvis, "min") ?? PelvisTiltMinDeg;

        LeftStrideMeanM = ReadDouble(root, "left_stride_mean_m") ?? LeftStrideMeanM;
        RightStrideMeanM = ReadDouble(root, "right_stride_mean_m") ?? RightStrideMeanM;
        LeftStanceRatioPct = ReadDouble(root, "left_stance_ratio_pct") ?? eventPhaseMetrics.LeftStanceRatioPct ?? LeftStanceRatioPct;
        RightStanceRatioPct = ReadDouble(root, "right_stance_ratio_pct") ?? eventPhaseMetrics.RightStanceRatioPct ?? RightStanceRatioPct;
        ValidFrameRatio = ReadDouble(root["quality_control"] as JsonObject, "valid_frame_ratio") ?? ValidFrameRatio;
    }

    private void ApplyPreferredVideoMetadata(Report report)
    {
        var metadata = ResolveInputVideoMetadata(report.MeasurementRecord)
            ?? VideoMetadataProbe.TryRead(report.AnalysisResult?.AnnotatedVideoPath);
        if (metadata is null)
        {
            return;
        }

        if (metadata.FrameRate is > 0
            && (VideoFps is not > 0 || Math.Abs(VideoFps.Value - metadata.FrameRate.Value) > 0.5d))
        {
            VideoFps = metadata.FrameRate;
        }

        if (metadata.DurationSeconds is > 0
            && (VideoDurationSec is not > 0 || Math.Abs(VideoDurationSec.Value - metadata.DurationSeconds.Value) > 0.5d))
        {
            VideoDurationSec = metadata.DurationSeconds;
        }
    }

    private static VideoProbeMetadata? ResolveInputVideoMetadata(MeasurementRecord? record)
    {
        var videoPaths = new[]
        {
            record?.SideVideoPath,
            record?.FrontVideoPath,
            record?.VideoFilePath
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

    private static string? ResolveResultJsonPath(BTFX.Models.Analysis.AnalysisResult? result)
    {
        if (result is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(result.SummaryFilePath) && File.Exists(result.SummaryFilePath))
        {
            return result.SummaryFilePath;
        }

        return Directory.Exists(result.OutputDirectory)
            ? Directory.GetFiles(result.OutputDirectory, "result.json", SearchOption.AllDirectories).FirstOrDefault()
            : null;
    }

    private static string? ResolveJointAngleCsvPath(BTFX.Models.Analysis.AnalysisResult? result)
    {
        if (result is null)
        {
            return null;
        }

        var csvPath = result.CsvFiles?.FirstOrDefault(file => file.FileType == CsvFileType.JointAngle && File.Exists(file.FilePath))?.FilePath;
        if (!string.IsNullOrWhiteSpace(csvPath))
        {
            return csvPath;
        }

        return Directory.Exists(result.OutputDirectory)
            ? Directory.GetFiles(result.OutputDirectory, "joint_angle.csv", SearchOption.AllDirectories).FirstOrDefault()
            : null;
    }

    private static List<ReportAngleFrame> ParseAngleCsv(string path, double fps, double? videoDurationSec)
    {
        var frames = new List<ReportAngleFrame>();
        var safeFps = fps > 0 ? fps : 30d;
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var parts = line.Split(',');
            if (parts.Length < 7 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var frameIndex))
            {
                continue;
            }

            var computedTime = frameIndex / safeFps;
            var csvTime = ReadCsvNullableDouble(parts, 9);
            var time = csvTime is >= 0
                       && (videoDurationSec is not > 0 || csvTime.Value <= videoDurationSec.Value + 0.5d)
                ? csvTime.Value
                : computedTime;

            frames.Add(new ReportAngleFrame(
                time,
                ReadCsvDouble(parts, 1),
                ReadCsvDouble(parts, 2),
                ReadCsvDouble(parts, 3),
                ReadCsvDouble(parts, 4),
                ReadCsvDouble(parts, 5),
                ReadCsvDouble(parts, 6),
                ReadCsvDouble(parts, 7),
                parts.Length > 8 ? ReadCsvDouble(parts, 8) : double.NaN));
        }

        return frames;
    }

    private static double? ReadCsvNullableDouble(string[] parts, int index)
        => index < parts.Length && double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static double ReadCsvDouble(string[] parts, int index)
        => index < parts.Length && double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;

    private static double? ReadDouble(JsonObject? obj, string name)
    {
        if (obj?[name] is JsonValue value && value.TryGetValue<double>(out var doubleValue))
        {
            return doubleValue;
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
               ?? Diff(ReadDouble(joint, "max_flexion_deg"), ReadDouble(joint, "min_flexion_deg"))
               ?? Diff(ReadDouble(joint, "max"), ReadDouble(joint, "min"));
    }

    private static int? ReadInt(JsonObject? obj, string name)
    {
        if (obj?[name] is JsonValue value && value.TryGetValue<int>(out var intValue))
        {
            return intValue;
        }

        return null;
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
            .ToArray();
        return values.Length == 0 ? null : values.Average();
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

    private static double? CalculateSymmetryScore(ReportAnalysisSnapshot snapshot)
    {
        var differences = new[]
        {
            DiffPercent(snapshot.LeftStrideMeanM, snapshot.RightStrideMeanM),
            DiffPercent(snapshot.LeftStanceRatioPct, snapshot.RightStanceRatioPct),
            DiffPercent(snapshot.LeftKneeRomDeg, snapshot.RightKneeRomDeg),
            DiffPercent(snapshot.LeftHipRomDeg, snapshot.RightHipRomDeg),
            DiffPercent(snapshot.LeftAnkleRomDeg, snapshot.RightAnkleRomDeg)
        }.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return differences.Length == 0 ? null : Math.Clamp(100d - differences.Average(), 0d, 100d);
    }

    private static double? Average(double? left, double? right)
        => left.HasValue && right.HasValue ? (left.Value + right.Value) / 2d : left ?? right;

    private static double? Diff(double? min, double? max)
        => min.HasValue && max.HasValue ? Math.Abs(max.Value - min.Value) : null;

    private static double? MetersFromCentimeters(double? centimeters)
        => centimeters.HasValue ? centimeters.Value / 100d : null;

    private static double? DiffPercent(double? left, double? right)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return null;
        }

        var denominator = (Math.Abs(left.Value) + Math.Abs(right.Value)) / 2d;
        return denominator <= 0.000001d ? null : Math.Abs(left.Value - right.Value) / denominator * 100d;
    }
}

internal sealed record ReportAngleFrame(
    double TimeS,
    double RightAnkle,
    double LeftAnkle,
    double RightKnee,
    double LeftKnee,
    double RightHip,
    double LeftHip,
    double Pelvis,
    double Trunk);

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
