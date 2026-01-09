using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ToolHelper.LoggingDiagnostics.Abstractions;
using ToolHelper.LoggingDiagnostics.Alarm;
using ToolHelper.LoggingDiagnostics.Configuration;
using ToolHelper.LoggingDiagnostics.ErrorManagement;
using ToolHelper.LoggingDiagnostics.Logging;
using ToolHelper.LoggingDiagnostics.Performance;
using ToolHelper.LoggingDiagnostics.Tracing;

namespace ToolHelper.LoggingDiagnostics.Extensions;

/// <summary>
/// 日志诊断模块依赖注入扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加所有日志诊断服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddLoggingDiagnostics(options => {
    ///     options.Log.MinimumLevel = LogLevel.Debug;
    ///     options.Trace.Enabled = true;
    ///     options.Alarm.EnableSound = false;
    ///     options.Performance.CollectionIntervalSeconds = 10;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddLoggingDiagnostics(
        this IServiceCollection services,
        Action<LoggingDiagnosticsBuilder>? configure = null)
    {
        var builder = new LoggingDiagnosticsBuilder(services);
        configure?.Invoke(builder);

        // 注册所有服务
        services.AddLogHelper(options => 
        {
            var logOptions = builder.LogOptions;
            options.MinimumLevel = logOptions.MinimumLevel;
            options.LogDirectory = logOptions.LogDirectory;
            options.FileNameFormat = logOptions.FileNameFormat;
            options.DateFormat = logOptions.DateFormat;
            options.TimestampFormat = logOptions.TimestampFormat;
            options.MaxFileSizeMB = logOptions.MaxFileSizeMB;
            options.MaxRetainedFiles = logOptions.MaxRetainedFiles;
            options.ArchiveAfterDays = logOptions.ArchiveAfterDays;
            options.ArchiveRetentionDays = logOptions.ArchiveRetentionDays;
            options.EnableAsyncWrite = logOptions.EnableAsyncWrite;
            options.BufferSize = logOptions.BufferSize;
            options.FlushIntervalMs = logOptions.FlushIntervalMs;
            options.SeparateFileByLevel = logOptions.SeparateFileByLevel;
            options.IncludeCallerInfo = logOptions.IncludeCallerInfo;
            options.EnableConsoleOutput = logOptions.EnableConsoleOutput;
            options.UseColoredConsole = logOptions.UseColoredConsole;
            options.MessageTemplate = logOptions.MessageTemplate;
        });

        services.AddTraceHelper(options =>
        {
            var traceOptions = builder.TraceOptions;
            options.Enabled = traceOptions.Enabled;
            options.CaptureCallStackByDefault = traceOptions.CaptureCallStackByDefault;
            options.MaxHistoryCount = traceOptions.MaxHistoryCount;
            options.SlowOperationThresholdMs = traceOptions.SlowOperationThresholdMs;
            options.TrackMemoryUsage = traceOptions.TrackMemoryUsage;
            options.AutoLogResults = traceOptions.AutoLogResults;
            options.MinDurationForLoggingMs = traceOptions.MinDurationForLoggingMs;
            options.MaxStackDepth = traceOptions.MaxStackDepth;
        });

        services.AddErrorCodeManager(options =>
        {
            var errorOptions = builder.ErrorCodeOptions;
            options.DefaultCulture = errorOptions.DefaultCulture;
            options.ConfigFilePath = errorOptions.ConfigFilePath;
            options.LoadOnStartup = errorOptions.LoadOnStartup;
            options.UnknownErrorMessage = errorOptions.UnknownErrorMessage;
            options.AllowOverwrite = errorOptions.AllowOverwrite;
            options.ErrorCodePrefix = errorOptions.ErrorCodePrefix;
        });

        services.AddAlarmHelper(options =>
        {
            var alarmOptions = builder.AlarmOptions;
            options.EnableSound = alarmOptions.EnableSound;
            options.SoundFiles = alarmOptions.SoundFiles;
            options.UseSystemBeep = alarmOptions.UseSystemBeep;
            options.LoopSound = alarmOptions.LoopSound;
            options.SoundLoopIntervalSeconds = alarmOptions.SoundLoopIntervalSeconds;
            options.MaxActiveAlarms = alarmOptions.MaxActiveAlarms;
            options.HistoryRetentionDays = alarmOptions.HistoryRetentionDays;
            options.PersistToFile = alarmOptions.PersistToFile;
            options.AlarmDirectory = alarmOptions.AlarmDirectory;
            options.AutoAcknowledgeMinutes = alarmOptions.AutoAcknowledgeMinutes;
            options.AllowDuplicateAlarms = alarmOptions.AllowDuplicateAlarms;
        });

        services.AddPerformanceMonitor(options =>
        {
            var perfOptions = builder.PerformanceMonitorOptions;
            options.Enabled = perfOptions.Enabled;
            options.CollectionIntervalSeconds = perfOptions.CollectionIntervalSeconds;
            options.CpuWarningThreshold = perfOptions.CpuWarningThreshold;
            options.CpuCriticalThreshold = perfOptions.CpuCriticalThreshold;
            options.MemoryWarningThreshold = perfOptions.MemoryWarningThreshold;
            options.MemoryCriticalThreshold = perfOptions.MemoryCriticalThreshold;
            options.DiskWarningThreshold = perfOptions.DiskWarningThreshold;
            options.DiskCriticalThreshold = perfOptions.DiskCriticalThreshold;
            options.MonitorAllNetworkInterfaces = perfOptions.MonitorAllNetworkInterfaces;
            options.NetworkInterfaceNames = perfOptions.NetworkInterfaceNames;
            options.MonitorAllDisks = perfOptions.MonitorAllDisks;
            options.DiskNames = perfOptions.DiskNames;
            options.HistoryRetentionHours = perfOptions.HistoryRetentionHours;
            options.MaxHistoryRecords = perfOptions.MaxHistoryRecords;
            options.EnableAlerts = perfOptions.EnableAlerts;
            options.AlertCooldownSeconds = perfOptions.AlertCooldownSeconds;
            options.StartOnInitialization = perfOptions.StartOnInitialization;
        });

        return services;
    }

    /// <summary>
    /// 添加日志帮助类服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddLogHelper(
        this IServiceCollection services,
        Action<LogOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<LogOptions>(options => { });
        }

        services.TryAddSingleton<ILogHelper, LogHelper>();

        return services;
    }

    /// <summary>
    /// 添加追踪帮助类服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddTraceHelper(
        this IServiceCollection services,
        Action<TraceOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<TraceOptions>(options => { });
        }

        services.TryAddSingleton<ITraceHelper, TraceHelper>();

        return services;
    }

    /// <summary>
    /// 添加错误码管理服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddErrorCodeManager(
        this IServiceCollection services,
        Action<ErrorCodeOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<ErrorCodeOptions>(options => { });
        }

        services.TryAddSingleton<IErrorCodeManager, ErrorCodeManager>();

        return services;
    }

    /// <summary>
    /// 添加报警帮助类服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAlarmHelper(
        this IServiceCollection services,
        Action<AlarmOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<AlarmOptions>(options => { });
        }

        services.TryAddSingleton<IAlarmHelper, AlarmHelper>();

        return services;
    }

    /// <summary>
    /// 添加性能监控服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddPerformanceMonitor(
        this IServiceCollection services,
        Action<PerformanceMonitorOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<PerformanceMonitorOptions>(options => { });
        }

        services.TryAddSingleton<IPerformanceMonitor, PerformanceMonitor>();

        return services;
    }
}

