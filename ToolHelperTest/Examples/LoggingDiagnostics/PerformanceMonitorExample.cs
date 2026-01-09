using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ToolHelper.LoggingDiagnostics.Abstractions;
using ToolHelper.LoggingDiagnostics.Configuration;
using ToolHelper.LoggingDiagnostics.Extensions;
using ToolHelper.LoggingDiagnostics.Performance;

namespace ToolHelperTest.Examples.LoggingDiagnostics;

/// <summary>
/// PerformanceMonitor 使用示例
/// 演示CPU、内存、网络IO等系统性能监控功能
/// </summary>
public class PerformanceMonitorExample
{
    /// <summary>
    /// 示例 1: 获取当前系统信息
    /// </summary>
    public static async Task GetCurrentSystemInfoAsync()
    {
        Console.WriteLine("=== PerformanceMonitor 获取系统信息示例 ===\n");

        var options = Options.Create(new PerformanceMonitorOptions
        {
            Enabled = true,
            StartOnInitialization = false
        });

        await using var monitor = new PerformanceMonitor(options);

        // 获取系统综合信息
        var systemInfo = await monitor.GetSystemInfoAsync();

        Console.WriteLine("系统综合信息:");
        Console.WriteLine($"  采集时间: {systemInfo.Timestamp}");
        Console.WriteLine($"  系统运行时间: {systemInfo.SystemUptime}");
        Console.WriteLine($"  进程运行时间: {systemInfo.ProcessUptime}");
        Console.WriteLine($"  线程数: {systemInfo.ThreadCount}");
        Console.WriteLine($"  句柄数: {systemInfo.HandleCount}");

        Console.WriteLine("\nCPU信息:");
        Console.WriteLine($"  处理器数量: {systemInfo.Cpu.ProcessorCount}");
        Console.WriteLine($"  进程CPU使用率: {systemInfo.Cpu.ProcessUsage:F1}%");

        Console.WriteLine("\n内存信息:");
        Console.WriteLine($"  总物理内存: {systemInfo.Memory.TotalPhysicalMemory / 1024 / 1024:N0} MB");
        Console.WriteLine($"  可用物理内存: {systemInfo.Memory.AvailablePhysicalMemory / 1024 / 1024:N0} MB");
        Console.WriteLine($"  内存使用率: {systemInfo.Memory.PhysicalMemoryUsage:F1}%");
        Console.WriteLine($"  进程工作集: {systemInfo.Memory.ProcessWorkingSet / 1024 / 1024:N1} MB");
        Console.WriteLine($"  GC堆大小: {systemInfo.Memory.GCHeapSize / 1024 / 1024:N1} MB");

        Console.WriteLine("\n? 获取系统信息完成\n");
    }

    /// <summary>
    /// 示例 2: 内存监控详情
    /// </summary>
    public static async Task MemoryMonitoringAsync()
    {
        Console.WriteLine("=== PerformanceMonitor 内存监控示例 ===\n");

        var options = Options.Create(new PerformanceMonitorOptions());
        await using var monitor = new PerformanceMonitor(options);

        // 获取内存信息
        var memInfo = monitor.GetMemoryUsage();

        Console.WriteLine("内存详情:");
        Console.WriteLine($"  总物理内存: {memInfo.TotalPhysicalMemory / 1024 / 1024:N0} MB");
        Console.WriteLine($"  已用内存: {memInfo.UsedPhysicalMemory / 1024 / 1024:N0} MB");
        Console.WriteLine($"  可用内存: {memInfo.AvailablePhysicalMemory / 1024 / 1024:N0} MB");
        Console.WriteLine($"  使用率: {memInfo.PhysicalMemoryUsage:F1}%");

        Console.WriteLine("\n进程内存:");
        Console.WriteLine($"  工作集: {memInfo.ProcessWorkingSet / 1024 / 1024:N1} MB");
        Console.WriteLine($"  私有内存: {memInfo.ProcessPrivateMemory / 1024 / 1024:N1} MB");

        Console.WriteLine("\nGC信息:");
        Console.WriteLine($"  堆大小: {memInfo.GCHeapSize / 1024:N0} KB");
        if (memInfo.GCGenerationSizes != null)
        {
            for (int i = 0; i < memInfo.GCGenerationSizes.Count; i++)
            {
                Console.WriteLine($"  Gen{i}大小: {memInfo.GCGenerationSizes[i] / 1024:N0} KB");
            }
        }
        if (memInfo.GCCollectionCounts != null)
        {
            for (int i = 0; i < memInfo.GCCollectionCounts.Count; i++)
            {
                Console.WriteLine($"  Gen{i}回收次数: {memInfo.GCCollectionCounts[i]}");
            }
        }

        Console.WriteLine("\n? 内存监控完成\n");
    }

