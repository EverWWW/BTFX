using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ToolHelper.LoggingDiagnostics.Abstractions;
using ToolHelper.LoggingDiagnostics.Configuration;
using ToolHelper.LoggingDiagnostics.Extensions;
using ToolHelper.LoggingDiagnostics.Logging;

namespace ToolHelperTest.Examples.LoggingDiagnostics;

/// <summary>
/// LogHelper 使用示例
/// 演示日志记录、分级输出、文件归档等功能
/// </summary>
public class LogHelperExample
{
    /// <summary>
    /// 示例 1: 基本日志记录
    /// </summary>
    public static async Task BasicLoggingAsync()
    {
        Console.WriteLine("=== LogHelper 基本日志记录示例 ===\n");

        // 直接创建实例
        var options = Options.Create(new LogOptions
        {
            MinimumLevel = LogLevel.Trace,
            LogDirectory = "logs",
            EnableConsoleOutput = true,
            UseColoredConsole = true
        });

        await using var logHelper = new LogHelper(options, "BasicExample");

        // 记录不同级别的日志
        logHelper.Trace("这是跟踪日志");
        logHelper.Debug("这是调试日志");
        logHelper.Information("这是信息日志");
        logHelper.Warning("这是警告日志");
        logHelper.Error("这是错误日志");
        logHelper.Critical("这是严重错误日志");

        Console.WriteLine("\n? 基本日志记录完成\n");
    }

    /// <summary>
    /// 示例 2: 带属性的日志记录
    /// </summary>
    public static async Task LoggingWithPropertiesAsync()
    {
        Console.WriteLine("=== LogHelper 带属性日志记录示例 ===\n");

        var options = Options.Create(new LogOptions
        {
            MinimumLevel = LogLevel.Information,
            EnableConsoleOutput = true
        });

        await using var logHelper = new LogHelper(options, "PropertiesExample");

        // 带附加属性的日志
        logHelper.Information("用户登录成功", new Dictionary<string, object>
        {
            ["UserId"] = 12345,
            ["Username"] = "张三",
            ["IP"] = "192.168.1.100",
            ["LoginTime"] = DateTime.Now
        });

        // 带异常的错误日志
        try
        {
            throw new InvalidOperationException("模拟的业务异常");
        }
        catch (Exception ex)
        {
            logHelper.Error("处理请求时发生错误", ex, new Dictionary<string, object>
            {
                ["RequestId"] = Guid.NewGuid(),
                ["Endpoint"] = "/api/users"
            });
        }

        Console.WriteLine("\n? 带属性日志记录完成\n");
    }

    /// <summary>
    /// 示例 3: 异步日志记录
    /// </summary>
    public static async Task AsyncLoggingAsync()
    {
        Console.WriteLine("=== LogHelper 异步日志记录示例 ===\n");

        var options = Options.Create(new LogOptions
        {
            MinimumLevel = LogLevel.Information,
            EnableAsyncWrite = true,
            BufferSize = 100,
            FlushIntervalMs = 500,
            EnableConsoleOutput = true
        });

        await using var logHelper = new LogHelper(options, "AsyncExample");

        // 批量异步记录日志
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            var entry = new LogEntry
            {
                Level = LogLevel.Information,
                Category = "AsyncExample",
                Message = $"异步日志消息 #{i + 1}"
            };

            tasks.Add(logHelper.LogAsync(entry).AsTask());
        }

        await Task.WhenAll(tasks);
        await logHelper.FlushAsync();

        Console.WriteLine("? 100条异步日志记录完成\n");
    }

    /// <summary>
    /// 示例 4: 使用依赖注入
    /// </summary>
    public static async Task DependencyInjectionExampleAsync()
    {
        Console.WriteLine("=== LogHelper 依赖注入示例 ===\n");

        // 配置服务
        var services = new ServiceCollection();
        services.AddLogHelper(options =>
        {
            options.MinimumLevel = LogLevel.Debug;
            options.LogDirectory = "logs/di-example";
            options.EnableConsoleOutput = true;
            options.MessageTemplate = "[{timestamp}] [{level}] {message}";
        });

        await using var serviceProvider = services.BuildServiceProvider();
        var logHelper = serviceProvider.GetRequiredService<ILogHelper>();

        logHelper.Information("通过依赖注入获取的日志服务");
        logHelper.Debug("调试信息");

        // 创建带类别的日志记录器
        var orderLogger = logHelper.ForCategory("OrderService");
        orderLogger.Information("订单创建成功: #12345");

        var paymentLogger = logHelper.ForCategory("PaymentService");
        paymentLogger.Information("支付处理完成: ?99.00");

        Console.WriteLine("\n? 依赖注入示例完成\n");
    }

    /// <summary>
    /// 示例 5: 日志归档
    /// </summary>
    public static async Task LogArchivingAsync()
    {
        Console.WriteLine("=== LogHelper 日志归档示例 ===\n");

        var options = Options.Create(new LogOptions
        {
            MinimumLevel = LogLevel.Information,
            LogDirectory = "logs/archive-example",
            ArchiveAfterDays = 0, // 立即归档（仅演示）
            ArchiveRetentionDays = 30,
            EnableConsoleOutput = true
        });

        await using var logHelper = new LogHelper(options);

        logHelper.Information("这条日志将被归档");
        await logHelper.FlushAsync();

        // 触发归档
        Console.WriteLine("执行归档操作...");
        await logHelper.ArchiveAsync();

        Console.WriteLine("? 日志归档完成\n");
    }

    /// <summary>
    /// 示例 6: 分级别分文件
    /// </summary>
    public static async Task SeparateFileByLevelAsync()
    {
        Console.WriteLine("=== LogHelper 分级别分文件示例 ===\n");

        var options = Options.Create(new LogOptions
        {
            MinimumLevel = LogLevel.Trace,
            LogDirectory = "logs/level-separate",
            SeparateFileByLevel = true,
            EnableConsoleOutput = true
        });

        await using var logHelper = new LogHelper(options);

        logHelper.Trace("跟踪信息 - 写入 log_xxx_Trace.txt");
        logHelper.Debug("调试信息 - 写入 log_xxx_Debug.txt");
        logHelper.Information("常规信息 - 写入 log_xxx_Information.txt");
        logHelper.Warning("警告信息 - 写入 log_xxx_Warning.txt");
        logHelper.Error("错误信息 - 写入 log_xxx_Error.txt");
        logHelper.Critical("严重错误 - 写入 log_xxx_Critical.txt");

        await logHelper.FlushAsync();

        Console.WriteLine("\n? 分级别文件日志完成\n");
    }

    /// <summary>
    /// 运行所有示例
    /// </summary>
    public static async Task RunAllAsync()
    {
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║      LogHelper 使用示例演示            ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        await BasicLoggingAsync();
        await LoggingWithPropertiesAsync();
        await AsyncLoggingAsync();
        await DependencyInjectionExampleAsync();
        await SeparateFileByLevelAsync();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("所有 LogHelper 示例执行完成！");
        Console.WriteLine("═══════════════════════════════════════════\n");
    }
}
