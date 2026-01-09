using ToolHelper.LoggingDiagnostics.Abstractions;

namespace ToolHelper.LoggingDiagnostics.Configuration;

/// <summary>
/// 日志配置选项
/// </summary>
public class LogOptions
{
    /// <summary>
    /// 最低日志级别
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// 日志文件目录
    /// </summary>
    public string LogDirectory { get; set; } = "logs";

    /// <summary>
    /// 日志文件名格式
    /// 支持 {date} 占位符
    /// </summary>
    public string FileNameFormat { get; set; } = "log_{date}.txt";

    /// <summary>
    /// 日期格式
    /// </summary>
    public string DateFormat { get; set; } = "yyyy-MM-dd";

    /// <summary>
    /// 时间戳格式
    /// </summary>
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";

    /// <summary>
    /// 单个日志文件最大大小(MB)
    /// </summary>
    public int MaxFileSizeMB { get; set; } = 10;

    /// <summary>
    /// 最大保留文件数量
    /// </summary>
    public int MaxRetainedFiles { get; set; } = 30;

    /// <summary>
    /// 自动归档天数(超过此天数的日志将被压缩归档)
    /// </summary>
    public int ArchiveAfterDays { get; set; } = 7;

    /// <summary>
    /// 归档文件保留天数
    /// </summary>
    public int ArchiveRetentionDays { get; set; } = 90;

    /// <summary>
    /// 是否启用异步写入
    /// </summary>
    public bool EnableAsyncWrite { get; set; } = true;

    /// <summary>
    /// 异步写入缓冲区大小
    /// </summary>
    public int BufferSize { get; set; } = 1024;

    /// <summary>
    /// 刷新间隔(毫秒)
    /// </summary>
    public int FlushIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 是否按级别分文件
    /// </summary>
    public bool SeparateFileByLevel { get; set; } = false;

    /// <summary>
    /// 是否包含调用者信息
    /// </summary>
    public bool IncludeCallerInfo { get; set; } = false;

    /// <summary>
    /// 是否启用控制台输出
    /// </summary>
    public bool EnableConsoleOutput { get; set; } = true;

    /// <summary>
    /// 控制台输出是否使用颜色
    /// </summary>
    public bool UseColoredConsole { get; set; } = true;

    /// <summary>
    /// 日志消息模板
    /// 支持占位符: {timestamp}, {level}, {category}, {message}, {exception}, {threadId}
    /// </summary>
    public string MessageTemplate { get; set; } = "[{timestamp}] [{level}] [{category}] {message}";
}

/// <summary>
/// 追踪配置选项
/// </summary>
public class TraceOptions
{
    /// <summary>
    /// 是否启用追踪
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 默认是否捕获调用栈
    /// </summary>
    public bool CaptureCallStackByDefault { get; set; } = false;

    /// <summary>
    /// 历史记录最大保留数量
    /// </summary>
    public int MaxHistoryCount { get; set; } = 10000;

    /// <summary>
    /// 慢操作阈值(毫秒)
    /// 超过此阈值会被标记为慢操作
    /// </summary>
    public int SlowOperationThresholdMs { get; set; } = 1000;

    /// <summary>
    /// 是否记录内存变化
    /// </summary>
    public bool TrackMemoryUsage { get; set; } = false;

    /// <summary>
    /// 是否自动输出到日志
    /// </summary>
    public bool AutoLogResults { get; set; } = true;

    /// <summary>
    /// 输出日志的最低耗时(毫秒)
    /// 只有耗时超过此值才会输出日志
    /// </summary>
    public int MinDurationForLoggingMs { get; set; } = 0;

    /// <summary>
    /// 调用栈最大深度
    /// </summary>
    public int MaxStackDepth { get; set; } = 50;
}

/// <summary>
/// 错误码配置选项
/// </summary>
public class ErrorCodeOptions
{
    /// <summary>
    /// 默认语言
    /// </summary>
    public string DefaultCulture { get; set; } = "zh-CN";

    /// <summary>
    /// 错误码配置文件路径
    /// </summary>
    public string? ConfigFilePath { get; set; }

    /// <summary>
    /// 是否在启动时加载配置文件
    /// </summary>
    public bool LoadOnStartup { get; set; } = true;

    /// <summary>
    /// 未找到错误码时的默认消息
    /// </summary>
    public string UnknownErrorMessage { get; set; } = "未知错误: {0}";

    /// <summary>
    /// 是否允许重复注册(覆盖)
    /// </summary>
    public bool AllowOverwrite { get; set; } = false;

