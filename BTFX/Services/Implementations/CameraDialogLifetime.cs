namespace BTFX.Services.Implementations;

internal sealed class CameraDialogLifetime : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly CancellationToken _token;
    private int _isClosed;

    public CameraDialogLifetime()
    {
        _token = _cancellation.Token;
    }

    public bool IsClosed => Volatile.Read(ref _isClosed) == 1;

    public bool CanStartPreview => !IsClosed;

    public CancellationToken Token => _token;

    public bool Close()
    {
        if (Interlocked.Exchange(ref _isClosed, 1) == 1)
        {
            return false;
        }

        _cancellation.Cancel();
        return true;
    }

    public void Dispose()
    {
        Close();
        _cancellation.Dispose();
    }
}
