using System.Collections.Concurrent;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using ToolHelper.LoggingDiagnostics.Abstractions;
using ToolHelper.LoggingDiagnostics.Configuration;

namespace ToolHelper.LoggingDiagnostics.Logging;

/// <summary>
/// 日志记录帮助类
/// 提供分级、分文件、自动归档的日志功能
/// </summary>
/// <example>
/// <code>
/// // 使用依赖注入
/// services.AddLoggingDiagnostics(options => {
///     options.Log.MinimumLevel = LogLevel.Debug;
///     options.Log.LogDirectory = "logs";
/// });
/// 
/// // 直接创建
/// var logHelper = new LogHelper(Options.Create(new LogOptions()));
/// logHelper.Information("这是一条信息日志");
/// logHelper.Error("发生错误", exception);
/// </code>
/// </example>
public class LogHelper : ILogHelper
{
    private readonly LogOptions _options;
    private readonly string _category;
    private readonly Channel<LogEntry> _logChannel;
    private readonly CancellationTokenSource _cts;
    private readonly Task _writeTask;
    private readonly ObjectPool<StringBuilder> _stringBuilderPool;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ConcurrentDictionary<string, StreamWriter> _writers = new();
    
    private bool _disposed;
    private string _currentLogFile = string.Empty;
    private DateTime _currentLogDate = DateTime.MinValue;

    /// <inheritdoc/>
    public LogLevel MinimumLevel { get; set; }

    /// <summary>
    /// 创建LogHelper实例
    /// </summary>
    /// <param name="options">日志配置选项</param>
    /// <param name="category">日志类别</param>
    public LogHelper(IOptions<LogOptions> options, string category = "Default")
    {
        _options = options.Value;
        _category = category;
        MinimumLevel = _options.MinimumLevel;
        
        _logChannel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(_options.BufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
        
        _cts = new CancellationTokenSource();
        
        var policy = new StringBuilderPooledObjectPolicy();
        _stringBuilderPool = new DefaultObjectPool<StringBuilder>(policy, 100);
        
        EnsureLogDirectoryExists();
        
        if (_options.EnableAsyncWrite)
        {
            _writeTask = Task.Run(ProcessLogQueueAsync);
        }
        else
        {
            _writeTask = Task.CompletedTask;
        }
    }

    /// <inheritdoc/>
    public void Trace(string message, IDictionary<string, object>? properties = null)
        => Log(LogLevel.Trace, message, null, properties);

    /// <inheritdoc/>
    public void Debug(string message, IDictionary<string, object>? properties = null)
        => Log(LogLevel.Debug, message, null, properties);

    /// <inheritdoc/>
    public void Information(string message, IDictionary<string, object>? properties = null)
        => Log(LogLevel.Information, message, null, properties);

    /// <inheritdoc/>
    public void Warning(string message, IDictionary<string, object>? properties = null)
        => Log(LogLevel.Warning, message, null, properties);

    /// <inheritdoc/>
    public void Error(string message, Exception? exception = null, IDictionary<string, object>? properties = null)
        => Log(LogLevel.Error, message, exception, properties);

    /// <inheritdoc/>
    public void Critical(string message, Exception? exception = null, IDictionary<string, object>? properties = null)
        => Log(LogLevel.Critical, message, exception, properties);

    /// <inheritdoc/>
    public void Log(LogLevel level, string message, Exception? exception = null, IDictionary<string, object>? properties = null)
    {
        if (level < MinimumLevel) return;

        var entry = new LogEntry
        {
            Level = level,
            Category = _category,
            Message = message,
            Exception = exception,
            Properties = properties
        };

        if (_options.EnableAsyncWrite)
        {
            _logChannel.Writer.TryWrite(entry);
        }
        else
        {
            WriteLogEntrySync(entry);
        }

        if (_options.EnableConsoleOutput)
        {
            WriteToConsole(entry);
        }
    }

    /// <inheritdoc/>
    public async ValueTask LogAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.Level < MinimumLevel) return;

        if (_options.EnableAsyncWrite)
        {
            await _logChannel.Writer.WriteAsync(entry, cancellationToken);
        }
        else
        {
            WriteLogEntrySync(entry);
        }

