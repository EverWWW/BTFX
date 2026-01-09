namespace ToolHelper.LoggingDiagnostics.Abstractions;

/// <summary>
/// CPU使用信息
/// </summary>
public record CpuUsageInfo
{
    /// <summary>采集时间</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
    
    /// <summary>总CPU使用率(0-100)</summary>
    public double TotalUsage { get; init; }
    
    /// <summary>当前进程CPU使用率(0-100)</summary>
    public double ProcessUsage { get; init; }
    
    /// <summary>处理器核心数</summary>
    public int ProcessorCount { get; init; }
    
    /// <summary>各核心使用率</summary>
    public IReadOnlyList<double>? CoreUsages { get; init; }
}

/// <summary>
/// 内存使用信息
/// </summary>
public record MemoryUsageInfo
{
    /// <summary>采集时间</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
    
    /// <summary>总物理内存(字节)</summary>
    public long TotalPhysicalMemory { get; init; }
    
    /// <summary>可用物理内存(字节)</summary>
    public long AvailablePhysicalMemory { get; init; }
    
    /// <summary>已使用物理内存(字节)</summary>
    public long UsedPhysicalMemory => TotalPhysicalMemory - AvailablePhysicalMemory;
    
    /// <summary>物理内存使用率(0-100)</summary>
    public double PhysicalMemoryUsage => TotalPhysicalMemory > 0 
        ? (double)UsedPhysicalMemory / TotalPhysicalMemory * 100 
        : 0;
    
    /// <summary>当前进程工作集(字节)</summary>
    public long ProcessWorkingSet { get; init; }
    
    /// <summary>当前进程私有内存(字节)</summary>
    public long ProcessPrivateMemory { get; init; }
    
    /// <summary>GC堆内存(字节)</summary>
    public long GCHeapSize { get; init; }
    
    /// <summary>GC各代大小</summary>
    public IReadOnlyList<long>? GCGenerationSizes { get; init; }
    
    /// <summary>GC各代回收次数</summary>
    public IReadOnlyList<int>? GCCollectionCounts { get; init; }
}

/// <summary>
/// 网络IO信息
/// </summary>
public record NetworkIOInfo
{
    /// <summary>采集时间</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
    
    /// <summary>接口名称</summary>
    public string InterfaceName { get; init; } = string.Empty;
    
    /// <summary>发送字节数</summary>
    public long BytesSent { get; init; }
    
    /// <summary>接收字节数</summary>
    public long BytesReceived { get; init; }
    
    /// <summary>发送速率(字节/秒)</summary>
    public double SendRate { get; init; }
    
    /// <summary>接收速率(字节/秒)</summary>
    public double ReceiveRate { get; init; }
    
    /// <summary>活动连接数</summary>
    public int ActiveConnections { get; init; }
}

/// <summary>
/// 磁盘IO信息
/// </summary>
public record DiskIOInfo
{
    /// <summary>采集时间</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
    
    /// <summary>驱动器名称</summary>
    public string DriveName { get; init; } = string.Empty;
    
    /// <summary>总空间(字节)</summary>
    public long TotalSpace { get; init; }
    
    /// <summary>可用空间(字节)</summary>
    public long AvailableSpace { get; init; }
    
    /// <summary>已用空间(字节)</summary>
    public long UsedSpace => TotalSpace - AvailableSpace;
    
    /// <summary>使用率(0-100)</summary>
    public double UsagePercentage => TotalSpace > 0 
        ? (double)UsedSpace / TotalSpace * 100 
        : 0;
    
    /// <summary>读取速率(字节/秒)</summary>
    public double ReadRate { get; init; }
    
    /// <summary>写入速率(字节/秒)</summary>
    public double WriteRate { get; init; }
}

/// <summary>
/// 系统综合信息
/// </summary>
public record SystemInfo
{
    /// <summary>采集时间</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
    
    /// <summary>CPU信息</summary>
    public CpuUsageInfo Cpu { get; init; } = new();
    
    /// <summary>内存信息</summary>
    public MemoryUsageInfo Memory { get; init; } = new();
    
    /// <summary>网络IO信息</summary>
    public IReadOnlyList<NetworkIOInfo> NetworkIO { get; init; } = [];
    
    /// <summary>磁盘IO信息</summary>
    public IReadOnlyList<DiskIOInfo> DiskIO { get; init; } = [];
    
    /// <summary>系统运行时间</summary>
    public TimeSpan SystemUptime { get; init; }
    
    /// <summary>进程运行时间</summary>
    public TimeSpan ProcessUptime { get; init; }
    
    /// <summary>线程数</summary>
    public int ThreadCount { get; init; }
    
    /// <summary>句柄数</summary>
    public int HandleCount { get; init; }
}

/// <summary>
/// 性能阈值配置
/// </summary>
public record PerformanceThreshold
{
    /// <summary>CPU使用率警告阈值</summary>
    public double CpuWarningThreshold { get; init; } = 70;
    
    /// <summary>CPU使用率严重阈值</summary>
    public double CpuCriticalThreshold { get; init; } = 90;
    
