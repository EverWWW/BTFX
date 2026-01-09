# ToolHelper.LoggingDiagnostics

日志与诊断工具类库，为上位机软件开发提供全面的日志记录、调试追踪、错误管理、报警处理和性能监控功能。

## 特性

- ? **模块化设计**: 每个类独立，按需引用
- ? **接口抽象**: 所有功能通过接口定义，便于扩展和测试
- ? **依赖注入**: 支持 Microsoft.Extensions.DependencyInjection
- ? **异步优先**: 所有IO操作使用 async/await
- ? **配置驱动**: 通过配置文件或代码配置
- ? **性能优化**: 使用对象池、Channel减少GC压力
- ? **文档完善**: XML注释 + 示例代码

## 安装

```bash
# 项目引用
dotnet add reference ToolHelper.LoggingDiagnostics
```

## 快速开始

### 使用依赖注入（推荐）

```csharp
using Microsoft.Extensions.DependencyInjection;
using ToolHelper.LoggingDiagnostics.Extensions;

// 注册所有服务
services.AddLoggingDiagnostics(options =>
{
    // 日志配置
    options.LogOptions.MinimumLevel = LogLevel.Debug;
    options.LogOptions.LogDirectory = "logs";
    
    // 追踪配置
    options.TraceOptions.Enabled = true;
    options.TraceOptions.SlowOperationThresholdMs = 100;
    
    // 报警配置
    options.AlarmOptions.EnableSound = true;
    
    // 性能监控配置
    options.PerformanceMonitorOptions.CollectionIntervalSeconds = 5;
});

// 或者单独注册
services.AddLogHelper(options => { ... });
services.AddTraceHelper(options => { ... });
services.AddErrorCodeManager(options => { ... });
services.AddAlarmHelper(options => { ... });
services.AddPerformanceMonitor(options => { ... });
```

### 直接创建实例

```csharp
using Microsoft.Extensions.Options;
using ToolHelper.LoggingDiagnostics.Logging;
using ToolHelper.LoggingDiagnostics.Configuration;

var options = Options.Create(new LogOptions
{
    MinimumLevel = LogLevel.Debug,
    LogDirectory = "logs"
});

var logHelper = new LogHelper(options);
```

## 功能模块

### 1. LogHelper - 日志记录

提供分级、分文件、自动归档的日志功能。

```csharp
// 不同级别的日志
logHelper.Trace("跟踪信息");
logHelper.Debug("调试信息");
logHelper.Information("常规信息");
logHelper.Warning("警告信息");
logHelper.Error("错误信息", exception);
logHelper.Critical("严重错误", exception);

// 带属性的日志
logHelper.Information("用户登录", new Dictionary<string, object>
{
    ["UserId"] = 12345,
    ["IP"] = "192.168.1.100"
});

// 异步日志
await logHelper.LogAsync(new LogEntry
{
    Level = LogLevel.Information,
    Message = "异步日志消息"
});

// 创建带类别的日志器
var orderLogger = logHelper.ForCategory("OrderService");
orderLogger.Information("订单创建成功");

// 手动归档
await logHelper.ArchiveAsync();
```

**配置选项**:
| 选项 | 说明 | 默认值 |
|------|------|--------|
| MinimumLevel | 最低日志级别 | Information |
| LogDirectory | 日志目录 | logs |
| FileNameFormat | 文件名格式 | log_{date}.txt |
| MaxFileSizeMB | 单文件最大大小 | 10 |
| ArchiveAfterDays | 归档天数 | 7 |
| EnableAsyncWrite | 启用异步写入 | true |
| SeparateFileByLevel | 按级别分文件 | false |

### 2. TraceHelper - 调试追踪

提供调用栈分析、性能分析功能。

