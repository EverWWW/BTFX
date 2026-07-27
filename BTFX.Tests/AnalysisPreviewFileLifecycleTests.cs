using BTFX.Helpers;
using Xunit;

namespace BTFX.Tests;

public sealed class AnalysisPreviewFileLifecycleTests : IDisposable
{
    private readonly string _outputDirectory = Path.Combine(
        Path.GetTempPath(),
        $"btfx-preview-files-{Guid.NewGuid():N}");

    [Fact]
    public void GetReadyPreviewPath_ReturnsNull_WhilePreviewIsGenerating()
    {
        Directory.CreateDirectory(AnalysisPreviewFiles.GetPreviewDirectory(_outputDirectory));
        File.WriteAllText(AnalysisPreviewFiles.GetPreviewPath(_outputDirectory), "old preview");
        File.WriteAllText(AnalysisPreviewFiles.GetGeneratingPath(_outputDirectory), "generating");

        Assert.Null(AnalysisPreviewFiles.GetReadyPreviewPath(_outputDirectory));
    }

    [Fact]
    public void GetReadyPreviewPath_IgnoresEncodingOutput()
    {
        Directory.CreateDirectory(AnalysisPreviewFiles.GetPreviewDirectory(_outputDirectory));
        File.WriteAllText(AnalysisPreviewFiles.GetEncodingPath(_outputDirectory), "incomplete preview");

        Assert.Null(AnalysisPreviewFiles.GetReadyPreviewPath(_outputDirectory));
    }

    [Fact]
    public void GetReadyPreviewPath_ReturnsNull_AfterGenerationFailure()
    {
        Directory.CreateDirectory(AnalysisPreviewFiles.GetPreviewDirectory(_outputDirectory));
        File.WriteAllText(AnalysisPreviewFiles.GetPreviewPath(_outputDirectory), "partial preview");
        File.WriteAllText(AnalysisPreviewFiles.GetFailedPath(_outputDirectory), "failed");

        Assert.Null(AnalysisPreviewFiles.GetReadyPreviewPath(_outputDirectory));
    }

    [Fact]
    public void GetReadyPreviewPath_ReturnsPublishedPreview()
    {
        Directory.CreateDirectory(AnalysisPreviewFiles.GetPreviewDirectory(_outputDirectory));
        var previewPath = AnalysisPreviewFiles.GetPreviewPath(_outputDirectory);
        File.WriteAllText(previewPath, "complete preview");

        Assert.Equal(previewPath, AnalysisPreviewFiles.GetReadyPreviewPath(_outputDirectory));
    }

    [Fact]
    public void PublishEncodingOutput_AtomicallyReplacesFinalPreview()
    {
        Directory.CreateDirectory(AnalysisPreviewFiles.GetPreviewDirectory(_outputDirectory));
        var previewPath = AnalysisPreviewFiles.GetPreviewPath(_outputDirectory);
        var encodingPath = AnalysisPreviewFiles.GetEncodingPath(_outputDirectory);
        File.WriteAllText(previewPath, "old preview");
        File.WriteAllText(encodingPath, "complete preview");

        AnalysisPreviewFiles.PublishEncodingOutput(_outputDirectory);

        Assert.Equal("complete preview", File.ReadAllText(previewPath));
        Assert.False(File.Exists(encodingPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }
}
