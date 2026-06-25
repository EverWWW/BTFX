using System.Collections.ObjectModel;
using System.Windows;
using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using BTFX.ViewModels.Measurement;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolHelper.LoggingDiagnostics.Abstractions;
using DialogHost = MaterialDesignThemes.Wpf.DialogHost;

namespace BTFX.ViewModels;

/// <summary>
/// 数据管理视图模型
/// </summary>
public partial class DataManagementViewModel : ObservableObject, IDisposable
{
    private readonly IMeasurementService _measurementService;
    private readonly ISessionService _sessionService;
    private readonly ILocalizationService _localizationService;
    private readonly IExportImportService _exportImportService;
    private readonly IMeasurementWorkflowResumeService _measurementWorkflowResumeService;
    private readonly IMeasurementWorkflowCoordinator _measurementWorkflowCoordinator;
    private readonly ILogHelper? _logHelper;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private volatile bool _disposed;

    /// <summary>
    /// 全局选中的记录ID集合（跨页面持久化）
    /// </summary>
    private readonly HashSet<int> _globalSelectedIds = new();

    #region 筛选条件

    /// <summary>
    /// 患者姓名筛选
    /// </summary>
    [ObservableProperty]
    private string _filterPatientName = string.Empty;

    /// <summary>
    /// 开始日期筛选
    /// </summary>
    [ObservableProperty]
    private DateTime? _filterStartDate;

    /// <summary>
    /// 结束日期筛选
    /// </summary>
    [ObservableProperty]
    private DateTime? _filterEndDate;

    /// <summary>
    /// 状态筛选
    /// </summary>
    [ObservableProperty]
    private MeasurementStatus? _filterStatus;

    /// <summary>
    /// 最大日期（今天）
    /// </summary>
    public DateTime MaxDate => DateTime.Today;

    /// <summary>
    /// 状态选项列表
    /// </summary>
    public ObservableCollection<StatusOption> StatusOptions { get; } = new();

    /// <summary>
    /// 选中的状态选项
    /// </summary>
    [ObservableProperty]
    private StatusOption? _selectedStatusOption;

    #endregion

    #region 数据列表

    /// <summary>
    /// 测量记录列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<MeasurementRecordItem> _measurementRecords = new();

    /// <summary>
    /// 选中的记录
    /// </summary>
    [ObservableProperty]
    private MeasurementRecordItem? _selectedRecord;

    /// <summary>
    /// 是否全选
    /// </summary>
    private bool _isAllSelected;

    public bool IsAllSelected
    {
        get => _isAllSelected;
        private set => SetProperty(ref _isAllSelected, value);
    }

    /// <summary>
    /// 全选状态：0=未选，1=部分选，2=全选。
    /// </summary>
    public int SelectAllState
    {
        get
        {
            if (MeasurementRecords.Count == 0) return 0;

            var currentPageSelectedCount = MeasurementRecords.Count(r => r.IsSelected);
            if (currentPageSelectedCount == 0) return 0;
            return currentPageSelectedCount == MeasurementRecords.Count ? 2 : 1;
        }
    }

    /// <summary>
    /// 是否正在更新选中状态（防止循环触发）
    /// </summary>
    private bool _isUpdatingSelection = false;

    #endregion

    #region 分页

    /// <summary>
    /// 当前页码
    /// </summary>
    [ObservableProperty]
    private int _currentPage = 1;

    /// <summary>
    /// 总页数
    /// </summary>
    [ObservableProperty]
    private int _totalPages = 1;

    /// <summary>
    /// 总记录数
    /// </summary>
    [ObservableProperty]
    private int _totalRecords;

    /// <summary>
    /// 已选记录数
    /// </summary>
    [ObservableProperty]
    private int _selectedCount;

    public string SelectedCountText => L("DataManagement.SelectedCountFormat", SelectedCount);

    /// <summary>
    /// 分页页码集合
    /// </summary>
    private ObservableCollection<PageItem> _dataPageNumbers = new();

    public ObservableCollection<PageItem> DataPageNumbers
    {
        get => _dataPageNumbers;
        set => SetProperty(ref _dataPageNumbers, value);
    }

    /// <summary>
    /// 是否允许上一页
    /// </summary>
    private bool _canPagePrevious;

    public bool CanPagePrevious
    {
        get => _canPagePrevious;
        set => SetProperty(ref _canPagePrevious, value);
    }

    /// <summary>
    /// 是否允许下一页
    /// </summary>
    private bool _canPageNext;

