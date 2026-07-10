using System.Buffers;
using BTFX.Services.Implementations;
using Xunit;

namespace BTFX.Tests;

public sealed class DahengRecordingPrimitivesTests
{
    [Fact]
    public void SdkLifetime_InitializesAndUninitializesOnlyOnce()
    {
        var initializeCount = 0;
        var uninitializeCount = 0;
        var lifetime = new DahengSdkLifetime(
            () => initializeCount++,
            () => uninitializeCount++);

        lifetime.EnsureInitialized();
        lifetime.EnsureInitialized();
        lifetime.Dispose();
        lifetime.Dispose();

        Assert.Equal(1, initializeCount);
        Assert.Equal(1, uninitializeCount);
    }

    [Fact]
    public void ResolveStartAt_UsesRequestedTimeWhenPreparationFinishesEarly()
    {
        var requested = DateTimeOffset.Parse("2026-07-10T10:00:05Z");
        var ready = requested.AddSeconds(-2);

        var actual = DahengRecordingSchedule.ResolveStartAt(
            requested,
            ready,
            TimeSpan.FromMilliseconds(250));

        Assert.Equal(requested, actual);
    }

    [Fact]
    public void ResolveStartAt_AddsMarginWhenPreparationFinishesLate()
    {
        var requested = DateTimeOffset.Parse("2026-07-10T10:00:05Z");
        var ready = requested.AddMilliseconds(100);

        var actual = DahengRecordingSchedule.ResolveStartAt(
            requested,
            ready,
            TimeSpan.FromMilliseconds(250));

        Assert.Equal(ready.AddMilliseconds(250), actual);
    }

    [Fact]
    public void StartStreamsThenResolveStartAt_WaitsUntilEveryStreamHasStarted()
    {
        var startedStreams = 0;
        var requested = DateTimeOffset.Parse("2026-07-10T10:00:05Z");
        var streamsReadyAt = requested.AddMilliseconds(100);
        var startActions = new Action[]
        {
            () => startedStreams++,
            () => startedStreams++
        };

        var actual = DahengRecordingSchedule.StartStreamsThenResolveStartAt(
            startActions,
            requested,
            () =>
            {
                Assert.Equal(2, startedStreams);
                return streamsReadyAt;
            },
            TimeSpan.FromMilliseconds(500));

        Assert.Equal(streamsReadyAt.AddMilliseconds(500), actual);
    }

    [Fact]
    public void PooledFrameLease_ReturnsBufferOnlyAfterLastReferenceIsReleased()
    {
        var pool = new TrackingArrayPool();
        var lease = new PooledFrameLease(pool, 128);
        lease.AddReference();
        lease.AddReference();

        lease.Release();
        lease.Release();

        Assert.Equal(0, pool.ReturnCount);

        lease.Release();
        lease.Release();

        Assert.Equal(1, pool.ReturnCount);
    }

    [Fact]
    public void Retry_ExecutesAgainAfterTransientFailure()
    {
        var attempts = 0;
        var waits = new List<int>();

        DahengRecordingRetry.Execute(
            maxAttempts: 3,
            action: () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new NullReferenceException("SDK stream is not ready");
                }
            },
            waitBeforeRetry: waits.Add);

        Assert.Equal(2, attempts);
        Assert.Equal([1], waits);
    }

    [Theory]
    [InlineData("Connected", true)]
    [InlineData("NotFound", false)]
    [InlineData("ProbeFailed", false)]
    [InlineData("Unconfigured", false)]
    public void PreviewStartGuard_OnlyAllowsConnectedDevice(string probeStatus, bool expected)
    {
        Assert.Equal(expected, DahengPreviewStartGuard.CanOpenDevice(probeStatus));
    }

    private sealed class TrackingArrayPool : ArrayPool<byte>
    {
        public int ReturnCount { get; private set; }

        public override byte[] Rent(int minimumLength) => new byte[minimumLength];

        public override void Return(byte[] array, bool clearArray = false)
        {
            ReturnCount++;
        }
    }
}
