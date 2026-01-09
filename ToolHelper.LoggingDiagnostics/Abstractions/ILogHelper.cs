namespace ToolHelper.LoggingDiagnostics.Abstractions;

/// <summary>
/// 日志级别枚举
/// </summary>
public enum LogLevel
{
    /// <summary>跟踪级别 - 最详细的日志</summary>
    Trace = 0,
    /// <summary>调试级别</summary>
    Debug = 1,
    /// <summary>信息级别</summary>
    Information = 2,
    /// <summary>警告级别</summary>
    Warning = 3,
    /// <summary>错误级别</summary>
    Error = 4,
    /// <summary>严重错误级别</summary>
    Critical = 5,
    /// <summary>不记录日志</summary>
    None = 6
}

/// <summary>
/// 日志条目
/// </summary>
public record LogEntry
{
    /// <summary>日志时间</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
    
    /// <summary>日志级别</summary>
    public LogLevel Level { get; init; }
    
    /// <summary>日志类别/来源</summary>
    public string Category { get; init; } = string.Empty;
    
    /// <summary>日志消息</summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>异常信息</summary>
    public Exception? Exception { get; init; }
    
    /// <summary>线程ID</summary>
    public int ThreadId { get; init; } = Environment.CurrentManagedThreadId;
    
    /// <summary>额外属性</summary>
    public IDictionary<string, object>? Properties { get; init; }
}

/// <summary>
/// 日志记录接口
/// 提供分级、分文件、自动归档的日志功能
/// </summary>
public interface ILogHelper : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 当前最低日志级别
    /// </summary>
    LogLevel MinimumLevel { get; set; }

    /// <summary>
    /// 记录跟踪日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="properties">附加属性</param>
    void Trace(string message, IDictionary<string, object>? properties = null);

    /// <summary>
    /// 记录调试日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="properties">附加属性</param>
    void Debug(string message, IDictionary<string, object>? properties = null);

    /// <summary>
    /// 记录信息日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="properties">附加属性</param>
    void Information(string message, IDictionary<string, object>? properties = null);

    /// <summary>
    /// 记录警告日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="properties">附加属性</param>
    void Warning(string message, IDictionary<string, object>? properties = null);

    /// <summary>
    /// 记录错误日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="exception">异常信息</param>
    /// <param name="properties">附加属性</param>
    void Error(string message, Exception? exception = null, IDictionary<string, object>? properties = null);

    /// <summary>
    /// 记录严重错误日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="exception">异常信息</param>
    /// <param name="properties">附加属性</param>
    void Critical(string message, Exception? exception = null, IDictionary<string, object>? properties = null);

    /// <summary>
    /// 通用日志记录方法
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="message">日志消息</param>
    /// <param name="exception">异常信息</param>
    /// <param name="properties">附加属性</param>
    void Log(LogLevel level, string message, Exception? exception = null, IDictionary<string, object>? properties = null);

    /// <summary>
    /// 异步记录日志
    /// </summary>
    /// <param name="entry">日志条目</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask LogAsync(LogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步刷新日志缓冲区
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 手动触发日志归档
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task ArchiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定时间范围的日志
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="level">日志级别筛选</param>
    /// <param name="cancellationToken">取消令牌</param>
    IAsyncEnumerable<LogEntry> GetLogsAsync(
        DateTime startTime,
        DateTime endTime,
        LogLevel? level = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建带类别的日志记录器
    /// </summary>
    /// <param name="category">日志类别</param>
    ILogHelper ForCategory(string category);
}
