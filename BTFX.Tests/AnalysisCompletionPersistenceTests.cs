using BTFX.Common;
using BTFX.Models;
using BTFX.Models.Analysis;
using BTFX.Services.Implementations;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Sqlite;
using Xunit;

namespace BTFX.Tests;

public sealed class AnalysisCompletionPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "btfx-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FinalizeAsync_KeepsOneReportAndMovesItToLatestResult()
    {
        var databasePath = Path.Combine(_root, "test.db");
        Directory.CreateDirectory(_root);
        SqliteSugarHelper CreateDatabase() => new(new SqliteSugarOptions
        {
            DatabasePath = databasePath,
            EnableSqlLog = false
        });

        int measurementId;
        int firstResultId;
        int secondResultId;
        using (var db = CreateDatabase())
        {
            db.CreateTables(typeof(MeasurementRecord), typeof(AnalysisResult), typeof(Report));
            measurementId = (int)await db.InsertReturnIdentityAsync(new MeasurementRecord
            {
                PatientId = 5,
                OperatorId = 7,
                MeasurementName = "reanalysis",
                Status = MeasurementStatus.InProgress
            });
            firstResultId = (int)await db.InsertReturnIdentityAsync(new AnalysisResult
            {
                MeasurementId = measurementId,
                RequestId = "FIRST",
                Success = true,
                CreatedAt = DateTime.Now.AddMinutes(-1)
            });
            secondResultId = (int)await db.InsertReturnIdentityAsync(new AnalysisResult
            {
                MeasurementId = measurementId,
                RequestId = "SECOND",
                Success = true,
                CreatedAt = DateTime.Now
            });
        }

        var measurement = new MeasurementRecord
        {
            Id = measurementId,
            PatientId = 5,
            OperatorId = 7,
            MeasurementName = "reanalysis"
        };
        var service = new AnalysisCompletionPersistenceService(CreateDatabase);

        Assert.True(await service.FinalizeAsync(measurement, new AnalysisResult
        {
            Id = firstResultId,
            MeasurementId = measurementId,
            Success = true
        }));
        Assert.True(await service.FinalizeAsync(measurement, new AnalysisResult
        {
            Id = secondResultId,
            MeasurementId = measurementId,
            Success = true
        }));

        using var verify = CreateDatabase();
        var storedMeasurement = await verify.GetByIdAsync<MeasurementRecord>(measurementId);
        var reports = await verify.Queryable<Report>().Where(r => r.MeasurementId == measurementId).ToListAsync();
        var report = Assert.Single(reports);
        Assert.Equal(MeasurementStatus.Completed, storedMeasurement!.Status);
        Assert.Equal(secondResultId, report.AnalysisResultId);
        Assert.Equal(ReportStatus.Completed, report.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
