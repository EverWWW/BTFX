using BTFX.Common;
using BTFX.Data;
using BTFX.Models;
using BTFX.Models.Analysis;
using ToolHelper.Database.Sqlite;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

public sealed class AnalysisCompletionPersistenceService
{
    private readonly Func<SqliteSugarHelper> _databaseFactory;
    private readonly ILogHelper? _logHelper;

    public AnalysisCompletionPersistenceService(ILogHelper? logHelper = null)
        : this(DatabaseFactory.CreateSqliteSugarHelper, logHelper)
    {
    }

    internal AnalysisCompletionPersistenceService(
        Func<SqliteSugarHelper> databaseFactory,
        ILogHelper? logHelper = null)
    {
        _databaseFactory = databaseFactory;
        _logHelper = logHelper;
    }

    public async Task<bool> FinalizeAsync(MeasurementRecord measurement, AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        ArgumentNullException.ThrowIfNull(result);
        if (measurement.Id <= 0 || result.Id <= 0 || result.MeasurementId != measurement.Id || !result.Success)
        {
            return false;
        }

        using var db = _databaseFactory();
        db.BeginTran();
        try
        {
            var updated = await db.UpdateAsync<MeasurementRecord>(
                record => new MeasurementRecord
                {
                    Status = MeasurementStatus.Completed,
                    UpdatedAt = DateTime.Now
                },
                record => record.Id == measurement.Id);
            if (updated <= 0)
            {
                throw new InvalidOperationException($"找不到待完成的测量记录：{measurement.Id}");
            }

            var reports = await db.Queryable<Report>()
                .Where(report => report.MeasurementId == measurement.Id)
                .OrderByDescending(report => report.UpdatedAt)
                .OrderByDescending(report => report.Id)
                .ToListAsync();
            var report = reports.FirstOrDefault();
            if (report is null)
            {
                report = new Report
                {
                    MeasurementId = measurement.Id,
                    PatientId = measurement.PatientId,
                    CreatedBy = measurement.OperatorId,
                    AnalysisResultId = result.Id,
                    ReportNumber = $"RPT-{DateTime.Now:yyyyMMdd}-{measurement.Id:D6}",
                    Status = ReportStatus.Completed,
                    Title = string.Empty,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                await db.InsertAsync(report);
            }
            else
            {
                report.PatientId = measurement.PatientId;
                report.CreatedBy = measurement.OperatorId;
                report.AnalysisResultId = result.Id;
                report.Status = ReportStatus.Completed;
                report.UpdatedAt = DateTime.Now;
                await db.UpdateAsync(report);

                var duplicateIds = reports.Skip(1).Select(item => item.Id).ToArray();
                if (duplicateIds.Length > 0)
                {
                    await db.DeleteAsync<Report>(item => duplicateIds.Contains(item.Id));
                }
            }

            db.CommitTran();
            return true;
        }
        catch (Exception ex)
        {
            db.RollbackTran();
            _logHelper?.Error($"分析完成数据收尾失败：MeasurementId={measurement.Id}, AnalysisResultId={result.Id}", ex);
            return false;
        }
    }
}