/// <summary>
/// 日志诊断配置构建器
/// </summary>
public class LoggingDiagnosticsBuilder
{
    private readonly IServiceCollection _services;

    /// <summary>
    /// 日志配置选项
    /// </summary>
    public LogOptions LogOptions { get; } = new();

    /// <summary>
    /// 追踪配置选项
    /// </summary>
    public TraceOptions TraceOptions { get; } = new();

    /// <summary>
    /// 错误码配置选项
    /// </summary>
    public ErrorCodeOptions ErrorCodeOptions { get; } = new();

    /// <summary>
    /// 报警配置选项
    /// </summary>
    public AlarmOptions AlarmOptions { get; } = new();

    /// <summary>
    /// 性能监控配置选项
    /// </summary>
    public PerformanceMonitorOptions PerformanceMonitorOptions { get; } = new();

    internal LoggingDiagnosticsBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// 配置日志选项
    /// </summary>
    /// <param name="configure">配置委托</param>
    /// <returns>构建器</returns>
    public LoggingDiagnosticsBuilder ConfigureLog(Action<LogOptions> configure)
    {
        configure(LogOptions);
        return this;
    }

    /// <summary>
    /// 配置追踪选项
    /// </summary>
    /// <param name="configure">配置委托</param>
    /// <returns>构建器</returns>
    public LoggingDiagnosticsBuilder ConfigureTrace(Action<TraceOptions> configure)
    {
        configure(TraceOptions);
        return this;
    }

    /// <summary>
    /// 配置错误码选项
    /// </summary>
    /// <param name="configure">配置委托</param>
    /// <returns>构建器</returns>
    public LoggingDiagnosticsBuilder ConfigureErrorCode(Action<ErrorCodeOptions> configure)
    {
        configure(ErrorCodeOptions);
        return this;
    }

    /// <summary>
    /// 配置报警选项
    /// </summary>
    /// <param name="configure">配置委托</param>
    /// <returns>构建器</returns>
    public LoggingDiagnosticsBuilder ConfigureAlarm(Action<AlarmOptions> configure)
    {
        configure(AlarmOptions);
        return this;
    }

    /// <summary>
    /// 配置性能监控选项
    /// </summary>
    /// <param name="configure">配置委托</param>
    /// <returns>构建器</returns>
    public LoggingDiagnosticsBuilder ConfigurePerformance(Action<PerformanceMonitorOptions> configure)
    {
        configure(PerformanceMonitorOptions);
        return this;
    }
}
