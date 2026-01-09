using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using ToolHelper.LoggingDiagnostics.Abstractions;
using ToolHelper.LoggingDiagnostics.Configuration;

namespace ToolHelper.LoggingDiagnostics.Performance;

/// <summary>
/// 性能监控类
/// 提供CPU、内存、网络IO等系统性能监控功能
/// </summary>
/// <example>
/// <code>
/// // 启动监控
/// await performanceMonitor.StartAsync();
/// 
/// // 获取当前系统信息
/// var systemInfo = await performanceMonitor.GetSystemInfoAsync();
/// Console.WriteLine($"CPU: {systemInfo.Cpu.TotalUsage:F1}%");
/// Console.WriteLine($"内存: {systemInfo.Memory.PhysicalMemoryUsage:F1}%");
/// 
/// // 监听性能告警
/// performanceMonitor.PerformanceAlert += (s, e) => {
///     Console.WriteLine($"告警: {e.AlertType} - {e.Message}");
/// };
/// </code>
/// </example>
public class PerformanceMonitor : IPerformanceMonitor
{
    private readonly PerformanceMonitorOptions _options;
    private readonly ConcurrentQueue<SystemInfo> _history;
    private readonly ConcurrentDictionary<PerformanceAlertType, DateTime> _lastAlertTimes;
    private readonly Process _currentProcess;
    private readonly Stopwatch _processStopwatch;
    
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private bool _disposed;
    
    // 用于计算CPU使用率
    private DateTime _lastCpuCheck = DateTime.MinValue;
    private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;
    
    // 网络统计缓存
    private readonly ConcurrentDictionary<string, (long sent, long received, DateTime time)> _networkStats = new();

    /// <inheritdoc/>
    public event EventHandler<PerformanceAlertEventArgs>? PerformanceAlert;

    /// <inheritdoc/>
    public event EventHandler<SystemInfo>? DataCollected;

    /// <inheritdoc/>
    public bool IsMonitoring => _monitorTask is { IsCompleted: false };

    /// <inheritdoc/>
    public TimeSpan CollectionInterval { get; set; }

    /// <inheritdoc/>
    public PerformanceThreshold Threshold { get; set; }

    /// <summary>
    /// 创建PerformanceMonitor实例
    /// </summary>
    /// <param name="options">性能监控配置选项</param>
    public PerformanceMonitor(IOptions<PerformanceMonitorOptions> options)
    {
        _options = options.Value;
        _history = new ConcurrentQueue<SystemInfo>();
        _lastAlertTimes = new ConcurrentDictionary<PerformanceAlertType, DateTime>();
        _currentProcess = Process.GetCurrentProcess();
        _processStopwatch = Stopwatch.StartNew();
        
        CollectionInterval = TimeSpan.FromSeconds(_options.CollectionIntervalSeconds);
        Threshold = new PerformanceThreshold
        {
            CpuWarningThreshold = _options.CpuWarningThreshold,
            CpuCriticalThreshold = _options.CpuCriticalThreshold,
            MemoryWarningThreshold = _options.MemoryWarningThreshold,
            MemoryCriticalThreshold = _options.MemoryCriticalThreshold,
            DiskWarningThreshold = _options.DiskWarningThreshold,
            DiskCriticalThreshold = _options.DiskCriticalThreshold
        };

        if (_options.StartOnInitialization && _options.Enabled)
        {
            _ = StartAsync();
        }
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsMonitoring) return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitorTask = MonitorLoopAsync(_cts.Token);

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        if (!IsMonitoring) return;

        _cts?.Cancel();
        
        if (_monitorTask != null)
        {
            try
            {
                await _monitorTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                // 超时，继续
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }

        _cts?.Dispose();
        _cts = null;
    }

    /// <inheritdoc/>
    public async Task<CpuUsageInfo> GetCpuUsageAsync()
    {
        _currentProcess.Refresh();
        
        var currentTime = DateTime.UtcNow;
        var currentTotalProcessorTime = _currentProcess.TotalProcessorTime;
        
        double processCpuUsage = 0;
        
        if (_lastCpuCheck != DateTime.MinValue)
        {
            var timeDiff = (currentTime - _lastCpuCheck).TotalMilliseconds;
            var cpuDiff = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
            
            if (timeDiff > 0)
            {
                processCpuUsage = (cpuDiff / timeDiff) * 100 / Environment.ProcessorCount;
            }
        }
        
        _lastCpuCheck = currentTime;
        _lastTotalProcessorTime = currentTotalProcessorTime;

        // 获取系统CPU使用率（简化版本）
        double totalUsage = processCpuUsage; // 在实际场景中可以通过性能计数器获取

        return new CpuUsageInfo
        {
            TotalUsage = Math.Min(totalUsage, 100),
            ProcessUsage = Math.Min(processCpuUsage, 100),
            ProcessorCount = Environment.ProcessorCount
        };
    }

