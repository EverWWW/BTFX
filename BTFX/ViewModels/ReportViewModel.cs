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
/// 报告视图模型
/// </summary>
public partial class ReportViewModel : ObservableObject, IDisposable
{
    private readonly IReportService _reportService;
    private readonly IMeasurementService _measurementService;
    private readonly ISessionService _sessionService;
    private readonly IExportImportService _exportImportService;
    private readonly ILogHelper? _logHelper;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private volatile bool _disposed;
    private readonly HashSet<int> _selectedReportIds = new();
    private List<Report> _filteredReports = new();
    private bool _isUpdatingSelection;
    private const int MaxReportPageSize = 7;
    private const double ReportRowHeight = 60d;
    private const double ReportRowSpacing = 8d;
    private const int MinimumReportPageSize = 1;

    #region 模式切换

    /// <summary>
    /// 当前模式（0=报告列表，1=生成报告）
    /// </summary>
    [ObservableProperty]
    private int _currentModeIndex;

    /// <summary>
    /// 是否为报告列表模式
    /// </summary>
    public bool IsListMode => CurrentModeIndex == 0;

    /// <summary>
    /// 是否为生成报告模式
    /// </summary>
    public bool IsGenerateMode => CurrentModeIndex == 1;

    #endregion

    #region 报告列表

    /// <summary>
    /// 报告列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ReportItem> _reports = new();

    /// <summary>
    /// 选中的报告
    /// </summary>
    [ObservableProperty]
    private ReportItem? _selectedReport;

    /// <summary>
    /// 报告筛选-患者姓名
    /// </summary>
    [ObservableProperty]
    private string _reportFilterPatientName = string.Empty;

    /// <summary>
    /// 报告筛选-开始日期
    /// </summary>
    [ObservableProperty]
    private DateTime? _reportFilterStartDate;

    /// <summary>
    /// 报告筛选-结束日期
    /// </summary>
    [ObservableProperty]
    private DateTime? _reportFilterEndDate;

    /// <summary>
    /// 是否全选
    /// </summary>
    private bool _isAllSelected;

    public bool IsAllSelected
    {
        get => _isAllSelected;
        private set => SetProperty(ref _isAllSelected, value);
    }

    private bool? _headerSelectionState = false;

    public bool? HeaderSelectionState
    {
        get => _headerSelectionState;
        private set => SetProperty(ref _headerSelectionState, value);
    }

    /// <summary>
    /// 表头全选状态：0=未选，1=部分选，2=全选。
    /// </summary>
    public int SelectAllState
    {
        get
        {
            var selectedCount = SelectedReportCount;
            if (selectedCount == 0) return 0;
            return selectedCount == Reports.Count ? 2 : 1;
        }
    }

    /// <summary>
    /// 当前页已选报告数
    /// </summary>
    public int SelectedReportCount => Reports.Count(r => r.IsSelected);

    #endregion

    #region 报告分页

    /// <summary>
    /// 当前页码
    /// </summary>
    private int _currentPage = 1;

    public int CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    /// <summary>
    /// 总页数
    /// </summary>
    private int _totalPages;

    public int TotalPages
    {
        get => _totalPages;
        set => SetProperty(ref _totalPages, value);
    }

    /// <summary>
    /// 总记录数
    /// </summary>
    private int _totalRecords;

    public int TotalRecords
    {
        get => _totalRecords;
        set => SetProperty(ref _totalRecords, value);
    }

    /// <summary>
    /// 报告分页页码集合
    /// </summary>
    private ObservableCollection<PageItem> _reportPageNumbers = new();

