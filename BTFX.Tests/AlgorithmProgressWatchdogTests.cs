using BTFX.Services.Implementations;
using Xunit;

namespace BTFX.Tests;

public sealed class AlgorithmProgressWatchdogTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(30, 10)]
    public void GetEffectiveTotalTimeoutMinutes_ClampsToSupportedRange(int configured, int expected)
    {
        Assert.Equal(expected, AnalysisTimeoutPolicy.GetEffectiveTotalTimeoutMinutes(configured));
    }

    [Fact]
    public void Check_ReturnsStartupTimeout_WhenNoStatusArrivesWithinLimit()
    {
        var watchdog = CreateWatchdog();

        Assert.Null(watchdog.Check(StartedAt.AddSeconds(59)));

        var timeout = watchdog.Check(StartedAt.AddSeconds(60));
        Assert.NotNull(timeout);
        Assert.Equal(AlgorithmWatchdogTimeoutKind.Startup, timeout.Kind);
    }

    [Fact]
    public void Check_ReturnsPoseTimeout_WhenPoseStageReachesFiveMinutes()
    {
        var watchdog = CreateWatchdog();
        watchdog.ObserveStage("pose_estimation_side", StartedAt.AddSeconds(10));

        Assert.Null(watchdog.Check(StartedAt.AddMinutes(5).AddSeconds(9)));

        var timeout = watchdog.Check(StartedAt.AddMinutes(5).AddSeconds(10));
        Assert.NotNull(timeout);
        Assert.Equal(AlgorithmWatchdogTimeoutKind.PoseEstimation, timeout.Kind);
        Assert.Equal("pose_estimation_side", timeout.Stage);
    }

    [Fact]
    public void Check_ReturnsProcessingTimeout_WhenOtherStageReachesTwoMinutes()
    {
        var watchdog = CreateWatchdog();
        watchdog.ObserveStage("gait_event_detection", StartedAt.AddSeconds(10));

        var timeout = watchdog.Check(StartedAt.AddMinutes(2).AddSeconds(10));

        Assert.NotNull(timeout);
        Assert.Equal(AlgorithmWatchdogTimeoutKind.Processing, timeout.Kind);
        Assert.Equal("gait_event_detection", timeout.Stage);
    }

    [Fact]
    public void ObserveStage_ResetsTimerOnlyWhenStageChanges()
    {
        var watchdog = CreateWatchdog();
        watchdog.ObserveStage("pose_estimation_side", StartedAt);
        watchdog.ObserveStage("pose_estimation_side", StartedAt.AddMinutes(4));

        Assert.NotNull(watchdog.Check(StartedAt.AddMinutes(5)));

        watchdog.ObserveStage("pose_estimation_front", StartedAt.AddMinutes(5));
        Assert.Null(watchdog.Check(StartedAt.AddMinutes(9)));
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("failed")]
    public void Check_DoesNotTimeoutTerminalStage(string stage)
    {
        var watchdog = CreateWatchdog();
        watchdog.ObserveStage(stage, StartedAt.AddSeconds(5));

        Assert.Null(watchdog.Check(StartedAt.AddHours(1)));
    }

    private static AlgorithmProgressWatchdog CreateWatchdog()
    {
        return new AlgorithmProgressWatchdog(
            StartedAt,
            TimeSpan.FromSeconds(60),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(2));
    }
}