```csharp
// 使用追踪作用域
using (traceHelper.BeginTrace("数据库查询"))
{
    // 执行数据库查询
}

// 追踪方法执行
var result = traceHelper.Trace("计算", () => 
{
    return CalculateSomething();
});

// 追踪异步方法
var data = await traceHelper.TraceAsync("API调用", async ct =>
{
    return await httpClient.GetStringAsync(url, ct);
});

// 获取调用栈
var callStack = traceHelper.GetCallStackString();

// 获取性能统计
var stats = traceHelper.GetStatistics("数据库查询");
Console.WriteLine($"平均耗时: {stats.AverageDuration.TotalMilliseconds}ms");
```

**配置选项**:
| 选项 | 说明 | 默认值 |
|------|------|--------|
| Enabled | 启用追踪 | true |
| CaptureCallStackByDefault | 默认捕获调用栈 | false |
| SlowOperationThresholdMs | 慢操作阈值 | 1000 |
| TrackMemoryUsage | 跟踪内存使用 | false |
| MaxHistoryCount | 最大历史记录数 | 10000 |

### 3. ErrorCodeManager - 错误码管理

提供多语言错误描述、错误码注册和查询功能。

```csharp
// 注册错误码
errorManager.Register(new ErrorCodeInfo
{
    Code = "E1001",
    Category = "Database",
    Severity = ErrorSeverity.Error,
    DefaultMessage = "数据库连接失败",
    LocalizedMessages = new Dictionary<string, string>
    {
        ["zh-CN"] = "数据库连接失败: {0}",
        ["en-US"] = "Database connection failed: {0}"
    },
    SuggestedSolution = "检查数据库服务是否运行"
});

// 获取本地化消息
var message = errorManager.GetMessage("E1001", args: "超时");

// 切换语言
errorManager.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

// 创建错误结果
var error = errorManager.CreateError("E1001", 
    args: new[] { "连接超时" },
    context: new Dictionary<string, object> { ["Server"] = "db01" });

// 从文件加载
await errorManager.LoadFromFileAsync("error_codes.json");

// 导出到文件
await errorManager.ExportToFileAsync("error_codes_export.json");
```

**配置选项**:
| 选项 | 说明 | 默认值 |
|------|------|--------|
| DefaultCulture | 默认语言 | zh-CN |
| ConfigFilePath | 配置文件路径 | null |
| LoadOnStartup | 启动时加载 | true |
| AllowOverwrite | 允许覆盖 | false |

### 4. AlarmHelper - 报警管理

提供报警记录、声音提示、报警历史管理功能。

```csharp
// 触发报警
var alarm = alarmHelper.Trigger(
    "ALM001",
    "温度过高",
    AlarmLevel.Critical,
    "传感器1",
    new Dictionary<string, object>
    {
        ["CurrentTemp"] = 85.5,
        ["Threshold"] = 80.0
    });

// 确认报警
alarmHelper.Acknowledge(alarm.Id, "张三", "已处理");

// 恢复报警
alarmHelper.Recover(alarm.Id);

// 查询报警
var query = new AlarmQuery
{
    Level = AlarmLevel.Critical,
    StartTime = DateTime.Today
};
var alarms = await alarmHelper.QueryAsync(query);

// 获取统计
var stats = alarmHelper.GetStatistics();

// 事件订阅
alarmHelper.AlarmTriggered += (s, e) =>
{
    Console.WriteLine($"新报警: {e.Alarm.Message}");
};
```

**配置选项**:
| 选项 | 说明 | 默认值 |
|------|------|--------|
| EnableSound | 启用声音 | true |
| UseSystemBeep | 使用系统蜂鸣器 | true |
| LoopSound | 循环播放 | false |
| PersistToFile | 持久化到文件 | true |
| AlarmDirectory | 报警目录 | alarms |
| AutoAcknowledgeMinutes | 自动确认时间 | 0 |

### 5. PerformanceMonitor - 性能监控

提供CPU、内存、网络IO等系统性能监控功能。