    /// <summary>
    /// 错误码前缀
    /// </summary>
    public string? ErrorCodePrefix { get; set; }
}

/// <summary>
/// 报警配置选项
/// </summary>
public class AlarmOptions
{
    /// <summary>
    /// 是否启用声音提示
    /// </summary>
    public bool EnableSound { get; set; } = true;

    /// <summary>
    /// 各级别报警声音文件路径
    /// </summary>
    public Dictionary<AlarmLevel, string> SoundFiles { get; set; } = new()
    {
        [AlarmLevel.Info] = "",
        [AlarmLevel.Warning] = "",
        [AlarmLevel.Alarm] = "",
        [AlarmLevel.Critical] = "",
        [AlarmLevel.Emergency] = ""
    };

    /// <summary>
    /// 是否使用系统蜂鸣器(当无声音文件时)
    /// </summary>
    public bool UseSystemBeep { get; set; } = true;

    /// <summary>
    /// 报警声音循环播放
    /// </summary>
    public bool LoopSound { get; set; } = false;

    /// <summary>
    /// 声音循环间隔(秒)
    /// </summary>
    public int SoundLoopIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// 最大活动报警数量
    /// </summary>
    public int MaxActiveAlarms { get; set; } = 10000;

    /// <summary>
    /// 历史记录保留天数
    /// </summary>
    public int HistoryRetentionDays { get; set; } = 30;

    /// <summary>
    /// 是否持久化到文件
    /// </summary>
    public bool PersistToFile { get; set; } = true;

    /// <summary>
    /// 报警记录文件目录
    /// </summary>
    public string AlarmDirectory { get; set; } = "alarms";

    /// <summary>
    /// 自动确认超时(分钟)
    /// 0表示不自动确认
    /// </summary>
    public int AutoAcknowledgeMinutes { get; set; } = 0;

    /// <summary>
    /// 是否允许重复报警(相同code未恢复时再次触发)
    /// </summary>
    public bool AllowDuplicateAlarms { get; set; } = true;
}

/// <summary>
/// 性能监控配置选项
/// </summary>
public class PerformanceMonitorOptions
{
    /// <summary>
    /// 是否启用监控
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 数据采集间隔(秒)
    /// </summary>
    public int CollectionIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// CPU使用率警告阈值(0-100)
    /// </summary>
    public double CpuWarningThreshold { get; set; } = 70;

    /// <summary>
    /// CPU使用率严重阈值(0-100)
    /// </summary>
    public double CpuCriticalThreshold { get; set; } = 90;

    /// <summary>
    /// 内存使用率警告阈值(0-100)
    /// </summary>
    public double MemoryWarningThreshold { get; set; } = 70;

    /// <summary>
    /// 内存使用率严重阈值(0-100)
    /// </summary>
    public double MemoryCriticalThreshold { get; set; } = 90;

    /// <summary>
    /// 磁盘使用率警告阈值(0-100)
    /// </summary>
    public double DiskWarningThreshold { get; set; } = 80;

    /// <summary>
    /// 磁盘使用率严重阈值(0-100)
    /// </summary>
    public double DiskCriticalThreshold { get; set; } = 95;

    /// <summary>
    /// 是否监控所有网络接口
    /// </summary>
    public bool MonitorAllNetworkInterfaces { get; set; } = false;

    /// <summary>
    /// 指定监控的网络接口名称
    /// </summary>
    public List<string> NetworkInterfaceNames { get; set; } = [];

    /// <summary>
    /// 是否监控所有磁盘
    /// </summary>
    public bool MonitorAllDisks { get; set; } = true;

    /// <summary>
    /// 指定监控的磁盘名称
    /// </summary>
    public List<string> DiskNames { get; set; } = [];

    /// <summary>
    /// 历史数据保留时间(小时)
    /// </summary>
    public int HistoryRetentionHours { get; set; } = 24;

    /// <summary>
    /// 历史数据最大记录数
    /// </summary>
    public int MaxHistoryRecords { get; set; } = 100000;

    /// <summary>
    /// 是否启用告警
    /// </summary>
    public bool EnableAlerts { get; set; } = true;

    /// <summary>
    /// 告警冷却时间(秒)
    /// 同类型告警在此时间内不会重复触发
    /// </summary>
    public int AlertCooldownSeconds { get; set; } = 60;

    /// <summary>
    /// 是否在启动时立即开始监控
    /// </summary>
    public bool StartOnInitialization { get; set; } = false;
}
