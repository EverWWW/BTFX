using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

public sealed class MeasurementWorkflowCoordinator : IMeasurementWorkflowCoordinator
{
    public event EventHandler<MeasurementWorkflowResumeRequestedEventArgs>? ResumeRequested;

    public void RequestResume(MeasurementRecord record, MeasurementResumeDecision decision)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(decision);

        ResumeRequested?.Invoke(this, new MeasurementWorkflowResumeRequestedEventArgs(record, decision));
    }
}
