namespace ToolHelper.LoggingDiagnostics.Abstractions;

/// <summary>
/// 报警级别
/// </summary>
public enum AlarmLevel
{
    /// <summary>信息</summary>
    Info = 0,
    /// <summary>警告</summary>
    Warning = 1,
    /// <summary>一般报警</summary>
    Alarm = 2,
    /// <summary>严重报警</summary>
    Critical = 3,
    /// <summary>紧急报警</summary>
    Emergency = 4
}

/// <summary>
/// 报警状态
/// </summary>
public enum AlarmStatus
{
    /// <summary>活动中</summary>
    Active = 0,
    /// <summary>已确认</summary>
    Acknowledged = 1,
    /// <summary>已恢复</summary>
    Recovered = 2,
    /// <summary>已关闭</summary>
    Closed = 3
}

/// <summary>
/// 报警记录
/// </summary>
public record AlarmRecord
{
    /// <summary>报警ID</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    
    /// <summary>报警码</summary>
    public string Code { get; init; } = string.Empty;
    
    /// <summary>报警级别</summary>
    public AlarmLevel Level { get; init; }
    
    /// <summary>报警状态</summary>
    public AlarmStatus Status { get; set; }
    
    /// <summary>报警消息</summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>报警来源</summary>
    public string Source { get; init; } = string.Empty;
    
    /// <summary>触发时间</summary>
    public DateTime TriggerTime { get; init; } = DateTime.Now;
    
    /// <summary>确认时间</summary>
    public DateTime? AcknowledgeTime { get; set; }
    
    /// <summary>确认人</summary>
    public string? AcknowledgedBy { get; set; }
    
    /// <summary>恢复时间</summary>
    public DateTime? RecoveryTime { get; set; }
    
    /// <summary>关闭时间</summary>
    public DateTime? CloseTime { get; set; }
    
    /// <summary>附加数据</summary>
    public IDictionary<string, object>? Data { get; init; }
    
    /// <summary>备注</summary>
    public string? Remarks { get; set; }
}

/// <summary>
/// 报警事件参数
/// </summary>
public class AlarmEventArgs : EventArgs
{
    /// <summary>报警记录</summary>
    public AlarmRecord Alarm { get; init; }
    
    /// <summary>事件类型</summary>
    public AlarmEventType EventType { get; init; }
    
    public AlarmEventArgs(AlarmRecord alarm, AlarmEventType eventType)
    {
        Alarm = alarm;
        EventType = eventType;
    }
}

/// <summary>
/// 报警事件类型
/// </summary>
public enum AlarmEventType
{
    /// <summary>新报警</summary>
    Triggered,
    /// <summary>已确认</summary>
    Acknowledged,
    /// <summary>已恢复</summary>
    Recovered,
    /// <summary>已关闭</summary>
    Closed,
    /// <summary>级别变更</summary>
    LevelChanged
}

/// <summary>
/// 报警查询条件
/// </summary>
public record AlarmQuery
{
    /// <summary>开始时间</summary>
    public DateTime? StartTime { get; init; }
    
    /// <summary>结束时间</summary>
    public DateTime? EndTime { get; init; }
    
    /// <summary>报警级别</summary>
    public AlarmLevel? Level { get; init; }
    
    /// <summary>报警状态</summary>
    public AlarmStatus? Status { get; init; }
    
    /// <summary>报警来源</summary>
    public string? Source { get; init; }
    
    /// <summary>报警码</summary>
    public string? Code { get; init; }
    
    /// <summary>分页大小</summary>
    public int PageSize { get; init; } = 100;
    
    /// <summary>页码（从0开始）</summary>
    public int PageIndex { get; init; }
}

