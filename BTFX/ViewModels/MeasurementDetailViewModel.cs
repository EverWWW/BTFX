using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTFX.ViewModels;

/// <summary>
/// 测量详情视图模型
/// </summary>
public partial class MeasurementDetailViewModel : ObservableObject
{
    private readonly ISessionService _sessionService;
    private readonly IExportImportService _exportImportService;

    #region 属性

    /// <summary>
    /// 测量记录
    /// </summary>
    [ObservableProperty]
    private MeasurementRecord? _record;

    /// <summary>
    /// 是否可导出
    /// </summary>
    [ObservableProperty]
    private bool _canExport;

    #endregion

    #region 显示属性

    /// <summary>
    /// 患者姓名
    /// </summary>
    public string PatientName => Record?.Patient?.Name ?? "--";

    /// <summary>
    /// 性别
    /// </summary>
    public string Gender => Record?.Patient?.GenderDisplay ?? "--";

    /// <summary>
    /// 年龄
    /// </summary>
    public string Age => Record?.Patient?.Age?.ToString() ?? "--";

    /// <summary>
    /// 电话
    /// </summary>
    public string Phone => Record?.Patient?.Phone ?? "--";

    /// <summary>
    /// 证件号
    /// </summary>
    public string IdNumber => Record?.Patient?.IdNumber ?? "--";

    /// <summary>
    /// 测量日期
    /// </summary>
    public string MeasurementDate => Record?.MeasurementDate.ToString(Constants.DATETIME_FORMAT) ?? "--";

    /// <summary>
    /// 操作员
    /// </summary>
    public string OperatorName => Record?.Operator?.Name ?? "--";

    /// <summary>
    /// 测量状态
    /// </summary>
    public string Status => Record?.Status switch
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
    public string StatusColor => Record?.Status switch
    {
        MeasurementStatus.Pending => "#FF9800",
        MeasurementStatus.InProgress => "#2196F3",
        MeasurementStatus.Completed => "#4CAF50",
        MeasurementStatus.Cancelled => "#9E9E9E",
        MeasurementStatus.Failed => "#F44336",
        _ => "#9E9E9E"
    };

    /// <summary>
    /// 测量时长
    /// </summary>
    public string Duration => Record?.DurationSeconds != null
        ? $"{Record.DurationSeconds / 60}分{Record.DurationSeconds % 60}秒"
        : "--";

    /// <summary>
    /// 是否有步态参数
    /// </summary>
    public bool HasGaitParameters => Record?.GaitParameters != null;

    #region 步态参数

    /// <summary>
    /// 左脚步幅
    /// </summary>
    public string StrideLengthLeft => Record?.GaitParameters?.StrideLengthLeft?.ToString("F2") ?? "--";

    /// <summary>
    /// 右脚步幅
    /// </summary>
    public string StrideLengthRight => Record?.GaitParameters?.StrideLengthRight?.ToString("F2") ?? "--";

    /// <summary>
    /// 步频
    /// </summary>
    public string Cadence => Record?.GaitParameters?.Cadence?.ToString("F1") ?? "--";

    /// <summary>
    /// 步速
    /// </summary>
    public string Velocity => Record?.GaitParameters?.Velocity?.ToString("F2") ?? "--";

    /// <summary>
    /// 左脚支撑相
    /// </summary>
    public string StancePhaseLeft => Record?.GaitParameters?.StancePhaseLeft?.ToString("F1") ?? "--";

    /// <summary>
    /// 右脚支撑相
    /// </summary>
    public string StancePhaseRight => Record?.GaitParameters?.StancePhaseRight?.ToString("F1") ?? "--";

    /// <summary>
    /// 双支撑时间
    /// </summary>
    public string DoubleSupport => Record?.GaitParameters?.DoubleSupport?.ToString("F1") ?? "--";

    /// <summary>
    /// 步宽
    /// </summary>
    public string StepWidth => Record?.GaitParameters?.StepWidth?.ToString("F2") ?? "--";

    #endregion

    #endregion

    /// <summary>
    /// 对话框关闭请求事件
    /// </summary>
    public event Action? CloseRequested;

    /// <summary>
    /// 构造函数
    /// </summary>
    public MeasurementDetailViewModel(ISessionService sessionService, IExportImportService exportImportService)
    {
        _sessionService = sessionService;
        _exportImportService = exportImportService;
        CanExport = _sessionService.HasPermission("export");
    }

    /// <summary>
    /// 初始化
    /// </summary>
    public void Initialize(MeasurementRecord record)
    {
        Record = record;
        OnPropertyChanged(nameof(PatientName));
        OnPropertyChanged(nameof(Gender));
        OnPropertyChanged(nameof(Age));
        OnPropertyChanged(nameof(Phone));
        OnPropertyChanged(nameof(IdNumber));
        OnPropertyChanged(nameof(MeasurementDate));
        OnPropertyChanged(nameof(OperatorName));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(HasGaitParameters));
        OnPropertyChanged(nameof(StrideLengthLeft));
        OnPropertyChanged(nameof(StrideLengthRight));
        OnPropertyChanged(nameof(Cadence));
        OnPropertyChanged(nameof(Velocity));
        OnPropertyChanged(nameof(StancePhaseLeft));
        OnPropertyChanged(nameof(StancePhaseRight));
        OnPropertyChanged(nameof(DoubleSupport));
        OnPropertyChanged(nameof(StepWidth));
    }

    /// <summary>
    /// 关闭命令
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke();
    }

    /// <summary>
    /// 导出命令
    /// </summary>
    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Record == null || !CanExport) return;

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                                Title = "导出测量数据",
                                Filter = "Excel文件 (*.xlsx)|*.xlsx|CSV文件 (*.csv)|*.csv",
                                FileName = $"测量数据_{Record.Patient?.Name}_{Record.MeasurementDate:yyyyMMdd}"
                            };

                            if (dialog.ShowDialog() == true)
                            {
                                var format = dialog.FilterIndex == 1 ? ExportFormat.Excel : ExportFormat.CSV;
                                var success = await _exportImportService.ExportMeasurementsAsync(
                                    new List<MeasurementRecord> { Record }, format, dialog.FileName);

                                if (success)
                                {
                                    System.Windows.MessageBox.Show("导出成功！", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                                }
                                else
                                {
                                    System.Windows.MessageBox.Show("导出失败，请重试", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show($"导出失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        }
                    }
                }
