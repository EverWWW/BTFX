using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToolHelper.LoggingDiagnostics.Configuration;
using ToolHelper.LoggingDiagnostics.Extensions;
using ToolHelper.LoggingDiagnostics.Logging;
using MSLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace ToolHelperTest.Examples.LoggingDiagnostics;

/// <summary>
/// 日志导出助手示例
/// 演示日志导出、清理、统计等功能
/// </summary>
public class LogExportHelperExample
{
    private static readonly string TestLogDirectory = "test_logs";
    private static readonly string ExportDirectory = "log_exports";

    /// <summary>
    /// 运行所有日志导出示例
    /// </summary>
    public static async Task RunAllExamplesAsync()
    {
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║    日志导出助手示例                  ║");
        Console.WriteLine("╚══════════════════════════════════════╝\n");

        try
        {
            // 准备测试环境
            await PrepareTestEnvironmentAsync();

            // 示例1: 导出日志到文本文件
            await ExportLogsToTextAsync();

            // 示例2: 导出日志到CSV文件
            await ExportLogsToCsvAsync();

            // 示例3: 获取日志统计信息
            await GetLogStatisticsAsync();

            // 示例4: 获取日志目录信息
            GetDirectoryInfo();

            // 示例5: 使用依赖注入
            await DependencyInjectionExampleAsync();
        }
        finally
        {
            // 清理测试环境
            CleanupTestEnvironment();
        }
    }

    /// <summary>
    /// 准备测试环境（创建模拟日志文件）
    /// </summary>
    private static async Task PrepareTestEnvironmentAsync()
    {
        Console.WriteLine("?? 准备测试环境...\n");

        Directory.CreateDirectory(TestLogDirectory);
        Directory.CreateDirectory(ExportDirectory);

        // 创建模拟日志文件
        var logContent = new[]
        {
            "[2025-01-10 09:00:00] [INFO] [System] 应用程序启动",
            "[2025-01-10 09:00:01] [DEBUG] [Database] 数据库连接成功",
            "[2025-01-10 09:00:02] [INFO] [Auth] 用户 admin 登录成功",
            "[2025-01-10 09:00:05] [WARNING] [Memory] 内存使用率达到 75%",
            "[2025-01-10 09:00:10] [ERROR] [Network] 网络连接超时",
            "[2025-01-10 09:00:15] [INFO] [Task] 任务执行完成",
            "[2025-01-10 09:00:20] [DEBUG] [Cache] 缓存刷新",
            "[2025-01-10 09:00:25] [WARNING] [Disk] 磁盘空间不足 20%",
            "[2025-01-10 09:00:30] [ERROR] [File] 文件读取失败: config.json",
            "[2025-01-10 09:00:35] [CRITICAL] [Security] 检测到可疑登录行为"
        };

        var logFile1 = Path.Combine(TestLogDirectory, "app_20250110.txt");
        await File.WriteAllLinesAsync(logFile1, logContent);

        var logContent2 = new[]
        {
            "[2025-01-11 10:00:00] [INFO] [System] 系统正常运行",
            "[2025-01-11 10:00:05] [INFO] [Report] 报告生成完成",
            "[2025-01-11 10:00:10] [DEBUG] [API] API调用成功: /api/users",
            "[2025-01-11 10:00:15] [WARNING] [Performance] 响应时间过长: 3.5s",
            "[2025-01-11 10:00:20] [INFO] [Backup] 数据备份完成"
        };

        var logFile2 = Path.Combine(TestLogDirectory, "app_20250111.txt");
        await File.WriteAllLinesAsync(logFile2, logContent2);

        Console.WriteLine("? 测试环境准备完成\n");
    }

    /// <summary>
    /// 示例1: 导出日志到文本文件
    /// </summary>
    private static async Task ExportLogsToTextAsync()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("示例1: 导出日志到文本文件");
        Console.WriteLine("═══════════════════════════════════════\n");

        var exportHelper = new LogExportHelper(TestLogDirectory);
        var outputPath = Path.Combine(ExportDirectory, "logs_all.txt");

        var count = await exportHelper.ExportLogsAsync(
            outputPath: outputPath,
            startDate: DateTime.Today.AddDays(-30),
            endDate: DateTime.Today.AddDays(1));

