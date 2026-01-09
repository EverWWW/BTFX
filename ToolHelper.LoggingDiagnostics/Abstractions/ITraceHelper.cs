using System.Diagnostics;

namespace ToolHelper.LoggingDiagnostics.Abstractions;

/// <summary>
/// 性能分析结果
/// </summary>
public record PerformanceResult
{
    /// <summary>操作名称</summary>
    public string OperationName { get; init; } = string.Empty;
    
    /// <summary>开始时间</summary>
    public DateTime StartTime { get; init; }
    
    /// <summary>结束时间</summary>
    public DateTime EndTime { get; init; }
    
    /// <summary>执行耗时</summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>是否成功</summary>
    public bool IsSuccess { get; init; }
    
    /// <summary>异常信息</summary>
    public Exception? Exception { get; init; }
    
    /// <summary>调用栈信息</summary>
    public string? CallStack { get; init; }
    
    /// <summary>内存使用变化(字节)</summary>
    public long MemoryDelta { get; init; }
    
    /// <summary>附加数据</summary>
    public IDictionary<string, object>? Data { get; init; }
}

/// <summary>
/// 调用栈帧信息
/// </summary>
public record StackFrameInfo
{
    /// <summary>方法名</summary>
    public string MethodName { get; init; } = string.Empty;
    
    /// <summary>类名</summary>
    public string ClassName { get; init; } = string.Empty;
    
    /// <summary>文件名</summary>
    public string? FileName { get; init; }
    
    /// <summary>行号</summary>
    public int? LineNumber { get; init; }
    
    /// <summary>列号</summary>
    public int? ColumnNumber { get; init; }
    
    /// <summary>IL偏移量</summary>
    public int ILOffset { get; init; }
}

/// <summary>
/// 追踪作用域，用于自动计时
/// </summary>
public interface ITraceScope : IDisposable
{
    /// <summary>操作名称</summary>
    string OperationName { get; }
    
    /// <summary>开始时间</summary>
    DateTime StartTime { get; }
    
    /// <summary>设置附加数据</summary>
    void SetData(string key, object value);
    
    /// <summary>标记为失败</summary>
    void SetFailed(Exception? exception = null);
}

/// <summary>
/// 调试追踪接口
/// 提供调用栈分析、性能分析功能
/// </summary>
public interface ITraceHelper
{
    /// <summary>
    /// 追踪结果事件
    /// </summary>
    event EventHandler<PerformanceResult>? TraceCompleted;

    /// <summary>
    /// 开始追踪操作（使用using自动结束）
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="captureCallStack">是否捕获调用栈</param>
    /// <returns>追踪作用域</returns>
    /// <example>
    /// <code>
    /// using (traceHelper.BeginTrace("DatabaseQuery"))
    /// {
    ///     // 执行数据库查询
    /// }
    /// </code>
    /// </example>
    ITraceScope BeginTrace(string operationName, bool captureCallStack = false);

    /// <summary>
    /// 追踪同步方法执行
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="operationName">操作名称</param>
    /// <param name="action">要执行的方法</param>
    /// <param name="captureCallStack">是否捕获调用栈</param>
    /// <returns>方法返回值</returns>
    T Trace<T>(string operationName, Func<T> action, bool captureCallStack = false);

    /// <summary>
    /// 追踪同步方法执行（无返回值）
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="action">要执行的方法</param>
    /// <param name="captureCallStack">是否捕获调用栈</param>
    void Trace(string operationName, Action action, bool captureCallStack = false);

    /// <summary>
    /// 追踪异步方法执行
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="operationName">操作名称</param>
    /// <param name="asyncAction">要执行的异步方法</param>
    /// <param name="captureCallStack">是否捕获调用栈</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>方法返回值</returns>
    Task<T> TraceAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> asyncAction,
        bool captureCallStack = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 追踪异步方法执行（无返回值）
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="asyncAction">要执行的异步方法</param>
    /// <param name="captureCallStack">是否捕获调用栈</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task TraceAsync(
        string operationName,
        Func<CancellationToken, Task> asyncAction,
        bool captureCallStack = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前调用栈信息
    /// </summary>
    /// <param name="skipFrames">跳过的帧数</param>
    /// <returns>调用栈帧集合</returns>
    IReadOnlyList<StackFrameInfo> GetCallStack(int skipFrames = 0);

    /// <summary>
    /// 获取格式化的调用栈字符串
    /// </summary>
    /// <param name="skipFrames">跳过的帧数</param>
    /// <returns>格式化的调用栈</returns>
    string GetCallStackString(int skipFrames = 0);

    /// <summary>
    /// 获取性能分析历史记录
    /// </summary>
    /// <param name="operationName">操作名称筛选</param>
    /// <param name="count">返回数量</param>
    /// <returns>性能结果集合</returns>
    IReadOnlyList<PerformanceResult> GetHistory(string? operationName = null, int count = 100);

    /// <summary>
    /// 获取指定操作的统计信息
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <returns>统计信息</returns>
    OperationStatistics GetStatistics(string operationName);

    /// <summary>
    /// 清除历史记录
    /// </summary>
    void ClearHistory();
}

/// <summary>
/// 操作统计信息
/// </summary>
public record OperationStatistics
{
    /// <summary>操作名称</summary>
    public string OperationName { get; init; } = string.Empty;
    
    /// <summary>调用次数</summary>
    public long CallCount { get; init; }
    
    /// <summary>成功次数</summary>
    public long SuccessCount { get; init; }
    
    /// <summary>失败次数</summary>
    public long FailureCount { get; init; }
    
    /// <summary>平均耗时</summary>
    public TimeSpan AverageDuration { get; init; }
    
    /// <summary>最小耗时</summary>
    public TimeSpan MinDuration { get; init; }
    
    /// <summary>最大耗时</summary>
    public TimeSpan MaxDuration { get; init; }
    
    /// <summary>总耗时</summary>
    public TimeSpan TotalDuration { get; init; }
    
    /// <summary>最后调用时间</summary>
    public DateTime? LastCallTime { get; init; }
}