    /// <summary>
    /// 示例 3: 磁盘信息
    /// </summary>
    public static async Task DiskMonitoringAsync()
    {
        Console.WriteLine("=== PerformanceMonitor 磁盘监控示例 ===\n");

        var options = Options.Create(new PerformanceMonitorOptions
        {
            MonitorAllDisks = true
        });

        await using var monitor = new PerformanceMonitor(options);

        var diskInfo = monitor.GetDiskIO();

        Console.WriteLine($"发现 {diskInfo.Count} 个磁盘:\n");

        foreach (var disk in diskInfo)
        {
            Console.WriteLine($"磁盘: {disk.DriveName}");
            Console.WriteLine($"  总空间: {disk.TotalSpace / 1024 / 1024 / 1024:N1} GB");
            Console.WriteLine($"  已用空间: {disk.UsedSpace / 1024 / 1024 / 1024:N1} GB");
            Console.WriteLine($"  可用空间: {disk.AvailableSpace / 1024 / 1024 / 1024:N1} GB");
            Console.WriteLine($"  使用率: {disk.UsagePercentage:F1}%");
            Console.WriteLine();
        }

        Console.WriteLine("? 磁盘监控完成\n");
    }

    /// <summary>
    /// 示例 4: 网络监控
    /// </summary>
    public static async Task NetworkMonitoringAsync()
    {
        Console.WriteLine("=== PerformanceMonitor 网络监控示例 ===\n");

        var options = Options.Create(new PerformanceMonitorOptions
        {
            MonitorAllNetworkInterfaces = true
        });

        await using var monitor = new PerformanceMonitor(options);

        // 第一次获取（建立基准）
        var networkInfo1 = monitor.GetNetworkIO();
        await Task.Delay(1000);
        
        // 第二次获取（计算速率）
        var networkInfo2 = monitor.GetNetworkIO();

        Console.WriteLine($"发现 {networkInfo2.Count} 个网络接口:\n");

        foreach (var net in networkInfo2)
        {
            Console.WriteLine($"接口: {net.InterfaceName}");
            Console.WriteLine($"  发送: {net.BytesSent / 1024 / 1024:N1} MB");
            Console.WriteLine($"  接收: {net.BytesReceived / 1024 / 1024:N1} MB");
            Console.WriteLine($"  发送速率: {net.SendRate / 1024:N1} KB/s");
            Console.WriteLine($"  接收速率: {net.ReceiveRate / 1024:N1} KB/s");
            Console.WriteLine();
        }

        Console.WriteLine("? 网络监控完成\n");
    }

