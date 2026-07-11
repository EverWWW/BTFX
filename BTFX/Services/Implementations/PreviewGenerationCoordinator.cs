namespace BTFX.Services.Implementations;

internal sealed class PreviewGenerationCoordinator : IDisposable
{
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _currentCancellation;
    private long _currentVersion;
    private bool _disposed;

    public PreviewGenerationLease Begin()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            CancelSafely(_currentCancellation);
            var cancellation = new CancellationTokenSource();
            _currentCancellation = cancellation;
            var version = ++_currentVersion;
            return new PreviewGenerationLease(version, cancellation);
        }
    }

    public bool IsCurrent(long version)
    {
        lock (_syncRoot)
        {
            return !_disposed
                && version == _currentVersion
                && _currentCancellation is { IsCancellationRequested: false };
        }
    }

    public void CancelCurrent()
    {
        lock (_syncRoot)
        {
            CancelSafely(_currentCancellation);
            _currentCancellation = null;
            _currentVersion++;
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            CancelSafely(_currentCancellation);
            _currentCancellation = null;
            _currentVersion++;
            _disposed = true;
        }
    }

    private static void CancelSafely(CancellationTokenSource? cancellation)
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}

internal sealed class PreviewGenerationLease : IDisposable
{
    private readonly CancellationTokenSource _cancellation;
    private bool _disposed;

    internal PreviewGenerationLease(long version, CancellationTokenSource cancellation)
    {
        Version = version;
        _cancellation = cancellation;
        Token = cancellation.Token;
    }

    public long Version { get; }

    public CancellationToken Token { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cancellation.Dispose();
        _disposed = true;
    }
}
