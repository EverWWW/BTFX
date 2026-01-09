using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolHelper.LoggingDiagnostics.Abstractions;
using TraceOptionsConfig = ToolHelper.LoggingDiagnostics.Configuration.TraceOptions;

namespace ToolHelper.LoggingDiagnostics.Tracing;

/// <summary>
/// 调试追踪帮助类
/// 提供调用栈分析、性能分析功能
/// </summary>
/// <example>
/// <code>
/// // 使用追踪作用域
/// using (traceHelper.BeginTrace("数据库查询"))
/// {
///     // 执行数据库查询
/// }
/// 
/// // 追踪方法执行
/// var result = await traceHelper.TraceAsync("API调用", async ct => {
///     return await httpClient.GetStringAsync(url, ct);
/// });
/// </code>
/// </example>
public class TraceHelper : ITraceHelper
{
    private readonly TraceOptionsConfig _options;
    private readonly ILogger<TraceHelper>? _logger;
    private readonly ConcurrentQueue<PerformanceResult> _history;
    private readonly ConcurrentDictionary<string, OperationStats> _statistics;
    private readonly object _statsLock = new();

    /// <inheritdoc/>
    public event EventHandler<PerformanceResult>? TraceCompleted;

    /// <summary>
    /// 创建TraceHelper实例
    /// </summary>
    /// <param name="options">追踪配置选项</param>
    /// <param name="logger">日志记录器（可选）</param>
    public TraceHelper(IOptions<TraceOptionsConfig> options, ILogger<TraceHelper>? logger = null)
    {
        _options = options.Value;
        _logger = logger;
        _history = new ConcurrentQueue<PerformanceResult>();
        _statistics = new ConcurrentDictionary<string, OperationStats>();
    }

    /// <inheritdoc/>
    public ITraceScope BeginTrace(string operationName, bool captureCallStack = false)
    {
        if (!_options.Enabled)
        {
            return new NoOpTraceScope(operationName);
        }

        var shouldCaptureStack = captureCallStack || _options.CaptureCallStackByDefault;
        return new TraceScope(this, operationName, shouldCaptureStack, _options.TrackMemoryUsage);
    }

