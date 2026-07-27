using System.IO;

namespace BTFX.Helpers;

internal static class AnalysisPreviewFiles
{
    private const string PreviewDirectoryName = "preview";
    private const string PreviewFileName = "analysis_preview.mp4";
    private const string EncodingFileName = "analysis_preview.encoding.mp4";
    private const string GeneratingFileName = "analysis_preview.generating";
    private const string FailedFileName = "analysis_preview.failed";

    internal static string GetPreviewDirectory(string outputDirectory)
    {
        return Path.Combine(outputDirectory, PreviewDirectoryName);
    }

    internal static string GetPreviewPath(string outputDirectory)
    {
        return Path.Combine(GetPreviewDirectory(outputDirectory), PreviewFileName);
    }

    internal static string GetEncodingPath(string outputDirectory)
    {
        return Path.Combine(GetPreviewDirectory(outputDirectory), EncodingFileName);
    }

    internal static string GetGeneratingPath(string outputDirectory)
    {
        return Path.Combine(GetPreviewDirectory(outputDirectory), GeneratingFileName);
    }

    internal static string GetFailedPath(string outputDirectory)
    {
        return Path.Combine(GetPreviewDirectory(outputDirectory), FailedFileName);
    }

    internal static string? GetReadyPreviewPath(string outputDirectory)
    {
        var previewPath = GetPreviewPath(outputDirectory);
        return !File.Exists(GetGeneratingPath(outputDirectory))
               && !File.Exists(GetFailedPath(outputDirectory))
               && File.Exists(previewPath)
            ? previewPath
            : null;
    }

    internal static void PublishEncodingOutput(string outputDirectory)
    {
        var encodingPath = GetEncodingPath(outputDirectory);
        if (!File.Exists(encodingPath))
        {
            throw new FileNotFoundException("Preview encoding output was not created.", encodingPath);
        }

        Directory.CreateDirectory(GetPreviewDirectory(outputDirectory));
        File.Move(encodingPath, GetPreviewPath(outputDirectory), overwrite: true);
    }
}