    /// <inheritdoc/>
    public MemoryUsageInfo GetMemoryUsage()
    {
        _currentProcess.Refresh();

        var gcMemoryInfo = GC.GetGCMemoryInfo();

        return new MemoryUsageInfo
        {
            TotalPhysicalMemory = gcMemoryInfo.TotalAvailableMemoryBytes,
            AvailablePhysicalMemory = gcMemoryInfo.TotalAvailableMemoryBytes - GC.GetTotalMemory(false),
            ProcessWorkingSet = _currentProcess.WorkingSet64,
            ProcessPrivateMemory = _currentProcess.PrivateMemorySize64,
            GCHeapSize = GC.GetTotalMemory(false),
            GCGenerationSizes = null, // Generation sizes not directly available in this .NET version
            GCCollectionCounts = [
                GC.CollectionCount(0),
                GC.CollectionCount(1),
                GC.CollectionCount(2)
            ]
        };
    }

    /// <inheritdoc/>
    public IReadOnlyList<NetworkIOInfo> GetNetworkIO()
    {
        var results = new List<NetworkIOInfo>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            if (!_options.MonitorAllNetworkInterfaces && _options.NetworkInterfaceNames.Count > 0)
            {
                interfaces = interfaces.Where(ni => 
                    _options.NetworkInterfaceNames.Contains(ni.Name, StringComparer.OrdinalIgnoreCase));
            }

            var now = DateTime.UtcNow;

            foreach (var ni in interfaces)
            {
                var stats = ni.GetIPv4Statistics();
                var sent = stats.BytesSent;
                var received = stats.BytesReceived;

                double sendRate = 0, receiveRate = 0;

                if (_networkStats.TryGetValue(ni.Name, out var lastStats))
                {
                    var timeDiff = (now - lastStats.time).TotalSeconds;
                    if (timeDiff > 0)
                    {
                        sendRate = (sent - lastStats.sent) / timeDiff;
                        receiveRate = (received - lastStats.received) / timeDiff;
                    }
                }

                _networkStats[ni.Name] = (sent, received, now);

                results.Add(new NetworkIOInfo
                {
                    InterfaceName = ni.Name,
                    BytesSent = sent,
                    BytesReceived = received,
                    SendRate = Math.Max(0, sendRate),
                    ReceiveRate = Math.Max(0, receiveRate)
                });
            }
        }
        catch
        {
            // 忽略网络监控错误
        }

