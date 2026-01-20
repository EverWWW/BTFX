using System.IO;
using System.Windows;
using System.Windows.Threading;
using BTFX.Common;
using BTFX.Data;
using BTFX.Services.Implementations;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ToolHelper.LoggingDiagnostics.Abstractions;
using ToolHelper.LoggingDiagnostics.Configuration;
using ToolHelper.LoggingDiagnostics.Logging;

namespace BTFX;

/// <summary>
/// App.xaml 的交互逻辑
/// </summary>
public partial class App : Application
{
    private static Mutex? _mutex;
    private static ILogHelper? _logHelper;
    private static volatile bool _isShuttingDown;

    /// <summary>
    /// 应用程序是否正在关闭
    /// </summary>
    public static bool IsShuttingDown => _isShuttingDown;

    /// <summary>
    /// 服务提供者
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// 应用程序启动
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. 单实例检测
        if (!CheckSingleInstance())
        {
            MessageBox.Show(
                FindResource("ProgramAlreadyRunning")?.ToString() ?? "程序已运行，请勿重复启动。",
                Constants.APP_DISPLAY_NAME,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // 2. 注册全局异常处理
        RegisterExceptionHandlers();

        // 3. 初始化目录结构
        InitializeDirectories();

        // 4. 初始化日志框架
        InitializeLogging();

        // 5. 初始化数据库
        InitializeDatabase();

        // 6. 配置依赖注入
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // 7. 加载配置
        var settingsService = Services.GetRequiredService<ISettingsService>();
        settingsService.LoadSettings();

        // 8. 应用主题
        var themeService = Services.GetRequiredService<IThemeService>();
        themeService.ApplyTheme(settingsService.CurrentSettings.Application.Theme);

        // 9. 应用语言
        var localizationService = Services.GetRequiredService<ILocalizationService>();
        localizationService.ApplyLanguage(settingsService.CurrentSettings.Application.Language);

        // 10. 显示主窗口
        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // 11. 注册视图映射
        var navigationService = Services.GetRequiredService<INavigationService>() as NavigationService;
        if (navigationService != null)
        {
            navigationService.RegisterView<LoginViewModel, Views.LoginView>();
            navigationService.RegisterView<PatientSelectionViewModel, Views.PatientSelectionView>();
            navigationService.RegisterView<MainContainerViewModel, Views.MainContainerView>();
            navigationService.RegisterView<MeasurementViewModel, Views.MeasurementView>();
            navigationService.RegisterView<DataManagementViewModel, Views.DataManagementView>();
            // TODO: Register other sub-views (Report, Settings)

            // Navigate to login view
            navigationService.NavigateTo<LoginViewModel>();
        }
    }

    /// <summary>
    /// 应用程序退出
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        // 立即设置关闭标志，这是最高优先级
        _isShuttingDown = true;

        try
        {
            // 处理 Dispatcher 中的待处理消息，防止关闭时触发新的 UI 操作
            try
            {
                // 处理所有待处理的 Dispatcher 消息
                if (Current?.Dispatcher != null && !Current.Dispatcher.HasShutdownStarted)
                {
                    Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
            }
            catch { }

            // 等待异步操作检测到关闭标志
            Thread.Sleep(100);

            // 记录关闭日志
            try
            {
                _logHelper?.Information($"{Constants.APP_DISPLAY_NAME} 正在关闭");
            }
            catch { }

            // 保存配置
            try
            {
                var settingsService = Services?.GetService<ISettingsService>();
                settingsService?.SaveSettings();
            }
            catch { }

            // 释放日志资源（设置超时避免卡住）
            try
            {
                var flushTask = _logHelper?.FlushAsync();
                flushTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch { }

            // 释放 Mutex
            try
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
                _mutex = null;
            }
            catch { }

            // 释放服务提供者 - 这会触发所有 IDisposable 服务的 Dispose
            // 注意：这可能会触发 ViewModel 的属性变更
            try
            {
                if (Services is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnExit error: {ex.Message}");
        }

        // 调用基类的 OnExit
        base.OnExit(e);

        // 不使用 Environment.Exit(0)，让应用程序自然退出
        // 如果应用仍然无法退出，可能是因为有后台线程在运行
        // 在这种情况下，可以考虑使用延迟的强制退出
        Task.Run(async () =>
        {
            await Task.Delay(2000); // 等待 2 秒
            if (!Environment.HasShutdownStarted)
            {
                // 如果 2 秒后应用仍未退出，强制退出
                Environment.Exit(0);
            }
        });
    }

    /// <summary>
    /// 检查单实例
    /// </summary>
    private static bool CheckSingleInstance()
    {
        _mutex = new Mutex(true, Constants.MUTEX_NAME, out var createdNew);
        return createdNew;
    }

    /// <summary>
    /// 注册全局异常处理
    /// </summary>
    private void RegisterExceptionHandlers()
    {
        // UI线程异常
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 非UI线程异常
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // Task异常
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// UI线程异常处理
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // 如果应用正在关闭，标记异常已处理并返回
        if (_isShuttingDown)
        {
            e.Handled = true;
            return;
        }
        HandleException(e.Exception, "UI线程异常");
        e.Handled = true;
    }

    /// <summary>
    /// 非UI线程异常处理
    /// </summary>
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // 如果应用正在关闭，不处理
        if (_isShuttingDown) 
            return;

        if (e.ExceptionObject is Exception ex)
        {
            HandleException(ex, "应用程序异常");
        }
    }

    /// <summary>
    /// Task异常处理
    /// </summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // 始终标记为已观察，防止应用崩溃
        e.SetObserved();

        // 如果应用正在关闭，不处理
        if (_isShuttingDown) return;

        // 忽略取消异常
        if (e.Exception?.InnerException is TaskCanceledException ||
            e.Exception?.InnerException is OperationCanceledException)
        {
            return;
        }

        HandleException(e.Exception!, "Task异常");
    }

    /// <summary>
    /// 处理异常
    /// </summary>
    private static void HandleException(Exception ex, string source)
    {
        // 如果应用正在关闭，不显示错误弹窗
        if (_isShuttingDown)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[Shutdown Exception] [{source}] {ex.GetType().Name}: {ex.Message}");
            }
            catch { }
            return;
        }

