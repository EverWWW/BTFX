using System.Collections.ObjectModel;
using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.ViewModels.Measurement;

/// <summary>
/// 测量评估模块主 ViewModel
/// 管理四步向导流程和全局状态
/// </summary>
public partial class MeasurementViewModel : ObservableObject
{
    private readonly ISessionService _sessionService;
    private readonly IMeasurementService _measurementService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;

    #region 步骤状态

    /// <summary>
    /// 当前步骤 (1-4)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(IsStep3))]
    [NotifyPropertyChangedFor(nameof(IsStep4))]
    [NotifyPropertyChangedFor(nameof(CanGoToStep2))]
    [NotifyPropertyChangedFor(nameof(CanGoToStep3))]
    [NotifyPropertyChangedFor(nameof(CanGoToStep4))]
    private int _currentStep = 1;

    /// <summary>
    /// 是否在步骤1
    /// </summary>
    public bool IsStep1 => CurrentStep == 1;

    /// <summary>
    /// 是否在步骤2
    /// </summary>
    public bool IsStep2 => CurrentStep == 2;

    /// <summary>
    /// 是否在步骤3
    /// </summary>
    public bool IsStep3 => CurrentStep == 3;

    /// <summary>
    /// 是否在步骤4
    /// </summary>
    public bool IsStep4 => CurrentStep == 4;

    #endregion

    #region 测量记录状态