        if (_options.EnableConsoleOutput)
        {
            WriteToConsole(entry);
        }
    }

    /// <inheritdoc/>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var writer in _writers.Values)
            {
                await writer.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task ArchiveAsync(CancellationToken cancellationToken = default)
    {
        var logDir = GetLogDirectory();
        if (!Directory.Exists(logDir)) return;

        var archiveDate = DateTime.Today.AddDays(-_options.ArchiveAfterDays);
        var files = Directory.GetFiles(logDir, "*.txt")
            .Select(f => new FileInfo(f))
            .Where(f => f.LastWriteTime.Date < archiveDate)
            .ToList();

        if (files.Count == 0) return;

        var archiveDir = Path.Combine(logDir, "archive");
        Directory.CreateDirectory(archiveDir);

        var archiveName = $"logs_{archiveDate:yyyyMMdd}.zip";
        var archivePath = Path.Combine(archiveDir, archiveName);

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                archive.CreateEntryFromFile(file.FullName, file.Name, CompressionLevel.Optimal);
            }
        }

        // 删除已归档的文件
        foreach (var file in files)
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // 忽略删除失败
            }
        }

        // 清理过期归档
        await CleanOldArchivesAsync(archiveDir, cancellationToken);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<LogEntry> GetLogsAsync(
        DateTime startTime,
        DateTime endTime,
        LogLevel? level = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var logDir = GetLogDirectory();
        if (!Directory.Exists(logDir)) yield break;

        var files = Directory.GetFiles(logDir, "*.txt")
            .Select(f => new FileInfo(f))
            .Where(f => f.LastWriteTime >= startTime && f.CreationTime <= endTime)
            .OrderBy(f => f.CreationTime);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await foreach (var entry in ReadLogFileAsync(file.FullName, startTime, endTime, level, cancellationToken))
            {
                yield return entry;
            }
        }
    }

    /// <inheritdoc/>
    public ILogHelper ForCategory(string category)
    {
        return new LogHelper(Options.Create(_options), category);
    }

    private async Task ProcessLogQueueAsync()
    {
        var buffer = new List<LogEntry>(_options.BufferSize);
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.FlushIntervalMs));

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var hasData = false;

                // 尝试读取所有可用的日志条目
                while (buffer.Count < _options.BufferSize && 
                       _logChannel.Reader.TryRead(out var entry))
                {
                    buffer.Add(entry);
                    hasData = true;
                }

                // 如果有数据，写入文件
                if (hasData)
                {
                    await WriteBatchAsync(buffer);
                    buffer.Clear();
                }

                // 等待定时器或新数据
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                cts.CancelAfter(TimeSpan.FromMilliseconds(_options.FlushIntervalMs));

                try
                {
                    await _logChannel.Reader.WaitToReadAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // 超时或取消，继续处理
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        finally
        {
            // 处理剩余的日志
            while (_logChannel.Reader.TryRead(out var entry))
            {
                buffer.Add(entry);
            }

            if (buffer.Count > 0)
            {
                await WriteBatchAsync(buffer);
            }
        }
    }

    private async Task WriteBatchAsync(IList<LogEntry> entries)
    {
        await _fileLock.WaitAsync();
        try
        {
            foreach (var entry in entries)
            {
                var writer = await GetOrCreateWriterAsync(entry.Level);
                var line = FormatLogEntry(entry);
                await writer.WriteLineAsync(line);
            }

            foreach (var writer in _writers.Values)
            {
                await writer.FlushAsync();
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private void WriteLogEntrySync(LogEntry entry)
    {
        _fileLock.Wait();
        try
        {
            var writer = GetOrCreateWriterAsync(entry.Level).GetAwaiter().GetResult();
            var line = FormatLogEntry(entry);
            writer.WriteLine(line);
            writer.Flush();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<StreamWriter> GetOrCreateWriterAsync(LogLevel level)
    {
        var fileName = GetLogFileName(level);
        
        if (_writers.TryGetValue(fileName, out var existingWriter))
        {
            // 检查是否需要滚动文件
            if (await ShouldRollFileAsync(fileName))
            {
                await RollFileAsync(fileName);
            }
            else
            {
                return existingWriter;
            }
        }

        var filePath = Path.Combine(GetLogDirectory(), fileName);
        var writer = new StreamWriter(filePath, append: true, Encoding.UTF8)
        {
            AutoFlush = false
        };

        _writers[fileName] = writer;
        return writer;
    }

    private string GetLogFileName(LogLevel level)
    {
        var date = DateTime.Today;
        var dateStr = date.ToString(_options.DateFormat);
        
        var fileName = _options.FileNameFormat.Replace("{date}", dateStr);
        
        if (_options.SeparateFileByLevel)
        {
            var ext = Path.GetExtension(fileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            fileName = $"{nameWithoutExt}_{level}{ext}";
        }

        return fileName;
    }

    private async Task<bool> ShouldRollFileAsync(string fileName)
    {
        var filePath = Path.Combine(GetLogDirectory(), fileName);
        if (!File.Exists(filePath)) return false;

        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length > _options.MaxFileSizeMB * 1024 * 1024;
    }

    private async Task RollFileAsync(string fileName)
    {
        if (_writers.TryRemove(fileName, out var writer))
        {
            await writer.DisposeAsync();
        }

        var filePath = Path.Combine(GetLogDirectory(), fileName);
        var ext = Path.GetExtension(fileName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        
        // 查找下一个可用的滚动编号
        var rollNumber = 1;
        string newFilePath;
        do
        {
            newFilePath = Path.Combine(GetLogDirectory(), $"{nameWithoutExt}.{rollNumber}{ext}");
            rollNumber++;
        } while (File.Exists(newFilePath));

        File.Move(filePath, newFilePath);
    }

    private string FormatLogEntry(LogEntry entry)
    {
        var sb = _stringBuilderPool.Get();
        try
        {
            var template = _options.MessageTemplate;
            
            template = template.Replace("{timestamp}", entry.Timestamp.ToString(_options.TimestampFormat));
            template = template.Replace("{level}", entry.Level.ToString().ToUpper().PadRight(11));
            template = template.Replace("{category}", entry.Category);
            template = template.Replace("{message}", entry.Message);
            template = template.Replace("{threadId}", entry.ThreadId.ToString());

            sb.Append(template);

            if (entry.Exception != null)
            {
                sb.AppendLine();
                sb.Append("Exception: ");
                sb.Append(entry.Exception);
            }

            if (entry.Properties?.Count > 0)
            {
                sb.AppendLine();
                sb.Append("Properties: ");
                foreach (var prop in entry.Properties)
                {
                    sb.Append($"{prop.Key}={prop.Value}, ");
                }
            }

            return sb.ToString();
        }
        finally
        {
            sb.Clear();
            _stringBuilderPool.Return(sb);
        }
    }

    private void WriteToConsole(LogEntry entry)
    {
        if (_options.UseColoredConsole)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = GetConsoleColor(entry.Level);
            Console.WriteLine(FormatLogEntry(entry));
            Console.ForegroundColor = originalColor;
        }
        else
        {
            Console.WriteLine(FormatLogEntry(entry));
        }
    }

    private static ConsoleColor GetConsoleColor(LogLevel level) => level switch
    {
        LogLevel.Trace => ConsoleColor.Gray,
        LogLevel.Debug => ConsoleColor.DarkGray,
        LogLevel.Information => ConsoleColor.White,
        LogLevel.Warning => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        LogLevel.Critical => ConsoleColor.DarkRed,
        _ => ConsoleColor.White
    };

    private string GetLogDirectory()
    {
        return Path.IsPathRooted(_options.LogDirectory)
            ? _options.LogDirectory
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _options.LogDirectory);
    }

    private void EnsureLogDirectoryExists()
    {
        var dir = GetLogDirectory();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private async Task CleanOldArchivesAsync(string archiveDir, CancellationToken cancellationToken)
    {
        var cutoffDate = DateTime.Today.AddDays(-_options.ArchiveRetentionDays);
        var oldArchives = Directory.GetFiles(archiveDir, "*.zip")
            .Select(f => new FileInfo(f))
            .Where(f => f.CreationTime < cutoffDate);

        foreach (var archive in oldArchives)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                archive.Delete();
            }
            catch
            {
                // 忽略删除失败
            }
        }
    }

    private async IAsyncEnumerable<LogEntry> ReadLogFileAsync(
        string filePath,
        DateTime startTime,
        DateTime endTime,
        LogLevel? level,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var entry = ParseLogLine(line);
            if (entry == null) continue;

            if (entry.Timestamp < startTime || entry.Timestamp > endTime) continue;
            if (level.HasValue && entry.Level != level.Value) continue;

            yield return entry;
        }
    }

    private LogEntry? ParseLogLine(string line)
    {
        try
        {
            // 简单解析，假设格式为 [timestamp] [level] [category] message
            var timestampEnd = line.IndexOf(']');
            if (timestampEnd < 0) return null;

            var timestampStr = line[1..timestampEnd];
            if (!DateTime.TryParse(timestampStr, out var timestamp)) return null;

            var levelStart = line.IndexOf('[', timestampEnd + 1);
            var levelEnd = line.IndexOf(']', levelStart + 1);
            if (levelStart < 0 || levelEnd < 0) return null;

            var levelStr = line[(levelStart + 1)..levelEnd].Trim();
            if (!Enum.TryParse<LogLevel>(levelStr, true, out var logLevel)) return null;

            var categoryStart = line.IndexOf('[', levelEnd + 1);
            var categoryEnd = line.IndexOf(']', categoryStart + 1);
            var category = categoryStart >= 0 && categoryEnd > categoryStart 
                ? line[(categoryStart + 1)..categoryEnd] 
                : "Default";

            var message = line[(categoryEnd + 1)..].Trim();

            return new LogEntry
            {
                Timestamp = timestamp,
                Level = logLevel,
                Category = category,
                Message = message
            };
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _cts.Cancel();
            _logChannel.Writer.Complete();
            _writeTask.Wait(TimeSpan.FromSeconds(5));
            
            foreach (var writer in _writers.Values)
            {
                writer.Dispose();
            }
            _writers.Clear();
            
            _fileLock.Dispose();
            _cts.Dispose();
        }

        _disposed = true;
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_disposed) return;

        _cts.Cancel();
        _logChannel.Writer.Complete();
        
        try
        {
            await _writeTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            // 超时，继续清理
        }

        foreach (var writer in _writers.Values)
        {
            await writer.DisposeAsync();
        }
        _writers.Clear();
    }
}

/// <summary>
/// StringBuilder对象池策略
/// </summary>
internal class StringBuilderPooledObjectPolicy : PooledObjectPolicy<StringBuilder>
{
    public int InitialCapacity { get; set; } = 256;
    public int MaximumRetainedCapacity { get; set; } = 4096;

    public override StringBuilder Create() => new(InitialCapacity);

    public override bool Return(StringBuilder obj)
    {
        if (obj.Capacity > MaximumRetainedCapacity)
        {
            return false;
        }

        obj.Clear();
        return true;
    }
}
