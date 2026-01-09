using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ToolHelper.LoggingDiagnostics.Abstractions;
using ToolHelper.LoggingDiagnostics.Alarm;
using ToolHelper.LoggingDiagnostics.Configuration;
using ToolHelper.LoggingDiagnostics.Extensions;

namespace ToolHelperTest.Examples.LoggingDiagnostics;

/// <summary>
/// AlarmHelper 使用示例
/// 演示报警记录、声音提示、报警历史管理功能
/// </summary>
public class AlarmHelperExample
{
    /// <summary>
    /// 示例 1: 基本报警操作
    /// </summary>
    public static async Task BasicAlarmOperationsAsync()
    {
        Console.WriteLine("=== AlarmHelper 基本报警操作示例 ===\n");

        var options = Options.Create(new AlarmOptions
        {
            EnableSound = true, // 演示时禁用声音
            PersistToFile = false
        });

        await using var alarmHelper = new AlarmHelper(options);

        // 订阅报警事件
        alarmHelper.AlarmTriggered += (s, e) =>
        {
            Console.WriteLine($"  ?? 新报警: [{e.Alarm.Level}] {e.Alarm.Code} - {e.Alarm.Message}");
        };

        alarmHelper.AlarmStatusChanged += (s, e) =>
        {
            Console.WriteLine($"  ?? 状态变更: {e.Alarm.Code} -> {e.EventType}");
        };

        // 触发报警
        var alarm1 = alarmHelper.Trigger("ALM001", "温度过高", AlarmLevel.Warning, "传感器1");
        var alarm2 = alarmHelper.Trigger("ALM002", "压力异常", AlarmLevel.Alarm, "传感器2");
        var alarm3 = alarmHelper.Trigger("ALM003", "设备故障", AlarmLevel.Critical, "设备A");

        Console.WriteLine($"\n当前活动报警数: {alarmHelper.ActiveAlarmCount}");

        // 确认报警
        Console.WriteLine("\n确认报警...");
        alarmHelper.Acknowledge(alarm1.Id, "张三", "已检查，正常范围");
        
        // 恢复报警
        Console.WriteLine("恢复报警...");
        alarmHelper.Recover(alarm2.Id);

        // 关闭报警
        Console.WriteLine("关闭报警...");
        alarmHelper.Close(alarm3.Id, "设备已维修");

        Console.WriteLine($"\n最终活动报警数: {alarmHelper.ActiveAlarmCount}");
        Console.WriteLine("\n? 基本报警操作完成\n");
    }

    /// <summary>
    /// 示例 2: 报警查询
    /// </summary>
    public static async Task AlarmQueryAsync()
    {
        Console.WriteLine("=== AlarmHelper 报警查询示例 ===\n");

        var options = Options.Create(new AlarmOptions
        {
            EnableSound = true,
            PersistToFile = false
        });

        await using var alarmHelper = new AlarmHelper(options);

        // 生成一些测试报警
        var sources = new[] { "传感器1", "传感器2", "设备A", "设备B" };
        var levels = new[] { AlarmLevel.Info, AlarmLevel.Warning, AlarmLevel.Alarm, AlarmLevel.Critical };
        
        for (int i = 0; i < 20; i++)
        {
            await alarmHelper.TriggerAsync(
                $"ALM{i:D3}",
                $"测试报警 #{i + 1}",
                levels[i % 4],
                sources[i % 4]);
            
            await Task.Delay(10); // 间隔生成
        }

        Console.WriteLine($"已生成 20 条测试报警\n");

        // 查询所有活动报警
        var activeAlarms = alarmHelper.GetActiveAlarms();
        Console.WriteLine($"活动报警数: {activeAlarms.Count}");

        // 按条件查询
        var query = new AlarmQuery
        {
            Level = AlarmLevel.Critical,
            PageSize = 10
        };

        var criticalAlarms = await alarmHelper.QueryAsync(query);
        Console.WriteLine($"严重报警数: {criticalAlarms.Count}");

        // 按来源查询
        var sourceQuery = new AlarmQuery
        {
            Source = "设备A"
        };

        var deviceAlarms = await alarmHelper.QueryAsync(sourceQuery);
        Console.WriteLine($"设备A报警数: {deviceAlarms.Count}");

        Console.WriteLine("\n? 报警查询完成\n");
    }

    /// <summary>
    /// 示例 3: 报警统计
    /// </summary>
    public static async Task AlarmStatisticsAsync()
    {
        Console.WriteLine("=== AlarmHelper 报警统计示例 ===\n");

        var options = Options.Create(new AlarmOptions
        {
            EnableSound = false,
            PersistToFile = false
        });

        await using var alarmHelper = new AlarmHelper(options);

        // 生成不同级别的报警
        for (int i = 0; i < 5; i++)
            alarmHelper.Trigger($"INFO{i}", "信息", AlarmLevel.Info, "系统");
        for (int i = 0; i < 8; i++)
            alarmHelper.Trigger($"WARN{i}", "警告", AlarmLevel.Warning, "应用");
        for (int i = 0; i < 3; i++)
            alarmHelper.Trigger($"ALARM{i}", "报警", AlarmLevel.Alarm, "设备");
        for (int i = 0; i < 2; i++)
            alarmHelper.Trigger($"CRIT{i}", "严重", AlarmLevel.Critical, "核心");

        // 确认部分报警
        var activeAlarms = alarmHelper.GetActiveAlarms();
        for (int i = 0; i < 5; i++)
        {
            alarmHelper.Acknowledge(activeAlarms[i].Id, "操作员");
        }

        // 恢复部分报警
        for (int i = 5; i < 8; i++)
        {
            alarmHelper.Recover(activeAlarms[i].Id);
        }

        // 获取统计
        var stats = alarmHelper.GetStatistics();

        Console.WriteLine("报警统计:");
        Console.WriteLine($"  总数: {stats.TotalCount}");
        Console.WriteLine($"  活动: {stats.ActiveCount}");
        Console.WriteLine($"  已确认: {stats.AcknowledgedCount}");
        Console.WriteLine($"  已恢复: {stats.RecoveredCount}");
        Console.WriteLine($"  已关闭: {stats.ClosedCount}");

        Console.WriteLine("\n按级别统计:");
        foreach (var kvp in stats.CountByLevel)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        Console.WriteLine("\n按来源统计:");
        foreach (var kvp in stats.CountBySource)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        Console.WriteLine("\n? 报警统计完成\n");
    }

