using System.Buffers;

namespace BTFX.Services.Implementations;

internal sealed class DahengSdkLifetime : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly Action _initialize;
    private readonly Action _uninitialize;
    private bool _isInitialized;
    private bool _isDisposed;

    public DahengSdkLifetime(Action initialize, Action uninitialize)
    {
        _initialize = initialize;
        _uninitialize = uninitialize;
    }

    public void EnsureInitialized()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_isInitialized)
            {
                return;
            }

            _initialize();
            _isInitialized = true;
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            if (!_isInitialized)
            {
                return;
            }

            _uninitialize();
            _isInitialized = false;
        }
    }
}

internal static class DahengRecordingSchedule
{
    public static DateTimeOffset StartStreamsThenResolveStartAt(
        IReadOnlyList<Action> startStreams,
        DateTimeOffset requestedStartAt,
        Func<DateTimeOffset> getReadyAt,
        TimeSpan preparationMargin)
    {
        ArgumentNullException.ThrowIfNull(startStreams);
        ArgumentNullException.ThrowIfNull(getReadyAt);

        foreach (var startStream in startStreams)
        {
            startStream();
        }

        return ResolveStartAt(requestedStartAt, getReadyAt(), preparationMargin);
    }

    public static DateTimeOffset ResolveStartAt(
        DateTimeOffset requestedStartAt,
        DateTimeOffset readyAt,
        TimeSpan preparationMargin)
    {
        var earliestSafeStart = readyAt + preparationMargin;
        return requestedStartAt >= earliestSafeStart ? requestedStartAt : earliestSafeStart;
    }
}

internal static class DahengRecordingRetry
{
    public static void Execute(int maxAttempts, Action action, Action<int> waitBeforeRetry)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(waitBeforeRetry);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch when (attempt < maxAttempts)
            {
                waitBeforeRetry(attempt);
            }
        }
    }
}

internal static class DahengPreviewStartGuard
{
    public static bool CanOpenDevice(string probeStatus)
    {
        return string.Equals(probeStatus, "Connected", StringComparison.Ordinal);
    }
}

internal sealed class PooledFrameLease
{
    private readonly ArrayPool<byte> _pool;
    private int _referenceCount = 1;
    private int _returned;

    public PooledFrameLease(ArrayPool<byte> pool, int minimumLength)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumLength);

        _pool = pool;
        Buffer = pool.Rent(minimumLength);
    }

    public byte[] Buffer { get; }

    public void AddReference()
    {
        while (true)
        {
            var current = Volatile.Read(ref _referenceCount);
            if (current <= 0)
            {
                throw new ObjectDisposedException(nameof(PooledFrameLease));
            }

            if (Interlocked.CompareExchange(ref _referenceCount, current + 1, current) == current)
            {
                return;
            }
        }
    }

    public void Release()
    {
        while (true)
        {
            var current = Volatile.Read(ref _referenceCount);
            if (current <= 0)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _referenceCount, current - 1, current) != current)
            {
                continue;
            }

            if (current == 1 && Interlocked.Exchange(ref _returned, 1) == 0)
            {
                _pool.Return(Buffer);
            }

            return;
        }
    }
}

internal sealed record DahengFramePacket(PooledFrameLease Lease, int Length, int RepeatCount);
