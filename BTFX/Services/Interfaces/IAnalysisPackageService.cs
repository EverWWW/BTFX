using BTFX.Models;
using BTFX.Models.Analysis;

namespace BTFX.Services.Interfaces;

public interface IAnalysisPackageService
{
    Task<AnalysisPackageOperationResult> CreatePackageAsync(
        AnalysisResult result,
        MeasurementRecord? measurement,
        CancellationToken cancellationToken = default);

    Task<AnalysisPackageValidationResult> ValidatePackageAsync(
        AnalysisResult result,
        CancellationToken cancellationToken = default);
}

public sealed class AnalysisPackageOperationResult
{
    public bool Success { get; init; }

    public string? PackagePath { get; init; }

    public string Message { get; init; } = string.Empty;
}

public sealed class AnalysisPackageValidationResult
{
    public bool IsValid { get; init; }

    public string Message { get; init; } = string.Empty;
}