    public bool CanPageNext
    {
        get => _canPageNext;
        set => SetProperty(ref _canPageNext, value);
    }

    /// <summary>
    /// 跳转页码输入
    /// </summary>
    [ObservableProperty]
    private string _goToPageInput = "1";

    /// <summary>
    /// 每页记录数
    /// </summary>
    private const int MaxPageSize = 7;
    private const int MinimumPageSize = 1;
    private const double RowHeight = 60d;
    private const double RowSpacing = 8d;
    private int _pageSize = MaxPageSize;

    #endregion

    #region 权限

    /// <summary>
    /// 是否可导出
    /// </summary>
    [ObservableProperty]
    private bool _canExport;

    /// <summary>
    /// 是否可删除
    /// </summary>
    [ObservableProperty]
    private bool _canDelete;

    /// <summary>
    /// 是否可批量操作
    /// </summary>
    [ObservableProperty]
    private bool _canBatchOperation;

    #endregion

    #region 加载状态

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    #endregion

    /// <summary>
    /// 构造函数
    /// </summary>
    public DataManagementViewModel(
        IMeasurementService measurementService,
        ISessionService sessionService,
        ILocalizationService localizationService,
        IExportImportService exportImportService,
        IMeasurementWorkflowResumeService measurementWorkflowResumeService,
        IMeasurementWorkflowCoordinator measurementWorkflowCoordinator)
    {
        _measurementService = measurementService;
        _sessionService = sessionService;
        _localizationService = localizationService;
        _exportImportService = exportImportService;
        _measurementWorkflowResumeService = measurementWorkflowResumeService;
        _measurementWorkflowCoordinator = measurementWorkflowCoordinator;
        _localizationService.LanguageChanged += OnLanguageChanged;

        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }

        // 设置权限
        InitializePermissions();

        // 设置默认状态选项
        RebuildStatusOptions();
        SelectedStatusOption = StatusOptions.First();

