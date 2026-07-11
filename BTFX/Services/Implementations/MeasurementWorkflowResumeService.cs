using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

public sealed class MeasurementWorkflowResumeService : IMeasurementWorkflowResumeService
{
    private readonly IMeasurementVideoValidationService _videoValidationService;
    private readonly IGaitAnalysisService _analysisService;
    private readonly ILocalizationService _localizationService;

    public MeasurementWorkflowResumeService(
        IMeasurementVideoValidationService videoValidationService,
        IGaitAnalysisService analysisService,
        ILocalizationService localizationService)
    {
        _videoValidationService = videoValidationService;
        _analysisService = analysisService;
        _localizationService = localizationService;
    }

    public async Task<MeasurementResumeDecision> DecideAsync(
        MeasurementRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var videoValidation = await _videoValidationService.ValidateAsync(record, cancellationToken);

        return record.Status switch
        {
            MeasurementStatus.Pending => videoValidation.CanContinue
                ? new MeasurementResumeDecision(true, 2, L("Measurement.Resume.Action.Continue"), L("Measurement.Resume.Message.PendingReview"))
                : new MeasurementResumeDecision(true, 1, L("Measurement.Resume.Action.Continue"), videoValidation.Message),

            MeasurementStatus.InProgress => _analysisService.IsAnalysisRunning
                ? new MeasurementResumeDecision(true, 3, L("Measurement.Resume.Action.ViewProgress"), L("Measurement.Resume.Message.InProgress"))
                : new MeasurementResumeDecision(true, 3, L("Measurement.Resume.Action.Continue"), L("Measurement.Resume.Message.ReanalysisNeeded"), RequiresReanalysis: true),

            MeasurementStatus.Completed => new MeasurementResumeDecision(true, 3, L("Measurement.Resume.Action.ViewDetails"), L("Measurement.Resume.Message.Completed")),

            MeasurementStatus.Failed => videoValidation.CanContinue
                ? new MeasurementResumeDecision(true, 3, L("Measurement.Resume.Action.Reanalyze"), L("Measurement.Resume.Message.Failed"), RequiresReanalysis: true)
                : new MeasurementResumeDecision(true, 1, L("Measurement.Resume.Action.Continue"), videoValidation.Message, RequiresReanalysis: true),

            MeasurementStatus.Cancelled => new MeasurementResumeDecision(true, 1, L("Measurement.Resume.Action.Continue"), L("Measurement.Resume.Message.Cancelled")),

            _ => new MeasurementResumeDecision(true, 1, L("Measurement.Resume.Action.Continue"), L("Measurement.Resume.Message.Default"))
        };
    }

    private string L(string key) => _localizationService.GetString(key);
}