    /// <summary>
    /// 是否已创建测量记录
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoToStep2))]
    [NotifyCanExecuteChangedFor(nameof(SelectFrontVideoCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectSideVideoCommand))]
    private bool _hasMeasurementRecord;

    /// <summary>
    /// 是否有正面视频
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoToStep3))]
    [NotifyPropertyChangedFor(nameof(CanGoToStep4))]
    [NotifyPropertyChangedFor(nameof(HasAnyVideo))]
    [NotifyPropertyChangedFor(nameof(HasDualVideo))]
    [NotifyCanExecuteChangedFor(nameof(ClearFrontVideoCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartAnalyzeCommand))]
    private bool _hasFrontVideo;

    /// <summary>
    /// 是否有侧面视频
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoToStep3))]
    [NotifyPropertyChangedFor(nameof(CanGoToStep4))]
    [NotifyPropertyChangedFor(nameof(HasAnyVideo))]
    [NotifyPropertyChangedFor(nameof(HasDualVideo))]
    [NotifyCanExecuteChangedFor(nameof(ClearSideVideoCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartAnalyzeCommand))]
    private bool _hasSideVideo;

    /// <summary>
    /// 是否有任一视频
    /// </summary>
    public bool HasAnyVideo => HasFrontVideo || HasSideVideo;

    /// <summary>
    /// 是否有双视频（可进行分析）
    /// </summary>
    public bool HasDualVideo => HasFrontVideo && HasSideVideo;

    #endregion

    #region 分析状态

    /// <summary>
    /// 是否正在分析
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartAnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelAnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectFrontVideoCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectSideVideoCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearFrontVideoCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearSideVideoCommand))]
    private bool _isAnalyzing;

    /// <summary>
    /// 已完成的分析阶段
    /// </summary>
    [ObservableProperty]
    private AnalysisStage _completedStage = AnalysisStage.None;

    /// <summary>
    /// 当前选择的分析阶段
    /// </summary>
    [ObservableProperty]
    private AnalysisStage _selectedStage = AnalysisStage.Keypoints;

    /// <summary>
    /// 分析进度 (0-100)
    /// </summary>
    [ObservableProperty]
    private int _analysisProgress;

    #endregion

    #region 任务抽屉

    /// <summary>
    /// 任务抽屉是否展开
    /// </summary>
    [ObservableProperty]
    private bool _isTaskDrawerOpen;

    /// <summary>
    /// 当前任务名称
    /// </summary>
    [ObservableProperty]
    private string _currentTaskName = string.Empty;

    /// <summary>
    /// 任务日志
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _taskLogs = new();

    #endregion

    #region 当前测量记录

    /// <summary>
    /// 当前测量记录（内存中）
    /// </summary>
    [ObservableProperty]
    private MeasurementRecord? _currentMeasurement;

    /// <summary>
    /// 正面视频路径
    /// </summary>
    [ObservableProperty]
    private string? _frontVideoPath;

    /// <summary>
    /// 侧面视频路径
    /// </summary>
    [ObservableProperty]
    private string? _sideVideoPath;

    #endregion

    #region Step1 字段

    /// <summary>
    /// 测量名称
    /// </summary>
    [ObservableProperty]
    private string _measurementName = string.Empty;

    /// <summary>
    /// 测量类型
    /// </summary>
    [ObservableProperty]
    private MeasurementType _selectedMeasurementType = MeasurementType.NormalWalk;

    /// <summary>
    /// 备注
    /// </summary>
    [ObservableProperty]
    private string _remark = string.Empty;

    /// <summary>
    /// 视频规格
    /// </summary>
    [ObservableProperty]
    private VideoSpec _selectedVideoSpec = VideoSpec.P1080_30fps;

    /// <summary>
    /// 步道长度
    /// </summary>
    [ObservableProperty]
    private double _walkwayLength = 6.0;

    /// <summary>
    /// 视频导入模式
    /// </summary>
    [ObservableProperty]
    private VideoImportMode _selectedVideoImportMode = VideoImportMode.Import;

    /// <summary>
    /// 导入策略
    /// </summary>
    [ObservableProperty]
    private ImportStrategy _selectedImportStrategy = ImportStrategy.CopyToFolder;

    /// <summary>
    /// 步道长度验证错误
    /// </summary>
    [ObservableProperty]
    private string? _walkwayLengthError;

    #endregion

    #region 步骤可进入规则

    /// <summary>
    /// 可以进入步骤2（需要已创建测量）
    /// </summary>
    public bool CanGoToStep2 => HasMeasurementRecord;

    /// <summary>
    /// 可以进入步骤3（需要至少一个视频）
    /// </summary>
    public bool CanGoToStep3 => HasFrontVideo || HasSideVideo;

    /// <summary>
    /// 可以进入步骤4（需要双视频）
    /// </summary>
    public bool CanGoToStep4 => HasFrontVideo && HasSideVideo;

    #endregion

    #region 患者信息（从 SessionService 获取）

    /// <summary>
    /// 当前患者
    /// </summary>
    public Patient? CurrentPatient => _sessionService.CurrentPatient;

    /// <summary>
    /// 是否有当前患者
    /// </summary>
    public bool HasCurrentPatient => CurrentPatient != null;

    /// <summary>
    /// 当前患者姓名
    /// </summary>
    public string CurrentPatientName => CurrentPatient?.Name ?? string.Empty;

    #endregion

    /// <summary>
    /// 构造函数
    /// </summary>
    public MeasurementViewModel(
        ISessionService sessionService,
        IMeasurementService measurementService,
        ILocalizationService localizationService,
        ILogHelper? logHelper = null)
    {
        _sessionService = sessionService;
        _measurementService = measurementService;
        _localizationService = localizationService;
        _logHelper = logHelper;

        // 初始化测量名称为当前日期时间
        ResetMeasurementFields();

        // 订阅患者变更事件
        _sessionService.CurrentPatientChanged += OnCurrentPatientChanged;

        _logHelper?.Information("MeasurementViewModel 初始化完成");
    }

    /// <summary>
    /// 患者变更事件处理
    /// </summary>
    private void OnCurrentPatientChanged(object? sender, Patient? patient)
    {
        OnPropertyChanged(nameof(CurrentPatient));
        OnPropertyChanged(nameof(HasCurrentPatient));
        OnPropertyChanged(nameof(CurrentPatientName));

        // 患者变更时重置测量状态
        if (patient == null)
        {
            ResetAllState();
        }
    }

    /// <summary>
    /// 重置测量字段为默认值
    /// </summary>
    private void ResetMeasurementFields()
    {
        MeasurementName = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        SelectedMeasurementType = MeasurementType.NormalWalk;
        Remark = string.Empty;
        SelectedVideoSpec = VideoSpec.P1080_30fps;
        WalkwayLength = 6.0;
        SelectedVideoImportMode = VideoImportMode.Import;
        SelectedImportStrategy = ImportStrategy.CopyToFolder;
        WalkwayLengthError = null;
    }

    /// <summary>
    /// 重置所有状态
    /// </summary>
    private void ResetAllState()
    {
        CurrentStep = 1;
        HasMeasurementRecord = false;
        HasFrontVideo = false;
        HasSideVideo = false;
        IsAnalyzing = false;
        CompletedStage = AnalysisStage.None;
        SelectedStage = AnalysisStage.Keypoints;
        AnalysisProgress = 0;
        CurrentMeasurement = null;
        FrontVideoPath = null;
        SideVideoPath = null;
        TaskLogs.Clear();
        ResetMeasurementFields();
    }

    #region 步骤导航命令

    /// <summary>
    /// 跳转到指定步骤
    /// </summary>
    [RelayCommand]
    private void GoToStep(object? stepParameter)
    {
        // 处理参数类型转换（XAML CommandParameter 传递的是字符串）
        int stepIndex;
        if (stepParameter is int intValue)
        {
            stepIndex = intValue;
        }
        else if (stepParameter is string strValue && int.TryParse(strValue, out var parsed))
        {
            stepIndex = parsed;
        }
        else
        {
            _logHelper?.Warning($"无效的步骤参数: {stepParameter}");
            return;
        }

        if (stepIndex < 1 || stepIndex > 4) return;

        // 检查步骤可进入规则
        switch (stepIndex)
        {
            case 2 when !CanGoToStep2:
                _logHelper?.Warning("无法进入步骤2：未创建测量记录");
                return;
            case 3 when !CanGoToStep3:
                _logHelper?.Warning("无法进入步骤3：未导入视频");
                return;
            case 4 when !CanGoToStep4:
                _logHelper?.Warning("无法进入步骤4：需要双视频");
                return;
        }

        CurrentStep = stepIndex;
        _logHelper?.Information($"切换到步骤 {stepIndex}");
    }

    /// <summary>
    /// 下一步
    /// </summary>
    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStep < 4)
        {
            GoToStep(CurrentStep + 1);
        }
    }

    /// <summary>
    /// 上一步
    /// </summary>
    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 1)
        {
            GoToStep(CurrentStep - 1);
        }
    }

    #endregion

    #region Step1 命令

    /// <summary>
    /// 创建测量记录
    /// </summary>
    [RelayCommand]
    private async Task CreateMeasurementAsync()
    {
        // 验证患者
        if (!HasCurrentPatient)
        {
            _logHelper?.Warning("创建测量失败：未选择患者");
            return;
        }

        // 验证步道长度
        if (WalkwayLength <= 0)
        {
            WalkwayLengthError = _localizationService.GetString("MA.Step1.Validation.WalkwayLengthInvalid");
            return;
        }
        WalkwayLengthError = null;

        // 如果已有测量记录，弹出确认对话框
        if (HasMeasurementRecord)
        {
            var result = await DialogHost.Show(
                new Views.Dialogs.ConfirmDialog
                {
                    DataContext = new
                    {
                        Title = _localizationService.GetString("MA.Dialog.OverrideMeasurement.Title"),
                        Message = _localizationService.GetString("MA.Dialog.OverrideMeasurement.Content")
                    }
                },
                "RootDialog");

            if (result is not true)
            {
                return;
            }
        }

        try
        {
            // 创建测量记录
            var measurement = new MeasurementRecord
            {
                PatientId = CurrentPatient!.Id,
                OperatorId = _sessionService.CurrentUser?.Id ?? 0,
                MeasurementName = MeasurementName,
                MeasurementType = SelectedMeasurementType,
                Remark = Remark,
                VideoSpec = SelectedVideoSpec,
                WalkwayLength = WalkwayLength,
                VideoImportMode = SelectedVideoImportMode,
                ImportStrategy = SelectedImportStrategy,
                Status = MeasurementStatus.Pending,
                IsGuestData = _sessionService.IsGuestMode,
                MeasurementDate = DateTime.Now,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            // 保存到数据库
            var id = await _measurementService.CreateMeasurementAsync(measurement);
            measurement.Id = id;

            CurrentMeasurement = measurement;
            HasMeasurementRecord = true;

            _logHelper?.Information($"创建测量记录成功: Id={id}, Name={MeasurementName}");

            // 自动跳转到步骤2
            CurrentStep = 2;
        }
        catch (Exception ex)
        {
            _logHelper?.Error("创建测量记录失败", ex);
        }
    }

    /// <summary>
    /// 重置测量表单
    /// </summary>
    [RelayCommand]
    private void ResetMeasurement()
    {
        ResetMeasurementFields();
    }

    #endregion

    #region Step2 命令

    /// <summary>
    /// 选择正面视频
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSelectVideo))]
    private void SelectFrontVideo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = _localizationService.GetString("MA.Step2.SelectVideoTitle"),
            Filter = $"{_localizationService.GetString("MA.Step2.VideoFilter")}|*.mp4;*.avi;*.mov;*.mkv;*.wmv|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            FrontVideoPath = dialog.FileName;
            HasFrontVideo = true;

            if (CurrentMeasurement != null)
            {
                CurrentMeasurement.FrontVideoPath = dialog.FileName;
            }

            _logHelper?.Information($"选择正面视频: {dialog.FileName}");
        }
    }

    /// <summary>
    /// 选择侧面视频
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSelectVideo))]
    private void SelectSideVideo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = _localizationService.GetString("MA.Step2.SelectVideoTitle"),
            Filter = $"{_localizationService.GetString("MA.Step2.VideoFilter")}|*.mp4;*.avi;*.mov;*.mkv;*.wmv|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            SideVideoPath = dialog.FileName;
            HasSideVideo = true;

            if (CurrentMeasurement != null)
            {
                CurrentMeasurement.SideVideoPath = dialog.FileName;
            }

            _logHelper?.Information($"选择侧面视频: {dialog.FileName}");
        }
    }

    private bool CanSelectVideo() => HasMeasurementRecord && !IsAnalyzing;

    /// <summary>
    /// 清除正面视频
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanClearFrontVideo))]
    private void ClearFrontVideo()
    {
        FrontVideoPath = null;
        HasFrontVideo = false;

        if (CurrentMeasurement != null)
        {
            CurrentMeasurement.FrontVideoPath = null;
        }

        _logHelper?.Information("清除正面视频");
    }

    private bool CanClearFrontVideo() => HasFrontVideo && !IsAnalyzing;

    /// <summary>
    /// 清除侧面视频
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanClearSideVideo))]
    private void ClearSideVideo()
    {
        SideVideoPath = null;
        HasSideVideo = false;

        if (CurrentMeasurement != null)
        {
            CurrentMeasurement.SideVideoPath = null;
        }

        _logHelper?.Information("清除侧面视频");
    }

    private bool CanClearSideVideo() => HasSideVideo && !IsAnalyzing;

    /// <summary>
    /// 确认导入并进入下一步
    /// </summary>
    [RelayCommand]
    private async Task ConfirmImportAsync()
    {
        if (!HasAnyVideo)
        {
            _logHelper?.Warning("确认导入失败：未选择任何视频");
            return;
        }

        // 如果只有单视频，弹出确认对话框
        if (!HasDualVideo)
        {
            var result = await DialogHost.Show(
                new Views.Dialogs.ConfirmDialog
                {
                    DataContext = new
                    {
                        Title = _localizationService.GetString("MA.Dialog.VideoIncomplete.Title"),
                        Message = _localizationService.GetString("MA.Dialog.VideoIncomplete.Content")
                    }
                },
                "RootDialog");

            if (result is not true)
            {
                return;
            }
        }

        // 更新数据库
        if (CurrentMeasurement != null)
        {
            CurrentMeasurement.UpdatedAt = DateTime.Now;
            await _measurementService.UpdateMeasurementAsync(CurrentMeasurement);
        }

        // 进入步骤3
        CurrentStep = 3;
        _logHelper?.Information("确认导入视频，进入回放检查");
    }

    #endregion

    #region Step4 命令

    /// <summary>
    /// 开始分析
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartAnalyze))]
    private async Task StartAnalyzeAsync()
    {
        IsAnalyzing = true;
        IsTaskDrawerOpen = true;
        AnalysisProgress = 0;
        CurrentTaskName = _localizationService.GetString("MA.Task.Analyzing");
        TaskLogs.Clear();
        TaskLogs.Add($"[{DateTime.Now:HH:mm:ss}] 开始分析: {SelectedStage}");

        try
        {
            // 模拟分析进度
            for (int i = 0; i <= 100; i += 5)
            {
                if (!IsAnalyzing) break; // 取消检测

                AnalysisProgress = i;
                await Task.Delay(150);

                if (i % 20 == 0)
                {
                    TaskLogs.Add($"[{DateTime.Now:HH:mm:ss}] 进度: {i}%");
                }
            }

            if (IsAnalyzing)
            {
                // 完成
                CompletedStage = SelectedStage;
                UpdateStageCompletionFlags();
                TaskLogs.Add($"[{DateTime.Now:HH:mm:ss}] 分析完成");
                _logHelper?.Information($"分析完成: {SelectedStage}");
            }
        }
        catch (Exception ex)
        {
            TaskLogs.Add($"[{DateTime.Now:HH:mm:ss}] 分析失败: {ex.Message}");
            _logHelper?.Error("分析失败", ex);
        }
        finally
        {
            IsAnalyzing = false;
            CurrentTaskName = string.Empty;
        }
    }

    private bool CanStartAnalyze() => HasFrontVideo && HasSideVideo && !IsAnalyzing;

    /// <summary>
    /// 取消分析
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancelAnalyze))]
    private void CancelAnalyze()
    {
        IsAnalyzing = false;
        TaskLogs.Add($"[{DateTime.Now:HH:mm:ss}] 分析已取消");
        _logHelper?.Information("分析已取消");
    }

    private bool CanCancelAnalyze() => IsAnalyzing;

    /// <summary>
    /// 更新阶段完成标记
    /// </summary>
    private void UpdateStageCompletionFlags()
    {
        if (CurrentMeasurement == null) return;

        switch (CompletedStage)
        {
            case AnalysisStage.Keypoints:
                CurrentMeasurement.KeypointsCompleted = true;
                break;
            case AnalysisStage.Events:
                CurrentMeasurement.EventsCompleted = true;
                break;
            case AnalysisStage.Kinematics:
                CurrentMeasurement.KinematicsCompleted = true;
                break;
        }

        CurrentMeasurement.CurrentAnalysisStage = CompletedStage;
    }

    /// <summary>
    /// 切换任务抽屉
    /// </summary>
    [RelayCommand]
    private void ToggleTaskDrawer()
    {
        IsTaskDrawerOpen = !IsTaskDrawerOpen;
    }

    /// <summary>
    /// 选择分析阶段
    /// </summary>
    [RelayCommand]
    private void SelectStage(AnalysisStage stage)
    {
        SelectedStage = stage;
        _logHelper?.Information($"选择分析阶段: {stage}");
    }

    #endregion
}
