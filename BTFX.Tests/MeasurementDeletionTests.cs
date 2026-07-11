using BTFX.Data;
using BTFX.Models;
using BTFX.Models.Analysis;
using BTFX.Services.Implementations;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Sqlite;
using Xunit;

namespace BTFX.Tests;

public sealed class MeasurementDeletionTests
{
    [Fact]
    public async Task DeleteMeasurementAsync_RemovesRelatedRowsAndOwnedResultsOnly()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "btfx-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(testRoot, "test.db");
        var resultDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Analysis",
            $"delete-test-{Guid.NewGuid():N}");
        var externalVideoDirectory = Path.Combine(testRoot, "external-video");
        var externalVideoPath = Path.Combine(externalVideoDirectory, "side.mp4");
        Directory.CreateDirectory(testRoot);
        Directory.CreateDirectory(resultDirectory);
        Directory.CreateDirectory(externalVideoDirectory);
        File.WriteAllText(Path.Combine(resultDirectory, "result.json"), "result");
        File.WriteAllText(externalVideoPath, "video");

        SqliteSugarHelper CreateDatabase() => new(new SqliteSugarOptions
        {
            DatabasePath = databasePath,
            EnableSqlLog = false
        });

        try
        {
            int measurementId;
            int analysisResultId;
            using (var db = CreateDatabase())
            {
                db.CreateTables(
                    typeof(MeasurementRecord),
                    typeof(GaitParameters),
                    typeof(Report),
                    typeof(AnalysisResult),
                    typeof(KinematicSummary),
                    typeof(AnalysisCsvFile),
                    typeof(QualityControlInfo));

                measurementId = (int)await db.InsertReturnIdentityAsync(new MeasurementRecord
                {
                    PatientId = 1,
                    OperatorId = 1,
                    MeasurementName = "delete test",
                    SideVideoPath = externalVideoPath
                });
                analysisResultId = (int)await db.InsertReturnIdentityAsync(new AnalysisResult
                {
                    MeasurementId = measurementId,
                    RequestId = "DELETE_TEST",
                    OutputDirectory = resultDirectory
                });
                await db.InsertAsync(new GaitParameters { MeasurementRecordId = measurementId });
                await db.InsertAsync(new KinematicSummary { AnalysisResultId = analysisResultId });
                await db.InsertAsync(new QualityControlInfo { AnalysisResultId = analysisResultId });
                await db.InsertAsync(new AnalysisCsvFile
                {
                    AnalysisResultId = analysisResultId,
                    FilePath = Path.Combine(resultDirectory, "angles.csv")
                });
                await db.InsertAsync(new Report
                {
                    MeasurementId = measurementId,
                    AnalysisResultId = analysisResultId,
                    PatientId = 1,
                    CreatedBy = 1,
                    ReportNumber = "REPORT_DELETE_TEST"
                });
            }

            var service = new MeasurementService(CreateDatabase);

            Assert.True(await service.DeleteMeasurementAsync(measurementId));

            using var verifyDb = CreateDatabase();
            Assert.Equal(0, await verifyDb.Queryable<MeasurementRecord>().CountAsync());
            Assert.Equal(0, await verifyDb.Queryable<GaitParameters>().CountAsync());
            Assert.Equal(0, await verifyDb.Queryable<AnalysisResult>().CountAsync());
            Assert.Equal(0, await verifyDb.Queryable<KinematicSummary>().CountAsync());
            Assert.Equal(0, await verifyDb.Queryable<QualityControlInfo>().CountAsync());
            Assert.Equal(0, await verifyDb.Queryable<AnalysisCsvFile>().CountAsync());
            Assert.Equal(0, await verifyDb.Queryable<Report>().CountAsync());
            Assert.False(Directory.Exists(resultDirectory));
            Assert.True(File.Exists(externalVideoPath));
        }
        finally
        {
            if (Directory.Exists(resultDirectory))
            {
                Directory.Delete(resultDirectory, recursive: true);
            }

            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