        // 加载数据
        _ = LoadDataAsync();
    }

    private string L(string key)
    {
        var value = _localizationService.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private string L(string key, params object[] args)
    {
        var value = _localizationService.GetString(key, args);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private void OnLanguageChanged(object? sender, AppLanguage language)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var selectedValue = SelectedStatusOption?.Value;
            RebuildStatusOptions();
            SelectedStatusOption = StatusOptions.FirstOrDefault(option => option.Value == selectedValue) ?? StatusOptions.FirstOrDefault();

            foreach (var item in MeasurementRecords)
            {
                item.RefreshLocalization();
            }

            OnPropertyChanged(nameof(SelectedCountText));
        });
    }

    private void RebuildStatusOptions()
    {
        StatusOptions.Clear();
        StatusOptions.Add(new StatusOption { Value = null, Display = L("DataManagement.Status.All") });
        StatusOptions.Add(new StatusOption { Value = MeasurementStatus.Pending, Display = L("DataManagement.Status.Pending") });
        StatusOptions.Add(new StatusOption { Value = MeasurementStatus.InProgress, Display = L("DataManagement.Status.InProgress") });
        StatusOptions.Add(new StatusOption { Value = MeasurementStatus.Completed, Display = L("DataManagement.Status.Completed") });
        StatusOptions.Add(new StatusOption { Value = MeasurementStatus.Failed, Display = L("DataManagement.Status.Failed") });
    }

    /// <summary>
    /// 初始化权限
    /// </summary>
    private void InitializePermissions()
    {
        CanExport = _sessionService.HasPermission("export");
        CanDelete = _sessionService.HasPermission("deletemeasurement");
        CanBatchOperation = CanExport || CanDelete;
    }

    /// <summary>
    /// 加载数据
    /// </summary>
    private async Task LoadDataAsync()
    {
        if (_disposed) return;

        try
        {
            Application.Current?.Dispatcher?.Invoke(() => IsLoading = true);

            var (records, totalCount) = await _measurementService.GetMeasurementsPagedAsync(
                FilterPatientName,
                FilterStartDate,
                FilterEndDate,
                SelectedStatusOption?.Value,
                CurrentPage,
                _pageSize);

            var correctedTotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)_pageSize));
            if (CurrentPage > correctedTotalPages)
            {
                CurrentPage = correctedTotalPages;
                (records, totalCount) = await _measurementService.GetMeasurementsPagedAsync(
                    FilterPatientName,
                    FilterStartDate,
                    FilterEndDate,
                    SelectedStatusOption?.Value,
                    CurrentPage,
                    _pageSize);
            }

            if (_cancellationTokenSource.Token.IsCancellationRequested) return;

            // 在UI线程更新属性和集合
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            await dispatcher.InvokeAsync(() =>
            {
                if (_disposed) return;

                TotalRecords = totalCount;
                TotalPages = (int)Math.Ceiling(totalCount / (double)_pageSize);
                if (TotalPages < 1) TotalPages = 1;

                // 转换为视图项
                MeasurementRecords.Clear();
                int rowNumber = (CurrentPage - 1) * _pageSize + 1;
                foreach (var record in records)
                {
                    var item = new MeasurementRecordItem(record, rowNumber++);
                    item.SetLocalizationService(_localizationService);

                    // 恢复之前的选中状态
                    if (_globalSelectedIds.Contains(record.Id))
                    {
                        item.IsSelected = true;
                    }

                    MeasurementRecords.Add(item);
                }

                // 更新选中状态
                UpdateSelectionState();
                BuildPageNumbers();
            });

            _logHelper?.Information($"加载测量数据：第{CurrentPage}页，共{TotalRecords}条，已选中{_globalSelectedIds.Count}条");
        }
        catch (OperationCanceledException)
        {
            // 操作被取消，忽略
        }
        catch (Exception ex)
        {
            if (!_disposed)
            {
                _logHelper?.Error("加载测量数据失败", ex);
            }
        }
        finally
        {
            if (!_disposed)
            {
                try
                {
                    Application.Current?.Dispatcher?.Invoke(() => IsLoading = false);
                }
                catch
                {
                    // 忽略关闭时的异常
                }
            }
        }
    }

    /// <summary>
    /// 构建分页页码项集合（支持省略号）
    /// </summary>
    private void BuildPageNumbers()
    {
        DataPageNumbers.Clear();
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
                DataPageNumbers.Add(new PageItem
                {
                    DisplayText = "...",
                    IsEllipsis = true,
                    PageNumber = -1
                });
            }

            DataPageNumbers.Add(new PageItem
            {
                DisplayText = page.ToString(),
                PageNumber = page,
                IsCurrent = page == CurrentPage
            });

            previousPage = page;
        }

        CanPagePrevious = CurrentPage > 1;
        CanPageNext = CurrentPage < TotalPages;
    }

    /// <summary>
    /// 根据列表可视高度更新每页容量，最大不超过 7 条。
    /// </summary>
    /// <param name="viewportHeight">列表内容区可视高度。</param>
    public async Task UpdatePageSizeAsync(double viewportHeight)
    {
        if (viewportHeight <= 0 || _disposed)
        {
            return;
        }

        var effectiveViewportHeight = Math.Min(viewportHeight, MaxPageSize * (RowHeight + RowSpacing) - RowSpacing);
        var rowFullHeight = RowHeight + RowSpacing;
        var calculatedPageSize = Math.Max(MinimumPageSize, (int)Math.Floor((effectiveViewportHeight + RowSpacing) / rowFullHeight));
        var newPageSize = Math.Min(MaxPageSize, calculatedPageSize);

        if (newPageSize == _pageSize)
        {
            return;
        }

        _pageSize = newPageSize;

        var recalculatedTotalPages = TotalRecords <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)_pageSize));
        if (CurrentPage > recalculatedTotalPages)
        {
            CurrentPage = recalculatedTotalPages;
        }

        GoToPageInput = CurrentPage.ToString();
        await LoadDataAsync();
    }

    /// <summary>
    /// 更新选中状态
    /// </summary>
    private void UpdateSelectionState()
    {
        if (_isUpdatingSelection) return;

        try
        {
            _isUpdatingSelection = true;

            // 更新全局选中数量（显示所有页面的选中总数）
            SelectedCount = _globalSelectedIds.Count;

            // 全选状态：当前页所有项都选中
            var newIsAllSelected = MeasurementRecords.Count > 0 && MeasurementRecords.All(r => r.IsSelected);

            // 只在值真正改变时更新，避免触发不必要的通知
            if (_isAllSelected != newIsAllSelected)
            {
                _isAllSelected = newIsAllSelected;
                OnPropertyChanged(nameof(IsAllSelected));
            }

            OnPropertyChanged(nameof(SelectAllState));
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    #region 日期联动逻辑

    partial void OnFilterStartDateChanged(DateTime? value)
    {
        // 如果开始日期大于结束日期，修正结束日期
        if (value.HasValue && FilterEndDate.HasValue && value.Value > FilterEndDate.Value)
        {
            FilterEndDate = value;
        }
    }

    partial void OnFilterEndDateChanged(DateTime? value)
    {
        // 如果结束日期小于开始日期，修正开始日期
        if (value.HasValue && FilterStartDate.HasValue && value.Value < FilterStartDate.Value)
        {
            FilterStartDate = value;
        }
    }

    #endregion

    #region 命令

    /// <summary>
    /// 搜索命令
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1;
        FilterStatus = SelectedStatusOption?.Value;

        // 搜索时清空选中状态
        _globalSelectedIds.Clear();

        await LoadDataAsync();
        _logHelper?.Information($"搜索测量数据：姓名={FilterPatientName}, 状态={FilterStatus}");
    }

    /// <summary>
    /// 重置筛选条件命令
    /// </summary>
    [RelayCommand]
    private async Task ResetFilterAsync()
    {
        FilterPatientName = string.Empty;
        FilterStartDate = null;
        FilterEndDate = null;
        SelectedStatusOption = StatusOptions.First();

        // 重置时清空选中状态
        _globalSelectedIds.Clear();
        FilterStatus = null;
        CurrentPage = 1;
        await LoadDataAsync();
        _logHelper?.Information("重置筛选条件");
    }

    /// <summary>
    /// 查看详情命令
    /// </summary>
    [RelayCommand]
    private async Task ViewDetailAsync(MeasurementRecordItem? item)
    {
        if (item == null || !item.CanViewDetail) return;

        try
        {
            var viewModel = App.Services?.GetService(typeof(GaitAnalysisDetailViewModel)) as GaitAnalysisDetailViewModel;

            if (viewModel != null)
            {
                await viewModel.InitializeAsync(item.Record);
                var dialog = new Views.Dialogs.MeasurementDetailDialog();
                dialog.DataContext = viewModel;
                await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");
            }

            _logHelper?.Information($"查看分析详情：ID={item.Record.Id}");
        }
            catch (Exception ex)
            {
                _logHelper?.Error($"打开详情对话框失败：ID={item.Record.Id}", ex);
                MessageBox.Show(_localizationService.GetString("DataManagement.OpenDetailFailedFormat", ex.Message), _localizationService.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    /// <summary>
    /// 根据测量状态继续处理当前记录。
    /// </summary>
    [RelayCommand]
    private async Task ResumeMeasurementAsync(MeasurementRecordItem? item)
    {
        if (item == null) return;

        try
        {
            if (MergeActiveMeasurementState(item.Record))
            {
                await _measurementService.UpdateMeasurementAsync(item.Record);
            }

            var decision = await _measurementWorkflowResumeService.DecideAsync(item.Record, _cancellationTokenSource.Token);
            if (!decision.CanResume)
            {
                MessageBox.Show(decision.Message, _localizationService.GetString("Tip"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _measurementWorkflowCoordinator.RequestResume(item.Record, decision);
            _logHelper?.Information($"继续处理测量：ID={item.Record.Id}, Step={decision.TargetStep}, Action={decision.ActionText}");
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"继续处理测量失败：ID={item.Record.Id}", ex);
            MessageBox.Show(_localizationService.GetString("DataManagement.ResumeFailedFormat", ex.Message), _localizationService.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    partial void OnSelectedCountChanged(int value)
    {
        OnPropertyChanged(nameof(SelectedCountText));
    }

    private static bool MergeActiveMeasurementState(MeasurementRecord record)
    {
        var measurementViewModel = App.Services?.GetService(typeof(MeasurementViewModel)) as MeasurementViewModel;
        var activeMeasurement = measurementViewModel?.CurrentMeasurement;
        if (activeMeasurement?.Id != record.Id)
        {
            return false;
        }

        var changed = false;

        record.MeasurementName = string.IsNullOrWhiteSpace(record.MeasurementName)
            ? activeMeasurement.MeasurementName
            : record.MeasurementName;
        record.MeasurementType = activeMeasurement.MeasurementType;
        record.Remark = string.IsNullOrWhiteSpace(record.Remark)
            ? activeMeasurement.Remark
            : record.Remark;
        record.VideoSpec = activeMeasurement.VideoSpec;
        record.WalkwayLength = activeMeasurement.WalkwayLength;
        record.VideoImportMode = activeMeasurement.VideoImportMode;
        record.ImportStrategy = activeMeasurement.ImportStrategy;

        if (string.IsNullOrWhiteSpace(record.FrontVideoPath))
        {
            record.FrontVideoPath = activeMeasurement.FrontVideoPath;
            changed = !string.IsNullOrWhiteSpace(record.FrontVideoPath);
        }

        if (string.IsNullOrWhiteSpace(record.SideVideoPath))
        {
            record.SideVideoPath = activeMeasurement.SideVideoPath;
            changed = changed || !string.IsNullOrWhiteSpace(record.SideVideoPath);
        }

        return changed;
    }

        /// <summary>
        /// 导出单条命令
        /// </summary>
        [RelayCommand]
        private async Task ExportSingleAsync(MeasurementRecordItem? item)
        {
            if (item == null || !CanExport || !item.CanExportResult) return;

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = _localizationService.GetString("DataManagement.ExportArchive.Title"),
                    Filter = _localizationService.GetString("DataManagement.ArchiveFilter"),
                    FileName = _localizationService.GetString(
                        "DataManagement.ArchiveFileNameFormat",
                        item.Record.Patient?.Name ?? "--",
                        item.Record.MeasurementDate)
                };

                if (dialog.ShowDialog() == true)
                {
                    var result = await RunWithProgressDialogAsync(
                        _localizationService.GetString("DataManagement.ExportArchive.Title"),
                        _localizationService.GetString("DataManagement.ExportArchive.Stage"),
                        _localizationService.GetString("DataManagement.ExportArchive.Message"),
                        (progress, token) => _exportImportService.ExportMeasurementArchiveAsync(
                            new List<MeasurementRecord> { item.Record },
                            dialog.FileName,
                            progress,
                            token));

                    if (result.Success)
                    {
                        System.Windows.MessageBox.Show(result.Message, _localizationService.GetString("Tip"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                        _logHelper?.Information($"导出测量结果包：ID={item.Record.Id}, 文件={dialog.FileName}");
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(result.Message, _localizationService.GetString("Error"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Windows.MessageBox.Show(_localizationService.GetString("DataManagement.ExportCanceled"), _localizationService.GetString("Tip"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logHelper?.Error($"导出测量结果包失败：ID={item.Record.Id}", ex);
                System.Windows.MessageBox.Show($"{_localizationService.GetString("ExportFailed")}: {ex.Message}", _localizationService.GetString("Error"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除单条命令
        /// </summary>
    [RelayCommand]
    private async Task DeleteSingleAsync(MeasurementRecordItem? item)
    {
        if (item == null || !CanDelete) return;

        var result = await ShowConfirmDialogAsync(
            _localizationService.GetString("DataManagement.DeleteConfirmTitle"),
            _localizationService.GetString("DataManagement.DeleteConfirmMessage", item.Record.Patient?.Name ?? "--"),
            "TrashCanOutline");

        if (!result) return;

        try
        {
            var success = await _measurementService.DeleteMeasurementAsync(item.Record.Id);
            if (success)
            {
                _globalSelectedIds.Remove(item.Record.Id);
                await LoadDataAsync();
                _logHelper?.Information($"删除测量记录：ID={item.Record.Id}");
                await ShowNoticeDialogAsync(_localizationService.GetString("Information"), _localizationService.GetString("DeleteSuccess"));
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除测量记录失败：ID={item.Record.Id}", ex);
            await ShowNoticeDialogAsync(_localizationService.GetString("Error"), $"{_localizationService.GetString("DeleteFailed")}: {ex.Message}");
        }
    }

    /// <summary>
    /// 全选/取消全选命令
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        if (_isUpdatingSelection) return;

        try
        {
            _isUpdatingSelection = true;

            // 注意：IsAllSelected 在此时已经被UI更新了
            // 所以这里直接使用 IsAllSelected 的新值即可
            var shouldSelectAll = SelectAllState != 2;

            foreach (var item in MeasurementRecords)
            {
                item.IsSelected = shouldSelectAll;

                // 更新全局选中ID集合
                if (shouldSelectAll)
                {
                    _globalSelectedIds.Add(item.Record.Id);
                }
                else
                {
                    _globalSelectedIds.Remove(item.Record.Id);
                }
            }

            // 更新选中数量
            SelectedCount = _globalSelectedIds.Count;
            IsAllSelected = shouldSelectAll;
            OnPropertyChanged(nameof(SelectAllState));
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    /// <summary>
    /// 项目选中状态改变（由单个复选框触发）
    /// </summary>
    /// <param name="item">变化的项</param>
    public void OnItemSelectionChanged(MeasurementRecordItem item)
    {
        if (_isUpdatingSelection) return;

        try
        {
            _isUpdatingSelection = true;

            // 同步全局选中ID集合
            if (item.IsSelected)
            {
                _globalSelectedIds.Add(item.Record.Id);
            }
            else
            {
                _globalSelectedIds.Remove(item.Record.Id);
            }

            // 更新选中数量
            SelectedCount = _globalSelectedIds.Count;

            // 更新全选状态
            var newIsAllSelected = MeasurementRecords.Count > 0 && MeasurementRecords.All(r => r.IsSelected);
            if (_isAllSelected != newIsAllSelected)
            {
                _isAllSelected = newIsAllSelected;
                OnPropertyChanged(nameof(IsAllSelected));
            }

            OnPropertyChanged(nameof(SelectAllState));
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    /// <summary>
    /// 项目选中状态改变命令（保留用于兼容）
    /// </summary>
    [RelayCommand]
    private void ItemSelectionChanged()
    {
        if (_isUpdatingSelection) return;

        UpdateSelectionState();
    }

    /// <summary>
    /// 批量导出命令
    /// </summary>
    [RelayCommand]
    private async Task BatchExportAsync()
    {
        if (!CanExport) return;

        if (_globalSelectedIds.Count == 0)
        {
            System.Windows.MessageBox.Show(_localizationService.GetString("DataManagement.SelectExportFirst"), _localizationService.GetString("Tip"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = _localizationService.GetString("DataManagement.ExportArchive.BatchTitle"),
                Filter = _localizationService.GetString("DataManagement.ArchiveFilterBatch"),
                FileName = _localizationService.GetString("DataManagement.BatchArchiveFileNameFormat", DateTime.Now)
            };

            if (dialog.ShowDialog() == true)
            {
                // 获取所有选中的记录（包括其他页面的）
                var allRecords = await _measurementService.GetMeasurementsByIdsAsync(_globalSelectedIds.ToList());

                var result = await RunWithProgressDialogAsync(
                    _localizationService.GetString("DataManagement.ExportArchive.BatchTitle"),
                    _localizationService.GetString("DataManagement.ExportArchive.Stage"),
                    _localizationService.GetString("DataManagement.ExportArchive.BatchMessage"),
                    (progress, token) => _exportImportService.ExportMeasurementArchiveAsync(
                        allRecords,
                        dialog.FileName,
                        progress,
                        token));

                if (result.Success)
                {
                    System.Windows.MessageBox.Show(result.Message, _localizationService.GetString("Tip"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    _logHelper?.Information($"批量导出测量结果包：{_globalSelectedIds.Count}条, 文件={dialog.FileName}");
                }
                else
                {
                    System.Windows.MessageBox.Show(result.Message, _localizationService.GetString("Error"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
        catch (OperationCanceledException)
        {
            System.Windows.MessageBox.Show(_localizationService.GetString("DataManagement.BatchExportCanceled"), _localizationService.GetString("Tip"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"批量导出测量数据失败", ex);
            System.Windows.MessageBox.Show($"{_localizationService.GetString("ExportFailed")}: {ex.Message}", _localizationService.GetString("Error"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        }

    /// <summary>
    /// 导入测量结果包
    /// </summary>
    [RelayCommand]
    private async Task ImportMeasurementArchiveAsync()
    {
        if (!CanExport) return;

        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = _localizationService.GetString("DataManagement.ImportArchive.Title"),
                Filter = _localizationService.GetString("DataManagement.ArchiveFilterWithZip")
            };

            if (dialog.ShowDialog(Application.Current.MainWindow) != true)
            {
                return;
            }

            var result = await RunWithProgressDialogAsync(
                _localizationService.GetString("DataManagement.ImportArchive.Title"),
                _localizationService.GetString("DataManagement.ImportArchive.Stage"),
                _localizationService.GetString("DataManagement.ImportArchive.Message"),
                (progress, token) => _exportImportService.ImportMeasurementArchiveAsync(
                    dialog.FileName,
                    progress,
                    token));

            if (result.Success)
            {
                _globalSelectedIds.Clear();
                SelectedCount = 0;
                await LoadDataAsync();
                System.Windows.MessageBox.Show(result.Message, _localizationService.GetString("Tip"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                _logHelper?.Information($"导入测量结果包成功：{dialog.FileName}, Count={result.ImportedCount}");
            }
            else
            {
                System.Windows.MessageBox.Show(result.Message, _localizationService.GetString("Error"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        catch (OperationCanceledException)
        {
            System.Windows.MessageBox.Show(_localizationService.GetString("DataManagement.ImportCanceled"), _localizationService.GetString("Tip"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logHelper?.Error("导入测量结果包失败", ex);
            System.Windows.MessageBox.Show($"{_localizationService.GetString("ImportFailed")}: {ex.Message}", _localizationService.GetString("Error"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            RestoreMainWindowIfMinimized();
        }
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
            var result = await Task.Run(() => operation(progress, operationCts.Token), operationCts.Token);
            progressViewModel.MarkCompleted(GetGlobalString("OperationProgress.CompletedMessage"));
            await Task.Delay(650);
            DialogHost.Close("RootDialog");
            await dialogTask;
            RestoreMainWindowIfMinimized();
            return result;
        }
        catch (OperationCanceledException)
        {
            progressViewModel.MarkFailed(GetGlobalString("OperationProgress.CanceledMessage"));
            await Task.Delay(350);
            DialogHost.Close("RootDialog");
            await dialogTask;
            RestoreMainWindowIfMinimized();
            throw;
        }
        catch
        {
            progressViewModel.MarkFailed(GetGlobalString("OperationProgress.FailedMessage"));
            await Task.Delay(350);
            DialogHost.Close("RootDialog");
            await dialogTask;
            RestoreMainWindowIfMinimized();
            throw;
        }
    }

    private static string GetGlobalString(string key)
    {
        try
        {
            return Application.Current.FindResource(key)?.ToString() ?? key;
        }
        catch
        {
            return key;
        }
    }

    private static void RestoreMainWindowIfMinimized()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.BeginInvoke(new Action(async () =>
        {
            RestoreMainWindowNow();
            await Task.Delay(300);
            RestoreMainWindowNow();
            await Task.Delay(900);
            RestoreMainWindowNow();
        }));
    }

    private static void RestoreMainWindowNow()
    {
        var window = Application.Current?.MainWindow;
        if (window is null)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        if (!window.IsActive)
        {
            window.Activate();
        }
    }

        /// <summary>
    /// 批量删除命令
    /// </summary>
    [RelayCommand]
    private async Task BatchDeleteAsync()
    {
        if (!CanDelete) return;

        if (_globalSelectedIds.Count == 0)
        {
            await ShowNoticeDialogAsync(
                _localizationService.GetString("Information"),
                _localizationService.GetString("DataManagement.SelectDeleteFirst"));
            return;
        }

        var result = await ShowConfirmDialogAsync(
            _localizationService.GetString("DataManagement.BatchDeleteConfirmTitle"),
            _localizationService.GetString("DataManagement.BatchDeleteConfirmMessage", _globalSelectedIds.Count),
            "TrashCanOutline");

        if (!result) return;

        try
        {
            var ids = _globalSelectedIds.ToList();
            var count = await _measurementService.DeleteMeasurementsAsync(ids);
            _globalSelectedIds.Clear();
            await LoadDataAsync();
            _logHelper?.Information($"批量删除测量记录：{count}条");
            await ShowNoticeDialogAsync(_localizationService.GetString("Information"), _localizationService.GetString("DeleteSuccess"));
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"批量删除测量记录失败", ex);
            await ShowNoticeDialogAsync(_localizationService.GetString("Error"), $"{_localizationService.GetString("DeleteFailed")}: {ex.Message}");
        }
    }

    private async Task<bool> ShowConfirmDialogAsync(string title, string message, string iconKind = "HelpCircleOutline")
    {
        var result = await DialogHost.Show(
            new Views.Dialogs.ConfirmDialog
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
            new Views.Dialogs.ConfirmDialog
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

    /// <summary>
    /// 上一页命令
    /// </summary>
    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadDataAsync();
        }
    }

    /// <summary>
    /// 下一页命令
    /// </summary>
    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadDataAsync();
        }
    }

    /// <summary>
    /// 跳转页命令
    /// </summary>
    [RelayCommand]
    private async Task GoToPageAsync()
    {
        if (int.TryParse(GoToPageInput, out int page) && page >= 1 && page <= TotalPages)
        {
            CurrentPage = page;
            await LoadDataAsync();
        }
        else
        {
            GoToPageInput = CurrentPage.ToString();
        }
    }

    /// <summary>
    /// 页码跳转命令
    /// </summary>
    [RelayCommand]
    private async Task GoToPageNumberAsync(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > TotalPages || pageNumber == CurrentPage)
        {
            return;
        }

        CurrentPage = pageNumber;
        GoToPageInput = pageNumber.ToString();
        await LoadDataAsync();
    }

        /// <summary>
        /// 刷新命令
        /// </summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadDataAsync();
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _localizationService.LanguageChanged -= OnLanguageChanged;
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            _logHelper?.Information("DataManagementViewModel disposed");
        }

        #endregion
    }

    /// <summary>
    /// 测量记录项（包含行号和选中状态）
    /// </summary>
public partial class MeasurementRecordItem : ObservableObject
{
    private ILocalizationService? _localizationService;

    /// <summary>
    /// 测量记录
    /// </summary>
    public MeasurementRecord Record { get; }

    /// <summary>
    /// 行号
    /// </summary>
    public int RowNumber { get; }

    /// <summary>
    /// 是否选中
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// 患者姓名
    /// </summary>
    public string PatientName => Record.Patient?.Name ?? "--";

    /// <summary>
    /// 性别显示
    /// </summary>
    public string GenderDisplay => Record.Patient?.GenderDisplay ?? "--";

    /// <summary>
    /// 年龄
    /// </summary>
    public string AgeDisplay => Record.Patient?.Age?.ToString() ?? "--";

    /// <summary>
    /// 测量日期显示
    /// </summary>
    public string MeasurementDateDisplay => Record.MeasurementDate.ToString(Constants.DATETIME_LIST_FORMAT);

    /// <summary>
    /// 状态显示
    /// </summary>
    public string StatusDisplay => Record.Status switch
    {
        MeasurementStatus.Pending => L("DataManagement.Status.Pending"),
        MeasurementStatus.InProgress => L("DataManagement.Status.InProgress"),
        MeasurementStatus.Completed => L("DataManagement.Status.Completed"),
        MeasurementStatus.Cancelled => L("DataManagement.Status.Pending"),
        MeasurementStatus.Failed => L("DataManagement.Status.Failed"),
        _ => "--"
    };

    /// <summary>
    /// 状态图标
    /// </summary>
    public string StatusIcon => Record.Status switch
    {
        MeasurementStatus.Completed => "/Resources/Images/DataManagement/yiwancheng.png",
        _ => "/Resources/Images/DataManagement/daichuli.png"
    };

    /// <summary>
    /// 状态背景色
    /// </summary>
    public string StatusBackground => Record.Status switch
    {
        MeasurementStatus.Completed => "#E9F7E3",
        MeasurementStatus.InProgress => "#EAF3FF",
        MeasurementStatus.Failed => "#FDECEC",
        _ => "#FDF4E6"
    };

    /// <summary>
    /// 状态前景色
    /// </summary>
    public string StatusForeground => Record.Status switch
    {
        MeasurementStatus.Completed => "#44BE13",
        MeasurementStatus.InProgress => "#2F80ED",
        MeasurementStatus.Failed => "#E4004A",
        _ => "#FF932D"
    };

    /// <summary>
    /// 状态颜色
    /// </summary>
    public string StatusColor => Record.Status switch
    {
        MeasurementStatus.Pending => "#FF9800",
        MeasurementStatus.InProgress => "#2196F3",
        MeasurementStatus.Completed => "#4CAF50",
        MeasurementStatus.Cancelled => "#FF9800",
        MeasurementStatus.Failed => "#F44336",
        _ => "#9E9E9E"
    };

    public string PrimaryActionText => Record.Status switch
    {
        MeasurementStatus.Pending => L("DataManagement.Action.ContinueProcess"),
        MeasurementStatus.InProgress => L("DataManagement.Action.ViewProgress"),
        MeasurementStatus.Completed => L("DataManagement.Action.ViewResult"),
        MeasurementStatus.Failed => L("DataManagement.Action.Reanalyze"),
        _ => L("DataManagement.Action.ContinueProcess")
    };

    public bool HasCompletedAnalysis => Record.Status == MeasurementStatus.Completed;

    public bool CanViewDetail => HasCompletedAnalysis;

    public bool CanExportResult => HasCompletedAnalysis;

    /// <summary>
    /// 构造函数
    /// </summary>
    public MeasurementRecordItem(MeasurementRecord record, int rowNumber)
    {
        Record = record;
        RowNumber = rowNumber;
    }

    public void SetLocalizationService(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(GenderDisplay));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(PrimaryActionText));
    }

    private string L(string key)
    {
        var value = _localizationService?.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }
}

/// <summary>
/// 状态选项
/// </summary>
public class StatusOption
{
    /// <summary>
    /// 状态值
    /// </summary>
    public MeasurementStatus? Value { get; set; }

    /// <summary>
    /// 显示文本
    /// </summary>
    public string Display { get; set; } = string.Empty;
}

