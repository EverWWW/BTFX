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
