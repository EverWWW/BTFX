using GxIAPINET;

namespace BTFX.Services.Implementations;

public sealed class DahengCameraRuntime : IDisposable
{
    private readonly object _syncRoot = new();
    private IGXFactory? _factory;
    private DahengSdkLifetime? _lifetime;
    private bool _isDisposed;

    public IGXFactory GetInitializedFactory()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _factory ??= IGXFactory.GetInstance()
                        ?? throw new InvalidOperationException("未能获取大恒相机 SDK 工厂实例。");
            var factory = _factory;
            _lifetime ??= new DahengSdkLifetime(
                () =>
                {
                    factory.Init();
                    Thread.Sleep(500);
                },
                factory.Uninit);
            _lifetime.EnsureInitialized();
            return _factory;
        }
    }

    public void Execute(Action<IGXFactory> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_syncRoot)
        {
            action(GetInitializedFactory());
        }
    }

    public T Execute<T>(Func<IGXFactory, T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_syncRoot)
        {
            return action(GetInitializedFactory());
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
            try
            {
                _lifetime?.Dispose();
            }
            finally
            {
                _lifetime = null;
                _factory = null;
            }
        }
    }
}
