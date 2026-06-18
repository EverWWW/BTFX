using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using BTFX.Common;
using BTFX.Helpers;
using BTFX.Models;
using BTFX.Models.Analysis;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using ToolHelper.LoggingDiagnostics.Abstractions;
using Constants = BTFX.Common.Constants;

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
    private readonly ILocalizationService? _localizationService;
    private readonly ILogHelper? _logHelper;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private volatile bool _disposed;
    private readonly HashSet<int> _selectedReportIds = new();
    private List<Report> _filteredReports = new();
    private bool _isUpdatingSelection;
    private int _previewLoadVersion;
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

    /// <summary>
    /// 已选报告总数
    /// </summary>
    public int SelectedReportTotalCount => _selectedReportIds.Count;

    public string SelectedReportTotalCountText => L("DataManagement.SelectedCountFormat", SelectedReportTotalCount);

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
    /// 报告基础信息摘要。
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ReportSummaryField> _previewBasicFields = new();

    /// <summary>
    /// 报告关键指标摘要。
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ReportSummaryMetric> _previewMetrics = new();

    /// <summary>
    /// 报告包含内容标签。
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _previewSectionTags = new();

    /// <summary>
    /// 报告摘要说明。
    /// </summary>
    [ObservableProperty]
    private string _previewSummaryMessage = string.Empty;

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
            _localizationService = App.Services?.GetService<ILocalizationService>();
            if (_localizationService is not null)
            {
                _localizationService.LanguageChanged += OnLanguageChanged;
            }
        }
        catch { }

        PreviewSummaryMessage = L("Report.SummaryHint");

        // 初始化权限
        InitializePermissions();

        // 加载数据
        _ = LoadReportsAsync();
    }

    private string L(string key)
    {
        var value = _localizationService?.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private string L(string key, params object[] args)
    {
        var value = _localizationService?.GetString(key, args);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private void OnLanguageChanged(object? sender, AppLanguage language)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var report in Reports)
            {
                report.SetLocalizationService(_localizationService);
                report.RefreshLocalization();
            }

            if (_currentPreviewReport is not null)
            {
                UpdatePreviewSummary(_currentPreviewReport);
            }
            else
            {
                PreviewSummaryMessage = L("Report.SummaryHint");
            }

            OnPropertyChanged(nameof(SelectedReportTotalCountText));
        });
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
    private async Task ViewReportAsync(ReportItem? item)
    {
        if (item == null) return;
        SelectedReport = item;
        _logHelper?.Information($"查看报告：ID={item.Report.Id}");

        try
        {
            await OpenReportPreviewAsync(item.Report.Id);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"打开报告预览弹窗失败：ReportId={item.Report.Id}", ex);
            MessageBox.Show(L("Report.OpenPreviewFailedFormat", ex.Message), L("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 单行导出报告命令。
    /// </summary>
    [RelayCommand]
    private async Task ExportReportAsync(ReportItem? item)
    {
        if (item == null || _disposed || App.IsShuttingDown) return;
        SelectedReport = item;
        await ExportReportPdfAsync(item.Report.Id);
    }

    /// <summary>
    /// 单行打印报告命令。
    /// </summary>
    [RelayCommand]
    private async Task PrintReportItemAsync(ReportItem? item)
    {
        if (item == null || _disposed || App.IsShuttingDown) return;
        SelectedReport = item;
        await PrintReportByIdAsync(item.Report.Id);
    }

    [RelayCommand]
    private async Task ResumeReportAsync(ReportItem? item)
    {
        if (item == null) return;
        SelectedReport = item;

        try
        {
            var report = item.Report;
            if (report.Status is ReportStatus.Draft or ReportStatus.Outdated)
            {
                if (!CanGenerateReport)
                {
                    await ShowNoticeDialogAsync(L("Tip"), L("Report.NoGeneratePermission"));
                    return;
                }

                var analysisService = App.Services?.GetService(typeof(IGaitAnalysisService)) as IGaitAnalysisService;
                var latestResult = analysisService is null
                    ? null
                    : await analysisService.GetLatestAnalysisResultAsync(report.MeasurementId);
                if (latestResult?.Success != true)
                {
                    await ShowNoticeDialogAsync(L("Tip"), L("Report.NoAnalysisResultForGeneration"));
                    return;
                }

                var regenerated = await _reportService.GenerateReportAsync(
                    report.MeasurementId,
                    _sessionService.CurrentUser?.Id ?? report.CreatedBy);

                if (regenerated is null)
                {
                    await ShowNoticeDialogAsync(L("Tip"), L("Report.GenerateFailedNoResult"));
                    return;
                }

                await LoadReportsAsync();
                var refreshedItem = Reports.FirstOrDefault(r => r.Report.Id == regenerated.Id);
                if (refreshedItem is not null)
                {
                    SelectedReport = refreshedItem;
                    item = refreshedItem;
                }

                _logHelper?.Information($"继续生成报告：ID={regenerated.Id}, MeasurementId={regenerated.MeasurementId}");
                await OpenReportPreviewAsync(regenerated.Id);
                return;
            }

            await OpenReportPreviewAsync(report.Id);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"继续处理报告失败：ReportId={item.Report.Id}", ex);
            MessageBox.Show(L("Report.ResumeFailedFormat", ex.Message), L("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task OpenReportPreviewAsync(int reportId)
    {
        try
        {
            var fullReport = await _reportService.GetReportWithAnalysisDataAsync(reportId);
            if (fullReport is null)
            {
                MessageBox.Show(L("Report.NoDataForPreview"), L("Tip"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var previewViewModel = App.Services.GetRequiredService<ReportPreviewDialogViewModel>();
            var settingsService = App.Services.GetService<ISettingsService>();
            var unitName = settingsService?.CurrentSettings?.Unit?.Name ?? Constants.APP_DISPLAY_NAME;
            var logoPath = settingsService?.CurrentSettings?.Unit?.LogoPath;
            var previewDocument = ReportPreviewHelper.GenerateReportDocument(fullReport, unitName, logoPath);
            await previewViewModel.InitializeAsync(fullReport, previewDocument);

            await DialogHost.Show(
                new Views.Dialogs.ReportPreviewDialog
                {
                    DataContext = previewViewModel
                },
                "RootDialog");
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"打开报告预览弹窗失败：ReportId={reportId}", ex);
            throw;
        }
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
            var result = await ShowConfirmDialogAsync(L("Confirm"), L("ConfirmOverwriteReport"));

            if (!result) return;
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

                    _logHelper?.Information($"生成报告成功：ID={report.Id}");

                    // 切换到报告列表
                    CurrentModeIndex = 0;
                });

                await ShowNoticeDialogAsync(L("Tip"), L("Report.GenerateSuccess"));

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
                await ShowNoticeDialogAsync(L("Error"), L("Report.GenerateFailedFormat", ex.Message));
            }
        }
        finally
        {
            TryInvokeOnUI(() => IsGenerating = false);
        }
    }

    /// <summary>
    /// 显示确认对话框。
    /// </summary>
    private static async Task<bool> ShowConfirmDialogAsync(string title, string message, string iconKind = "HelpCircleOutline")
    {
        var result = await DialogHost.Show(
            new Views.Dialogs.ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel
                {
                    Title = title,
                    Message = message,
                    ConfirmText = GetGlobalString("Confirm"),
                    CancelText = GetGlobalString("Cancel"),
                    IsCancelVisible = true,
                    IconKind = iconKind
                }
            },
            "RootDialog").ConfigureAwait(true);

        return result is true;
    }

    /// <summary>
    /// 显示提示对话框。
    /// </summary>
    private static Task ShowNoticeDialogAsync(string title, string message)
    {
        return DialogHost.Show(
            new Views.Dialogs.ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel
                {
                    Title = title,
                    Message = message,
                    ConfirmText = GetGlobalString("Confirm"),
                    IsCancelVisible = false,
                    IconKind = "InformationOutline"
                }
            },
            "RootDialog");
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
                System.Windows.MessageBox.Show(L("SaveSuccess"), L("Tip"),
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
                System.Windows.MessageBox.Show(L("SaveFailedError", ex.Message), L("Error"),
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
        await PrintReportByIdAsync(_currentPreviewReport.Id, refreshPreview: true);
    }

    /// <summary>
    /// 导出PDF命令
    /// </summary>
    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (_currentPreviewReport == null || !CanExportReport || _disposed || App.IsShuttingDown) return;
        await ExportReportPdfAsync(_currentPreviewReport.Id);
    }

    private async Task ExportReportPdfAsync(int reportId)
    {
        if (!CanExportReport || _disposed || App.IsShuttingDown) return;

        try
        {
            var fullReport = await _reportService.GetReportWithAnalysisDataAsync(reportId);
            if (!HasUsableReportDataSource(fullReport))
            {
                await ShowNoticeDialogAsync(L("Tip"), L("Report.ExportUnavailable"));
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = L("Report.ExportTitle"),
                Filter = L("Report.PdfFilter"),
                FileName = $"报告_{fullReport!.ReportNumber}"
            };

            if (dialog.ShowDialog(Application.Current.MainWindow) == true)
            {
                TryInvokeOnUI(() => IsLoading = true);
                var fileName = dialog.FileName;
                var success = await ExportReportPdfToFileAsync(reportId, fileName);

                if (_disposed || App.IsShuttingDown) return;

                if (success)
                {
                    await LoadReportsAsync();
                    System.Windows.MessageBox.Show(L("Report.ExportSuccessFormat", fileName), L("ReportPreview.Export.SuccessTitle"),
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    _logHelper?.Information($"导出报告PDF成功：ID={reportId}, 文件={fileName}");

                    var openResult = System.Windows.MessageBox.Show(L("Report.OpenExportedFile"), L("Tip"),
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
                    System.Windows.MessageBox.Show(L("Report.ExportPdfFailed"), L("Error"),
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            if (!_disposed && !App.IsShuttingDown)
            {
                _logHelper?.Error($"导出报告PDF失败：ID={reportId}", ex);
                System.Windows.MessageBox.Show($"{L("ExportFailed")}: {ex.Message}", L("Error"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        finally
        {
            TryInvokeOnUI(() => IsLoading = false);
        }
    }

    private async Task<bool> ExportReportPdfToFileAsync(
        int reportId,
        string fileName,
        IProgress<OperationProgressInfo>? progress = null,
        int index = 0,
        int total = 1)
    {
        progress?.Report(new OperationProgressInfo(
            CalculateBatchProgress(index, total, 0.05),
            "读取报告数据",
            $"正在读取报告 {Path.GetFileNameWithoutExtension(fileName)}..."));

        var report = await _reportService.GetReportWithAnalysisDataAsync(reportId);
        if (!HasUsableReportDataSource(report))
        {
            _logHelper?.Warning($"导出报告失败：报告关联的测量或分析结果不可用，ReportId={reportId}");
            return false;
        }

        progress?.Report(new OperationProgressInfo(
            CalculateBatchProgress(index, total, 0.35),
            "生成报告预览",
            $"正在生成 {Path.GetFileName(fileName)} 的预览文档..."));

        var originalStatus = report!.Status;
        report.Status = ReportStatus.Completed;
        var previewDocument = await BuildExportPreviewDocumentAsync(report);
        if (previewDocument is null)
        {
            report.Status = originalStatus;
            return false;
        }

        progress?.Report(new OperationProgressInfo(
            CalculateBatchProgress(index, total, 0.55),
            "导出PDF",
            $"正在写入 {Path.GetFileName(fileName)}..."));

        var success = PrintHelper.ExportDocumentToPdf(previewDocument, fileName);

        if (!success)
        {
            report.Status = originalStatus;
            return false;
        }

        progress?.Report(new OperationProgressInfo(
            CalculateBatchProgress(index, total, 0.82),
            "保存导出记录",
            $"正在更新报告导出路径 {Path.GetFileName(fileName)}..."));

        report.PdfFilePath = fileName;
        report.Status = ReportStatus.Completed;
        return await _reportService.UpdateReportAsync(report);
    }

    private static async Task<FlowDocument?> BuildExportPreviewDocumentAsync(Report report)
    {
        var previewViewModel = App.Services?.GetService(typeof(ReportPreviewDialogViewModel)) as ReportPreviewDialogViewModel;
        var settingsService = App.Services?.GetService(typeof(ISettingsService)) as ISettingsService;
        if (previewViewModel is null || settingsService is null)
        {
            return null;
        }

        var unitName = settingsService.CurrentSettings?.Unit?.Name ?? Constants.APP_DISPLAY_NAME;
        var logoPath = settingsService.CurrentSettings?.Unit?.LogoPath;
        var baseDocument = ReportPreviewHelper.GenerateReportDocument(report, unitName, logoPath);
        await previewViewModel.InitializeAsync(report, baseDocument);
        return previewViewModel.PreviewDocument;
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

    private static double CalculateBatchProgress(int index, int total, double innerRatio)
    {
        if (total <= 0)
        {
            return 0;
        }

        var start = Math.Clamp(index / (double)total, 0, 1);
        var end = Math.Clamp((index + 1d) / total, 0, 1);
        return Math.Clamp((start + (end - start) * Math.Clamp(innerRatio, 0, 1)) * 100, 0, 100);
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

    private static string BuildReportPdfFileName(Report report)
    {
        var patientName = report.Patient?.Name ?? report.MeasurementRecord?.Patient?.Name ?? "未知患者";
        var dateText = report.ReportDate == default ? DateTime.Now.ToString("yyyyMMdd_HHmmss") : report.ReportDate.ToString("yyyyMMdd_HHmmss");
        return MakeSafeFileName($"报告_{report.ReportNumber}_{patientName}_{dateText}.pdf");
    }

    private static string GetAvailableFilePath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(directory, $"{name}_{i}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{name}_{DateTime.Now:yyyyMMdd_HHmmssfff}{extension}");
    }

    private static string MakeSafeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeChars = fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        return new string(safeChars);
    }

    private async Task PrintReportByIdAsync(int reportId, bool refreshPreview = false)
    {
        if (_disposed || App.IsShuttingDown) return;

        try
        {
            var fullReport = await _reportService.GetReportWithAnalysisDataAsync(reportId);
            if (!HasUsableReportDataSource(fullReport))
            {
                await ShowNoticeDialogAsync(L("Tip"), L("Report.PrintUnavailable"));
                return;
            }

            TryInvokeOnUI(() => IsLoading = true);

            var success = await _reportService.PrintReportAsync(reportId);

            if (_disposed || App.IsShuttingDown) return;

            if (success)
            {
                _logHelper?.Information($"打印报告成功：ID={reportId}");
                await LoadReportsAsync();

                if (refreshPreview && SelectedReport != null)
                {
                    TryInvokeOnUI(() =>
                    {
                        if (_disposed || App.IsShuttingDown) return;
                        _ = LoadReportPreviewAsync(SelectedReport.Report);
                    });
                }
            }
            else if (!App.IsShuttingDown)
            {
                System.Windows.MessageBox.Show(L("Report.PrintFailedOrCanceled"), L("Tip"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            if (!_disposed && !App.IsShuttingDown)
            {
                _logHelper?.Error($"打印报告失败：ID={reportId}", ex);
                System.Windows.MessageBox.Show(L("Report.PrintFailedFormat", ex.Message), L("Error"),
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

        var result = await ShowConfirmDialogAsync(
            L("ConfirmDelete"),
            L("Report.DeleteConfirmFormat", item.Report.ReportNumber),
            "TrashCanOutline");

        if (!result) return;

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
                    await ShowNoticeDialogAsync(L("Tip"), L("DeleteSuccess"));
                }
            }
        }
        catch (Exception ex)
        {
            if (!_disposed && !App.IsShuttingDown)
            {
                _logHelper?.Error($"删除报告失败：ID={item.Report.Id}", ex);
                await ShowNoticeDialogAsync(L("Error"), L("DeleteFailedError"));
            }
        }
    }

    /// <summary>
    /// 全部导出选中的报告命令。
    /// </summary>
    [RelayCommand]
    private async Task ExportSelectedReportsAsync()
    {
        if (SelectedReportTotalCount <= 0 || _disposed || App.IsShuttingDown)
        {
            return;
        }

        if (!CanExportReport)
        {
            await ShowNoticeDialogAsync(L("Tip"), L("Report.NoExportPermission"));
            return;
        }

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = L("Report.ExportFolderTitle"),
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var selectedIds = _selectedReportIds.ToList();
        var selectedReports = _filteredReports
            .Where(r => selectedIds.Contains(r.Id))
            .OrderByDescending(r => r.ReportDate)
            .ToList();

        if (selectedReports.Count == 0)
        {
            await ShowNoticeDialogAsync(L("Tip"), L("Report.NoSelectedExportableReport"));
            return;
        }

        try
        {
            var result = await RunWithProgressDialogAsync(
                L("Report.BatchExportTitle"),
                L("Report.BatchExportStage"),
                L("Report.BatchExportPreparing"),
                async (progress, token) =>
                {
                    var successCount = 0;
                    var failedReports = new List<string>();

                    for (var i = 0; i < selectedReports.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();

                        var report = selectedReports[i];
                        var fileName = BuildReportPdfFileName(report);
                        var filePath = GetAvailableFilePath(Path.Combine(dialog.FolderName, fileName));

                        progress.Report(new OperationProgressInfo(
                            CalculateBatchProgress(i, selectedReports.Count, 0),
                            L("Report.BatchExportStage"),
                            L("Report.BatchExportItemFormat", report.ReportNumber)));

                        var success = await ExportReportPdfToFileAsync(report.Id, filePath, progress, i, selectedReports.Count);
                        if (success)
                        {
                            successCount++;
                            _logHelper?.Information($"批量导出报告成功：ID={report.Id}, 文件={filePath}");
                        }
                        else
                        {
                            failedReports.Add(report.ReportNumber);
                        }

                        progress.Report(new OperationProgressInfo(
                            CalculateBatchProgress(i, selectedReports.Count, 1),
                            L("Report.BatchExportStage"),
                            L("Report.BatchExportProgressFormat", i + 1, selectedReports.Count)));
                    }

                    return new BatchReportExportResult(successCount, failedReports.Count, failedReports, dialog.FolderName);
                });

            await LoadReportsAsync();

            if (result.FailedCount == 0)
            {
                System.Windows.MessageBox.Show(
                    L("Report.BatchExportSuccessFormat", result.SuccessCount, result.OutputDirectory),
                    L("ExportCompleted"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    L("Report.BatchExportPartialFormat", result.SuccessCount, result.FailedCount, string.Join(", ", result.FailedReports.Take(5))),
                    L("ExportCompleted"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            System.Windows.MessageBox.Show(L("Report.BatchExportCanceled"), L("Tip"),
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logHelper?.Error("批量导出报告失败", ex);
            System.Windows.MessageBox.Show($"{L("ExportFailed")}: {ex.Message}", L("Error"),
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
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
            OnPropertyChanged(nameof(SelectedReportTotalCount));
            OnPropertyChanged(nameof(SelectedReportTotalCountText));
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
            OnPropertyChanged(nameof(SelectedReportTotalCount));
            OnPropertyChanged(nameof(SelectedReportTotalCountText));
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
            var visibleReports = reports
                .Where(HasUsableReportDataSource)
                .ToList();

            if (_disposed || App.IsShuttingDown || _cancellationTokenSource.Token.IsCancellationRequested) return;

            // 在UI线程上更新集合
            await TryInvokeOnUIAsync(() =>
            {
                if (_disposed) return;

                _filteredReports = visibleReports;
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

            _logHelper?.Information($"加载报告列表：共{visibleReports.Count}条，可用报告={visibleReports.Count}，原始记录={reports.Count}");
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
    /// 报告列表只展示已有完整分析数据源的报告，避免旧草稿或孤立记录触发预览、导出、打印错误。
    /// </summary>
    private static bool HasUsableReportDataSource(Report? report)
    {
        return report?.MeasurementRecord?.Status == MeasurementStatus.Completed
            && report.AnalysisResult?.Success == true;
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
            var item = new ReportItem(report, rowNumber++, _localizationService)
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
            OnPropertyChanged(nameof(SelectedReportTotalCount));
            OnPropertyChanged(nameof(SelectedReportTotalCountText));
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
            OnPropertyChanged(nameof(SelectedReportTotalCount));
            OnPropertyChanged(nameof(SelectedReportTotalCountText));
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

    private async Task<bool> ValidateReportAnalysisPackageAsync(Report report)
    {
        if (report.AnalysisResult is null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(report.AnalysisResult.PackagePath))
        {
            return true;
        }

        if (!System.IO.File.Exists(report.AnalysisResult.PackagePath))
        {
            _logHelper?.Warning(
                $"报告关联的结果包文件不存在，已跳过结果包校验: ReportId={report.Id}, AnalysisResultId={report.AnalysisResult.Id}, PackagePath={report.AnalysisResult.PackagePath}");
            return true;
        }

        var packageService = App.Services?.GetService(typeof(IAnalysisPackageService)) as IAnalysisPackageService;
        if (packageService is null)
        {
            return true;
        }

        var validation = await packageService.ValidatePackageAsync(report.AnalysisResult, _cancellationTokenSource.Token);
        if (validation.IsValid)
        {
            return true;
        }

        TryInvokeOnUI(() =>
        {
            MessageBox.Show(
                $"{validation.Message}\n请重新分析后再生成或查看报告。",
                "结果包校验失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        });

        return false;
    }

    /// <summary>
    /// 加载报告预览
    /// </summary>
    private async Task LoadReportPreviewAsync(Report report)
    {
        // 如果应用正在关闭或已释放，不执行任何操作
        if (_disposed || App.IsShuttingDown) return;

        var loadVersion = Interlocked.Increment(ref _previewLoadVersion);

        try
        {
            var fullReport = HasUsableReportDataSource(report)
                ? report
                : await _reportService.GetReportWithAnalysisDataAsync(report.Id);
            if (fullReport == null || _disposed || App.IsShuttingDown)
            {
                return;
            }

            if (loadVersion != _previewLoadVersion)
            {
                return;
            }

            _currentPreviewReport = fullReport;
            DoctorOpinion = fullReport.DoctorOpinion ?? string.Empty;
            HasPreviewContent = true;
            UpdatePreviewSummary(fullReport);

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

            if (loadVersion == _previewLoadVersion)
            {
                TryInvokeOnUI(() => PreviewContent = sb.ToString());
            }
        }
        catch (Exception ex)
        {
            if (!_disposed && !App.IsShuttingDown)
            {
                _logHelper?.Error($"加载报告预览失败：ReportId={report.Id}", ex);
            }
        }
    }

    /// <summary>
    /// 清除预览
    /// </summary>
    private void ClearPreview()
    {
        // 如果应用正在关闭或已释放，不执行任何操作
        if (_disposed || App.IsShuttingDown) return;
        Interlocked.Increment(ref _previewLoadVersion);

        _currentPreviewReport = null;
        PreviewContent = string.Empty;
        DoctorOpinion = string.Empty;
        HasPreviewContent = false;
        PreviewBasicFields.Clear();
        PreviewMetrics.Clear();
        PreviewSectionTags.Clear();
        PreviewSummaryMessage = L("Report.SummaryHint");
    }

    /// <summary>
    /// 更新右侧报告摘要面板。
    /// </summary>
    private void UpdatePreviewSummary(Report report)
    {
        var record = report.MeasurementRecord;
        var patient = record?.Patient ?? report.Patient;
        var gait = record?.GaitParameters;
        var analysis = report.AnalysisResult;
        var quality = report.QualityControl ?? analysis?.QualityControl;
        var data = ReportAnalysisSnapshot.From(report);

        var title = NormalizeReportTitle(report.Title);
        var measurementType = record is null ? "--" : GetMeasurementTypeText(record.MeasurementType);
        var analysisMode = record is null
            ? "--"
            : record.HasDualVideo ? L("Report.Mode.Dual") : record.HasSideVideo || record.HasFrontVideo ? L("Report.Mode.Single") : "--";

        PreviewBasicFields = new ObservableCollection<ReportSummaryField>
        {
            new(L("Report.Field.Title"), title),
            new(L("Report.Field.Number"), EmptyToDash(report.ReportNumber)),
            new(L("Report.Field.PatientName"), EmptyToDash(patient?.Name)),
            new(L("Report.Field.MeasurementType"), measurementType),
            new(L("Report.Field.AnalysisMode"), analysisMode),
            new(L("Report.Field.MeasurementTime"), record?.MeasurementDate.ToString(Constants.DATETIME_FORMAT) ?? "--"),
            new(L("Report.Field.GeneratedTime"), report.CreatedAt.ToString(Constants.DATETIME_FORMAT)),
            new(L("Report.Field.Status"), GetStatusText(report.Status))
        };

        PreviewMetrics = new ObservableCollection<ReportSummaryMetric>
        {
            new(L("Report.Metric.GaitCycle"), FormatNumber(data.MeanCycleDurationSec ?? analysis?.GaitCycleDurationS ?? gait?.GaitCycleDurationS, "F2"), "s"),
            new(L("Report.Metric.AverageStepLength"), FormatNumber(data.MeanStepLengthM ?? analysis?.StepLengthM ?? gait?.StepLengthM, "F2"), "m"),
            new(L("Report.Metric.AverageCadence"), FormatNumber(data.CadenceStepPerMin ?? gait?.Cadence, "F1"), "step/min"),
            new(L("Report.Metric.AverageGaitSpeed"), FormatNumber(data.GaitSpeedMPerS ?? analysis?.GaitSpeedMPerS ?? gait?.GaitSpeedMPerS ?? gait?.Velocity, "F2"), "m/s"),
            new(L("Report.Metric.ValidFrameRatio"), FormatPercent(data.ValidFrameRatio ?? quality?.ValidFrameRatio), string.Empty)
        };

        PreviewSectionTags = new ObservableCollection<string>(BuildReportSectionTags(report));
        PreviewSummaryMessage = BuildPreviewSummaryMessage(report, PreviewSectionTags.Count);
    }

    private string BuildPreviewSummaryMessage(Report report, int sectionCount)
    {
        return L("Report.SummaryMessageFormat", sectionCount);
    }

    private string NormalizeReportTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return L("Report.DefaultTitle");
        }

        const string legacyDefaultTitle = "步态分析报告";
        const string legacyDefaultTitlePrefix = "步态分析报告 - ";

        if (string.Equals(title, legacyDefaultTitle, StringComparison.Ordinal))
        {
            return L("Report.DefaultTitle");
        }

        if (title.StartsWith(legacyDefaultTitlePrefix, StringComparison.Ordinal))
        {
            var suffix = title[legacyDefaultTitlePrefix.Length..].Trim();
            return string.IsNullOrWhiteSpace(suffix)
                ? L("Report.DefaultTitle")
                : L("AnalysisDetail.ReportConfig.DefaultReportTitleFormat", suffix);
        }

        return title;
    }

    private IReadOnlyList<string> BuildReportSectionTags(Report report)
    {
        var tags = new List<string>();
        var options = ParseReportOptions(report.ReportOptionsJson);

        if (options is null || options.IncludeSpatiotemporalParameters)
        {
            tags.Add(L("Report.Section.Spatiotemporal"));
        }

        if (options is null || options.IncludeKinematicSummary)
        {
            tags.Add(L("Report.Section.Kinematic"));
            tags.Add(L("Report.Section.TrunkPelvis"));
            tags.Add(L("Report.Section.Charts"));
        }

        if (options is null || options.IncludeQualityControl)
        {
            tags.Add(L("Report.Section.Symmetry"));
        }

        tags.Add(L("Report.Section.SideParameters"));
        return tags.Distinct().ToArray();
    }

    private static ReportDraftOptions? ParseReportOptions(string? reportOptionsJson)
    {
        if (string.IsNullOrWhiteSpace(reportOptionsJson))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<ReportDraftOptions>(reportOptionsJson);
        }
        catch
        {
            return null;
        }
    }

    private static string EmptyToDash(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }

    private static string FormatNumber(double? value, string format)
    {
        return value.HasValue ? value.Value.ToString(format) : "--";
    }

    private static string FormatPercent(double? value)
    {
        if (!value.HasValue)
        {
            return "--";
        }

        var percent = value.Value <= 1 ? value.Value * 100 : value.Value;
        return $"{percent:F1}%";
    }

    private static string BuildQualityLevel(QualityControlInfo? quality)
    {
        if (quality?.ValidFrameRatio is not double ratio)
        {
            return "--";
        }

        return ratio >= 0.95 ? "A级" : ratio >= 0.85 ? "B级" : "C级";
    }

    private string GetMeasurementTypeText(MeasurementType type)
    {
        return type switch
        {
            MeasurementType.NormalWalk => L("MeasurementType.NormalWalk"),
            MeasurementType.FastWalk => L("MeasurementType.FastWalk"),
            MeasurementType.SlowWalk => L("MeasurementType.SlowWalk"),
            MeasurementType.Other => L("MeasurementType.Other"),
            _ => type.ToString()
        };
    }

        /// <summary>
        /// 获取状态文本
        /// </summary>
        private string GetStatusText(ReportStatus status)
        {
            return status switch
            {
                ReportStatus.Draft => L("Report.Status.Viewable"),
                ReportStatus.Completed => L("Report.Status.Viewable"),
                ReportStatus.Printed => L("Report.Status.Viewable"),
                ReportStatus.Outdated => L("Report.Status.Viewable"),
                _ => L("Report.Status.Unknown")
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
            if (_localizationService is not null)
            {
                _localizationService.LanguageChanged -= OnLanguageChanged;
            }
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            _logHelper?.Information("ReportViewModel disposed");
        }

        #endregion
    }

#region 辅助类

/// <summary>
/// 报告摘要字段。
/// </summary>
public sealed record ReportSummaryField(string Label, string Value);

/// <summary>
/// 报告摘要指标。
/// </summary>
public sealed record ReportSummaryMetric(string Name, string Value, string Unit);

/// <summary>
/// 批量导出报告结果。
/// </summary>
public sealed record BatchReportExportResult(
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<string> FailedReports,
    string OutputDirectory);

/// <summary>
/// 报告列表项
/// </summary>
public partial class ReportItem : ObservableObject
{
    private ILocalizationService? _localizationService;

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
        ReportStatus.Outdated => "需重新生成",
        _ => "待生成"
    };

    /// <summary>
    /// 状态图标
    /// </summary>
    public string StatusIcon => Report.Status is ReportStatus.Completed or ReportStatus.Printed
        ? "/Resources/Images/DataManagement/yiwancheng.png"
        : "/Resources/Images/Report/daishengcheng.png";

    /// <summary>
    /// 状态背景
    /// </summary>
    public string StatusBackground => Report.Status switch
    {
        ReportStatus.Completed or ReportStatus.Printed => "#E9F7E3",
        ReportStatus.Outdated => "#FDECEC",
        _ => "#FDF4E6"
    };

    /// <summary>
    /// 状态前景色
    /// </summary>
    public string StatusForeground => Report.Status switch
    {
        ReportStatus.Completed or ReportStatus.Printed => "#44BE13",
        ReportStatus.Outdated => "#E4004A",
        _ => "#FF932D"
    };

    /// <summary>
    /// 详情提示
    /// </summary>
    public string DetailHint => L("Report.Tooltip.DetailFormat", PatientName);

    /// <summary>
    /// 导出提示
    /// </summary>
    public string ExportHint => L("Report.Tooltip.ExportFormat", PatientName);

    /// <summary>
    /// 打印提示
    /// </summary>
    public string PrintHint => L("Report.Tooltip.PrintFormat", PatientName);

    public string PrimaryActionText => Report.Status switch
    {
        _ => L("Report.Action.ViewReport")
    };

    /// <summary>
    /// 状态颜色
    /// </summary>
    public string StatusColor => Report.Status switch
    {
        ReportStatus.Draft => "#FF932D",
        ReportStatus.Completed => "#44BE13",
        ReportStatus.Printed => "#44BE13",
        ReportStatus.Outdated => "#E4004A",
        _ => "#9E9E9E"
    };

    public ReportItem(Report report, int rowNumber, ILocalizationService? localizationService = null)
    {
        Report = report;
        RowNumber = rowNumber;
        _localizationService = localizationService;
    }

    public void SetLocalizationService(ILocalizationService? localizationService)
    {
        _localizationService = localizationService;
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(DetailHint));
        OnPropertyChanged(nameof(ExportHint));
        OnPropertyChanged(nameof(PrintHint));
        OnPropertyChanged(nameof(PrimaryActionText));
    }

    private string L(string key)
    {
        var value = _localizationService?.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private string L(string key, params object[] args)
    {
        var value = _localizationService?.GetString(key, args);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }
}

#endregion