        return results;
    }

    /// <inheritdoc/>
    public IReadOnlyList<DiskIOInfo> GetDiskIO()
    {
        var results = new List<DiskIOInfo>();

        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

            if (!_options.MonitorAllDisks && _options.DiskNames.Count > 0)
            {
                drives = drives.Where(d => 
                    _options.DiskNames.Contains(d.Name, StringComparer.OrdinalIgnoreCase));
            }

            foreach (var drive in drives)
            {
                results.Add(new DiskIOInfo
                {
                    DriveName = drive.Name,
                    TotalSpace = drive.TotalSize,
                    AvailableSpace = drive.AvailableFreeSpace
                });
            }
        }
        catch
        {
            // 忽略磁盘监控错误
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        var cpuInfo = await GetCpuUsageAsync();
        var memoryInfo = GetMemoryUsage();
        var networkIO = GetNetworkIO();
        var diskIO = GetDiskIO();

        _currentProcess.Refresh();

        return new SystemInfo
        {
            Cpu = cpuInfo,
            Memory = memoryInfo,
            NetworkIO = networkIO,
            DiskIO = diskIO,
            SystemUptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
            ProcessUptime = _processStopwatch.Elapsed,
            ThreadCount = _currentProcess.Threads.Count,
            HandleCount = _currentProcess.HandleCount
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SystemInfo> GetHistoryAsync(
        DateTime startTime,
        DateTime endTime,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var info in _history)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (info.Timestamp >= startTime && info.Timestamp <= endTime)
            {
                yield return info;
            }
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void ForceGC(int generation = -1, bool blocking = false)
    {
        if (generation < 0)
        {
            GC.Collect();
        }
        else
        {
            GC.Collect(generation, blocking ? GCCollectionMode.Forced : GCCollectionMode.Optimized);
        }

        if (blocking)
        {
            GC.WaitForPendingFinalizers();
        }
    }

    /// <inheritdoc/>
    public GCStatistics GetGCStatistics()
    {
        var gcInfo = GC.GetGCMemoryInfo();

        return new GCStatistics
        {
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            TotalMemory = GC.GetTotalMemory(false),
            HeapSize = gcInfo.HeapSizeBytes,
            FragmentedBytes = gcInfo.FragmentedBytes,
            PauseTimePercentage = gcInfo.PauseTimePercentage,
            IsCompacting = gcInfo.Compacted,
            IsConcurrent = gcInfo.Concurrent
        };
    }

    /// <inheritdoc/>
    public void ClearHistory(DateTime beforeTime)
    {
        var newHistory = new ConcurrentQueue<SystemInfo>();

        while (_history.TryDequeue(out var info))
        {
            if (info.Timestamp >= beforeTime)
            {
                newHistory.Enqueue(info);
            }
        }

        foreach (var info in newHistory)
        {
            _history.Enqueue(info);
        }
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        // 初始化CPU计算
        await GetCpuUsageAsync();
        await Task.Delay(100, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var systemInfo = await GetSystemInfoAsync();
                
                // 添加到历史记录
                _history.Enqueue(systemInfo);
                TrimHistory();

                // 触发数据收集事件
                DataCollected?.Invoke(this, systemInfo);

                // 检查阈值并触发告警
                if (_options.EnableAlerts)
                {
                    CheckAndRaiseAlerts(systemInfo);
                }

                await Task.Delay(CollectionInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // 忽略单次收集错误，继续监控
                await Task.Delay(CollectionInterval, cancellationToken);
            }
        }
    }

    private void TrimHistory()
    {
        var cutoffTime = DateTime.Now.AddHours(-_options.HistoryRetentionHours);
        
        while (_history.Count > _options.MaxHistoryRecords ||
               (_history.TryPeek(out var oldest) && oldest.Timestamp < cutoffTime))
        {
            _history.TryDequeue(out _);
        }
    }

    private void CheckAndRaiseAlerts(SystemInfo info)
    {
        // CPU告警
        CheckAlert(
            PerformanceAlertType.CpuUsage,
            info.Cpu.ProcessUsage,
            Threshold.CpuWarningThreshold,
            Threshold.CpuCriticalThreshold,
            "CPU使用率");

        // 内存告警
        CheckAlert(
            PerformanceAlertType.MemoryUsage,
            info.Memory.PhysicalMemoryUsage,
            Threshold.MemoryWarningThreshold,
            Threshold.MemoryCriticalThreshold,
            "内存使用率");

        // 磁盘告警
        foreach (var disk in info.DiskIO)
        {
            CheckAlert(
                PerformanceAlertType.DiskUsage,
                disk.UsagePercentage,
                Threshold.DiskWarningThreshold,
                Threshold.DiskCriticalThreshold,
                $"磁盘 {disk.DriveName} 使用率");
        }
    }

    private void CheckAlert(
        PerformanceAlertType alertType,
        double currentValue,
        double warningThreshold,
        double criticalThreshold,
        string resourceName)
    {
        AlertLevel level;
        
        if (currentValue >= criticalThreshold)
        {
            level = AlertLevel.Critical;
        }
        else if (currentValue >= warningThreshold)
        {
            level = AlertLevel.Warning;
        }
        else
        {
            return; // 正常，不告警
        }

        // 检查冷却时间
        if (_lastAlertTimes.TryGetValue(alertType, out var lastAlertTime))
        {
            if ((DateTime.Now - lastAlertTime).TotalSeconds < _options.AlertCooldownSeconds)
            {
                return; // 还在冷却期
            }
        }

        _lastAlertTimes[alertType] = DateTime.Now;

        var threshold = level == AlertLevel.Critical ? criticalThreshold : warningThreshold;
        var args = new PerformanceAlertEventArgs
        {
            AlertType = alertType,
            Level = level,
            CurrentValue = currentValue,
            Threshold = threshold,
            Message = $"{resourceName}过高: {currentValue:F1}% (阈值: {threshold:F1}%)"
        };

        PerformanceAlert?.Invoke(this, args);
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
            _cts?.Cancel();
            _monitorTask?.Wait(TimeSpan.FromSeconds(2));
            _cts?.Dispose();
            _currentProcess.Dispose();
        }

        _disposed = true;
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_disposed) return;

        await StopAsync();
        _currentProcess.Dispose();
    }
}
