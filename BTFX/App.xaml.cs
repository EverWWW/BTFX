using System.IO;
using System.Windows;
using System.Windows.Threading;
using BTFX.Common;
using BTFX.Services.Implementations;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ToolHelper.LoggingDiagnostics;

namespace BTFX;

/// <summary>
/// App.xaml 的交互逻辑
/// </summary>
public partial class App : Application
{
    private static Mutex? _mutex;

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

        // 4. 初始化日志框架 (TODO: 第四阶段完善)
        // InitializeLogging();

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

        // 9. 导航到登录视图
        var navigationService = Services.GetRequiredService<INavigationService>() as NavigationService;
        // 暂时不导航，等创建LoginView后再启用
        // navigationService?.NavigateTo<LoginViewModel>();
    }

    /// <summary>
    /// 应用程序退出
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        // 保存配置
        var settingsService = Services?.GetService<ISettingsService>();
        settingsService?.SaveSettings();

        // 释放Mutex
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();

        base.OnExit(e);
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
        // 记录日志 (TODO: 第四阶段使用ToolHelper.LoggingDiagnostics)
        System.Diagnostics.Debug.WriteLine($"[{source}] {ex.Message}");
        System.Diagnostics.Debug.WriteLine(ex.StackTrace);

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

        // ========== ViewModel 注册 ==========
        services.AddTransient<MainWindowViewModel>();

        // ========== View 注册 ==========
        services.AddTransient<MainWindow>();
    }
}

