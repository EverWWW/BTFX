using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ToolHelper.LoggingDiagnostics.Abstractions;
using ToolHelper.LoggingDiagnostics.Extensions;
using ToolHelper.LoggingDiagnostics.Tracing;
using TraceOptions = ToolHelper.LoggingDiagnostics.Configuration.TraceOptions;

namespace ToolHelperTest.Examples.LoggingDiagnostics;

/// <summary>
/// TraceHelper 使用示例
/// 演示调用栈追踪、性能分析、方法计时等功能
/// </summary>
public class TraceHelperExample
{
    /// <summary>
    /// 示例 1: 基本追踪
    /// </summary>
    public static void BasicTracing()
    {
        Console.WriteLine("=== TraceHelper 基本追踪示例 ===\n");

        var options = Options.Create(new TraceOptions
        {
            Enabled = true,
            AutoLogResults = true,
            MinDurationForLoggingMs = 0
        });

        var traceHelper = new TraceHelper(options);

        // 订阅追踪完成事件
        traceHelper.TraceCompleted += (s, result) =>
        {
            Console.WriteLine($"  追踪完成: {result.OperationName} - {result.Duration.TotalMilliseconds:F2}ms");
        };

        // 使用追踪作用域
        using (traceHelper.BeginTrace("数据库查询"))
        {
            // 模拟数据库查询
            Thread.Sleep(100);
        }

        // 追踪方法执行
        var result = traceHelper.Trace("计算操作", () =>
        {
            Thread.Sleep(50);
            return 42;
        });

        Console.WriteLine($"\n计算结果: {result}");
        Console.WriteLine("\n? 基本追踪完成\n");
    }

    /// <summary>
    /// 示例 2: 异步方法追踪
    /// </summary>
    public static async Task AsyncTracingAsync()
    {
        Console.WriteLine("=== TraceHelper 异步追踪示例 ===\n");

        var options = Options.Create(new TraceOptions
        {
            Enabled = true,
            TrackMemoryUsage = true
        });

        var traceHelper = new TraceHelper(options);

        traceHelper.TraceCompleted += (s, result) =>
        {
            Console.WriteLine($"  操作: {result.OperationName}");
            Console.WriteLine($"  耗时: {result.Duration.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  内存变化: {result.MemoryDelta / 1024:N0} KB");
            Console.WriteLine();
        };

        // 追踪异步HTTP请求（模拟）
        var response = await traceHelper.TraceAsync("HTTP请求", async ct =>
        {
            await Task.Delay(150, ct);
            return "Response Data";
        });

        Console.WriteLine($"响应: {response}");

        // 追踪异步文件操作（模拟）
        await traceHelper.TraceAsync("文件写入", async ct =>
        {
            await Task.Delay(80, ct);
        });

        Console.WriteLine("? 异步追踪完成\n");
    }

    /// <summary>
    /// 示例 3: 获取调用栈
    /// </summary>
    public static void CallStackExample()
    {
        Console.WriteLine("=== TraceHelper 调用栈示例 ===\n");

        var options = Options.Create(new TraceOptions
        {
            MaxStackDepth = 10
        });

        var traceHelper = new TraceHelper(options);

        // 嵌套调用演示
        OuterMethod(traceHelper);

        Console.WriteLine("? 调用栈示例完成\n");
    }

    private static void OuterMethod(TraceHelper traceHelper)
    {
        MiddleMethod(traceHelper);
    }

    private static void MiddleMethod(TraceHelper traceHelper)
    {
        InnerMethod(traceHelper);
    }

    private static void InnerMethod(TraceHelper traceHelper)
    {
        Console.WriteLine("当前调用栈:");
        Console.WriteLine(traceHelper.GetCallStackString(0));
        Console.WriteLine();

        // 获取结构化的调用栈信息
        var frames = traceHelper.GetCallStack(0);
        Console.WriteLine($"调用栈深度: {frames.Count}");
        Console.WriteLine($"当前方法: {frames.FirstOrDefault()?.MethodName}");
        Console.WriteLine();
    }

