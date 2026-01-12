using System.Collections.ObjectModel;
using System.Windows;
using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolHelper.LoggingDiagnostics.Abstractions;

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
    public ObservableCollection<StatusOption> StatusOptions { get; } = new()
    {
        new StatusOption { Value = null, Display = "全部" },
        new StatusOption { Value = MeasurementStatus.Pending, Display = "待处理" },
        new StatusOption { Value = MeasurementStatus.InProgress, Display = "进行中" },
        new StatusOption { Value = MeasurementStatus.Completed, Display = "已完成" },
        new StatusOption { Value = MeasurementStatus.Cancelled, Display = "已取消" },
        new StatusOption { Value = MeasurementStatus.Failed, Display = "测量失败" }
    };

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
        set => SetProperty(ref _isAllSelected, value);
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

    /// <summary>
    /// 跳转页码输入
    /// </summary>
    [ObservableProperty]
    private string _goToPageInput = "1";

    /// <summary>
    /// 每页记录数
    /// </summary>
    private const int PageSize = Constants.DEFAULT_PAGE_SIZE;

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
        IExportImportService exportImportService)
    {
        _measurementService = measurementService;
        _sessionService = sessionService;
        _localizationService = localizationService;
        _exportImportService = exportImportService;

        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }

        // 设置权限
        InitializePermissions();

        // 设置默认状态选项
        SelectedStatusOption = StatusOptions.First();

        // 加载数据
        _ = LoadDataAsync();
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
                PageSize);

            if (_cancellationTokenSource.Token.IsCancellationRequested) return;

            // 在UI线程更新属性和集合
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_disposed) return;

                TotalRecords = totalCount;
                TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
                if (TotalPages < 1) TotalPages = 1;

                // 确保当前页有效
                if (CurrentPage > TotalPages)
                {
                    CurrentPage = TotalPages;
                }

                // 转换为视图项
                MeasurementRecords.Clear();
                int rowNumber = (CurrentPage - 1) * PageSize + 1;
                foreach (var record in records)
                {
                    var item = new MeasurementRecordItem(record, rowNumber++);

                    // 恢复之前的选中状态
                    if (_globalSelectedIds.Contains(record.Id))
                    {
                        item.IsSelected = true;
                    }

                    MeasurementRecords.Add(item);
                }

                // 更新选中状态
                UpdateSelectionState();
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
        if (item == null) return;

        try
        {
            var dialog = App.Services?.GetService(typeof(Views.Dialogs.MeasurementDetailDialog)) as Views.Dialogs.MeasurementDetailDialog;
            var viewModel = App.Services?.GetService(typeof(MeasurementDetailViewModel)) as MeasurementDetailViewModel;

            if (dialog != null && viewModel != null)
            {
                viewModel.Initialize(item.Record);
                dialog.DataContext = viewModel;
                dialog.ShowDialog();
            }

            _logHelper?.Information($"查看测量详情：ID={item.Record.Id}");
        }
            catch (Exception ex)
            {
                _logHelper?.Error($"打开详情对话框失败：ID={item.Record.Id}", ex);
            }
        }

        /// <summary>
        /// 导出单条命令
        /// </summary>
        [RelayCommand]
        private async Task ExportSingleAsync(MeasurementRecordItem? item)
        {
            if (item == null || !CanExport) return;

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "导出测量数据",
                    Filter = "Excel文件 (*.xlsx)|*.xlsx|CSV文件 (*.csv)|*.csv",
                    FileName = $"测量数据_{item.Record.Patient?.Name}_{item.Record.MeasurementDate:yyyyMMdd}"
                };

                if (dialog.ShowDialog() == true)
                {
                    var format = dialog.FilterIndex == 1 ? ExportFormat.Excel : ExportFormat.CSV;
                    var success = await _exportImportService.ExportMeasurementsAsync(
                        new List<MeasurementRecord> { item.Record }, format, dialog.FileName);

                    if (success)
                    {
                        System.Windows.MessageBox.Show("导出成功！", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                        _logHelper?.Information($"导出测量数据：ID={item.Record.Id}, 文件={dialog.FileName}");
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("导出失败，请重试", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logHelper?.Error($"导出测量数据失败：ID={item.Record.Id}", ex);
                System.Windows.MessageBox.Show($"导出失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除单条命令
        /// </summary>
    [RelayCommand]
    private async Task DeleteSingleAsync(MeasurementRecordItem? item)
    {
        if (item == null || !CanDelete) return;

        var result = System.Windows.MessageBox.Show(
            $"确定要删除 {item.Record.Patient?.Name} 的测量记录吗？\n此操作不可恢复。",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            var success = await _measurementService.DeleteMeasurementAsync(item.Record.Id);
            if (success)
            {
                await LoadDataAsync();
                _logHelper?.Information($"删除测量记录：ID={item.Record.Id}");
                System.Windows.MessageBox.Show("删除成功！", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除测量记录失败：ID={item.Record.Id}", ex);
            System.Windows.MessageBox.Show($"删除失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
            var shouldSelectAll = IsAllSelected;

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
            System.Windows.MessageBox.Show("请先选择要导出的记录", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "批量导出测量数据",
                Filter = "Excel文件 (*.xlsx)|*.xlsx|CSV文件 (*.csv)|*.csv",
                FileName = $"测量数据_批量导出_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                // 获取所有选中的记录（包括其他页面的）
                var allRecords = await _measurementService.GetMeasurementsByIdsAsync(_globalSelectedIds.ToList());

                var format = dialog.FilterIndex == 1 ? ExportFormat.Excel : ExportFormat.CSV;
                var success = await _exportImportService.ExportMeasurementsAsync(allRecords, format, dialog.FileName);

                if (success)
                {
                    System.Windows.MessageBox.Show($"成功导出 {_globalSelectedIds.Count} 条记录！", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    _logHelper?.Information($"批量导出测量数据：{_globalSelectedIds.Count}条, 文件={dialog.FileName}");
                }
                else
                {
                    System.Windows.MessageBox.Show("导出失败，请重试", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"批量导出测量数据失败", ex);
            System.Windows.MessageBox.Show($"批量导出失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        }

        /// <summary>
    /// 批量删除命令
    /// </summary>
    [RelayCommand]
    private async Task BatchDeleteAsync()
    {
        if (!CanDelete) return;

        var selectedItems = MeasurementRecords.Where(r => r.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            System.Windows.MessageBox.Show("请先选择要删除的记录", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"确定要删除选中的 {selectedItems.Count} 条记录吗？\n此操作不可恢复。",
            "确认批量删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            var ids = selectedItems.Select(r => r.Record.Id);
            var count = await _measurementService.DeleteMeasurementsAsync(ids);
            await LoadDataAsync();
            _logHelper?.Information($"批量删除测量记录：{count}条");
            System.Windows.MessageBox.Show($"成功删除 {count} 条记录！", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"批量删除测量记录失败", ex);
            System.Windows.MessageBox.Show($"批量删除失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
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
        MeasurementStatus.Pending => "待处理",
        MeasurementStatus.InProgress => "进行中",
        MeasurementStatus.Completed => "已完成",
        MeasurementStatus.Cancelled => "已取消",
        MeasurementStatus.Failed => "测量失败",
        _ => "--"
    };

    /// <summary>
    /// 状态颜色
    /// </summary>
    public string StatusColor => Record.Status switch
    {
        MeasurementStatus.Pending => "#FF9800",
        MeasurementStatus.InProgress => "#2196F3",
        MeasurementStatus.Completed => "#4CAF50",
        MeasurementStatus.Cancelled => "#9E9E9E",
        MeasurementStatus.Failed => "#F44336",
        _ => "#9E9E9E"
    };

    /// <summary>
    /// 构造函数
    /// </summary>
    public MeasurementRecordItem(MeasurementRecord record, int rowNumber)
    {
        Record = record;
        RowNumber = rowNumber;
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