    /// <summary>
    /// 示例 5: GC统计
    /// </summary>
    public static async Task GCStatisticsAsync()
    {
        Console.WriteLine("=== PerformanceMonitor GC统计示例 ===\n");

        var options = Options.Create(new PerformanceMonitorOptions());
        await using var monitor = new PerformanceMonitor(options);

        // 制造一些GC压力
        Console.WriteLine("制造GC压力...");
        var lists = new List<byte[]>();
        for (int i = 0; i < 100; i++)
        {
            lists.Add(new byte[1024 * 100]); // 100KB
        }
        lists.Clear();
        
        // 强制GC
        monitor.ForceGC(blocking: true);

        // 获取GC统计
        var gcStats = monitor.GetGCStatistics();

        Console.WriteLine("\nGC统计:");
        Console.WriteLine($"  Gen0回收次数: {gcStats.Gen0Collections}");
        Console.WriteLine($"  Gen1回收次数: {gcStats.Gen1Collections}");
        Console.WriteLine($"  Gen2回收次数: {gcStats.Gen2Collections}");
        Console.WriteLine($"  总内存: {gcStats.TotalMemory / 1024:N0} KB");
        Console.WriteLine($"  堆大小: {gcStats.HeapSize / 1024:N0} KB");
        Console.WriteLine($"  碎片大小: {gcStats.FragmentedBytes / 1024:N0} KB");
        Console.WriteLine($"  暂停时间百分比: {gcStats.PauseTimePercentage:F2}%");
        Console.WriteLine($"  是否压缩: {gcStats.IsCompacting}");
        Console.WriteLine($"  是否并发: {gcStats.IsConcurrent}");

        Console.WriteLine("\n? GC统计完成\n");
    }

    /// <summary>
    /// 示例 6: 持续监控与告警
    /// </summary>
    public static async Task ContinuousMonitoringAsync()
    {
        Console.WriteLine("=== PerformanceMonitor 持续监控示例 ===\n");

        var options = Options.Create(new PerformanceMonitorOptions
        {
            Enabled = true,
            CollectionIntervalSeconds = 1,
            EnableAlerts = true,
            CpuWarningThreshold = 50,
            CpuCriticalThreshold = 80,
            MemoryWarningThreshold = 60,
            MemoryCriticalThreshold = 85
        });

        await using var monitor = new PerformanceMonitor(options);

        // 订阅事件
        monitor.DataCollected += (s, info) =>
        {
            Console.WriteLine($"[{info.Timestamp:HH:mm:ss}] CPU: {info.Cpu.ProcessUsage:F1}% | 内存: {info.Memory.PhysicalMemoryUsage:F1}%");
        };

        monitor.PerformanceAlert += (s, e) =>
        {
            var icon = e.Level == AlertLevel.Critical ? "??" : "??";
            Console.WriteLine($"\n{icon} 告警: {e.Message}");
        };

        // 启动监控
        Console.WriteLine("启动持续监控（5秒）...\n");
        await monitor.StartAsync();

        // 监控5秒
        await Task.Delay(5000);

        // 停止监控
        await monitor.StopAsync();

        Console.WriteLine("\n? 持续监控完成\n");
    }

    /// <summary>
    /// 示例 7: 使用依赖注入
    /// </summary>
    public static async Task DependencyInjectionExampleAsync()
    {
        Console.WriteLine("=== PerformanceMonitor 依赖注入示例 ===\n");

        var services = new ServiceCollection();
        services.AddPerformanceMonitor(options =>
        {
            options.Enabled = true;
            options.CollectionIntervalSeconds = 2;
            options.EnableAlerts = true;
            options.StartOnInitialization = false;
        });

        await using var serviceProvider = services.BuildServiceProvider();
        var monitor = serviceProvider.GetRequiredService<IPerformanceMonitor>();

        // 获取信息
        var info = await monitor.GetSystemInfoAsync();
        Console.WriteLine($"通过DI获取的监控服务:");
        Console.WriteLine($"  CPU: {info.Cpu.ProcessUsage:F1}%");
        Console.WriteLine($"  内存: {info.Memory.PhysicalMemoryUsage:F1}%");
        Console.WriteLine($"  线程数: {info.ThreadCount}");

        Console.WriteLine("\n? 依赖注入示例完成\n");
    }

    /// <summary>
    /// 运行所有示例
    /// </summary>
    public static async Task RunAllAsync()
    {
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║   PerformanceMonitor 使用示例演示      ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        await GetCurrentSystemInfoAsync();
        await MemoryMonitoringAsync();
        await DiskMonitoringAsync();
        await NetworkMonitoringAsync();
        await GCStatisticsAsync();
        await ContinuousMonitoringAsync();
        await DependencyInjectionExampleAsync();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("所有 PerformanceMonitor 示例执行完成！");
        Console.WriteLine("═══════════════════════════════════════════\n");
    }
}
