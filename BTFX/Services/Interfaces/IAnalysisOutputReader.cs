using BTFX.Models.Analysis;

namespace BTFX.Services.Interfaces;

/// <summary>
/// Reads algorithm output files and converts them to the application's analysis summary model.
/// </summary>
public interface IAnalysisOutputReader
{
    /// <summary>
    /// Reads result.json or summary.json from an algorithm output directory.
    /// </summary>
    Task<AnalysisOutputReadResult> ReadAsync(string outputDirectory, CancellationToken cancellationToken = default);
}

/// <summary>
/// Algorithm output read result.
/// </summary>
public sealed class AnalysisOutputReadResult
{
    public required string SummaryPath { get; init; }

    public string? ResultPath { get; init; }

    public required AnalysisSummary Summary { get; init; }
}
