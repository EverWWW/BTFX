using BTFX.Models;
using BTFX.Models.Analysis;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

public sealed class AnalysisReportCoordinator
{
    private readonly Func<MeasurementRecord, AnalysisResult, Task<bool>> _finalizeAnalysis;
    private readonly ILogHelper? _logHelper;

    public AnalysisReportCoordinator(
        AnalysisCompletionPersistenceService persistenceService,
        ILogHelper? logHelper = null)
        : this(persistenceService.FinalizeAsync, logHelper)
    {
    }

    internal AnalysisReportCoordinator(
        Func<MeasurementRecord, AnalysisResult, Task<bool>> finalizeAnalysis,
        ILogHelper? logHelper = null)
    {
        _finalizeAnalysis = finalizeAnalysis;
        _logHelper = logHelper;
    }

    public async Task<bool> FinalizeAsync(MeasurementRecord measurement, AnalysisResult result)
    {
        try
        {
            return await _finalizeAnalysis(measurement, result);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"分析完成收尾失败: MeasurementId={measurement.Id}", ex);
            return false;
        }
    }
}