    /// <summary>
    /// 示例 4: 性能统计
    /// </summary>
    public static async Task PerformanceStatisticsAsync()
    {
        Console.WriteLine("=== TraceHelper 性能统计示例 ===\n");

        var options = Options.Create(new TraceOptions
        {
            Enabled = true,
            SlowOperationThresholdMs = 100
        });

        var traceHelper = new TraceHelper(options);

        // 模拟多次数据库查询
        var random = new Random();
        for (int i = 0; i < 20; i++)
        {
            await traceHelper.TraceAsync("数据库查询", async ct =>
            {
                await Task.Delay(random.Next(20, 150), ct);
            });
        }

        // 获取统计信息
        var stats = traceHelper.GetStatistics("数据库查询");

        Console.WriteLine("数据库查询统计:");
        Console.WriteLine($"  调用次数: {stats.CallCount}");
        Console.WriteLine($"  成功次数: {stats.SuccessCount}");
        Console.WriteLine($"  失败次数: {stats.FailureCount}");
        Console.WriteLine($"  平均耗时: {stats.AverageDuration.TotalMilliseconds:F2}ms");
        Console.WriteLine($"  最小耗时: {stats.MinDuration.TotalMilliseconds:F2}ms");
        Console.WriteLine($"  最大耗时: {stats.MaxDuration.TotalMilliseconds:F2}ms");
        Console.WriteLine($"  总耗时: {stats.TotalDuration.TotalMilliseconds:F2}ms");

        Console.WriteLine("\n? 性能统计完成\n");
    }

    /// <summary>
    /// 示例 5: 带数据的追踪
    /// </summary>
    public static void TracingWithData()
    {
        Console.WriteLine("=== TraceHelper 带数据追踪示例 ===\n");

        var options = Options.Create(new TraceOptions { Enabled = true });
        var traceHelper = new TraceHelper(options);

        traceHelper.TraceCompleted += (s, result) =>
        {
            Console.WriteLine($"操作: {result.OperationName}");
            Console.WriteLine($"耗时: {result.Duration.TotalMilliseconds:F2}ms");
            Console.WriteLine($"成功: {result.IsSuccess}");
            
            if (result.Data != null)
            {
                Console.WriteLine("附加数据:");
                foreach (var kvp in result.Data)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
            Console.WriteLine();
        };

        // 追踪订单处理
        using (var scope = traceHelper.BeginTrace("处理订单"))
        {
            scope.SetData("OrderId", "ORD-2024-001");
            scope.SetData("CustomerId", 12345);
            
            // 模拟处理
            Thread.Sleep(100);
            
            scope.SetData("ItemCount", 5);
            scope.SetData("TotalAmount", 299.99m);
        }

        Console.WriteLine("? 带数据追踪完成\n");
    }

    /// <summary>
    /// 示例 6: 使用依赖注入
    /// </summary>
    public static async Task DependencyInjectionExampleAsync()
    {
        Console.WriteLine("=== TraceHelper 依赖注入示例 ===\n");

        var services = new ServiceCollection();
        services.AddTraceHelper(options =>
        {
            options.Enabled = true;
            options.SlowOperationThresholdMs = 50;
            options.AutoLogResults = true;
        });

        await using var serviceProvider = services.BuildServiceProvider();
        var traceHelper = serviceProvider.GetRequiredService<ITraceHelper>();

        // 使用自动命名追踪
        using (traceHelper.BeginTraceAuto())
        {
            await Task.Delay(60);
        }

        Console.WriteLine("? 依赖注入示例完成\n");
    }

    /// <summary>
    /// 运行所有示例
    /// </summary>
    public static async Task RunAllAsync()
    {
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║      TraceHelper 使用示例演示          ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        BasicTracing();
        await AsyncTracingAsync();
        CallStackExample();
        await PerformanceStatisticsAsync();
        TracingWithData();
        await DependencyInjectionExampleAsync();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("所有 TraceHelper 示例执行完成！");
        Console.WriteLine("═══════════════════════════════════════════\n");
    }
}
