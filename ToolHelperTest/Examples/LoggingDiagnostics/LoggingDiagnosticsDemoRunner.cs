namespace ToolHelperTest.Examples.LoggingDiagnostics;

/// <summary>
/// 日志诊断模块示例运行器
/// 提供统一的入口运行所有日志诊断示例
/// </summary>
public class LoggingDiagnosticsDemoRunner
{
    /// <summary>
    /// 运行所有日志诊断示例
    /// </summary>
    public static async Task RunAllExamplesAsync()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║    ToolHelper.LoggingDiagnostics 示例演示        ║");
        Console.WriteLine("║    日志与诊断工具类库 - 功能展示                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝\n");

        var examples = new (string Name, Func<Task> Runner)[]
        {
            ("LogHelper - 日志记录", LogHelperExample.RunAllAsync),
            ("TraceHelper - 调试追踪", TraceHelperExample.RunAllAsync),
            ("ErrorCodeManager - 错误码管理", ErrorCodeManagerExample.RunAllAsync),
            ("AlarmHelper - 报警管理", AlarmHelperExample.RunAllAsync),
            ("PerformanceMonitor - 性能监控", PerformanceMonitorExample.RunAllAsync)
        };

        Console.WriteLine("可用示例:");
        for (int i = 0; i < examples.Length; i++)
        {
            Console.WriteLine($"  {i + 1}. {examples[i].Name}");
        }
        Console.WriteLine($"  {examples.Length + 1}. 运行所有示例");
        Console.WriteLine($"  0. 退出");
        Console.WriteLine();

        while (true)
        {
            Console.Write("请选择要运行的示例 (输入数字): ");
            var input = Console.ReadLine();

            if (!int.TryParse(input, out var choice))
            {
                Console.WriteLine("无效输入，请输入数字。\n");
                continue;
            }

            if (choice == 0)
            {
                Console.WriteLine("\n再见！");
                break;
            }

            if (choice == examples.Length + 1)
            {
                // 运行所有示例
                foreach (var example in examples)
                {
                    Console.WriteLine($"\n{'═'.ToString().PadLeft(50, '═')}");
                    Console.WriteLine($"运行: {example.Name}");
                    Console.WriteLine($"{'═'.ToString().PadLeft(50, '═')}\n");

                    try
                    {
                        await example.Runner();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"? 示例执行出错: {ex.Message}");
                    }

                    Console.WriteLine("\n按任意键继续下一个示例...");
                    Console.ReadKey(true);
                }

                Console.WriteLine("\n所有示例执行完成！\n");
            }
            else if (choice >= 1 && choice <= examples.Length)
            {
                var example = examples[choice - 1];
                Console.WriteLine($"\n运行: {example.Name}\n");

                try
                {
                    await example.Runner();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? 示例执行出错: {ex.Message}");
                    Console.WriteLine($"   {ex.StackTrace}");
                }
            }
            else
            {
                Console.WriteLine("无效选择，请重新输入。\n");
            }
        }
    }

    /// <summary>
    /// 快速演示所有功能
    /// </summary>
    public static async Task QuickDemoAsync()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║    ToolHelper.LoggingDiagnostics 快速演示        ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝\n");

        // LogHelper 快速演示
        Console.WriteLine("【LogHelper 日志记录】");
        await LogHelperExample.BasicLoggingAsync();

        // TraceHelper 快速演示
        Console.WriteLine("【TraceHelper 调试追踪】");
        TraceHelperExample.BasicTracing();

        // ErrorCodeManager 快速演示
        Console.WriteLine("【ErrorCodeManager 错误码管理】");
        ErrorCodeManagerExample.BasicErrorCodeUsage();

        // AlarmHelper 快速演示
        Console.WriteLine("【AlarmHelper 报警管理】");
        await AlarmHelperExample.BasicAlarmOperationsAsync();

        // PerformanceMonitor 快速演示
        Console.WriteLine("【PerformanceMonitor 性能监控】");
        await PerformanceMonitorExample.GetCurrentSystemInfoAsync();

        Console.WriteLine("\n快速演示完成！");
    }
}