/// <summary>
/// 报警管理接口
/// 提供报警记录、声音提示、报警历史管理功能
/// </summary>
public interface IAlarmHelper : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 报警触发事件
    /// </summary>
    event EventHandler<AlarmEventArgs>? AlarmTriggered;

    /// <summary>
    /// 报警状态变更事件
    /// </summary>
    event EventHandler<AlarmEventArgs>? AlarmStatusChanged;

    /// <summary>
    /// 当前活动报警数量
    /// </summary>
    int ActiveAlarmCount { get; }

    /// <summary>
    /// 是否启用声音提示
    /// </summary>
    bool SoundEnabled { get; set; }

    /// <summary>
    /// 触发报警
    /// </summary>
    /// <param name="code">报警码</param>
    /// <param name="message">报警消息</param>
    /// <param name="level">报警级别</param>
    /// <param name="source">报警来源</param>
    /// <param name="data">附加数据</param>
    /// <returns>报警记录</returns>
    AlarmRecord Trigger(
        string code,
        string message,
        AlarmLevel level = AlarmLevel.Alarm,
        string? source = null,
        IDictionary<string, object>? data = null);

    /// <summary>
    /// 异步触发报警
    /// </summary>
    /// <param name="code">报警码</param>
    /// <param name="message">报警消息</param>
    /// <param name="level">报警级别</param>
    /// <param name="source">报警来源</param>
    /// <param name="data">附加数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>报警记录</returns>
    Task<AlarmRecord> TriggerAsync(
        string code,
        string message,
        AlarmLevel level = AlarmLevel.Alarm,
        string? source = null,
        IDictionary<string, object>? data = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 确认报警
    /// </summary>
    /// <param name="alarmId">报警ID</param>
    /// <param name="acknowledgedBy">确认人</param>
    /// <param name="remarks">备注</param>
    /// <returns>是否成功</returns>
    bool Acknowledge(string alarmId, string? acknowledgedBy = null, string? remarks = null);

    /// <summary>
    /// 批量确认报警
    /// </summary>
    /// <param name="alarmIds">报警ID集合</param>
    /// <param name="acknowledgedBy">确认人</param>
    /// <returns>成功确认的数量</returns>
    int AcknowledgeRange(IEnumerable<string> alarmIds, string? acknowledgedBy = null);

    /// <summary>
    /// 确认所有活动报警
    /// </summary>
    /// <param name="acknowledgedBy">确认人</param>
    /// <returns>成功确认的数量</returns>
    int AcknowledgeAll(string? acknowledgedBy = null);

    /// <summary>
    /// 恢复报警
    /// </summary>
    /// <param name="alarmId">报警ID</param>
    /// <returns>是否成功</returns>
    bool Recover(string alarmId);

    /// <summary>
    /// 通过报警码恢复报警
    /// </summary>
    /// <param name="code">报警码</param>
    /// <returns>恢复的报警数量</returns>
    int RecoverByCode(string code);

    /// <summary>
    /// 关闭报警
    /// </summary>
    /// <param name="alarmId">报警ID</param>
    /// <param name="remarks">备注</param>
    /// <returns>是否成功</returns>
    bool Close(string alarmId, string? remarks = null);

    /// <summary>
    /// 获取报警记录
    /// </summary>
    /// <param name="alarmId">报警ID</param>
    /// <returns>报警记录</returns>
    AlarmRecord? GetAlarm(string alarmId);

    /// <summary>
    /// 获取所有活动报警
    /// </summary>
    /// <returns>活动报警集合</returns>
    IReadOnlyList<AlarmRecord> GetActiveAlarms();

    /// <summary>
    /// 查询报警历史
    /// </summary>
    /// <param name="query">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>报警记录集合</returns>
    Task<IReadOnlyList<AlarmRecord>> QueryAsync(AlarmQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式查询报警历史
    /// </summary>
    /// <param name="query">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    IAsyncEnumerable<AlarmRecord> QueryStreamAsync(AlarmQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 播放报警声音
    /// </summary>
    /// <param name="level">报警级别</param>
    void PlaySound(AlarmLevel level);

    /// <summary>
    /// 停止报警声音
    /// </summary>
    void StopSound();

    /// <summary>
    /// 获取报警统计
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <returns>统计信息</returns>
    AlarmStatistics GetStatistics(DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// 清除历史记录
    /// </summary>
    /// <param name="beforeTime">清除此时间之前的记录</param>
    /// <returns>清除的记录数</returns>
    int ClearHistory(DateTime beforeTime);
}

/// <summary>
/// 报警统计信息
/// </summary>
public record AlarmStatistics
{
    /// <summary>总报警数</summary>
    public int TotalCount { get; init; }
    
    /// <summary>活动报警数</summary>
    public int ActiveCount { get; init; }
    
    /// <summary>已确认数</summary>
    public int AcknowledgedCount { get; init; }
    
    /// <summary>已恢复数</summary>
    public int RecoveredCount { get; init; }
    
    /// <summary>已关闭数</summary>
    public int ClosedCount { get; init; }
    
    /// <summary>各级别统计</summary>
    public IDictionary<AlarmLevel, int> CountByLevel { get; init; } = new Dictionary<AlarmLevel, int>();
    
    /// <summary>各来源统计</summary>
    public IDictionary<string, int> CountBySource { get; init; } = new Dictionary<string, int>();
}