    public ObservableCollection<PageItem> ReportPageNumbers
    {
        get => _reportPageNumbers;
        set => SetProperty(ref _reportPageNumbers, value);
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

    private int _reportPageSize = MaxReportPageSize;

    #endregion

    #region 生成报告-测量数据选择

    /// <summary>
    /// 可选的测量数据列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<MeasurementRecordItem> _measurementRecords = new();

    /// <summary>
    /// 选中的测量数据
    /// </summary>
    [ObservableProperty]
    private MeasurementRecordItem? _selectedMeasurement;

    /// <summary>
    /// 测量数据筛选-患者姓名
    /// </summary>
    [ObservableProperty]
    private string _measurementFilterPatientName = string.Empty;

    /// <summary>
    /// 测量数据筛选-开始日期
    /// </summary>
    [ObservableProperty]
    private DateTime? _measurementFilterStartDate;

    /// <summary>
    /// 测量数据筛选-结束日期
    /// </summary>
    [ObservableProperty]
    private DateTime? _measurementFilterEndDate;

    /// <summary>
    /// 是否已有报告
    /// </summary>
    [ObservableProperty]
    private bool _hasExistingReport;

    /// <summary>
    /// 已有报告提示信息
    /// </summary>
    [ObservableProperty]
    private string _existingReportInfo = string.Empty;

    #endregion

    #region 报告预览与编辑

    /// <summary>
    /// 预览内容
    /// </summary>
    [ObservableProperty]
    private string _previewContent = string.Empty;

    /// <summary>
    /// 医生意见
    /// </summary>
    [ObservableProperty]
    private string _doctorOpinion = string.Empty;

    /// <summary>
    /// 医生意见字数
    /// </summary>
    public int DoctorOpinionLength => DoctorOpinion?.Length ?? 0;

    /// <summary>
    /// 医生意见最大字数
    /// </summary>
    public int DoctorOpinionMaxLength => Constants.DOCTOR_OPINION_MAX_LENGTH;

    /// <summary>
    /// 是否有预览内容
    /// </summary>
    [ObservableProperty]
    private bool _hasPreviewContent;

    /// <summary>
    /// 当前预览的报告
    /// </summary>
    private Report? _currentPreviewReport;

    #endregion

    #region 权限

    /// <summary>
    /// 是否可以生成报告
    /// </summary>
    [ObservableProperty]
    private bool _canGenerateReport;

    /// <summary>
    /// 是否可以编辑报告
    /// </summary>
    [ObservableProperty]
    private bool _canEditReport;

    /// <summary>
    /// 是否可以删除报告
    /// </summary>
    [ObservableProperty]
    private bool _canDeleteReport;

    /// <summary>
    /// 是否可以导出报告
    /// </summary>
    [ObservableProperty]
    private bool _canExportReport;

    #endregion

    #region 加载状态

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 是否正在生成报告
    /// </summary>
    [ObservableProperty]
    private bool _isGenerating;

    #endregion

    /// <summary>
    /// 最大日期（今天）
    /// </summary>
    public DateTime MaxDate => DateTime.Today;

    /// <summary>
    /// 安全地在 UI 线程执行操作
    /// </summary>
    private bool TryInvokeOnUI(Action action)
    {
        // 先检查关闭状态
        if (_disposed || App.IsShuttingDown) return false;

        try
        {
            var app = Application.Current;
            if (app == null) return false;

            var dispatcher = app.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return false;

            // 再次检查
            if (_disposed || App.IsShuttingDown) return false;

            if (dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
            return true;
        }
        catch
        {
            // 忽略所有异常
            return false;
        }
    }

    /// <summary>
    /// 安全地在 UI 线程异步执行操作
    /// </summary>
    private async Task<bool> TryInvokeOnUIAsync(Action action)
    {
        // 先检查关闭状态
        if (_disposed || App.IsShuttingDown) return false;

        try
        {
            var app = Application.Current;
            if (app == null) return false;

            var dispatcher = app.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return false;

            // 再次检查
            if (_disposed || App.IsShuttingDown) return false;

            await dispatcher.InvokeAsync(action);
            return true;
        }
        catch
        {
            // 忽略所有异常
            return false;
        }
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public ReportViewModel(
        IReportService reportService,
        IMeasurementService measurementService,
        ISessionService sessionService,
        IExportImportService exportImportService)
    {
        _reportService = reportService;
        _measurementService = measurementService;
        _sessionService = sessionService;
        _exportImportService = exportImportService;

        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }

        // 初始化权限
        InitializePermissions();

        // 加载数据
        _ = LoadReportsAsync();
    }

    /// <summary>
    /// 初始化权限
    /// </summary>
    private void InitializePermissions()
    {
        CanGenerateReport = _sessionService.HasPermission("generate_report");
        CanEditReport = _sessionService.HasPermission("edit_report");
        CanDeleteReport = _sessionService.HasPermission("delete_report");
        CanExportReport = _sessionService.HasPermission("export");
    }

    #region 属性变化处理

    partial void OnCurrentModeIndexChanged(int value)
    {
        // 如果应用正在关闭或已释放，不执行任何操作
        if (_disposed || App.IsShuttingDown) return;

        OnPropertyChanged(nameof(IsListMode));
        OnPropertyChanged(nameof(IsGenerateMode));

        if (IsGenerateMode)
        {
            _ = LoadMeasurementsAsync();
        }
    }

    partial void OnDoctorOpinionChanged(string value)
    {
        if (_disposed || App.IsShuttingDown) return;
        OnPropertyChanged(nameof(DoctorOpinionLength));
    }

    partial void OnSelectedReportChanged(ReportItem? value)
    {
        // 如果应用正在关闭或已释放，不执行任何操作
        if (_disposed || App.IsShuttingDown) return;

        if (value != null)
        {
            _ = LoadReportPreviewAsync(value.Report);
        }
        else
        {
            ClearPreview();
        }
    }

    partial void OnSelectedMeasurementChanged(MeasurementRecordItem? value)
    {
        // 如果应用正在关闭或已释放，不执行任何操作
        if (_disposed || App.IsShuttingDown) return;

        if (value != null)
        {
            _ = CheckExistingReportAsync(value.Record);
        }
        else
        {
            HasExistingReport = false;
            ExistingReportInfo = string.Empty;
        }
    }

    partial void OnReportFilterStartDateChanged(DateTime? value)
    {
        if (value.HasValue && ReportFilterEndDate.HasValue && value.Value > ReportFilterEndDate.Value)
        {
            ReportFilterEndDate = value;
        }
    }

    partial void OnReportFilterEndDateChanged(DateTime? value)
    {
        if (value.HasValue && ReportFilterStartDate.HasValue && value.Value < ReportFilterStartDate.Value)
        {
            ReportFilterStartDate = value;
        }
    }

    #endregion

    #region 命令

    /// <summary>
    /// 搜索报告命令
    /// </summary>
    [RelayCommand]
    private async Task SearchReportsAsync()
    {
        CurrentPage = 1;
        _selectedReportIds.Clear();
        await LoadReportsAsync();
    }

    /// <summary>
    /// 重置报告筛选命令
    /// </summary>
    [RelayCommand]
    private async Task ResetReportFilterAsync()
    {
        ReportFilterPatientName = string.Empty;
        ReportFilterStartDate = null;
        ReportFilterEndDate = null;
        CurrentPage = 1;
        _selectedReportIds.Clear();
        await LoadReportsAsync();
    }

    /// <summary>
    /// 清空报告姓名筛选命令
    /// </summary>
    [RelayCommand]
    private void ClearReportPatientName()
    {
        ReportFilterPatientName = string.Empty;
    }

    /// <summary>
    /// 清空报告日期筛选命令
    /// </summary>
    [RelayCommand]
    private void ClearReportDateRange()
    {
        ReportFilterStartDate = null;
        ReportFilterEndDate = null;
    }

    /// <summary>
    /// 搜索测量数据命令
    /// </summary>
    [RelayCommand]
    private async Task SearchMeasurementsAsync()
    {
        await LoadMeasurementsAsync();
    }

    /// <summary>
    /// 重置测量数据筛选命令
    /// </summary>
    [RelayCommand]
    private async Task ResetMeasurementFilterAsync()
    {
        MeasurementFilterPatientName = string.Empty;
        MeasurementFilterStartDate = null;
        MeasurementFilterEndDate = null;
        await LoadMeasurementsAsync();
    }

    /// <summary>
    /// 查看报告详情命令
    /// </summary>
    [RelayCommand]
    private void ViewReport(ReportItem? item)
    {
        if (item == null) return;
        SelectedReport = item;
        _logHelper?.Information($"查看报告：ID={item.Report.Id}");
    }

    /// <summary>
    /// 编辑报告命令
    /// </summary>
    [RelayCommand]
    private void EditReport(ReportItem? item)
    {
        if (item == null || !CanEditReport) return;

        SelectedReport = item;
        DoctorOpinion = item.Report.DoctorOpinion ?? string.Empty;
        _logHelper?.Information($"编辑报告：ID={item.Report.Id}");
    }

    /// <summary>
    /// 生成报告命令
    /// </summary>
    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        if (SelectedMeasurement == null || !CanGenerateReport || App.IsShuttingDown) return;

        // 检查是否已有报告
        if (HasExistingReport)
        {
            var result = System.Windows.MessageBox.Show(
                "该测量数据已有报告，是否覆盖？",
                "确认覆盖",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes) return;
        }

        if (_disposed || App.IsShuttingDown) return;

        try
        {
            TryInvokeOnUI(() => IsGenerating = true);

            var report = await _reportService.GenerateReportAsync(
                SelectedMeasurement.Record.Id,
                _sessionService.CurrentUser?.Id ?? 0);

            if (_disposed || App.IsShuttingDown) return;

            if (report != null)
            {
                await TryInvokeOnUIAsync(() =>
                {
                    if (_disposed || App.IsShuttingDown) return;

                    System.Windows.MessageBox.Show("报告生成成功！", "提示",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                    _logHelper?.Information($"生成报告成功：ID={report.Id}");

                    // 切换到报告列表
                    CurrentModeIndex = 0;
                });

                // 刷新报告列表
                await LoadReportsAsync();

                // 选中新生成的报告
                TryInvokeOnUI(() =>
                {
                    var newItem = Reports.FirstOrDefault(r => r.Report.Id == report.Id);
                    if (newItem != null)
                    {
                        SelectedReport = newItem;
                    }
                });
            }
        }
        catch (Exception ex)
        {
            if (!_disposed && !App.IsShuttingDown)
            {
                _logHelper?.Error("生成报告失败", ex);
                System.Windows.MessageBox.Show($"生成报告失败：{ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        finally
        {
            TryInvokeOnUI(() => IsGenerating = false);
        }
    }

    /// <summary>
    /// 保存报告命令
    /// </summary>
    [RelayCommand]
    private async Task SaveReportAsync()
    {
        if (_currentPreviewReport == null || !CanEditReport || App.IsShuttingDown) return;

        try
        {
            _currentPreviewReport.DoctorOpinion = DoctorOpinion;
            _currentPreviewReport.UpdatedAt = DateTime.Now;

            var success = await _reportService.UpdateReportAsync(_currentPreviewReport);

            if (_disposed || App.IsShuttingDown) return;

            if (success)
            {
                System.Windows.MessageBox.Show("保存成功！", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                _logHelper?.Information($"保存报告成功：ID={_currentPreviewReport.Id}");

                await LoadReportsAsync();
            }
        }
        catch (Exception ex)
        {
            if (!App.IsShuttingDown)
            {
                _logHelper?.Error($"保存报告失败：ID={_currentPreviewReport?.Id}", ex);
                System.Windows.MessageBox.Show($"保存失败：{ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// 打印报告命令
    /// </summary>
    [RelayCommand]
    private async Task PrintReportAsync()
    {
            if (_currentPreviewReport == null || _disposed || App.IsShuttingDown) return;

            try
            {
                TryInvokeOnUI(() => IsLoading = true);

                // 调用打印服务
                var success = await _reportService.PrintReportAsync(_currentPreviewReport.Id);

                if (_disposed || App.IsShuttingDown) return;

                if (success)
                {
                    _logHelper?.Information($"打印报告成功：ID={_currentPreviewReport.Id}");
                    await LoadReportsAsync();

                    TryInvokeOnUI(() =>
                    {
                        if (_disposed || App.IsShuttingDown) return;
                        // 刷新当前预览
                        if (SelectedReport != null)
                        {
                            _ = LoadReportPreviewAsync(SelectedReport.Report);
                        }
                    });
                }
                else if (!App.IsShuttingDown)
                {
                    System.Windows.MessageBox.Show("打印失败或已取消", "提示",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                if (!_disposed && !App.IsShuttingDown)
                {
                    _logHelper?.Error($"打印报告失败：ID={_currentPreviewReport?.Id}", ex);
                    System.Windows.MessageBox.Show($"打印失败：{ex.Message}", "错误",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            finally
            {
                TryInvokeOnUI(() => IsLoading = false);
            }
        }

    /// <summary>
    /// 导出PDF命令
    /// </summary>
    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (_currentPreviewReport == null || !CanExportReport || _disposed || App.IsShuttingDown) return;

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出报告",
                Filter = "PDF文件 (*.pdf)|*.pdf",
                FileName = $"报告_{_currentPreviewReport.ReportNumber}"
            };

            if (dialog.ShowDialog() == true)
            {
                TryInvokeOnUI(() => IsLoading = true);

                // 获取设置服务
                var settingsService = App.Services?.GetService(typeof(ISettingsService)) as ISettingsService;
                if (settingsService != null)
                {
                    // 异步导出在后台线程防止阻塞UI
                    var report = _currentPreviewReport;
                    var fileName = dialog.FileName;

                    var success = await Task.Run(() =>
                    {
                        if (App.IsShuttingDown) return false;
                        var exporter = new Helpers.ReportPdfExporter(settingsService);
                        return exporter.ExportToPdf(report, fileName);
                    });

                    if (_disposed || App.IsShuttingDown) return;

                    TryInvokeOnUI(() =>
                    {
                        if (_disposed || App.IsShuttingDown) return;

                        if (success)
                        {
                            System.Windows.MessageBox.Show($"报告已导出至：\n{fileName}", "导出成功",
                                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                            _logHelper?.Information($"导出报告PDF成功：ID={report.Id}, 文件={fileName}");

                            // 询问是否打开文件
                            var openResult = System.Windows.MessageBox.Show("是否打开导出的文件？", "提示",
                                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                            if (openResult == System.Windows.MessageBoxResult.Yes)
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = fileName,
                                    UseShellExecute = true
                                });
                            }
                        }
                        else
                        {
                            System.Windows.MessageBox.Show("PDF导出失败", "错误",
                                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            if (!_disposed && !App.IsShuttingDown)
            {
                _logHelper?.Error($"导出报告PDF失败：ID={_currentPreviewReport?.Id}", ex);
                System.Windows.MessageBox.Show($"导出失败：{ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        finally
        {
            TryInvokeOnUI(() => IsLoading = false);
        }
    }

    /// <summary>
    /// 删除报告命令
    /// </summary>
    [RelayCommand]
    private async Task DeleteReportAsync(ReportItem? item)
    {
        if (item == null || _disposed || App.IsShuttingDown) return;

        var result = System.Windows.MessageBox.Show(
            $"确定要删除报告 {item.Report.ReportNumber} 吗？此操作不可恢复！",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            _selectedReportIds.Remove(item.Report.Id);
            var success = await _reportService.DeleteReportAsync(item.Report.Id);

            if (_disposed || App.IsShuttingDown) return;

            if (success)
            {
                await LoadReportsAsync();

                TryInvokeOnUI(() =>
                {
                    if (_disposed || App.IsShuttingDown) return;
                    ClearPreview();
                });

                _logHelper?.Information($"删除报告：ID={item.Report.Id}");

                if (!App.IsShuttingDown)
                {
                    System.Windows.MessageBox.Show("删除成功！", "提示",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            if (!_disposed && !App.IsShuttingDown)
            {
                _logHelper?.Error($"删除报告失败：ID={item.Report.Id}", ex);
                System.Windows.MessageBox.Show($"删除失败：{ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// 全部导出选中的报告命令。
    /// </summary>
    [RelayCommand]
    private void ExportSelectedReports()
    {
        if (SelectedReportCount <= 0 || _disposed || App.IsShuttingDown)
        {
            return;
        }

        System.Windows.MessageBox.Show("全部导出功能暂未实现。", "提示",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// 上一页命令
    /// </summary>
    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage <= 1)
        {
            return;
        }

        CurrentPage--;
        RefreshPagedReports();
    }

    /// <summary>
    /// 下一页命令
    /// </summary>
    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage >= TotalPages)
        {
            return;
        }

        CurrentPage++;
        RefreshPagedReports();
    }

    /// <summary>
    /// 页码跳转命令
    /// </summary>
    [RelayCommand]
    private void GoToPageNumber(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > TotalPages || pageNumber == CurrentPage)
        {
            return;
        }

        CurrentPage = pageNumber;
        RefreshPagedReports();
    }

    /// <summary>
    /// 全选命令
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        try
        {
            _isUpdatingSelection = true;
            foreach (var item in Reports)
            {
                item.IsSelected = IsAllSelected;
                if (IsAllSelected)
                {
                    _selectedReportIds.Add(item.Report.Id);
                }
                else
                {
                    _selectedReportIds.Remove(item.Report.Id);
                }
            }

            OnPropertyChanged(nameof(SelectedReportCount));
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    /// <summary>
    /// 应用当前页全选状态
    /// </summary>
    public void ApplySelectAll(bool isSelected)
    {
        ApplySelectAllInternal(isSelected);
    }

    private void ApplySelectAllInternal(bool isSelected)
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        try
        {
            _isUpdatingSelection = true;
            foreach (var item in Reports)
            {
                item.IsSelected = isSelected;
                if (isSelected)
                {
                    _selectedReportIds.Add(item.Report.Id);
                }
                else
                {
                    _selectedReportIds.Remove(item.Report.Id);
                }
            }

            IsAllSelected = isSelected;
            HeaderSelectionState = isSelected;

            OnPropertyChanged(nameof(SelectedReportCount));
            OnPropertyChanged(nameof(SelectAllState));
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    /// <summary>
    /// 根据列表可视高度更新每页容量，最大不超过 6 条。
    /// </summary>
    /// <param name="viewportHeight">列表内容区可视高度。</param>
    public void UpdateReportPageSize(double viewportHeight)
    {
        if (viewportHeight <= 0)
        {
            return;
        }

        var effectiveViewportHeight = Math.Min(viewportHeight, MaxReportPageSize * (ReportRowHeight + ReportRowSpacing) - ReportRowSpacing);
        var rowFullHeight = ReportRowHeight + ReportRowSpacing;
        var calculatedPageSize = Math.Max(MinimumReportPageSize, (int)Math.Floor((effectiveViewportHeight + ReportRowSpacing) / rowFullHeight));
        var newPageSize = Math.Min(MaxReportPageSize, calculatedPageSize);

        if (newPageSize == _reportPageSize)
        {
            return;
        }

        _reportPageSize = newPageSize;

        if (_filteredReports.Count == 0)
        {
            TotalPages = 0;
            CurrentPage = 1;
            BuildReportPageNumbers();
            return;
        }

        TotalPages = (int)Math.Ceiling(_filteredReports.Count / (double)_reportPageSize);
        if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }

        RefreshPagedReports();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 加载报告列表
    /// </summary>
    private async Task LoadReportsAsync()
    {
        if (_disposed || App.IsShuttingDown) return;

        try
        {
            TryInvokeOnUI(() => IsLoading = true);

            var reports = await _reportService.GetReportsAsync(
                ReportFilterPatientName,
                ReportFilterStartDate,
                ReportFilterEndDate);

            if (_disposed || App.IsShuttingDown || _cancellationTokenSource.Token.IsCancellationRequested) return;

            // 在UI线程上更新集合
            await TryInvokeOnUIAsync(() =>
            {
                if (_disposed) return;

                _filteredReports = reports.ToList();
                TotalRecords = _filteredReports.Count;
                TotalPages = TotalRecords == 0 ? 0 : (int)Math.Ceiling(TotalRecords / (double)_reportPageSize);

                if (TotalPages == 0)
                {
                    CurrentPage = 1;
                }
                else if (CurrentPage > TotalPages)
                {
                    CurrentPage = TotalPages;
                }

                RefreshPagedReports();

                if (_currentPreviewReport != null && _filteredReports.All(r => r.Id != _currentPreviewReport.Id))
                {
                    ClearPreview();
                }
            });

            _logHelper?.Information($"加载报告列表：共{reports.Count}条");
        }
        catch (OperationCanceledException)
        {
            // 操作被取消，忽略
        }
        catch (Exception ex)
        {
            if (!_disposed)
            {
                _logHelper?.Error("加载报告列表失败", ex);
            }
        }
        finally
        {
            TryInvokeOnUI(() => IsLoading = false);
        }
    }

    /// <summary>
    /// 刷新当前页的报告列表
    /// </summary>
    private void RefreshPagedReports()
    {
        if (_disposed || App.IsShuttingDown)
        {
            return;
        }

        Reports.Clear();

        if (_filteredReports.Count == 0)
        {
            BuildReportPageNumbers();
            UpdateSelectionState();
            return;
        }

        var pageReports = _filteredReports
            .Skip((CurrentPage - 1) * _reportPageSize)
            .Take(_reportPageSize)
            .ToList();

        var rowNumber = (CurrentPage - 1) * _reportPageSize + 1;
        foreach (var report in pageReports)
        {
            var item = new ReportItem(report, rowNumber++)
            {
                IsSelected = _selectedReportIds.Contains(report.Id)
            };

            Reports.Add(item);
        }

        BuildReportPageNumbers();
        UpdateSelectionState();
    }

    /// <summary>
    /// 构建报告分页页码集合
    /// </summary>
    private void BuildReportPageNumbers()
    {
        ReportPageNumbers.Clear();
        if (TotalPages <= 0)
        {
            CanPagePrevious = false;
            CanPageNext = false;
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
                ReportPageNumbers.Add(new PageItem
                {
                    DisplayText = "...",
                    IsEllipsis = true,
                    PageNumber = -1
                });
            }

            ReportPageNumbers.Add(new PageItem
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
    /// 更新勾选状态
    /// </summary>
    private void UpdateSelectionState()
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        try
        {
            _isUpdatingSelection = true;
            var selectedCount = Reports.Count(r => r.IsSelected);
            var newIsAllSelected = Reports.Count > 0 && selectedCount == Reports.Count;
            bool? newHeaderSelectionState = selectedCount == 0 ? false : newIsAllSelected ? true : null;

            IsAllSelected = newIsAllSelected;
            HeaderSelectionState = newHeaderSelectionState;

            OnPropertyChanged(nameof(SelectedReportCount));
            OnPropertyChanged(nameof(SelectAllState));
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    /// <summary>
    /// 单项勾选状态变化
    /// </summary>
    /// <param name="item">报告行</param>
    public void OnReportSelectionChanged(ReportItem item)
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        try
        {
            _isUpdatingSelection = true;
            if (item.IsSelected)
            {
                _selectedReportIds.Add(item.Report.Id);
            }
            else
            {
                _selectedReportIds.Remove(item.Report.Id);
            }

            var selectedCount = Reports.Count(r => r.IsSelected);
            var newIsAllSelected = Reports.Count > 0 && selectedCount == Reports.Count;
            bool? newHeaderSelectionState = selectedCount == 0 ? false : newIsAllSelected ? true : null;

            IsAllSelected = newIsAllSelected;
            HeaderSelectionState = newHeaderSelectionState;

            OnPropertyChanged(nameof(SelectedReportCount));
            OnPropertyChanged(nameof(SelectAllState));
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

        /// <summary>
        /// 加载测量数据列表（仅已完成的测量）
        /// </summary>
        private async Task LoadMeasurementsAsync()
        {
            if (_disposed || App.IsShuttingDown) return;

            try
            {
                TryInvokeOnUI(() => IsLoading = true);

                var (records, _) = await _measurementService.GetMeasurementsPagedAsync(
                    MeasurementFilterPatientName,
                    MeasurementFilterStartDate,
                    MeasurementFilterEndDate,
                    MeasurementStatus.Completed, // 仅已完成的测量
                    1, 100); // 获取前100条

                if (_disposed || App.IsShuttingDown || _cancellationTokenSource.Token.IsCancellationRequested) return;

                // 在UI线程上更新集合
                await TryInvokeOnUIAsync(() =>
                {
                    if (_disposed || App.IsShuttingDown) return;

                    MeasurementRecords.Clear();
                    int rowNumber = 1;
                    foreach (var record in records)
                    {
                        MeasurementRecords.Add(new MeasurementRecordItem(record, rowNumber++));
                    }
                });

                _logHelper?.Information($"加载测量数据列表：共{records.Count}条");
            }
            catch (OperationCanceledException)
            {
                // 操作被取消，忽略
            }
            catch (Exception ex)
            {
                if (!_disposed && !App.IsShuttingDown)
                {
                    _logHelper?.Error("加载测量数据列表失败", ex);
                }
            }
            finally
            {
                TryInvokeOnUI(() => IsLoading = false);
            }
        }

    /// <summary>
    /// 检查是否已有报告
    /// </summary>
    private async Task CheckExistingReportAsync(MeasurementRecord record)
    {
        if (_disposed || App.IsShuttingDown) return;

        try
        {
            var existingReport = await _reportService.GetReportByMeasurementIdAsync(record.Id);

            if (_disposed || App.IsShuttingDown || _cancellationTokenSource.Token.IsCancellationRequested) return;

            await TryInvokeOnUIAsync(() =>
            {
                if (_disposed || App.IsShuttingDown) return;

                HasExistingReport = existingReport != null;

                if (HasExistingReport && existingReport != null)
                {
                    ExistingReportInfo = $"该测量数据已有报告：{existingReport.ReportNumber}（{existingReport.CreatedAt:yyyy-MM-dd}）";
                }
                else
                {
                    ExistingReportInfo = string.Empty;
                }
            });
        }
        catch (Exception ex)
        {
            if (!_disposed && !App.IsShuttingDown)
            {
                _logHelper?.Error($"检查已有报告失败：MeasurementId={record.Id}", ex);
                TryInvokeOnUI(() =>
                {
                    HasExistingReport = false;
                    ExistingReportInfo = string.Empty;
                });
            }
        }
    }

    /// <summary>
    /// 加载报告预览
    /// </summary>
    private async Task LoadReportPreviewAsync(Report report)
    {
        // 如果应用正在关闭或已释放，不执行任何操作
        if (_disposed || App.IsShuttingDown) return;

        try
        {
            TryInvokeOnUI(() => IsLoading = true);

            // 重新加载报告（含分析数据）
            var fullReport = await _reportService.GetReportWithAnalysisDataAsync(report.Id);
            if (fullReport == null || _disposed || App.IsShuttingDown)
            {
                TryInvokeOnUI(() => IsLoading = false);
                return;
            }

            _currentPreviewReport = fullReport;
            DoctorOpinion = fullReport.DoctorOpinion ?? string.Empty;
            HasPreviewContent = true;

            // 构建预览内容
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"报告编号：{fullReport.ReportNumber}");
            sb.AppendLine($"生成日期：{fullReport.CreatedAt:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"报告状态：{GetStatusText(fullReport.Status)}");
            sb.AppendLine();

            if (fullReport.MeasurementRecord?.Patient != null)
            {
                var patient = fullReport.MeasurementRecord.Patient;
                sb.AppendLine("【患者信息】");
                sb.AppendLine($"姓名：{patient.Name}");
                sb.AppendLine($"性别：{(patient.Gender == Gender.Male ? "男" : "女")}");
                sb.AppendLine($"年龄：{patient.Age}岁");
                sb.AppendLine();
            }

            if (fullReport.MeasurementRecord != null)
            {
                sb.AppendLine("【测量信息】");
                sb.AppendLine($"测量日期：{fullReport.MeasurementRecord.MeasurementDate:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"操作员：{fullReport.MeasurementRecord.Operator?.Name ?? "未知"}");
                sb.AppendLine();

                if (fullReport.MeasurementRecord.GaitParameters != null)
                {
                    var gait = fullReport.MeasurementRecord.GaitParameters;
                    sb.AppendLine("【步态参数】");
                    sb.AppendLine($"步幅（左）：{gait.StrideLengthLeft?.ToString("F2") ?? "--"} cm");
                    sb.AppendLine($"步幅（右）：{gait.StrideLengthRight?.ToString("F2") ?? "--"} cm");
                    sb.AppendLine($"步频：{gait.Cadence?.ToString("F1") ?? "--"} steps/min");
                    sb.AppendLine($"步速：{gait.Velocity?.ToString("F2") ?? "--"} m/s");
                    sb.AppendLine($"左脚支撑相：{gait.StancePhaseLeft?.ToString("F1") ?? "--"} %");
                    sb.AppendLine($"右脚支撑相：{gait.StancePhaseRight?.ToString("F1") ?? "--"} %");
                    sb.AppendLine($"双支撑时间：{gait.DoubleSupport?.ToString("F1") ?? "--"} %");
                    sb.AppendLine();
                }
            }

            // 运动学参数
            if (fullReport.KinematicSummary != null)
            {
                var ks = fullReport.KinematicSummary;
                sb.AppendLine("【运动学参数（ROM）】");
                sb.AppendLine($"髋关节 ROM：{ks.HipRomDeg?.ToString("F1") ?? "--"} °");
                sb.AppendLine($"膝关节 ROM：{ks.KneeRomDeg?.ToString("F1") ?? "--"} °");
                sb.AppendLine($"踝关节 ROM：{ks.AnkleRomDeg?.ToString("F1") ?? "--"} °");
                sb.AppendLine($"骨盆冠状面 ROM：{ks.PelvisCoronalRomDeg?.ToString("F1") ?? "--"} °");
                sb.AppendLine();
            }

            // 质量控制信息
            if (fullReport.QualityControl != null)
            {
                var qc = fullReport.QualityControl;
                sb.AppendLine("【质量控制信息】");
                sb.AppendLine($"平均置信度：{(qc.MeanKeypointConfidence.HasValue ? $"{qc.MeanKeypointConfidence * 100:F1}%" : "--")}");
                sb.AppendLine($"有效帧比例：{(qc.ValidFrameRatio.HasValue ? $"{qc.ValidFrameRatio * 100:F1}%" : "--")}");
                sb.AppendLine($"遮挡预警：{(qc.OcclusionWarning ? "⚠ 是" : "✓ 否")}");
                sb.AppendLine($"丢点预警：{(qc.MissingPointWarning ? "⚠ 是" : "✓ 否")}");
                sb.AppendLine();
            }

            // 分析信息
            if (fullReport.AnalysisResult != null)
            {
                var ar = fullReport.AnalysisResult;
                sb.AppendLine("【分析信息】");
                sb.AppendLine($"算法版本：{ar.AlgorithmVersion}");
                sb.AppendLine($"模型版本：{ar.ModelVersion}");
                sb.AppendLine($"分析耗时：{(ar.AnalysisDurationSeconds.HasValue ? $"{ar.AnalysisDurationSeconds:F1}秒" : "--")}");
                sb.AppendLine($"分析时间：{ar.CreatedAt:yyyy-MM-dd HH:mm}");
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(fullReport.DoctorOpinion))
            {
                sb.AppendLine("【医生意见】");
                sb.AppendLine(fullReport.DoctorOpinion);
            }

            // 再次检查关闭状态
            if (_disposed || App.IsShuttingDown) return;

            TryInvokeOnUI(() => PreviewContent = sb.ToString());
        }
        catch (Exception ex)
        {
            if (!_disposed && !App.IsShuttingDown)
            {
                _logHelper?.Error($"加载报告预览失败：ReportId={report.Id}", ex);
            }
        }
        finally
        {
            TryInvokeOnUI(() => IsLoading = false);
        }
    }

    /// <summary>
    /// 清除预览
    /// </summary>
    private void ClearPreview()
    {
        // 如果应用正在关闭或已释放，不执行任何操作
        if (_disposed || App.IsShuttingDown) return;

        _currentPreviewReport = null;
        PreviewContent = string.Empty;
        DoctorOpinion = string.Empty;
        HasPreviewContent = false;
    }

        /// <summary>
        /// 获取状态文本
        /// </summary>
        private static string GetStatusText(ReportStatus status)
        {
            return status switch
            {
                ReportStatus.Draft => "草稿",
                ReportStatus.Completed => "已完成",
                ReportStatus.Printed => "已打印",
                _ => "未知"
            };
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

            _logHelper?.Information("ReportViewModel disposed");
        }

        #endregion
    }

#region 辅助类

/// <summary>
/// 报告列表项
/// </summary>
public partial class ReportItem : ObservableObject
{
    /// <summary>
    /// 报告实体
    /// </summary>
    public Report Report { get; }

    /// <summary>
    /// 行号
    /// </summary>
    public int RowNumber { get; }

    /// <summary>
    /// 是否勾选
    /// </summary>
    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// 报告编号
    /// </summary>
    public string ReportNumber => Report.ReportNumber;

    /// <summary>
    /// 患者姓名
    /// </summary>
    public string PatientName => Report.MeasurementRecord?.Patient?.Name ?? "--";

    /// <summary>
    /// 生成日期
    /// </summary>
    public string GeneratedDateDisplay => Report.CreatedAt.ToString(Constants.DATETIME_FORMAT);

    /// <summary>
    /// 状态显示
    /// </summary>
    public string StatusDisplay => Report.Status switch
    {
        ReportStatus.Draft => "待生成",
        ReportStatus.Completed => "已生成",
        ReportStatus.Printed => "已生成",
        _ => "待生成"
    };

    /// <summary>
    /// 状态图标
    /// </summary>
    public string StatusIcon => Report.Status == ReportStatus.Draft
        ? "/Resources/Images/Report/daishengcheng.png"
        : "/Resources/Images/DataManagement/yiwancheng.png";

    /// <summary>
    /// 状态背景
    /// </summary>
    public string StatusBackground => Report.Status == ReportStatus.Draft
        ? "#E1E1E1"
        : "#E9F7E3";

    /// <summary>
    /// 状态前景色
    /// </summary>
    public string StatusForeground => Report.Status == ReportStatus.Draft
        ? "#9E9E9E"
        : "#44BE13";

    /// <summary>
    /// 详情提示
    /// </summary>
    public string DetailHint => $"查看 {PatientName} 的报告详情";

    /// <summary>
    /// 状态颜色
    /// </summary>
    public string StatusColor => Report.Status switch
    {
        ReportStatus.Draft => "#9E9E9E",
        ReportStatus.Completed => "#44BE13",
        ReportStatus.Printed => "#44BE13",
        _ => "#9E9E9E"
    };

    public ReportItem(Report report, int rowNumber)
    {
        Report = report;
        RowNumber = rowNumber;
    }
}

#endregion
