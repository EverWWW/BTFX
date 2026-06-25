using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;
using BTFX.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolHelper.LoggingDiagnostics.Abstractions;
using BtfxConstants = BTFX.Common.Constants;
using DialogHost = MaterialDesignThemes.Wpf.DialogHost;

namespace BTFX.ViewModels.Settings;

/// <summary>
/// 数据管理设置视图模型
/// </summary>
public partial class DataManagementSettingsViewModel : ObservableObject
{
    private const double BackupRowHeight = 60;
    private const double BackupRowTopMargin = 8;
    private const int MinimumPageSize = 3;
    private const int MaximumPageSize = 3;

    private readonly IBackupService _backupService;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;
    private readonly List<BackupHistoryItem> _allBackupHistory = [];

    private ObservableCollection<PageItem> _pageNumbers = [];

    private int _currentPage = 1;

    private int _totalPages = 1;

    private int _pageSize = MaximumPageSize;

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

    [ObservableProperty]
    private string _recordedVideoStorageDisplay = "--";

    [ObservableProperty]
    private string _analysisResultStorageDisplay = "--";

    public ObservableCollection<PageItem> PageNumbers => _pageNumbers;

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            if (SetProperty(ref _totalPages, value))
            {
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    public bool CanGoPrevious => CurrentPage > 1;

    public bool CanGoNext => CurrentPage < TotalPages;

    public DataManagementSettingsViewModel(
        IBackupService backupService,
        ISettingsService settingsService,
        ILocalizationService localizationService)
    {
        _backupService = backupService;
        _settingsService = settingsService;
        _localizationService = localizationService;

        try { _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper; } catch { }

        LoadBackupSettings();
        RefreshVideoStorageInfo();
        _ = LoadBackupHistoryAsync();
    }

    [RelayCommand]
    private void OpenRecordedVideoFolder()
    {
        OpenFolder(GetRecordedVideoDirectory());
    }

    [RelayCommand]
    private void OpenAnalysisResultFolder()
    {
        OpenFolder(GetAnalysisResultDirectory());
    }

    private void LoadBackupSettings()
    {
        try
        {
            var autoBackupSettings = _settingsService.CurrentSettings.AutoBackup;
            if (autoBackupSettings == null)
            {
                return;
            }

            AutoBackupEnabled = autoBackupSettings.Enabled;
            BackupTime = string.IsNullOrWhiteSpace(autoBackupSettings.Time)
                ? BtfxConstants.BACKUP_DEFAULT_TIME
                : autoBackupSettings.Time;
            BackupRetainCount = autoBackupSettings.RetainCount > 0
                ? autoBackupSettings.RetainCount
                : BtfxConstants.BACKUP_DEFAULT_RETAIN_COUNT;
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载自动备份设置失败", ex);
        }
    }

    private void RefreshVideoStorageInfo()
    {
        RecordedVideoStorageDisplay = FormatDirectorySize(GetRecordedVideoDirectory());
        AnalysisResultStorageDisplay = FormatDirectorySize(GetAnalysisResultDirectory());
    }

    [RelayCommand]
    private void RefreshVideoStorage()
    {
        RefreshVideoStorageInfo();
    }

    private static string GetRecordedVideoDirectory()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "video");
    }

    private static string GetAnalysisResultDirectory()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Analysis");
    }

    private static string FormatDirectorySize(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return "0 B";
        }

        try
        {
            var totalBytes = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Sum(path =>
                {
                    try
                    {
                        return new FileInfo(path).Length;
                    }
                    catch
                    {
                        return 0L;
                    }
                });

            return FormatFileSize(totalBytes);
        }
        catch
        {
            return "--";
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)Math.Max(0, bytes);
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{size:F0} {units[unitIndex]}" : $"{size:F2} {units[unitIndex]}";
    }

    private static void OpenFolder(string directory)
    {
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
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
                System.Windows.MessageBox.Show(_localizationService.GetString("BackupSuccessFormat", filePath), _localizationService.GetString("Information"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                _logHelper?.Information($"手动备份成功：{filePath}");
                await LoadBackupHistoryAsync();
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("手动备份失败", ex);
            System.Windows.MessageBox.Show(_localizationService.GetString("BackupFailedFormat", ex.Message), _localizationService.GetString("Error"),
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
            RefreshVideoStorageInfo();
            var backupFiles = await _backupService.GetBackupFilesAsync();

            _allBackupHistory.Clear();

            var rowNumber = 1;
            foreach (var backupFile in backupFiles)
            {
                var fullPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    BtfxConstants.BACKUP_DIRECTORY,
                    backupFile.FileName);

                _allBackupHistory.Add(new BackupHistoryItem(
                    fullPath,
                    backupFile.CreatedAt,
                    rowNumber++,
                    backupFile.FileSizeBytes));
            }

            CurrentPage = 1;
            RefreshPagedBackupHistory();
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
    private async Task DeleteBackupAsync(BackupHistoryItem? item)
    {
        if (item == null)
        {
            return;
        }

        var result = await ShowConfirmDialogAsync(
            _localizationService.GetString("ConfirmDelete"),
            _localizationService.GetString("ConfirmDeleteBackupFile", item.FileName),
            "TrashCanOutline");

        if (!result)
        {
            return;
        }

        try
        {
            IsSaving = true;
            var success = await _backupService.DeleteBackupAsync(item.FileName);
            if (success)
            {
                await LoadBackupHistoryAsync();
                await ShowNoticeDialogAsync(_localizationService.GetString("Information"), _localizationService.GetString("BackupFileDeleted"));
                _logHelper?.Information($"删除备份成功：{item.FileName}");
                return;
            }

            await ShowNoticeDialogAsync(_localizationService.GetString("Error"), _localizationService.GetString("DeleteBackupFailedRetry"));
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除备份失败：{item.FileName}", ex);
            await ShowNoticeDialogAsync(_localizationService.GetString("Error"), $"{_localizationService.GetString("DeleteFailed")}: {ex.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task<bool> ShowConfirmDialogAsync(string title, string message, string iconKind = "HelpCircleOutline")
    {
        var result = await DialogHost.Show(
            new ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel
                {
                    Title = title,
                    Message = message,
                    ConfirmText = _localizationService.GetString("Confirm"),
                    CancelText = _localizationService.GetString("Cancel"),
                    IsCancelVisible = true,
                    IconKind = iconKind
                }
            },
            "RootDialog").ConfigureAwait(true);

        return result is true;
    }

    private Task ShowNoticeDialogAsync(string title, string message)
    {
        return DialogHost.Show(
            new ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel
                {
                    Title = title,
                    Message = message,
                    ConfirmText = _localizationService.GetString("Confirm"),
                    IsCancelVisible = false,
                    IconKind = "InformationOutline"
                }
            },
            "RootDialog");
    }

    [RelayCommand]
    private async Task RestoreBackupAsync(BackupHistoryItem? item)
    {
        if (item == null) return;

        var result = System.Windows.MessageBox.Show(
            _localizationService.GetString("ConfirmRestoreBackup"),
            _localizationService.GetString("ConfirmRestore"),
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            IsSaving = true;
            var success = await _backupService.RestoreBackupAsync(item.FilePath);
            if (success)
            {
                System.Windows.MessageBox.Show(_localizationService.GetString("RestoreSuccessRestart"), _localizationService.GetString("Information"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                _logHelper?.Information($"恢复备份成功：{item.FilePath}");
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"恢复备份失败：{item.FilePath}", ex);
            System.Windows.MessageBox.Show(_localizationService.GetString("RestoreFailedFormat", ex.Message), _localizationService.GetString("Error"),
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
            IsSaving = true;

            var normalizedBackupTime = NormalizeBackupTime(BackupTime);
            var normalizedRetainCount = NormalizeBackupRetainCount(BackupRetainCount);
            var autoBackupSettings = _settingsService.CurrentSettings.AutoBackup ??= new Models.AutoBackupSettings();

            autoBackupSettings.Enabled = AutoBackupEnabled;
            autoBackupSettings.Time = normalizedBackupTime;
            autoBackupSettings.RetainCount = normalizedRetainCount;

            BackupTime = normalizedBackupTime;
            BackupRetainCount = normalizedRetainCount;

            _settingsService.SaveSettings();

            if (AutoBackupEnabled)
            {
                _backupService.StartAutoBackup();
            }
            else
            {
                _backupService.StopAutoBackup();
            }

            System.Windows.MessageBox.Show(_localizationService.GetString("BackupSettingsSaved"), _localizationService.GetString("Information"),
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            _logHelper?.Information($"保存备份设置：启用={AutoBackupEnabled}，时间={normalizedBackupTime}，保留数量={normalizedRetainCount}");
        }
        catch (ArgumentException ex)
        {
            _logHelper?.Warning($"保存备份设置失败：{ex.Message}");
            System.Windows.MessageBox.Show(ex.Message, _localizationService.GetString("Warning"),
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            _logHelper?.Error("保存备份设置失败", ex);
            System.Windows.MessageBox.Show(_localizationService.GetString("SaveExceptionFormat", ex.Message), _localizationService.GetString("Error"),
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private string NormalizeBackupTime(string? backupTime)
    {
        if (string.IsNullOrWhiteSpace(backupTime))
        {
            throw new ArgumentException(_localizationService.GetString("BackupTimeRequiredHint"), nameof(backupTime));
        }

        if (!TimeSpan.TryParse(backupTime, out var parsedTime))
        {
            throw new ArgumentException(_localizationService.GetString("BackupTimeFormatHint"), nameof(backupTime));
        }

        return parsedTime.ToString(@"hh\:mm");
    }

    private int NormalizeBackupRetainCount(int retainCount)
    {
        if (retainCount <= 0)
        {
            throw new ArgumentException(_localizationService.GetString("RetainCountPositiveHint"), nameof(retainCount));
        }

        return retainCount;
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        CurrentPage--;
        RefreshPagedBackupHistory();
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!CanGoNext)
        {
            return;
        }

        CurrentPage++;
        RefreshPagedBackupHistory();
    }

    [RelayCommand]
    private void GoToPage(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > TotalPages || pageNumber == CurrentPage)
        {
            return;
        }

        CurrentPage = pageNumber;
        RefreshPagedBackupHistory();
    }

    /// <summary>
    /// 根据列表可视区域高度动态更新每页条数。
    /// </summary>
    /// <param name="viewportHeight">列表可视区域高度。</param>
    public void UpdatePageSize(double viewportHeight)
    {
        if (viewportHeight <= 0)
        {
            return;
        }

        var rowFullHeight = BackupRowHeight + BackupRowTopMargin;
        var calculatedPageSize = Math.Clamp(
            (int)Math.Floor((viewportHeight + BackupRowTopMargin) / rowFullHeight),
            MinimumPageSize,
            MaximumPageSize);

        if (calculatedPageSize == _pageSize)
        {
            return;
        }

        _pageSize = calculatedPageSize;

        if (_allBackupHistory.Count > 0)
        {
            var maxPage = (int)Math.Ceiling(_allBackupHistory.Count / (double)_pageSize);
            if (CurrentPage > maxPage)
            {
                CurrentPage = maxPage;
            }
        }

        RefreshPagedBackupHistory();
        _logHelper?.Information($"备份历史每页条数已根据可视高度更新为 {_pageSize}。");
    }

    private void RefreshPagedBackupHistory()
    {
        BackupHistory.Clear();

        TotalPages = _allBackupHistory.Count == 0
            ? 1
            : (int)Math.Ceiling(_allBackupHistory.Count / (double)_pageSize);

        if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }

        if (CurrentPage < 1)
        {
            CurrentPage = 1;
        }

        var pageItems = _allBackupHistory
            .Skip((CurrentPage - 1) * _pageSize)
            .Take(_pageSize)
            .ToList();

        foreach (var item in pageItems)
        {
            BackupHistory.Add(item);
        }

        BuildPageNumbers();
    }

    private void BuildPageNumbers()
    {
        PageNumbers.Clear();
        if (TotalPages <= 0)
        {
            return;
        }

        var pagesToShow = new SortedSet<int> { 1, TotalPages };
        for (var page = Math.Max(1, CurrentPage - 1); page <= Math.Min(TotalPages, CurrentPage + 1); page++)
        {
            pagesToShow.Add(page);
        }

        var previousPage = 0;
        foreach (var page in pagesToShow)
        {
            if (previousPage > 0 && page - previousPage > 1)
            {
                PageNumbers.Add(new PageItem
                {
                    DisplayText = "...",
                    IsEllipsis = true,
                    PageNumber = -1
                });
            }

            PageNumbers.Add(new PageItem
            {
                DisplayText = page.ToString(),
                PageNumber = page,
                IsCurrent = page == CurrentPage
            });

            previousPage = page;
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
    public long FileSizeBytes { get; }

    public string FileName => Path.GetFileName(FilePath);
    public string CreatedAtDisplay => CreatedAt.ToString(BtfxConstants.DATETIME_FORMAT);

    public string FileSizeDisplay
    {
        get
        {
            try
            {
                if (FileSizeBytes > 0)
                {
                    return $"{FileSizeBytes / 1024.0:F2} KB";
                }

                if (File.Exists(FilePath))
                {
                    var fileInfo = new FileInfo(FilePath);
                    return $"{fileInfo.Length / 1024.0:F2} KB";
                }
            }
            catch { }
            return "--";
        }
    }

    public BackupHistoryItem(string filePath, DateTime createdAt, int rowNumber, long fileSizeBytes)
    {
        FilePath = filePath;
        CreatedAt = createdAt;
        RowNumber = rowNumber;
        FileSizeBytes = fileSizeBytes;
    }
}
