namespace BTFX.Models.Analysis;

/// <summary>
/// 分析进度变更事件参数
/// </summary>
public class AnalysisProgressEventArgs : EventArgs
{
    /// <summary>
    /// 请求编号
    /// </summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>
    /// 任务状态（pending/running/completed/failed/cancelled）
    /// </summary>
    public string TaskStatus { get; init; } = string.Empty;

    /// <summary>
    /// 进度百分比（0-100）
    /// </summary>
    public int Progress { get; init; }

    /// <summary>
    /// 状态描述文字
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 错误码（失败时）
    /// </summary>
    public int? ErrorCode { get; init; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

/// <summary>
/// 分析日志消息事件参数
/// </summary>
public class AnalysisLogEventArgs : EventArgs
{
    /// <summary>
    /// 日志内容
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>
    /// 是否为错误日志
    /// </summary>
    public bool IsError { get; init; }
}
