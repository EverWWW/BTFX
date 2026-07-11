using System.IO;

namespace BTFX.Services.Implementations;

internal sealed class ImportStagingSession : IDisposable
{
    private readonly string _stagingContainer;
    private readonly string _payloadDirectory;
    private readonly string _finalDirectory;
    private bool _promoted;
    private bool _committed;
    private bool _disposed;

    public ImportStagingSession(string stagingContainer, string finalDirectory)
    {
        _stagingContainer = Path.GetFullPath(stagingContainer);
        _payloadDirectory = Path.Combine(_stagingContainer, "payload");
        _finalDirectory = Path.GetFullPath(finalDirectory);
    }

    public string PayloadDirectory => _payloadDirectory;

    public void Promote()
    {
        if (_promoted)
        {
            return;
        }

        if (!Directory.Exists(_payloadDirectory))
        {
            throw new DirectoryNotFoundException($"导入临时目录不存在：{_payloadDirectory}");
        }

        if (Directory.Exists(_finalDirectory))
        {
            throw new IOException($"导入目标目录已存在：{_finalDirectory}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_finalDirectory)!);
        Directory.Move(_payloadDirectory, _finalDirectory);
        _promoted = true;
    }

    public void Commit()
    {
        if (!_promoted)
        {
            throw new InvalidOperationException("导入文件尚未转移到正式目录。");
        }

        _committed = true;
    }

    public void Rollback()
    {
        if (_committed)
        {
            return;
        }

        if (_promoted && Directory.Exists(_finalDirectory))
        {
            Directory.Delete(_finalDirectory, recursive: true);
        }

        _promoted = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_committed)
        {
            Rollback();
        }

        if (Directory.Exists(_stagingContainer))
        {
            Directory.Delete(_stagingContainer, recursive: true);
        }

        _disposed = true;
    }
}
