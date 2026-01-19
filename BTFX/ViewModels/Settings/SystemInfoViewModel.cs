using BTFX.Common;
using BTFX.Services.Interfaces;
using BTFX.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using ToolHelper.LoggingDiagnostics.Abstractions;
using ToolHelper.LoggingDiagnostics.Logging;
using BtfxConstants = BTFX.Common.Constants;

namespace BTFX.ViewModels.Settings;

/// <summary>
/// 系统信息视图模型
/// </summary>
public partial class SystemInfoViewModel : ObservableObject
{
    private readonly ISessionService _sessionService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;

    public string AppVersion => BtfxConstants.VERSION_FULL;
    public string AppName => BtfxConstants.APP_DISPLAY_NAME;

    [ObservableProperty]
    private string _databasePath = string.Empty;

    [ObservableProperty]
    private string _databaseSize = "--";

    [ObservableProperty]
    private string _logDirectory = string.Empty;

    [ObservableProperty]
    private string _currentUsername = string.Empty;

    [ObservableProperty]
    private string _currentUserRole = string.Empty;

    [ObservableProperty]
    private string _logStatistics = "正在加载...";

    [ObservableProperty]
    private int _logCleanupDays = 30;

    [ObservableProperty]
    private int _logTotalCount;

    [ObservableProperty]
    private int _logTodayCount;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isLoading;

    public SystemInfoViewModel(
        ISessionService sessionService,
        ILocalizationService localizationService)
    {
        _sessionService = sessionService;
        _localizationService = localizationService;

        try { _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper; } catch { }

        LoadSystemInfo();
        _ = LoadLogStatisticsAsync();
    }

    private void LoadSystemInfo()
    {
        try
        {
            var currentUser = _sessionService.CurrentUser;
            CurrentUsername = currentUser?.Name ?? _localizationService.GetString("Guest");
            CurrentUserRole = GetLocalizedRole(currentUser?.Role);

            // 使用正确的数据库路径：BaseDirectory/Data/Database/BTFX.db
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            DatabasePath = System.IO.Path.Combine(baseDir, BtfxConstants.DATABASE_DIRECTORY, BtfxConstants.DATABASE_FILENAME);

            if (System.IO.File.Exists(DatabasePath))
            {
                var fileInfo = new System.IO.FileInfo(DatabasePath);
                var sizeKB = fileInfo.Length / 1024.0;
                var sizeMB = sizeKB / 1024.0;
                DatabaseSize = sizeMB >= 1 ? $"{sizeMB:F2} MB" : $"{sizeKB:F2} KB";
            }
            else
            {
                DatabaseSize = "--";
            }

            // 使用正确的日志目录路径：BaseDirectory/Data/Logs
            LogDirectory = System.IO.Path.Combine(baseDir, BtfxConstants.LOG_DIRECTORY);

            _logHelper?.Information($"日志目录设置为: {LogDirectory}, 存在={System.IO.Directory.Exists(LogDirectory)}");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载系统信息失败", ex);
        }
    }

    /// <summary>
    /// 获取本地化的角色名称
    /// </summary>
    private string GetLocalizedRole(Common.UserRole? role)
    {
        return role switch
        {
            Common.UserRole.Administrator => _localizationService.GetString("Administrator"),
            Common.UserRole.Operator => _localizationService.GetString("Operator"),
            Common.UserRole.Guest => _localizationService.GetString("Guest"),
            _ => "--"
        };
    }

    [RelayCommand]
    private async Task ShowAboutDialogAsync()
    {
        try
        {
            var dialog = new AboutDialog();
            await DialogHost.Show(dialog, "RootDialog");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("显示关于对话框失败", ex);
        }
    }