    /// <summary>
    /// 示例 4: 批量操作
    /// </summary>
    public static async Task BatchOperationsAsync()
    {
        Console.WriteLine("=== AlarmHelper 批量操作示例 ===\n");

        var options = Options.Create(new AlarmOptions
        {
            EnableSound = false,
            PersistToFile = false,
            AllowDuplicateAlarms = true
        });

        await using var alarmHelper = new AlarmHelper(options);

        // 触发多个报警
        Console.WriteLine("触发10条报警...");
        for (int i = 0; i < 10; i++)
        {
            alarmHelper.Trigger($"BATCH{i}", $"批量报警 #{i + 1}", AlarmLevel.Warning, "批量测试");
        }

        Console.WriteLine($"活动报警数: {alarmHelper.ActiveAlarmCount}");

        // 批量确认
        Console.WriteLine("\n确认所有报警...");
        var count = alarmHelper.AcknowledgeAll("管理员");
        Console.WriteLine($"已确认 {count} 条报警");

        // 通过报警码恢复
        alarmHelper.Trigger("BATCH0", "重复报警", AlarmLevel.Alarm, "测试");
        alarmHelper.Trigger("BATCH0", "重复报警", AlarmLevel.Alarm, "测试");

        Console.WriteLine($"\n恢复报警码 BATCH0...");
        var recovered = alarmHelper.RecoverByCode("BATCH0");
        Console.WriteLine($"已恢复 {recovered} 条报警");

        Console.WriteLine("\n? 批量操作完成\n");
    }

    /// <summary>
    /// 示例 5: 报警附加数据
    /// </summary>
    public static async Task AlarmWithDataAsync()
    {
        Console.WriteLine("=== AlarmHelper 带附加数据的报警示例 ===\n");

        var options = Options.Create(new AlarmOptions
        {
            EnableSound = false,
            PersistToFile = false
        });

        await using var alarmHelper = new AlarmHelper(options);

        // 触发带数据的报警
        var alarm = await alarmHelper.TriggerAsync(
            "TEMP_HIGH",
            "温度超过阈值",
            AlarmLevel.Critical,
            "温控系统",
            new Dictionary<string, object>
            {
                ["CurrentTemp"] = 85.5,
                ["Threshold"] = 80.0,
                ["Unit"] = "°C",
                ["Location"] = "车间A-区域1",
                ["DeviceId"] = "TC-001",
                ["Timestamp"] = DateTime.Now
            });

        Console.WriteLine("报警详情:");
        Console.WriteLine($"  ID: {alarm.Id}");
        Console.WriteLine($"  代码: {alarm.Code}");
        Console.WriteLine($"  消息: {alarm.Message}");
        Console.WriteLine($"  级别: {alarm.Level}");
        Console.WriteLine($"  来源: {alarm.Source}");
        Console.WriteLine($"  时间: {alarm.TriggerTime}");
        Console.WriteLine("  附加数据:");
        if (alarm.Data != null)
        {
            foreach (var kvp in alarm.Data)
            {
                Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
            }
        }

        Console.WriteLine("\n? 带附加数据的报警完成\n");
    }

    /// <summary>
    /// 示例 6: 使用依赖注入
    /// </summary>
    public static async Task DependencyInjectionExampleAsync()
    {
        Console.WriteLine("=== AlarmHelper 依赖注入示例 ===\n");

        var services = new ServiceCollection();
        services.AddAlarmHelper(options =>
        {
            options.EnableSound = false;
            options.PersistToFile = false;
            options.MaxActiveAlarms = 1000;
        });

        await using var serviceProvider = services.BuildServiceProvider();
        var alarmHelper = serviceProvider.GetRequiredService<IAlarmHelper>();

        alarmHelper.AlarmTriggered += (s, e) =>
        {
            Console.WriteLine($"  [DI] 报警: {e.Alarm.Message}");
        };

        alarmHelper.Trigger("DI001", "依赖注入测试报警", AlarmLevel.Info);

        Console.WriteLine("\n? 依赖注入示例完成\n");
    }

    /// <summary>
    /// 运行所有示例
    /// </summary>
    public static async Task RunAllAsync()
    {
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║      AlarmHelper 使用示例演示          ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        await BasicAlarmOperationsAsync();
        await AlarmQueryAsync();
        await AlarmStatisticsAsync();
        await BatchOperationsAsync();
        await AlarmWithDataAsync();
        await DependencyInjectionExampleAsync();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("所有 AlarmHelper 示例执行完成！");
        Console.WriteLine("═══════════════════════════════════════════\n");
    }
}
