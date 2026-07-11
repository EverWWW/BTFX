using BTFX.Data;
using BTFX.Models;
using BTFX.Models.Analysis;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

public sealed class AnalysisConsistencyRepairService
{
    private readonly AnalysisCompletionPersistenceService _persistenceService;
    private readonly ILogHelper? _logHelper;

    public AnalysisConsistencyRepairService(
        AnalysisCompletionPersistenceService persistenceService,
        ILogHelper? logHelper = null)
    {
        _persistenceService = persistenceService;
        _logHelper = logHelper;
    }

    public async Task RepairAsync()
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            var successfulResults = await db.Queryable<AnalysisResult>()
                .Where(result => result.Success)
                .OrderByDescending(result => result.CreatedAt)
                .ToListAsync();
            var latestResults = successfulResults
                .GroupBy(result => result.MeasurementId)
                .Select(group => group.First())
                .ToList();

            foreach (var result in latestResults)
            {
                var measurement = await db.GetByIdAsync<MeasurementRecord>(result.MeasurementId);
                if (measurement is not null)
                {
                    await _persistenceService.FinalizeAsync(measurement, result);
                }
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("分析结果一致性修复失败", ex);
        }
    }
}