    /// <summary>内存使用率警告阈值</summary>
    public double MemoryWarningThreshold { get; init; } = 70;
    
    /// <summary>内存使用率严重阈值</summary>
    public double MemoryCriticalThreshold { get; init; } = 90;
    
    /// <summary>磁盘使用率警告阈值</summary>
    public double DiskWarningThreshold { get; init; } = 80;
    
    /// <summary>磁盘使用率严重阈值</summary>
    public double DiskCriticalThreshold { get; init; } = 95;
}

/// <summary>
/// 性能告警事件参数
/// </summary>
public class PerformanceAlertEventArgs : EventArgs
{
    /// <summary>告警类型</summary>
    public PerformanceAlertType AlertType { get; init; }
    
    /// <summary>告警级别</summary>
    public AlertLevel Level { get; init; }
    
    /// <summary>当前值</summary>
    public double CurrentValue { get; init; }
    
    /// <summary>阈值</summary>
    public double Threshold { get; init; }
    
    /// <summary>告警消息</summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>发生时间</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

/// <summary>
/// 性能告警类型
/// </summary>
public enum PerformanceAlertType
{
    /// <summary>CPU使用率</summary>
    CpuUsage,
    /// <summary>内存使用率</summary>
    MemoryUsage,
    /// <summary>磁盘使用率</summary>
    DiskUsage,
    /// <summary>网络流量</summary>
    NetworkTraffic,
    /// <summary>GC压力</summary>
    GCPressure
}

/// <summary>
/// 告警级别
/// </summary>
public enum AlertLevel
{
    /// <summary>正常</summary>
    Normal,
    /// <summary>警告</summary>
    Warning,
    /// <summary>严重</summary>
    Critical
}

/// <summary>
/// 性能监控接口
/// 提供CPU、内存、网络IO等系统性能监控功能
/// </summary>
public interface IPerformanceMonitor : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 性能告警事件
    /// </summary>
    event EventHandler<PerformanceAlertEventArgs>? PerformanceAlert;

    /// <summary>
    /// 数据采集完成事件
    /// </summary>
    event EventHandler<SystemInfo>? DataCollected;

    /// <summary>
    /// 是否正在监控
    /// </summary>
    bool IsMonitoring { get; }

    /// <summary>
    /// 采集间隔
    /// </summary>
    TimeSpan CollectionInterval { get; set; }

    /// <summary>
    /// 性能阈值配置
    /// </summary>
    PerformanceThreshold Threshold { get; set; }

    /// <summary>
    /// 启动监控
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止监控
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 获取当前CPU使用信息
    /// </summary>
    /// <returns>CPU使用信息</returns>
    Task<CpuUsageInfo> GetCpuUsageAsync();

    /// <summary>
    /// 获取当前内存使用信息
    /// </summary>
    /// <returns>内存使用信息</returns>
    MemoryUsageInfo GetMemoryUsage();

    /// <summary>
    /// 获取当前网络IO信息
    /// </summary>
    /// <returns>网络IO信息集合</returns>
    IReadOnlyList<NetworkIOInfo> GetNetworkIO();

    /// <summary>
    /// 获取当前磁盘IO信息
    /// </summary>
    /// <returns>磁盘IO信息集合</returns>
    IReadOnlyList<DiskIOInfo> GetDiskIO();

    /// <summary>
    /// 获取系统综合信息
    /// </summary>
    /// <returns>系统信息</returns>
    Task<SystemInfo> GetSystemInfoAsync();

    /// <summary>
    /// 获取历史数据
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>历史数据集合</returns>
    IAsyncEnumerable<SystemInfo> GetHistoryAsync(
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 强制执行GC
    /// </summary>
    /// <param name="generation">要回收的代数，-1表示全部</param>
    /// <param name="blocking">是否阻塞</param>
    void ForceGC(int generation = -1, bool blocking = false);

    /// <summary>
    /// 获取GC统计信息
    /// </summary>
    /// <returns>GC统计</returns>
    GCStatistics GetGCStatistics();

    /// <summary>
    /// 清除历史数据
    /// </summary>
    /// <param name="beforeTime">清除此时间之前的数据</param>
    void ClearHistory(DateTime beforeTime);
}

/// <summary>
/// GC统计信息
/// </summary>
public record GCStatistics
{
    /// <summary>Gen0回收次数</summary>
    public int Gen0Collections { get; init; }
    
    /// <summary>Gen1回收次数</summary>
    public int Gen1Collections { get; init; }
    
    /// <summary>Gen2回收次数</summary>
    public int Gen2Collections { get; init; }
    
    /// <summary>总内存</summary>
    public long TotalMemory { get; init; }
    
    /// <summary>堆大小</summary>
    public long HeapSize { get; init; }
    
    /// <summary>碎片大小</summary>
    public long FragmentedBytes { get; init; }
    
    /// <summary>GC暂停时间百分比</summary>
    public double PauseTimePercentage { get; init; }
    
    /// <summary>是否正在压缩</summary>
    public bool IsCompacting { get; init; }
    
    /// <summary>是否并发模式</summary>
    public bool IsConcurrent { get; init; }
}
