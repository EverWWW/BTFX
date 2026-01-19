using System.Collections.ObjectModel;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolHelper.LoggingDiagnostics.Abstractions;
using BtfxConstants = BTFX.Common.Constants;

namespace BTFX.ViewModels.Settings;

/// <summary>
/// 数据管理设置视图模型
/// </summary>
public partial class DataManagementSettingsViewModel : ObservableObject
{
    private readonly IBackupService _backupService;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;

    [ObservableProperty]
    private bool _autoBackupEnabled;

    [ObservableProperty]
    private string _backupTime = BtfxConstants.BACKUP_DEFAULT_TIME;

    [ObservableProperty]
    private int _backupRetainCount = BtfxConstants.BACKUP_DEFAULT_RETAIN_COUNT;

    [ObservableProperty]
    private ObservableCollection<BackupHistoryItem> _backupHistory = [];

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isLoading;

    public DataManagementSettingsViewModel(
        IBackupService backupService,
        ISettingsService settingsService,
        ILocalizationService localizationService)
    {
        _backupService = backupService;
        _settingsService = settingsService;
        _localizationService = localizationService;

        try { _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper; } catch { }

        _ = LoadBackupHistoryAsync();
    }

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        try
        {
            IsSaving = true;
            var filePath = await _backupService.CreateBackupAsync();

            if (!string.IsNullOrEmpty(filePath))
            {
                System.Windows.MessageBox.Show($"备份成功！\n文件：{filePath}", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                _logHelper?.Information($"手动备份成功：{filePath}");
                await LoadBackupHistoryAsync();
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("手动备份失败", ex);
            System.Windows.MessageBox.Show($"备份失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task LoadBackupHistoryAsync()
    {
        try
        {
            IsLoading = true;
            BackupHistory.Clear();
            // TODO: 实现备份列表获取
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载备份历史失败", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync(BackupHistoryItem? item)
    {
        if (item == null) return;

        var result = System.Windows.MessageBox.Show(
            "恢复备份将覆盖当前数据，此操作不可撤销！确定要继续吗？",
            "确认恢复",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            IsSaving = true;
            var success = await _backupService.RestoreBackupAsync(item.FilePath);
            if (success)
            {
                System.Windows.MessageBox.Show("恢复成功！程序需要重启。", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                _logHelper?.Information($"恢复备份成功：{item.FilePath}");
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"恢复备份失败：{item.FilePath}", ex);
            System.Windows.MessageBox.Show($"恢复失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void SaveBackupSettings()
    {
        try
        {
            System.Windows.MessageBox.Show("备份设置已保存！", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            _logHelper?.Information("保存备份设置");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("保存备份设置失败", ex);
            System.Windows.MessageBox.Show($"保存失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}

/// <summary>
/// 备份历史项
/// </summary>
public partial class BackupHistoryItem : ObservableObject
{
    public string FilePath { get; }
    public DateTime CreatedAt { get; }
    public int RowNumber { get; }

    public string FileName => System.IO.Path.GetFileName(FilePath);
    public string CreatedAtDisplay => CreatedAt.ToString(BtfxConstants.DATETIME_FORMAT);

    public string FileSizeDisplay
    {
        get
        {
            try
            {
                if (System.IO.File.Exists(FilePath))
                {
                    var fileInfo = new System.IO.FileInfo(FilePath);
                    return $"{fileInfo.Length / 1024.0:F2} KB";
                }
            }
            catch { }
            return "--";
        }
    }

    public BackupHistoryItem(string filePath, DateTime createdAt, int rowNumber)
    {
        FilePath = filePath;
        CreatedAt = createdAt;
        RowNumber = rowNumber;
    }
}
