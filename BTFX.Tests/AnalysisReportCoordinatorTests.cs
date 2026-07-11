using BTFX.Models;
using BTFX.Models.Analysis;
using BTFX.Services.Implementations;
using Xunit;

namespace BTFX.Tests;

public sealed class AnalysisReportCoordinatorTests
{
    [Fact]
    public async Task FinalizeAsync_ReturnsTrueWhenPersistenceSucceeds()
    {
        var calls = 0;
        var coordinator = new AnalysisReportCoordinator((measurement, result) =>
        {
            calls++;
            return Task.FromResult(measurement.Id == result.MeasurementId);
        });

        var succeeded = await coordinator.FinalizeAsync(CreateMeasurement(), CreateResult());

        Assert.True(succeeded);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task FinalizeAsync_ReturnsFalseWhenPersistenceFails()
    {
        var coordinator = new AnalysisReportCoordinator(
            (_, _) => Task.FromResult(false));

        var result = await coordinator.FinalizeAsync(CreateMeasurement(), CreateResult());

        Assert.False(result);
    }

    [Fact]
    public async Task FinalizeAsync_IsolatesPersistenceFailure()
    {
        var coordinator = new AnalysisReportCoordinator(
            (_, _) => throw new InvalidOperationException("database unavailable"));

        var exception = await Record.ExceptionAsync(() => coordinator.FinalizeAsync(CreateMeasurement(), CreateResult()));

        Assert.Null(exception);
        Assert.False(await coordinator.FinalizeAsync(CreateMeasurement(), CreateResult()));
    }

    private static MeasurementRecord CreateMeasurement() => new() { Id = 12, OperatorId = 3 };

    private static AnalysisResult CreateResult() => new() { Id = 5, MeasurementId = 12, Success = true };
}
