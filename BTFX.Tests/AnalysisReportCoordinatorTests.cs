using BTFX.Models;
using BTFX.Services.Implementations;
using Xunit;

namespace BTFX.Tests;

public sealed class AnalysisReportCoordinatorTests
{
    [Fact]
    public async Task EnsureReportExistsAsync_ReturnsTrueWhenReportExists()
    {
        var calls = 0;
        var coordinator = new AnalysisReportCoordinator((measurementId, operatorId) =>
        {
            calls++;
            return Task.FromResult<Report?>(new Report
            {
                Id = 9,
                MeasurementId = measurementId,
                CreatedBy = operatorId
            });
        });

        var result = await coordinator.EnsureReportExistsAsync(12, 3);

        Assert.True(result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task EnsureReportExistsAsync_ReturnsFalseWhenReportCannotBeCreated()
    {
        var coordinator = new AnalysisReportCoordinator(
            (_, _) => Task.FromResult<Report?>(null));

        var result = await coordinator.EnsureReportExistsAsync(12, 3);

        Assert.False(result);
    }

    [Fact]
    public async Task EnsureReportExistsAsync_IsolatesReportServiceFailure()
    {
        var coordinator = new AnalysisReportCoordinator(
            (_, _) => throw new InvalidOperationException("database unavailable"));

        var exception = await Record.ExceptionAsync(() => coordinator.EnsureReportExistsAsync(12, 3));

        Assert.Null(exception);
        Assert.False(await coordinator.EnsureReportExistsAsync(12, 3));
    }
}