        // 处理 TargetInvocationException，获取真实的内部异常
        var actualException = ex;
        if (ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null)
        {
            actualException = tie.InnerException;
        }

        // 忽略取消操作产生的异常
        if (actualException is TaskCanceledException || actualException is OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[Cancelled] [{source}] {actualException.Message}");
            return;
        }

        // 忽略跨线程访问异常（在关闭时常见）
        if (actualException is InvalidOperationException ioe && 
            ioe.Message.Contains("调用线程无法访问此对象"))
        {
            System.Diagnostics.Debug.WriteLine($"[Cross-thread] [{source}] {actualException.Message}");
            return;
        }

        // 检查内部异常是否为取消异常
        if (actualException.InnerException is TaskCanceledException || actualException.InnerException is OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[Cancelled Inner] [{source}] {actualException.InnerException.Message}");
            return;
        }

        // 记录日志
        try
        {
            _logHelper?.Error($"[{source}] 应用程序异常", ex, new Dictionary<string, object>
            {
                ["Source"] = source,
                ["ExceptionType"] = ex.GetType().Name,
                ["StackTrace"] = ex.StackTrace ?? "No stack trace"
            });
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"[{source}] {ex.Message}");
            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
        }

        // 显示友好提示
        try
        {
            // 再次检查关闭状态，防止竞态条件
            if (_isShuttingDown) return;

            var message = string.IsNullOrWhiteSpace(ex.Message)
                ? "应用程序发生未知错误。"
                : $"应用程序发生错误，请联系技术支持。\n\n错误信息：{ex.Message}";

            MessageBox.Show(
                message,
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // 如果显示 MessageBox 失败，忽略
        }
    }

    /// <summary>
    /// 初始化目录结构
    /// </summary>
    private static void InitializeDirectories()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var directories = new[]
        {
            Constants.DATABASE_DIRECTORY,
            Constants.BACKUP_DIRECTORY,
            Constants.LOG_DIRECTORY,
            Constants.REPORT_DIRECTORY,
            Constants.VIDEO_DIRECTORY,
            Constants.TEMP_DIRECTORY,
            Constants.CONFIG_DIRECTORY
        };

        foreach (var dir in directories)
        {
            var fullPath = Path.Combine(baseDir, dir);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
        }
    }

    /// <summary>
    /// 初始化日志框架
    /// </summary>
    private static void InitializeLogging()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var logDirectory = Path.Combine(baseDir, Constants.LOG_DIRECTORY);

            // 配置日志选项
            var logOptions = Options.Create(new LogOptions
            {
                MinimumLevel = ToolHelper.LoggingDiagnostics.Abstractions.LogLevel.Information,
                LogDirectory = logDirectory,
                EnableConsoleOutput = false, // WPF应用不需要控制台输出
                SeparateFileByLevel = true,  // 按日志级别分文件
                EnableAsyncWrite = true,     // 启用异步写入
                BufferSize = 100,            // 缓冲区大小
                FlushIntervalMs = 1000,      // 刷新间隔1秒
                ArchiveAfterDays = Constants.LOG_RETENTION_DAYS,
                //MessageTemplate = "[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}"
                MessageTemplate = "[{timestamp}] [{level}] {message}",
                TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff"
            });

            // 创建 LogHelper 实例
            _logHelper = new LogHelper(logOptions, Constants.APP_NAME);

            // 记录启动日志
            _logHelper.Information($"{Constants.APP_DISPLAY_NAME} {Constants.VERSION_FULL} 启动", new Dictionary<string, object>
            {
                ["Version"] = Constants.VERSION_FULL,
                ["StartTime"] = DateTime.Now,
                ["OSVersion"] = Environment.OSVersion.ToString(),
                ["MachineName"] = Environment.MachineName
            });
        }
        catch (Exception ex)
        {
            // 如果日志初始化失败，输出到调试窗口
            System.Diagnostics.Debug.WriteLine($"日志初始化失败: {ex.Message}");
            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
        }
    }

    /// <summary>
    /// 初始化数据库
    /// </summary>
    private static void InitializeDatabase()
    {
        try
        {
            _logHelper?.Information("开始初始化数据库...");

            var dbInitializer = new DatabaseInitializer(_logHelper);

            // 同步执行数据库初始化（启动时必须完成）
            dbInitializer.InitializeAsync().GetAwaiter().GetResult();

            _logHelper?.Information("数据库初始化完成");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("数据库初始化失败", ex);
            System.Diagnostics.Debug.WriteLine($"数据库初始化失败: {ex.Message}");
            System.Diagnostics.Debug.WriteLine(ex.StackTrace);

            // 数据库初始化失败是致命错误，显示错误并退出
            MessageBox.Show(
                $"数据库初始化失败：{ex.Message}\n\n应用程序将退出。",
                Constants.APP_DISPLAY_NAME,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // ========== Singleton 服务 ==========
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IBackupService, BackupService>();

        // ========== Transient 服务 ==========
        services.AddTransient<IPatientService, PatientService>();
        services.AddTransient<IMeasurementService, MeasurementService>();
        services.AddTransient<IReportService, ReportService>();
        services.AddTransient<IUserService, UserService>();
        services.AddTransient<IExportImportService, ExportImportService>();
        services.AddTransient<IDepartmentService, DepartmentService>();

        // ========== ViewModel 注册 ==========
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<PatientSelectionViewModel>();
        services.AddTransient<PatientEditViewModel>();
        services.AddTransient<MainContainerViewModel>();
        services.AddTransient<MeasurementViewModel>();
        services.AddTransient<DataManagementViewModel>();
        services.AddTransient<MeasurementDetailViewModel>();
        services.AddTransient<ReportViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Settings 子 ViewModel
        services.AddTransient<ViewModels.Settings.GeneralSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.UserManagementViewModel>();
        services.AddTransient<ViewModels.Settings.DepartmentManagementViewModel>();
        services.AddTransient<ViewModels.Settings.UnitSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.DataManagementSettingsViewModel>();
        services.AddTransient<ViewModels.Settings.SystemInfoViewModel>();

        // ========== View 注册 ==========
        services.AddTransient<MainWindow>();
        services.AddTransient<Views.LoginView>();
        services.AddTransient<Views.PatientSelectionView>();
        services.AddTransient<Views.Dialogs.PatientEditDialog>();
        services.AddTransient<Views.MainContainerView>();
        services.AddTransient<Views.MeasurementView>();
        services.AddTransient<Views.DataManagementView>();
        services.AddTransient<Views.ReportView>();
        services.AddTransient<Views.SettingsView>();
        services.AddTransient<Views.Dialogs.MeasurementDetailDialog>();
        services.AddTransient<Views.Dialogs.ConfirmDialog>();
        services.AddTransient<Views.Dialogs.AboutDialog>();
    }
}

