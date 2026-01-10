using System.IO;
using System.Windows;
using System.Windows.Threading;
using BTFX.Common;
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

        // 5. 配置依赖注入
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // 6. 加载配置
        var settingsService = Services.GetRequiredService<ISettingsService>();
        settingsService.LoadSettings();

        // 7. 应用主题
        var themeService = Services.GetRequiredService<IThemeService>();
        themeService.ApplyTheme(settingsService.CurrentSettings.Application.Theme);

        // 8. 应用语言
        var localizationService = Services.GetRequiredService<ILocalizationService>();
        localizationService.ApplyLanguage(settingsService.CurrentSettings.Application.Language);

            // 9. 显示主窗口
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

                    // 10. 注册视图映射
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
        try
        {
            // 记录关闭日志
            _logHelper?.Information($"{Constants.APP_DISPLAY_NAME} 正在关闭");

            // 保存配置
            var settingsService = Services?.GetService<ISettingsService>();
            settingsService?.SaveSettings();

            // 释放日志资源（设置超时避免卡住）
            try
            {
                var flushTask = _logHelper?.FlushAsync();
                if (flushTask != null)
                {
                    if (!flushTask.Wait(TimeSpan.FromSeconds(2)))
                    {
                        System.Diagnostics.Debug.WriteLine("Log flush timeout");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Log flush error: {ex.Message}");
            }

            // 释放Mutex
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch { }

            _mutex?.Dispose();

            // 强制释放服务提供者
            if (Services is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnExit error: {ex.Message}");
        }
        finally
        {
            base.OnExit(e);

            // 确保进程退出
            Environment.Exit(0);
        }
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
        HandleException(e.Exception, "UI线程异常");
        e.Handled = true;
    }

    /// <summary>
    /// 非UI线程异常处理
    /// </summary>
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
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
        HandleException(e.Exception, "Task异常");
        e.SetObserved();
    }

    /// <summary>
    /// 处理异常
    /// </summary>
    private static void HandleException(Exception ex, string source)
    {
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
            // 如果日志记录失败，回退到调试输出
            System.Diagnostics.Debug.WriteLine($"[{source}] {ex.Message}");
            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
        }

        // 显示友好提示
        MessageBox.Show(
            $"应用程序发生错误，请联系技术支持。\n\n错误信息：{ex.Message}",
            "错误",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
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
                        MessageTemplate = "[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}"
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