        Console.WriteLine($"? 导出完成!");
        Console.WriteLine($"   输出文件: {outputPath}");
        Console.WriteLine($"   导出条数: {count}\n");
    }

    /// <summary>
    /// 示例2: 导出日志到CSV文件
    /// </summary>
    private static async Task ExportLogsToCsvAsync()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("示例2: 导出日志到CSV文件");
        Console.WriteLine("═══════════════════════════════════════\n");

        var exportHelper = new LogExportHelper(TestLogDirectory);
        var outputPath = Path.Combine(ExportDirectory, "logs_export.csv");

        var count = await exportHelper.ExportLogsToCsvAsync(
            outputPath: outputPath,
            startDate: DateTime.Today.AddDays(-30),
            endDate: DateTime.Today.AddDays(1));

        Console.WriteLine($"? CSV导出完成!");
        Console.WriteLine($"   输出文件: {outputPath}");
        Console.WriteLine($"   导出条数: {count}\n");
    }

    /// <summary>
    /// 示例3: 获取日志统计信息
    /// </summary>
    private static async Task GetLogStatisticsAsync()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("示例3: 获取日志统计信息");
        Console.WriteLine("═══════════════════════════════════════\n");

        var exportHelper = new LogExportHelper(TestLogDirectory);

        var stats = await exportHelper.GetStatisticsAsync(
            startDate: DateTime.Today.AddDays(-30),
            endDate: DateTime.Today.AddDays(1));

        Console.WriteLine("?? 日志统计信息:");
        Console.WriteLine($"   统计范围: {stats.StartDate:yyyy-MM-dd} - {stats.EndDate:yyyy-MM-dd}");
        Console.WriteLine($"   日志文件数: {stats.FileCount}");
        Console.WriteLine($"   总日志条数: {stats.TotalCount}");
        Console.WriteLine($"   总文件大小: {stats.TotalSizeFormatted}");
        Console.WriteLine();
        Console.WriteLine("   按级别统计:");
        Console.WriteLine($"      Trace:       {stats.TraceCount}");
        Console.WriteLine($"      Debug:       {stats.DebugCount}");
        Console.WriteLine($"      Information: {stats.InformationCount}");
        Console.WriteLine($"      Warning:     {stats.WarningCount}");
        Console.WriteLine($"      Error:       {stats.ErrorCount}");
        Console.WriteLine($"      Critical:    {stats.CriticalCount}");
        Console.WriteLine();
    }

    /// <summary>
    /// 示例4: 获取日志目录信息
    /// </summary>
    private static void GetDirectoryInfo()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("示例4: 获取日志目录信息");
        Console.WriteLine("═══════════════════════════════════════\n");

        var exportHelper = new LogExportHelper(TestLogDirectory);
        var info = exportHelper.GetDirectoryInfo();

        Console.WriteLine("?? 日志目录信息:");
        Console.WriteLine($"   目录路径: {info.DirectoryPath}");
        Console.WriteLine($"   目录存在: {info.Exists}");
        Console.WriteLine($"   文件数量: {info.FileCount}");
        Console.WriteLine($"   总大小: {info.TotalSizeFormatted}");
        if (info.OldestFile.HasValue)
            Console.WriteLine($"   最旧文件: {info.OldestFile:yyyy-MM-dd HH:mm}");
        if (info.NewestFile.HasValue)
            Console.WriteLine($"   最新文件: {info.NewestFile:yyyy-MM-dd HH:mm}");
        Console.WriteLine();
    }

    /// <summary>
    /// 示例5: 使用依赖注入
    /// </summary>
    private static async Task DependencyInjectionExampleAsync()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("示例5: 使用依赖注入");
        Console.WriteLine("═══════════════════════════════════════\n");

        var services = new ServiceCollection();

        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(MSLogLevel.Information));

        services.AddLogExportHelper(options =>
        {
            options.LogDirectory = TestLogDirectory;
        });

        var serviceProvider = services.BuildServiceProvider();
        var exportHelper = serviceProvider.GetRequiredService<LogExportHelper>();

        var info = exportHelper.GetDirectoryInfo();

        Console.WriteLine($"? 通过依赖注入使用LogExportHelper成功!");
        Console.WriteLine($"   日志目录: {info.DirectoryPath}");
        Console.WriteLine($"   文件数量: {info.FileCount}\n");

        await Task.CompletedTask;
    }

    /// <summary>
    /// 清理测试环境
    /// </summary>
    private static void CleanupTestEnvironment()
    {
        Console.WriteLine("?? 清理测试环境...");

        try
        {
            if (Directory.Exists(TestLogDirectory))
            {
                Directory.Delete(TestLogDirectory, true);
            }
            if (Directory.Exists(ExportDirectory))
            {
                Directory.Delete(ExportDirectory, true);
            }
            Console.WriteLine("? 清理完成\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"?? 清理失败: {ex.Message}\n");
        }
    }
}
