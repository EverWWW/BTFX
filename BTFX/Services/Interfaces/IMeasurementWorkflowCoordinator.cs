using BTFX.Models;

namespace BTFX.Services.Interfaces;

public interface IMeasurementWorkflowCoordinator
{
    event EventHandler<MeasurementWorkflowResumeRequestedEventArgs>? ResumeRequested;

    void RequestResume(MeasurementRecord record, MeasurementResumeDecision decision);
}

public sealed class MeasurementWorkflowResumeRequestedEventArgs : EventArgs
{
    public MeasurementWorkflowResumeRequestedEventArgs(MeasurementRecord record, MeasurementResumeDecision decision)
    {
        Record = record;
        Decision = decision;
    }

    public MeasurementRecord Record { get; }

    public MeasurementResumeDecision Decision { get; }
}
