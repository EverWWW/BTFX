using BTFX.Models;

namespace BTFX.Services.Interfaces;

public interface IMeasurementWorkflowResumeService
{
    Task<MeasurementResumeDecision> DecideAsync(MeasurementRecord record, CancellationToken cancellationToken = default);
}

public sealed record MeasurementResumeDecision(
    bool CanResume,
    int TargetStep,
    string ActionText,
    string Message,
    bool RequiresReanalysis = false);
