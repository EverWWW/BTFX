using System.Collections.ObjectModel;
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
public partial class ReportViewModel : ObservableObject
{
    private readonly IReportService _reportService;
    private readonly IMeasurementService _measurementService;
    private readonly ISessionService _sessionService;
    private readonly IExportImportService _exportImportService;
    private readonly ILogHelper? _logHelper;

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
        OnPropertyChanged(nameof(IsListMode));
        OnPropertyChanged(nameof(IsGenerateMode));

        if (IsGenerateMode)
        {
            _ = LoadMeasurementsAsync();
        }
    }

    partial void OnDoctorOpinionChanged(string value)
    {
        OnPropertyChanged(nameof(DoctorOpinionLength));
    }

    partial void OnSelectedReportChanged(ReportItem? value)
    {
        if (value != null)
        {
            LoadReportPreview(value.Report);
        }
        else
        {
            ClearPreview();
        }
    }

    partial void OnSelectedMeasurementChanged(MeasurementRecordItem? value)
    {
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

    #endregion

    #region 命令

    /// <summary>
    /// 搜索报告命令
    /// </summary>
    [RelayCommand]
    private async Task SearchReportsAsync()
    {
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
        await LoadReportsAsync();
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
        if (SelectedMeasurement == null || !CanGenerateReport) return;

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

        try
        {
            IsGenerating = true;

            var report = await _reportService.GenerateReportAsync(
                SelectedMeasurement.Record.Id,
                _sessionService.CurrentUser?.Id ?? 0);

            if (report != null)
            {
                System.Windows.MessageBox.Show("报告生成成功！", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                _logHelper?.Information($"生成报告成功：ID={report.Id}");

                // 切换到报告列表并刷新
                CurrentModeIndex = 0;
                await LoadReportsAsync();

                // 选中新生成的报告
                var newItem = Reports.FirstOrDefault(r => r.Report.Id == report.Id);
                if (newItem != null)
                {
                    SelectedReport = newItem;
                }
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("生成报告失败", ex);
            System.Windows.MessageBox.Show($"生成报告失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>
    /// 保存报告命令
    /// </summary>
    [RelayCommand]
    private async Task SaveReportAsync()
    {
        if (_currentPreviewReport == null || !CanEditReport) return;

        try
        {
            _currentPreviewReport.DoctorOpinion = DoctorOpinion;
            _currentPreviewReport.UpdatedAt = DateTime.Now;

            var success = await _reportService.UpdateReportAsync(_currentPreviewReport);
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
            _logHelper?.Error($"保存报告失败：ID={_currentPreviewReport?.Id}", ex);
            System.Windows.MessageBox.Show($"保存失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 打印报告命令
    /// </summary>
    [RelayCommand]
    private async Task PrintReportAsync()
    {
        if (_currentPreviewReport == null) return;

        try
        {
            // TODO: 调用打印服务
            System.Windows.MessageBox.Show("打印功能开发中...", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

            // 更新报告状态为已打印
            _currentPreviewReport.Status = ReportStatus.Printed;
            await _reportService.UpdateReportAsync(_currentPreviewReport);
            await LoadReportsAsync();

            _logHelper?.Information($"打印报告：ID={_currentPreviewReport.Id}");
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"打印报告失败：ID={_currentPreviewReport?.Id}", ex);
        }
    }

    /// <summary>
    /// 导出PDF命令
    /// </summary>
    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (_currentPreviewReport == null || !CanExportReport) return;

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
                // TODO: 调用PDF导出服务
                await Task.Delay(100); // 模拟导出
                System.Windows.MessageBox.Show("PDF导出功能开发中...", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                _logHelper?.Information($"导出报告PDF：ID={_currentPreviewReport.Id}, 文件={dialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"导出报告PDF失败：ID={_currentPreviewReport?.Id}", ex);
            System.Windows.MessageBox.Show($"导出失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 删除报告命令
    /// </summary>
    [RelayCommand]
    private async Task DeleteReportAsync(ReportItem? item)
    {
        if (item == null || !CanDeleteReport) return;

        var result = System.Windows.MessageBox.Show(
            $"确定要删除报告 {item.Report.ReportNumber} 吗？此操作不可恢复！",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            var success = await _reportService.DeleteReportAsync(item.Report.Id);
            if (success)
            {
                await LoadReportsAsync();
                ClearPreview();
                _logHelper?.Information($"删除报告：ID={item.Report.Id}");
                System.Windows.MessageBox.Show("删除成功！", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除报告失败：ID={item.Report.Id}", ex);
            System.Windows.MessageBox.Show($"删除失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 加载报告列表
    /// </summary>
    private async Task LoadReportsAsync()
    {
        try
        {
            IsLoading = true;

            var reports = await _reportService.GetReportsAsync(
                ReportFilterPatientName,
                ReportFilterStartDate,
                ReportFilterEndDate);

            Reports.Clear();
            int rowNumber = 1;
            foreach (var report in reports)
            {
                Reports.Add(new ReportItem(report, rowNumber++));
            }

            _logHelper?.Information($"加载报告列表：共{reports.Count}条");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载报告列表失败", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 加载测量数据列表（仅已完成的测量）
    /// </summary>
    private async Task LoadMeasurementsAsync()
    {
        try
        {
            IsLoading = true;

            var (records, _) = await _measurementService.GetMeasurementsPagedAsync(
                MeasurementFilterPatientName,
                MeasurementFilterStartDate,
                MeasurementFilterEndDate,
                MeasurementStatus.Completed, // 仅已完成的测量
                1, 100); // 获取前100条

            MeasurementRecords.Clear();
            int rowNumber = 1;
            foreach (var record in records)
            {
                MeasurementRecords.Add(new MeasurementRecordItem(record, rowNumber++));
            }

            _logHelper?.Information($"加载测量数据列表：共{records.Count}条");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载测量数据列表失败", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 检查是否已有报告
    /// </summary>
    private async Task CheckExistingReportAsync(MeasurementRecord record)
    {
        try
        {
            var existingReport = await _reportService.GetReportByMeasurementIdAsync(record.Id);
            HasExistingReport = existingReport != null;

            if (HasExistingReport && existingReport != null)
            {
                ExistingReportInfo = $"该测量数据已有报告：{existingReport.ReportNumber}（{existingReport.CreatedAt:yyyy-MM-dd}）";
            }
            else
            {
                ExistingReportInfo = string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"检查已有报告失败：MeasurementId={record.Id}", ex);
            HasExistingReport = false;
            ExistingReportInfo = string.Empty;
        }
    }

    /// <summary>
    /// 加载报告预览
    /// </summary>
    private void LoadReportPreview(Report report)
    {
        _currentPreviewReport = report;
        DoctorOpinion = report.DoctorOpinion ?? string.Empty;
        HasPreviewContent = true;

        // 构建预览内容
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"报告编号：{report.ReportNumber}");
        sb.AppendLine($"生成日期：{report.CreatedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"报告状态：{GetStatusText(report.Status)}");
        sb.AppendLine();

        if (report.MeasurementRecord?.Patient != null)
        {
            var patient = report.MeasurementRecord.Patient;
            sb.AppendLine("【患者信息】");
            sb.AppendLine($"姓名：{patient.Name}");
            sb.AppendLine($"性别：{(patient.Gender == Gender.Male ? "男" : "女")}");
            sb.AppendLine($"年龄：{patient.Age}岁");
            sb.AppendLine();
        }

        if (report.MeasurementRecord != null)
        {
            sb.AppendLine("【测量信息】");
            sb.AppendLine($"测量日期：{report.MeasurementRecord.MeasurementDate:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"操作员：{report.MeasurementRecord.Operator?.Name ?? "未知"}");
            sb.AppendLine();

            if (report.MeasurementRecord.GaitParameters != null)
            {
                var gait = report.MeasurementRecord.GaitParameters;
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

        if (!string.IsNullOrEmpty(report.DoctorOpinion))
        {
            sb.AppendLine("【医生意见】");
            sb.AppendLine(report.DoctorOpinion);
        }

        PreviewContent = sb.ToString();
    }

    /// <summary>
    /// 清除预览
    /// </summary>
    private void ClearPreview()
    {
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
        ReportStatus.Draft => "草稿",
        ReportStatus.Completed => "已完成",
        ReportStatus.Printed => "已打印",
        _ => "未知"
    };

    /// <summary>
    /// 状态颜色
    /// </summary>
    public string StatusColor => Report.Status switch
    {
        ReportStatus.Draft => "#FF9800",      // 橙色
        ReportStatus.Completed => "#4CAF50",  // 绿色
        ReportStatus.Printed => "#2196F3",    // 蓝色
        _ => "#9E9E9E"                        // 灰色
    };

    public ReportItem(Report report, int rowNumber)
    {
        Report = report;
        RowNumber = rowNumber;
    }
}

#endregion