```csharp
// 获取系统信息
var systemInfo = await monitor.GetSystemInfoAsync();
Console.WriteLine($"CPU: {systemInfo.Cpu.ProcessUsage:F1}%");
Console.WriteLine($"内存: {systemInfo.Memory.PhysicalMemoryUsage:F1}%");

// 获取内存详情
var memory = monitor.GetMemoryUsage();

// 获取磁盘信息
var disks = monitor.GetDiskIO();

// 获取网络信息
var network = monitor.GetNetworkIO();

// 启动持续监控
await monitor.StartAsync();

// GC统计
var gcStats = monitor.GetGCStatistics();

// 强制GC
monitor.ForceGC();

// 事件订阅
monitor.DataCollected += (s, info) =>
{
    Console.WriteLine($"CPU: {info.Cpu.ProcessUsage}%");
};

monitor.PerformanceAlert += (s, e) =>
{
    Console.WriteLine($"告警: {e.Message}");
};
```

**配置选项**:
| 选项 | 说明 | 默认值 |
|------|------|--------|
| Enabled | 启用监控 | true |
| CollectionIntervalSeconds | 采集间隔 | 5 |
| CpuWarningThreshold | CPU警告阈值 | 70 |
| CpuCriticalThreshold | CPU严重阈值 | 90 |
| MemoryWarningThreshold | 内存警告阈值 | 70 |
| MemoryCriticalThreshold | 内存严重阈值 | 90 |
| EnableAlerts | 启用告警 | true |
| StartOnInitialization | 初始化时启动 | false |

## 项目结构

```
ToolHelper.LoggingDiagnostics/
├── Abstractions/           # 接口定义
│   ├── ILogHelper.cs
│   ├── ITraceHelper.cs
│   ├── IErrorCodeManager.cs
│   ├── IAlarmHelper.cs
│   └── IPerformanceMonitor.cs
├── Configuration/          # 配置选项
│   └── LoggingDiagnosticsOptions.cs
├── Logging/               # 日志实现
│   └── LogHelper.cs
├── Tracing/               # 追踪实现
│   └── TraceHelper.cs
├── ErrorManagement/       # 错误码实现
│   └── ErrorCodeManager.cs
├── Alarm/                 # 报警实现
│   └── AlarmHelper.cs
├── Performance/           # 性能监控实现
│   └── PerformanceMonitor.cs
└── Extensions/            # DI扩展
    └── ServiceCollectionExtensions.cs
```

## 示例代码

完整示例请参考 `ToolHelperTest/Examples/LoggingDiagnostics/` 目录：

- `LogHelperExample.cs` - 日志记录示例
- `TraceHelperExample.cs` - 调试追踪示例
- `ErrorCodeManagerExample.cs` - 错误码管理示例
- `AlarmHelperExample.cs` - 报警管理示例
- `PerformanceMonitorExample.cs` - 性能监控示例
- `LoggingDiagnosticsDemoRunner.cs` - 示例运行器

## 最佳实践

### 1. 日志记录
- 使用适当的日志级别
- 生产环境建议设置为 Information 或更高
- 敏感信息不要记录到日志
- 定期清理和归档日志文件

### 2. 性能追踪
- 只在需要时启用调用栈捕获
- 设置合理的慢操作阈值
- 定期清理历史记录

### 3. 错误码管理
- 统一错误码格式
- 提供有意义的错误消息
- 支持多语言本地化

### 4. 报警管理
- 设置合理的报警级别
- 避免报警风暴
- 及时确认和处理报警

### 5. 性能监控
- 设置合理的采集间隔
- 配置适当的告警阈值
- 定期清理历史数据

## 依赖

- Microsoft.Extensions.DependencyInjection.Abstractions
- Microsoft.Extensions.Logging.Abstractions
- Microsoft.Extensions.Options
- Microsoft.Extensions.Configuration.Abstractions
- Microsoft.Extensions.ObjectPool
- System.Diagnostics.DiagnosticSource
- System.IO.Pipelines

## 许可证

MIT License

## 更新日志

### v1.0.0
- 初始版本
- 实现 LogHelper 日志记录
- 实现 TraceHelper 调试追踪
- 实现 ErrorCodeManager 错误码管理
- 实现 AlarmHelper 报警管理
- 实现 PerformanceMonitor 性能监控
