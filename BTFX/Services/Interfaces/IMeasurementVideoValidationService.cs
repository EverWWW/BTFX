using BTFX.Models;

namespace BTFX.Services.Interfaces;

public interface IMeasurementVideoValidationService
{
    Task<MeasurementVideoValidationResult> ValidateAsync(MeasurementRecord record, CancellationToken cancellationToken = default);
}

public sealed record MeasurementVideoValidationResult(
    bool HasAnyVideo,
    bool CanContinue,
    string Message);
