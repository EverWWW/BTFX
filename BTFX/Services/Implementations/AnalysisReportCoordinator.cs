using BTFX.Models;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

public sealed class AnalysisReportCoordinator
{
    private readonly Func<int, int, Task<Report?>> _getOrCreateReport;
    private readonly ILogHelper? _logHelper;

    public AnalysisReportCoordinator(IReportService reportService, ILogHelper? logHelper = null)
        : this(reportService.GetOrCreateDraftReportAsync, logHelper)
    {
    }

    internal AnalysisReportCoordinator(
        Func<int, int, Task<Report?>> getOrCreateReport,
        ILogHelper? logHelper = null)
    {
        _getOrCreateReport = getOrCreateReport;
        _logHelper = logHelper;
    }

    public async Task<bool> EnsureReportExistsAsync(int measurementId, int operatorId)
    {
        try
        {
            var report = await _getOrCreateReport(measurementId, operatorId);
            return report is not null;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"自动创建报告失败: MeasurementId={measurementId}", ex);
            return false;
        }
    }
}