    /// <inheritdoc/>
    public T Trace<T>(string operationName, Func<T> action, bool captureCallStack = false)
    {
        using var scope = BeginTrace(operationName, captureCallStack);
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            scope.SetFailed(ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public void Trace(string operationName, Action action, bool captureCallStack = false)
    {
        using var scope = BeginTrace(operationName, captureCallStack);
        try
        {
            action();
        }
        catch (Exception ex)
        {
            scope.SetFailed(ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<T> TraceAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> asyncAction,
        bool captureCallStack = false,
        CancellationToken cancellationToken = default)
    {
        using var scope = BeginTrace(operationName, captureCallStack);
        try
        {
            return await asyncAction(cancellationToken);
        }
        catch (Exception ex)
        {
            scope.SetFailed(ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task TraceAsync(
        string operationName,
        Func<CancellationToken, Task> asyncAction,
        bool captureCallStack = false,
        CancellationToken cancellationToken = default)
    {
        using var scope = BeginTrace(operationName, captureCallStack);
        try
        {
            await asyncAction(cancellationToken);
        }
        catch (Exception ex)
        {
            scope.SetFailed(ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<StackFrameInfo> GetCallStack(int skipFrames = 0)
    {
        var stackTrace = new StackTrace(skipFrames + 1, true);
        var frames = new List<StackFrameInfo>();

        var maxDepth = Math.Min(stackTrace.FrameCount, _options.MaxStackDepth);
        
        for (var i = 0; i < maxDepth; i++)
        {
            var frame = stackTrace.GetFrame(i);
            if (frame == null) continue;

            var method = frame.GetMethod();
            if (method == null) continue;

            frames.Add(new StackFrameInfo
            {
                MethodName = method.Name,
                ClassName = method.DeclaringType?.FullName ?? "Unknown",
                FileName = frame.GetFileName(),
                LineNumber = frame.GetFileLineNumber() > 0 ? frame.GetFileLineNumber() : null,
                ColumnNumber = frame.GetFileColumnNumber() > 0 ? frame.GetFileColumnNumber() : null,
                ILOffset = frame.GetILOffset()
            });
        }

        return frames;
    }

    /// <inheritdoc/>
    public string GetCallStackString(int skipFrames = 0)
    {
        var frames = GetCallStack(skipFrames + 1);
        var lines = frames.Select((f, i) =>
        {
            var location = f.FileName != null
                ? $" in {f.FileName}:line {f.LineNumber}"
                : string.Empty;
            return $"  at {f.ClassName}.{f.MethodName}(){location}";
        });

        return string.Join(Environment.NewLine, lines);
    }

    /// <inheritdoc/>
    public IReadOnlyList<PerformanceResult> GetHistory(string? operationName = null, int count = 100)
    {
        var results = operationName == null
            ? _history.ToArray()
            : _history.Where(r => r.OperationName == operationName).ToArray();

        return results.TakeLast(count).ToList();
    }

    /// <inheritdoc/>
    public OperationStatistics GetStatistics(string operationName)
    {
        if (!_statistics.TryGetValue(operationName, out var stats))
        {
            return new OperationStatistics { OperationName = operationName };
        }

        lock (_statsLock)
        {
            return new OperationStatistics
            {
                OperationName = operationName,
                CallCount = stats.CallCount,
                SuccessCount = stats.SuccessCount,
                FailureCount = stats.FailureCount,
                AverageDuration = stats.CallCount > 0
                    ? TimeSpan.FromTicks(stats.TotalTicks / stats.CallCount)
                    : TimeSpan.Zero,
                MinDuration = TimeSpan.FromTicks(stats.MinTicks),
                MaxDuration = TimeSpan.FromTicks(stats.MaxTicks),
                TotalDuration = TimeSpan.FromTicks(stats.TotalTicks),
                LastCallTime = stats.LastCallTime
            };
        }
    }

    /// <inheritdoc/>
    public void ClearHistory()
    {
        while (_history.TryDequeue(out _)) { }
        _statistics.Clear();
    }

    internal void RecordResult(PerformanceResult result)
    {
        // 添加到历史记录
        _history.Enqueue(result);
        
        // 限制历史记录数量
        while (_history.Count > _options.MaxHistoryCount)
        {
            _history.TryDequeue(out _);
        }

        // 更新统计信息
        UpdateStatistics(result);

        // 触发事件
        TraceCompleted?.Invoke(this, result);

        // 输出日志
        if (_options.AutoLogResults && result.Duration.TotalMilliseconds >= _options.MinDurationForLoggingMs)
        {
            LogResult(result);
        }
    }

    private void UpdateStatistics(PerformanceResult result)
    {
        var stats = _statistics.GetOrAdd(result.OperationName, _ => new OperationStats());
        
        lock (_statsLock)
        {
            stats.CallCount++;
            if (result.IsSuccess)
                stats.SuccessCount++;
            else
                stats.FailureCount++;

            var ticks = result.Duration.Ticks;
            stats.TotalTicks += ticks;
            
            if (stats.MinTicks == 0 || ticks < stats.MinTicks)
                stats.MinTicks = ticks;
            if (ticks > stats.MaxTicks)
                stats.MaxTicks = ticks;
            
            stats.LastCallTime = result.EndTime;
        }
    }

    private void LogResult(PerformanceResult result)
    {
        var isSlow = result.Duration.TotalMilliseconds >= _options.SlowOperationThresholdMs;
        var message = $"[Trace] {result.OperationName} completed in {result.Duration.TotalMilliseconds:F2}ms" +
                     (isSlow ? " [SLOW]" : "") +
                     (result.IsSuccess ? "" : " [FAILED]");

        if (_logger != null)
        {
            if (!result.IsSuccess)
                _logger.LogWarning(result.Exception, message);
            else if (isSlow)
                _logger.LogWarning(message);
            else
                _logger.LogDebug(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    private class OperationStats
    {
        public long CallCount;
        public long SuccessCount;
        public long FailureCount;
        public long TotalTicks;
        public long MinTicks;
        public long MaxTicks;
        public DateTime? LastCallTime;
    }
}

/// <summary>
/// 追踪作用域实现
/// </summary>
internal class TraceScope : ITraceScope
{
    private readonly TraceHelper _traceHelper;
    private readonly Stopwatch _stopwatch;
    private readonly bool _captureCallStack;
    private readonly bool _trackMemory;
    private readonly long _startMemory;
    private readonly Dictionary<string, object> _data;
    private string? _callStack;
    private bool _isFailed;
    private Exception? _exception;
    private bool _disposed;

    public string OperationName { get; }
    public DateTime StartTime { get; }

    public TraceScope(TraceHelper traceHelper, string operationName, bool captureCallStack, bool trackMemory)
    {
        _traceHelper = traceHelper;
        OperationName = operationName;
        _captureCallStack = captureCallStack;
        _trackMemory = trackMemory;
        _data = new Dictionary<string, object>();
        
        StartTime = DateTime.Now;
        _stopwatch = Stopwatch.StartNew();
        
        if (_captureCallStack)
        {
            _callStack = _traceHelper.GetCallStackString(2);
        }

        if (_trackMemory)
        {
            _startMemory = GC.GetTotalMemory(false);
        }
    }

    public void SetData(string key, object value)
    {
        _data[key] = value;
    }

    public void SetFailed(Exception? exception = null)
    {
        _isFailed = true;
        _exception = exception;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stopwatch.Stop();
        
        var endMemory = _trackMemory ? GC.GetTotalMemory(false) : 0;
        
        var result = new PerformanceResult
        {
            OperationName = OperationName,
            StartTime = StartTime,
            EndTime = DateTime.Now,
            Duration = _stopwatch.Elapsed,
            IsSuccess = !_isFailed,
            Exception = _exception,
            CallStack = _callStack,
            MemoryDelta = _trackMemory ? endMemory - _startMemory : 0,
            Data = _data.Count > 0 ? _data : null
        };

        _traceHelper.RecordResult(result);
    }
}

/// <summary>
/// 空操作追踪作用域（禁用追踪时使用）
/// </summary>
internal class NoOpTraceScope : ITraceScope
{
    public string OperationName { get; }
    public DateTime StartTime { get; } = DateTime.Now;

    public NoOpTraceScope(string operationName)
    {
        OperationName = operationName;
    }

    public void SetData(string key, object value) { }
    public void SetFailed(Exception? exception = null) { }
    public void Dispose() { }
}

/// <summary>
/// TraceHelper扩展方法
/// </summary>
public static class TraceHelperExtensions
{
    /// <summary>
    /// 带自动命名的追踪（使用调用者信息）
    /// </summary>
    public static ITraceScope BeginTraceAuto(
        this ITraceHelper traceHelper,
        bool captureCallStack = false,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "")
    {
        var className = Path.GetFileNameWithoutExtension(filePath);
        var operationName = $"{className}.{memberName}";
        return traceHelper.BeginTrace(operationName, captureCallStack);
    }

    /// <summary>
    /// 追踪简单异步任务
    /// </summary>
    public static async Task<T> TraceAsync<T>(
        this ITraceHelper traceHelper,
        string operationName,
        Func<Task<T>> asyncAction,
        bool captureCallStack = false)
    {
        return await traceHelper.TraceAsync(operationName, _ => asyncAction(), captureCallStack);
    }

    /// <summary>
    /// 追踪简单异步任务（无返回值）
    /// </summary>
    public static async Task TraceAsync(
        this ITraceHelper traceHelper,
        string operationName,
        Func<Task> asyncAction,
        bool captureCallStack = false)
    {
        await traceHelper.TraceAsync(operationName, _ => asyncAction(), captureCallStack);
    }
}
