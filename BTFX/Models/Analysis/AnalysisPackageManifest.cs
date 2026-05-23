namespace BTFX.Models.Analysis;

/// <summary>
/// Manifest stored inside a BTFX analysis package.
/// </summary>
public sealed class AnalysisPackageManifest
{
    public string PackageVersion { get; set; } = "1.0";

    public string AppVersion { get; set; } = string.Empty;

    public int MeasurementId { get; set; }

    public string? MeasurementName { get; set; }

    public int PatientId { get; set; }

    public string? PatientName { get; set; }

    public int AnalysisResultId { get; set; }

    public string RequestId { get; set; } = string.Empty;

    public string TaskStatus { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string OutputDirectory { get; set; } = string.Empty;

    public string? SideVideoPath { get; set; }

    public long? SideVideoSize { get; set; }

    public DateTime? SideVideoModifiedAt { get; set; }

    public string? FrontVideoPath { get; set; }

    public long? FrontVideoSize { get; set; }

    public DateTime? FrontVideoModifiedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<AnalysisPackageFile> Files { get; set; } = [];
}

public sealed class AnalysisPackageFile
{
    public string EntryName { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Sha256 { get; set; } = string.Empty;
}
