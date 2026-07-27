using Xunit;

namespace BTFX.Tests;

public sealed class MeasurementLengthUnitTests
{
    [Fact]
    public void MeasurementAndReportViews_UseMetersForGaitLengths()
    {
        var projectDirectory = FindProjectDirectory();
        var analysisViewModel = File.ReadAllText(Path.Combine(
            projectDirectory,
            "ViewModels",
            "Measurement",
            "Step4AnalyzeViewModel.cs"));
        var reportViewModel = File.ReadAllText(Path.Combine(projectDirectory, "ViewModels", "ReportViewModel.cs"));
        var reportPreviewViewModel = File.ReadAllText(Path.Combine(
            projectDirectory,
            "ViewModels",
            "ReportPreviewDialogViewModel.cs"));
        var reportPreviewHelper = File.ReadAllText(Path.Combine(projectDirectory, "Helpers", "ReportPreviewHelper.cs"));
        var reportPdfExporter = File.ReadAllText(Path.Combine(projectDirectory, "Helpers", "ReportPdfExporter.cs"));

        Assert.DoesNotContain("FormatLengthCm", analysisViewModel, StringComparison.Ordinal);
        Assert.Contains("StepLength = FormatLengthMeters(result.StepLengthM)", analysisViewModel, StringComparison.Ordinal);
        Assert.Contains("StrideLength = FormatLengthMeters(result.StrideLengthM)", analysisViewModel, StringComparison.Ordinal);
        Assert.Contains("$\"{valueInMeters.Value:F2} m\"", analysisViewModel, StringComparison.Ordinal);

        Assert.DoesNotContain("StrideLengthLeft?.ToString(\"F2\") ?? \"--\"} cm", reportViewModel, StringComparison.Ordinal);
        Assert.Contains("ReportPreview.Param.MeanStepLength\"), FormatMeters(data.MeanStepLengthM), \"m\"", reportPreviewViewModel, StringComparison.Ordinal);
        Assert.Contains("ReportPreview.Param.MeanStrideLength\"), FormatMeters(data.MeanStrideLengthM), \"m\"", reportPreviewViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("StrideLengthLeft, \"cm\"", reportPreviewHelper, StringComparison.Ordinal);
        Assert.DoesNotContain("StrideLengthRight, \"cm\"", reportPreviewHelper, StringComparison.Ordinal);
        Assert.DoesNotContain("StrideLengthLeft, \"cm\"", reportPdfExporter, StringComparison.Ordinal);
        Assert.DoesNotContain("StrideLengthRight, \"cm\"", reportPdfExporter, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(63.0, 0.63)]
    [InlineData(126.0, 1.26)]
    public void GaitLengthUnitConverter_ConvertsStoredCentimetersToMeters(double centimeters, double expectedMeters)
    {
        Assert.Equal(expectedMeters, BTFX.Helpers.GaitLengthUnitConverter.ToMeters(centimeters), precision: 6);
    }

    [Fact]
    public void AllReportPaths_UseStepPerMinuteForCadence()
    {
        var projectDirectory = FindProjectDirectory();
        var reportPreviewHelper = File.ReadAllText(Path.Combine(projectDirectory, "Helpers", "ReportPreviewHelper.cs"));
        var reportPdfExporter = File.ReadAllText(Path.Combine(projectDirectory, "Helpers", "ReportPdfExporter.cs"));

        Assert.Contains("GaitCadenceCalculator.PreferCycleDerived(gait?.GaitCycleDurationS, gait?.Cadence)", reportPreviewHelper, StringComparison.Ordinal);
        Assert.Contains("GaitCadenceCalculator.PreferCycleDerived(gait?.GaitCycleDurationS, gait?.Cadence)", reportPdfExporter, StringComparison.Ordinal);
        Assert.DoesNotContain("gait?.Cadence, \"步/分\"", reportPreviewHelper, StringComparison.Ordinal);
        Assert.DoesNotContain("gait?.Cadence, \"步/分\"", reportPdfExporter, StringComparison.Ordinal);
    }

    [Fact]
    public void AllLegacyReportPaths_DisplayDoubleSupportTimeInSeconds()
    {
        var projectDirectory = FindProjectDirectory();
        var reportViewModel = File.ReadAllText(Path.Combine(projectDirectory, "ViewModels", "ReportViewModel.cs"));
        var reportPreviewHelper = File.ReadAllText(Path.Combine(projectDirectory, "Helpers", "ReportPreviewHelper.cs"));
        var reportPdfExporter = File.ReadAllText(Path.Combine(projectDirectory, "Helpers", "ReportPdfExporter.cs"));

        Assert.Contains("gait?.DoubleSupportTimeS, \"s\"", reportPreviewHelper, StringComparison.Ordinal);
        Assert.Contains("gait?.DoubleSupportTimeS, \"s\"", reportPdfExporter, StringComparison.Ordinal);
        Assert.Contains("gait.DoubleSupportTimeS?.ToString(\"F2\")", reportViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("gait?.DoubleSupport, \"%\"", reportPreviewHelper, StringComparison.Ordinal);
        Assert.DoesNotContain("gait?.DoubleSupport, \"%\"", reportPdfExporter, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalysisDetailAndReport_UseStrideDifferenceTerminology()
    {
        var projectDirectory = FindProjectDirectory();
        var detailView = File.ReadAllText(Path.Combine(
            projectDirectory,
            "Views",
            "Dialogs",
            "MeasurementDetailDialog.xaml"));
        var detailViewModel = File.ReadAllText(Path.Combine(
            projectDirectory,
            "ViewModels",
            "GaitAnalysisDetailViewModel.cs"));
        var chineseResources = File.ReadAllText(Path.Combine(
            projectDirectory,
            "Resources",
            "Localization",
            "Strings.zh.xaml"));
        var englishResources = File.ReadAllText(Path.Combine(
            projectDirectory,
            "Resources",
            "Localization",
            "Strings.en.xaml"));

        Assert.DoesNotContain("AnalysisDetail.StepLengthDiff", detailView, StringComparison.Ordinal);
        Assert.DoesNotContain("StepLengthDiffDisplay", detailViewModel, StringComparison.Ordinal);
        Assert.Contains("<system:String x:Key=\"AnalysisDetail.StrideDiff\">左右步幅差</system:String>", chineseResources, StringComparison.Ordinal);
        Assert.Contains("<system:String x:Key=\"AnalysisDetail.StrideDiffPercent\">左右步幅差百分比</system:String>", chineseResources, StringComparison.Ordinal);
        Assert.Contains("<system:String x:Key=\"ReportPreview.Param.StrideDiff\">左右步幅差</system:String>", chineseResources, StringComparison.Ordinal);
        Assert.Contains("<system:String x:Key=\"ReportPreview.Param.StrideDiffPercent\">左右步幅差百分比</system:String>", chineseResources, StringComparison.Ordinal);
        Assert.Contains("<system:String x:Key=\"AnalysisDetail.StrideDiff\">Stride Difference</system:String>", englishResources, StringComparison.Ordinal);
        Assert.Contains("<system:String x:Key=\"ReportPreview.Param.StrideDiff\">Stride Difference</system:String>", englishResources, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsDataTab_UsesStorageManagementName()
    {
        var projectDirectory = FindProjectDirectory();
        var chineseResources = File.ReadAllText(Path.Combine(
            projectDirectory,
            "Resources",
            "Localization",
            "Strings.zh.xaml"));
        var englishResources = File.ReadAllText(Path.Combine(
            projectDirectory,
            "Resources",
            "Localization",
            "Strings.en.xaml"));

        Assert.Contains("<system:String x:Key=\"DataManagementTab\">存储管理</system:String>", chineseResources, StringComparison.Ordinal);
        Assert.Contains("<system:String x:Key=\"DataManagementTab\">Storage</system:String>", englishResources, StringComparison.Ordinal);
    }

    private static string FindProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "BTFX", "BTFX.csproj");
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate)!;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the BTFX project directory.");
    }
}