    [RelayCommand]
    private void OpenLogDirectory()
    {
        try
        {
            if (System.IO.Directory.Exists(LogDirectory))
            {
                System.Diagnostics.Process.Start("explorer.exe", LogDirectory);
            }
            else
            {
                System.Windows.MessageBox.Show(_localizationService.GetString("Error"), _localizationService.GetString("Information"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("打开日志目录失败", ex);
        }
    }

    [RelayCommand]
    private void OpenDatabaseDirectory()
    {
        try
        {
            var dbDir = System.IO.Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(dbDir) && System.IO.Directory.Exists(dbDir))
            {
                System.Diagnostics.Process.Start("explorer.exe", dbDir);
            }
            else
            {
                System.Windows.MessageBox.Show(_localizationService.GetString("Error"), _localizationService.GetString("Information"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("打开数据库目录失败", ex);
        }
    }

    [RelayCommand]
    private async Task ExportLogsAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出日志",
                Filter = "文本文件 (*.txt)|*.txt|CSV文件 (*.csv)|*.csv",
                FileName = $"BTFX_Logs_{DateTime.Now:yyyyMMdd_HHmmss}",
                DefaultExt = ".txt"
            };

            if (dialog.ShowDialog() != true) return;

            IsSaving = true;

            var logExportHelper = new LogExportHelper(LogDirectory);
            var startDate = DateTime.Today.AddDays(-30);
            var endDate = DateTime.Today.AddDays(1);

            int exportedCount;
            if (dialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                exportedCount = await logExportHelper.ExportLogsToCsvAsync(dialog.FileName, startDate, endDate);
            }
            else
            {
                exportedCount = await logExportHelper.ExportLogsAsync(dialog.FileName, startDate, endDate);
            }

            System.Windows.MessageBox.Show($"日志导出完成！\n共导出 {exportedCount} 条记录\n文件：{dialog.FileName}", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            _logHelper?.Information($"日志导出完成：{exportedCount} 条记录");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("日志导出失败", ex);
            System.Windows.MessageBox.Show($"日志导出失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task CleanupLogsAsync()
    {
        var result = System.Windows.MessageBox.Show(
            $"确定要清理 {LogCleanupDays} 天前的日志吗？\n此操作不可撤销！",
            "确认清理",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            IsSaving = true;

            var logExportHelper = new LogExportHelper(LogDirectory);
            var deletedCount = await logExportHelper.CleanupOldLogsAsync(LogCleanupDays);

            System.Windows.MessageBox.Show($"日志清理完成！\n共删除 {deletedCount} 个日志文件", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            _logHelper?.Information($"日志清理完成：删除 {deletedCount} 个文件");

            // 刷新日志统计
            await LoadLogStatisticsAsync();
        }
        catch (Exception ex)
        {
            _logHelper?.Error("日志清理失败", ex);
            System.Windows.MessageBox.Show($"日志清理失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// 加载日志统计
    /// </summary>
    [RelayCommand]
    private async Task LoadLogStatisticsAsync()
    {
        try
        {
            if (!System.IO.Directory.Exists(LogDirectory))
            {
                LogStatistics = "日志目录不存在";
                return;
            }

            var logExportHelper = new ToolHelper.LoggingDiagnostics.Logging.LogExportHelper(LogDirectory);
            var stats = await logExportHelper.GetStatisticsAsync(DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1));

            LogTotalCount = stats.FileCount;
            LogTodayCount = stats.TotalCount;


            LogStatistics = $"近30天：共 {stats.TotalCount} 条日志\n" +
                           $"信息: {stats.InformationCount}  警告: {stats.WarningCount}  错误: {stats.ErrorCount}\n" +
                           $"日志文件: {stats.FileCount} 个，总大小: {stats.TotalSizeBytes / 1024.0:F1} KB";
        }
        catch (Exception ex)
        {
            LogStatistics = "加载统计失败";
            _logHelper?.Error("加载日志统计失败", ex);
        }
    }

    [RelayCommand]
    private async Task RefreshLogStatisticsAsync()
    {
        await LoadLogStatisticsAsync();
    }
}
