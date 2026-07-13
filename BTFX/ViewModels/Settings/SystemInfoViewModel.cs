using BTFX.Common;
using BTFX.Services.Interfaces;
using BTFX.Helpers;
using BTFX.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using ToolHelper.LoggingDiagnostics.Abstractions;
using ToolHelper.LoggingDiagnostics.Logging;
using BtfxConstants = BTFX.Common.Constants;

namespace BTFX.ViewModels.Settings;

/// <summary>
/// System information view model.
/// </summary>
public partial class SystemInfoViewModel : ObservableObject
{
    private readonly ISessionService _sessionService;
    private readonly ILocalizationService _localizationService;
    private readonly IAppUpdateService _appUpdateService;
    private readonly ILogHelper? _logHelper;

    public string AppVersion => BtfxConstants.VERSION_FULL;
    public string AppName => _localizationService.GetString("AppName");

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
    private string _logStatistics = "Loading...";

    [ObservableProperty]
    private string _logRangeCount = "--";

    [ObservableProperty]
    private string _logInformationCount = "--";

    [ObservableProperty]
    private string _logWarningCount = "--";

    [ObservableProperty]
    private string _logErrorCount = "--";

    [ObservableProperty]
    private string _logFileCount = "--";

    [ObservableProperty]
    private string _logTotalSize = "--";

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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckUpdateCommand))]
    private bool _isCheckingUpdate;

    public SystemInfoViewModel(
        ISessionService sessionService,
        ILocalizationService localizationService,
        IAppUpdateService appUpdateService)
    {
        _sessionService = sessionService;
        _localizationService = localizationService;
        _appUpdateService = appUpdateService;

        try { _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper; } catch { }

        _localizationService.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(AppName));
            LoadSystemInfo();
        };

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

            // Use BaseDirectory/Data/Database/BTFX.db.
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

            // Use BaseDirectory/Data/Logs.
            LogDirectory = NormalizeDirectoryPath(System.IO.Path.Combine(baseDir, BtfxConstants.LOG_DIRECTORY));
            System.IO.Directory.CreateDirectory(LogDirectory);

            _logHelper?.Information($"Log directory set to {LogDirectory}, exists={System.IO.Directory.Exists(LogDirectory)}");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("Failed to load system information", ex);
        }
    }

    /// <summary>
    /// Gets the localized role name.
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

    [RelayCommand(CanExecute = nameof(CanCheckUpdate))]
    private async Task CheckUpdateAsync()
    {
        try
        {
            IsCheckingUpdate = true;
            var updateInfo = await _appUpdateService.CheckForUpdatesAsync(true);
            if (updateInfo is null)
            {
                AppDialog.Show(_localizationService.GetString("Update.NoUpdate"), _localizationService.GetString("CheckForUpdates"),
                    AppDialogButtons.Ok, AppDialogIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(updateInfo.PackageUrl))
            {
                AppDialog.Show(
                    string.Format(_localizationService.GetString("Update.PackageUrlEmptyFormat"), updateInfo.Version),
                    _localizationService.GetString("CheckForUpdates"),
                    AppDialogButtons.Ok,
                    AppDialogIcon.Warning);
                return;
            }

            var message = string.IsNullOrWhiteSpace(updateInfo.Detail)
                ? string.Format(_localizationService.GetString("Update.FoundMessageFormat"), updateInfo.Version)
                : string.Format(_localizationService.GetString("Update.FoundMessageWithDetailFormat"), updateInfo.Version, updateInfo.Detail);
            var confirm = AppDialog.Show(
                message,
                _localizationService.GetString("Update.FoundTitle"),
                AppDialogButtons.YesNo,
                AppDialogIcon.Information);
            if (confirm != AppDialogResult.Yes)
            {
                return;
            }

            var installerPath = await RunWithProgressDialogAsync(
                _localizationService.GetString("Update.DownloadTitle"),
                _localizationService.GetString("Update.PreparingDownload"),
                _localizationService.GetString("Update.PreparingDownloadMessage"),
                (progress, token) => _appUpdateService.DownloadUpdatePackageAsync(updateInfo, progress, token));

            var installConfirm = AppDialog.Show(
                _localizationService.GetString("Update.DownloadCompletedMessage"),
                _localizationService.GetString("Update.InstallTitle"),
                AppDialogButtons.OkCancel,
                AppDialogIcon.Information);
            if (installConfirm == AppDialogResult.Ok)
            {
                _appUpdateService.StartInstallerAndShutdown(installerPath);
            }
        }
        catch (OperationCanceledException)
        {
            AppDialog.Show(_localizationService.GetString("Update.DownloadCanceled"), _localizationService.GetString("CheckForUpdates"),
                AppDialogButtons.Ok, AppDialogIcon.Information);
        }
        catch (Exception ex)
        {
            _logHelper?.Error("Failed to check or download update", ex);
            AppDialog.Show(string.Format(_localizationService.GetString("Update.CheckFailedFormat"), ex.Message), _localizationService.GetString("CheckForUpdates"),
                AppDialogButtons.Ok, AppDialogIcon.Error);
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    private bool CanCheckUpdate() => !IsCheckingUpdate;

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
            _logHelper?.Error("Failed to show about dialog", ex);
        }
    }

    [RelayCommand]
    private void OpenLogDirectory()
    {
        try
        {
            if (System.IO.Directory.Exists(LogDirectory))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = LogDirectory,
                    UseShellExecute = true
                });
            }
            else
            {
                AppDialog.Show(_localizationService.GetString("Error"), _localizationService.GetString("Information"),
                    AppDialogButtons.Ok, AppDialogIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("Failed to open log directory", ex);
        }
    }

    private static string NormalizeDirectoryPath(string path)
    {
        return System.IO.Path.GetFullPath(path)
            .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
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
                AppDialog.Show(_localizationService.GetString("Error"), _localizationService.GetString("Information"),
                    AppDialogButtons.Ok, AppDialogIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("Failed to open database directory", ex);
        }
    }

    [RelayCommand]
    private async Task ExportLogsAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = _localizationService.GetString("ExportLogs"),
                Filter = _localizationService.GetString("LogExportFilter"),
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

            AppDialog.Show(string.Format(_localizationService.GetString("LogExportSuccessFormat"), exportedCount, dialog.FileName), _localizationService.GetString("Tip"),
                AppDialogButtons.Ok, AppDialogIcon.Information);
            _logHelper?.Information($"Log export completed: {exportedCount} records");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("Log export failed", ex);
            AppDialog.Show(string.Format(_localizationService.GetString("LogExportFailedFormat"), ex.Message), _localizationService.GetString("Error"),
                AppDialogButtons.Ok, AppDialogIcon.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task CleanupLogsAsync()
    {
        var result = AppDialog.Show(
            string.Format(_localizationService.GetString("ConfirmCleanupLogs"), LogCleanupDays),
            _localizationService.GetString("Confirm"),
            AppDialogButtons.YesNo,
            AppDialogIcon.Warning);

        if (result != AppDialogResult.Yes) return;

        try
        {
            IsSaving = true;

            var logExportHelper = new LogExportHelper(LogDirectory);
            var deletedCount = await logExportHelper.CleanupOldLogsAsync(LogCleanupDays);

            AppDialog.Show(string.Format(_localizationService.GetString("LogCleanupSuccessFormat"), deletedCount), _localizationService.GetString("Tip"),
                AppDialogButtons.Ok, AppDialogIcon.Information);
            _logHelper?.Information($"Log cleanup completed: {deletedCount} files deleted");

            // Refresh log statistics.
            await LoadLogStatisticsAsync();
        }
        catch (Exception ex)
        {
            _logHelper?.Error("Log cleanup failed", ex);
            AppDialog.Show(string.Format(_localizationService.GetString("LogCleanupFailedFormat"), ex.Message), _localizationService.GetString("Error"),
                AppDialogButtons.Ok, AppDialogIcon.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Loads log statistics.
    /// </summary>
    [RelayCommand]
    private async Task LoadLogStatisticsAsync()
    {
        try
        {
            if (!System.IO.Directory.Exists(LogDirectory))
            {
                LogStatistics = _localizationService.GetString("LogDirectoryMissing");
                return;
            }

            var logExportHelper = new ToolHelper.LoggingDiagnostics.Logging.LogExportHelper(LogDirectory);
            var stats = await logExportHelper.GetStatisticsAsync(DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1));

            LogTotalCount = stats.FileCount;
            LogTodayCount = stats.TotalCount;


            LogStatistics = string.Format(
                _localizationService.GetString("LogStatisticsSummaryFormat"),
                stats.TotalCount,
                stats.InformationCount,
                stats.WarningCount,
                stats.ErrorCount,
                stats.FileCount,
                stats.TotalSizeBytes / 1024.0);
        }
        catch (Exception ex)
        {
            LogStatistics = _localizationService.GetString("LogStatisticsLoadFailed");
            _logHelper?.Error("Failed to load log statistics", ex);
        }
    }

    private static string GetGlobalString(string key)
    {
        try
        {
            return System.Windows.Application.Current.FindResource(key)?.ToString() ?? key;
        }
        catch
        {
            return key;
        }
    }

    private async Task RefreshLogStatisticsAsync()
    {
        await LoadLogStatisticsAsync();
    }

    partial void OnLogStatisticsChanged(string value)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(value ?? string.Empty, @"\d+(?:\.\d+)?");
        if (matches.Count < 5)
        {
            ResetLogStatisticDisplays();
            return;
        }

        var offset = matches.Count >= 7 ? 1 : 0;
        LogRangeCount = string.Format(_localizationService.GetString("LogRecordCountFormat"), matches[offset].Value);
        LogInformationCount = string.Format(_localizationService.GetString("LogRecordCountFormat"), matches[offset + 1].Value);
        LogWarningCount = string.Format(_localizationService.GetString("LogRecordCountFormat"), matches[offset + 2].Value);
        LogErrorCount = string.Format(_localizationService.GetString("LogRecordCountFormat"), matches[offset + 3].Value);
        LogFileCount = string.Format(_localizationService.GetString("LogFileCountFormat"), matches[offset + 4].Value);
        LogTotalSize = matches.Count > offset + 5 ? $"{matches[offset + 5].Value} KB" : "--";
    }

    private void ResetLogStatisticDisplays()
    {
        LogRangeCount = "--";
        LogInformationCount = "--";
        LogWarningCount = "--";
        LogErrorCount = "--";
        LogFileCount = "--";
        LogTotalSize = "--";
    }

    private static async Task<T> RunWithProgressDialogAsync<T>(
        string title,
        string stage,
        string message,
        Func<IProgress<OperationProgressInfo>, CancellationToken, Task<T>> operation)
    {
        using var operationCts = new CancellationTokenSource();
        var progressViewModel = new OperationProgressDialogViewModel(
            title,
            stage,
            message,
            operationCts,
            canCancel: true);

        var progress = new Progress<OperationProgressInfo>(progressViewModel.Update);
        var dialog = new Views.Dialogs.OperationProgressDialog
        {
            DataContext = progressViewModel
        };

        var dialogTask = DialogHost.Show(dialog, "RootDialog");
        try
        {
            var result = await operation(progress, operationCts.Token);
            progressViewModel.MarkCompleted(GetGlobalString("OperationProgress.CompletedMessage"));
            await Task.Delay(650);
            DialogHost.Close("RootDialog");
            await dialogTask;
            return result;
        }
        catch (OperationCanceledException)
        {
            progressViewModel.MarkFailed(GetGlobalString("OperationProgress.CanceledMessage"));
            await Task.Delay(350);
            DialogHost.Close("RootDialog");
            await dialogTask;
            throw;
        }
        catch
        {
            progressViewModel.MarkFailed(GetGlobalString("OperationProgress.FailedMessage"));
            await Task.Delay(350);
            DialogHost.Close("RootDialog");
            await dialogTask;
            throw;
        }
    }
}

