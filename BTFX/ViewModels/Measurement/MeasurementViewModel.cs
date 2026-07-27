using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using BTFX.Common;
using BTFX.Helpers;
using BTFX.Models;
using BTFX.Models.Camera;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.ViewModels.Measurement;

/// <summary>
/// 测量评估模块主 ViewModel
/// 管理封面和三步测量评估流程
/// </summary>
public partial class MeasurementViewModel : ObservableObject
{
    private readonly ISessionService _sessionService;
    private readonly IMeasurementService _measurementService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;
    private CancellationTokenSource? _captureCancellation;
    private string _lastDefaultMeasurementName = string.Empty;

    /// <summary>
    /// Step4 分析评估子 ViewModel
    /// </summary>
    public Step4AnalyzeViewModel AnalyzeViewModel { get; }

    #region 步骤状态

    /// <summary>
    /// 当前步骤：0=封面，1=新建测量，2=回放检查，3=分析结果。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCover))]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(IsStep3))]
    [NotifyPropertyChangedFor(nameof(IsInWorkflow))]
    [NotifyPropertyChangedFor(nameof(CurrentStepTitle))]
    [NotifyPropertyChangedFor(nameof(CanGoToStep2))]
    [NotifyPropertyChangedFor(nameof(CanGoToStep3))]
    private int _currentStep;

    /// <summary>
    /// 是否在封面。
    /// </summary>
    public bool IsCover => CurrentStep == 0;

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
    /// 是否在三步骤流程中。
    /// </summary>
    public bool IsInWorkflow => CurrentStep > 0;

    /// <summary>
    /// 当前步骤标题。
    /// </summary>
    public string CurrentStepTitle => CurrentStep switch
    {
        1 => L("MA.StepBar.Step1"),
        2 => L("MA.StepBar.Step3"),
        3 => L("MA.StepBar.Step4"),
        _ => L("Measurement")
    };

    #endregion

    #region 测量记录状态

    /// <summary>
    /// 是否已创建测量记录
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoToStep2))]
    [NotifyPropertyChangedFor(nameof(CanGoToStep3))]
    [NotifyCanExecuteChangedFor(nameof(SelectFrontVideoCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectSideVideoCommand))]
    private bool _hasMeasurementRecord;

    /// <summary>
    /// 是否有正面视频
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoToStep2))]
    [NotifyPropertyChangedFor(nameof(CanGoToStep3))]
    [NotifyPropertyChangedFor(nameof(CanEnterReview))]
    [NotifyPropertyChangedFor(nameof(EntryFailureReason))]
    [NotifyPropertyChangedFor(nameof(HasAnyVideo))]
    [NotifyPropertyChangedFor(nameof(HasDualVideo))]
    [NotifyCanExecuteChangedFor(nameof(ClearFrontVideoCommand))]
    private bool _hasFrontVideo;

    /// <summary>
    /// 是否有侧面视频
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoToStep2))]
    [NotifyPropertyChangedFor(nameof(CanGoToStep3))]
    [NotifyPropertyChangedFor(nameof(CanEnterReview))]
    [NotifyPropertyChangedFor(nameof(EntryFailureReason))]
    [NotifyPropertyChangedFor(nameof(HasAnyVideo))]
    [NotifyPropertyChangedFor(nameof(HasDualVideo))]
    [NotifyCanExecuteChangedFor(nameof(ClearSideVideoCommand))]
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

    /// <summary>
    /// 正面视频信息。
    /// </summary>
    public VideoFileInfoViewModel FrontVideoInfo { get; }

    /// <summary>
    /// 侧面视频信息。
    /// </summary>
    public VideoFileInfoViewModel SideVideoInfo { get; }

    #endregion

    #region Step1 字段

    /// <summary>
    /// 测量名称
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEnterReview))]
    [NotifyPropertyChangedFor(nameof(EntryFailureReason))]
    private string _measurementName = string.Empty;

    /// <summary>
    /// 分析模式。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSingleVideoMode))]
    [NotifyPropertyChangedFor(nameof(IsDualVideoMode))]
    [NotifyPropertyChangedFor(nameof(CanEnterReview))]
    [NotifyPropertyChangedFor(nameof(EntryFailureReason))]
    [NotifyPropertyChangedFor(nameof(CanGoToStep2))]
    [NotifyPropertyChangedFor(nameof(CanGoToStep3))]
    private AnalysisVideoMode _selectedAnalysisMode = AnalysisVideoMode.Dual;

    public bool IsSingleVideoMode => SelectedAnalysisMode == AnalysisVideoMode.Single;

    public bool IsDualVideoMode => SelectedAnalysisMode == AnalysisVideoMode.Dual;

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
    [NotifyPropertyChangedFor(nameof(CanEnterReview))]
    [NotifyPropertyChangedFor(nameof(EntryFailureReason))]
    private double _walkwayLength = 6.0;

    /// <summary>
    /// 视频导入模式
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImportMode))]
    [NotifyPropertyChangedFor(nameof(IsCaptureMode))]
    private VideoImportMode _selectedVideoImportMode = VideoImportMode.Import;

    /// <summary>
    /// 是否为视频导入模式。
    /// </summary>
    public bool IsImportMode => SelectedVideoImportMode == VideoImportMode.Import;

    /// <summary>
    /// 是否为视频采集模式。
    /// </summary>
    public bool IsCaptureMode => SelectedVideoImportMode == VideoImportMode.Capture;

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

    /// <summary>
    /// 双视频一致性校验提示。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReviewWarning))]
    [NotifyPropertyChangedFor(nameof(CanEnterReview))]
    [NotifyPropertyChangedFor(nameof(EntryFailureReason))]
    private string _videoConsistencyMessage = string.Empty;

    public bool HasReviewWarning => IsDualVideoMode && !string.IsNullOrWhiteSpace(VideoConsistencyMessage);

    public bool CanEnterReview => string.IsNullOrWhiteSpace(EntryFailureReason);

    public string EntryFailureReason
    {
        get
        {
            if (string.IsNullOrWhiteSpace(MeasurementName))
            {
                return L("MA.Step1.Validation.MeasurementNameRequired");
            }

            if (WalkwayLength <= 0)
            {
                return L("MA.Step1.Validation.WalkwayLengthRequired");
            }

            if (!SideVideoInfo.HasFile)
            {
                return L("MA.Step1.Validation.SideVideoRequired");
            }

            if (SideVideoInfo.Status == VideoValidationStatus.Failed)
            {
                return SideVideoInfo.ValidationMessage;
            }

            if (IsDualVideoMode)
            {
                if (!FrontVideoInfo.HasFile)
                {
                    return L("MA.Step1.Validation.FrontVideoRequired");
                }

                if (FrontVideoInfo.Status == VideoValidationStatus.Failed)
                {
                    return FrontVideoInfo.ValidationMessage;
                }

                var consistencyFailure = ValidateDualVideoConsistency(allowWarning: false);
                if (!string.IsNullOrWhiteSpace(consistencyFailure))
                {
                    return consistencyFailure;
                }
            }

            return string.Empty;
        }
    }

    #endregion

    #region 步骤可进入规则

    /// <summary>
    /// 可以进入步骤2（需要已创建测量且已有视频）
    /// </summary>
    public bool CanGoToStep2 => HasMeasurementRecord && CanEnterReview;

    /// <summary>
    /// 可以进入步骤3（需要已创建测量且已有视频）
    /// </summary>
    public bool CanGoToStep3 => HasMeasurementRecord && CanEnterReview;

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
        Step4AnalyzeViewModel analyzeViewModel,
        ILogHelper? logHelper = null)
    {
        _sessionService = sessionService;
        _measurementService = measurementService;
        _localizationService = localizationService;
        _logHelper = logHelper;
        FrontVideoInfo = new VideoFileInfoViewModel(L, "MA.Step2.FrontVideo");
        SideVideoInfo = new VideoFileInfoViewModel(L, "MA.Step2.SideVideo");

        // 初始化 Step4 子 ViewModel
        AnalyzeViewModel = analyzeViewModel;
        AnalyzeViewModel.NavigateToStepRequested += step => GoToStep(step);
        AnalyzeViewModel.ViewReportRequested += OnViewReportRequested;
        AnalyzeViewModel.GenerateReportRequested += OnGenerateReportRequested;
        AnalyzeViewModel.ReanalysisSetupRequested += PrepareNewMeasurementForReanalysis;
        AnalyzeViewModel.AnalysisCanceledRequested += OnAnalysisCanceledRequested;

        // 初始化测量名称为当前日期时间
        ResetMeasurementFields();

        // 订阅患者变更事件
        _sessionService.CurrentPatientChanged += OnCurrentPatientChanged;
        _localizationService.LanguageChanged += OnLanguageChanged;

        _logHelper?.Information("MeasurementViewModel 初始化完成");
    }

    private string L(string key) => _localizationService.GetString(key);

    private string L(string key, params object[] args) => _localizationService.GetString(key, args);

    private void OnLanguageChanged(object? sender, AppLanguage language)
    {
        FrontVideoInfo.RefreshLocalizedText();
        SideVideoInfo.RefreshLocalizedText();
        RefreshVideoValidationState();
        OnPropertyChanged(nameof(CurrentStepTitle));
    }

    /// <summary>
    /// 查看分析详情事件处理（由 Step4AnalyzeViewModel 触发）。
    /// </summary>
    private async void OnViewReportRequested()
    {
        if (CurrentMeasurement is null)
        {
            _logHelper?.Warning("打开分析详情失败：当前测量记录为空");
            return;
        }

        try
        {
            CurrentMeasurement.Patient ??= CurrentPatient;

            if (!await ValidateCurrentAnalysisPackageAsync())
            {
                return;
            }

            var viewModel = App.Services.GetRequiredService<GaitAnalysisDetailViewModel>();

            var dialog = new Views.Dialogs.MeasurementDetailDialog
            {
                DataContext = viewModel
            };

            var dialogTask = DialogHost.Show(dialog, "RootDialog");
            _ = viewModel.InitializeAsync(CurrentMeasurement);
            await dialogTask;
            _logHelper?.Information($"从测量分析步骤打开分析详情：MeasurementId={CurrentMeasurement.Id}");
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"从测量分析步骤打开分析详情失败：MeasurementId={CurrentMeasurement.Id}", ex);
            AppDialog.Show(
                L("MA.Dialog.Error.OpenAnalysisDetailFailed", ex.Message),
                L("Error"),
                AppDialogButtons.Ok,
                AppDialogIcon.Error);
        }
    }

    private void OnAnalysisCanceledRequested()
    {
        if (CanGoToStep2)
        {
            CurrentStep = 2;
            _logHelper?.Information("分析已停止，返回回放检查");
        }
    }

    /// <summary>
    /// 生成报告入口。直接生成当前分析结果的报告预览草稿。
    /// </summary>
    private async void OnGenerateReportRequested()
    {
        if (CurrentMeasurement is null)
        {
            _logHelper?.Warning("打开报告预览失败：当前测量记录为空");
            return;
        }

        try
        {
            CurrentMeasurement.Patient ??= CurrentPatient;

            var analysisResult = AnalyzeViewModel.AnalysisResult;
            if (!await ValidateCurrentAnalysisPackageAsync())
            {
                return;
            }

            var operatorId = _sessionService.CurrentUser?.Id ?? CurrentMeasurement.OperatorId;
            var reportService = App.Services.GetRequiredService<IReportService>();
            var report = await reportService.GetOrCreateDraftReportAsync(CurrentMeasurement.Id, operatorId);

            if (report is null)
            {
                AppDialog.Show(
                    L("MA.Dialog.ReportDraftCreateFailed"),
                    L("Tip"),
                    AppDialogButtons.Ok,
                    AppDialogIcon.Warning);
                return;
            }

            report.Title = string.IsNullOrWhiteSpace(report.Title)
                ? L("MA.Report.DefaultTitleFormat", CurrentMeasurement.MeasurementName ?? MeasurementName)
                : report.Title;
            report.DoctorOpinion ??= string.Empty;
            report.AnalysisResultId = analysisResult?.Id;
            report.AnalysisResult = analysisResult;
            report.KinematicSummary = analysisResult?.KinematicSummary;
            report.QualityControl = analysisResult?.QualityControl;
            report.MeasurementRecord = CurrentMeasurement;
            report.Patient = CurrentMeasurement.Patient ?? CurrentPatient;
            report.ReportOptionsJson = JsonSerializer.Serialize(new ReportDraftOptions(
                IncludeSpatiotemporalParameters: true,
                IncludeKinematicSummary: true,
                IncludeQualityControl: true,
                IncludeResultFiles: false));
            report.UpdatedAt = DateTime.Now;

            await reportService.SaveDraftSnapshotAsync(report);

            var previewViewModel = App.Services.GetRequiredService<ReportPreviewDialogViewModel>();
            var settingsService = App.Services.GetService<ISettingsService>();
            var unitName = settingsService?.CurrentSettings?.Unit?.Name ?? BTFX.Common.Constants.APP_DISPLAY_NAME;
            var logoPath = settingsService?.CurrentSettings?.Unit?.LogoPath;
            var previewDocument = ReportPreviewHelper.GenerateReportDocument(report, unitName, logoPath);
            await previewViewModel.InitializeAsync(report, previewDocument);

            var dialog = new Views.Dialogs.ReportPreviewDialog
            {
                DataContext = previewViewModel
            };

            await DialogHost.Show(dialog, "RootDialog");
            _logHelper?.Information($"从测量分析步骤打开报告预览：MeasurementId={CurrentMeasurement.Id}, AnalysisResultId={analysisResult?.Id}");
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"从测量分析步骤打开报告预览失败：MeasurementId={CurrentMeasurement.Id}", ex);
            AppDialog.Show(
                L("MA.Dialog.Error.OpenReportPreviewFailed", ex.Message),
                L("Error"),
                AppDialogButtons.Ok,
                AppDialogIcon.Error);
        }
    }

    private async Task<bool> ValidateCurrentAnalysisPackageAsync()
    {
        var analysisResult = AnalyzeViewModel.AnalysisResult;
        if (analysisResult is null)
        {
            return true;
        }

        var packageService = App.Services.GetService<IAnalysisPackageService>();
        if (packageService is null)
        {
            return true;
        }

        var validation = await packageService.ValidatePackageAsync(analysisResult);
        if (validation.IsValid)
        {
            return true;
        }

        AppDialog.Show(
            L("MA.Dialog.PackageValidationFailedMessage", validation.Message),
            L("MA.Dialog.PackageValidationFailedTitle"),
            AppDialogButtons.Ok,
            AppDialogIcon.Warning);
        return false;
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
        _lastDefaultMeasurementName = L("MA.Step1.DefaultMeasurementNameFormat", DateTime.Now);
        MeasurementName = _lastDefaultMeasurementName;
        SelectedMeasurementType = MeasurementType.NormalWalk;
        Remark = string.Empty;
        SelectedVideoSpec = VideoSpec.P1080_30fps;
        WalkwayLength = 6.0;
        SelectedAnalysisMode = AnalysisVideoMode.Dual;
        SelectedVideoImportMode = VideoImportMode.Import;
        SelectedImportStrategy = ImportStrategy.CopyToFolder;
        WalkwayLengthError = null;
        VideoConsistencyMessage = string.Empty;
    }

    /// <summary>
    /// 重置所有状态
    /// </summary>
    private void ResetAllState()
    {
        CurrentStep = 0;
        HasMeasurementRecord = false;
        HasFrontVideo = false;
        HasSideVideo = false;
        CurrentMeasurement = null;
        FrontVideoPath = null;
        SideVideoPath = null;
        FrontVideoInfo.Clear();
        SideVideoInfo.Clear();
        ResetMeasurementFields();
    }

    /// <summary>
    /// 从已完成分析结果重新分析时，保留当前患者，创建一个新的测量草稿并要求重新选择视频。
    /// </summary>
    private void PrepareNewMeasurementForReanalysis()
    {
        var patient = CurrentPatient;
        CurrentStep = 1;
        HasMeasurementRecord = false;
        HasFrontVideo = false;
        HasSideVideo = false;
        CurrentMeasurement = null;
        FrontVideoPath = null;
        SideVideoPath = null;
        FrontVideoInfo.Clear();
        SideVideoInfo.Clear();
        ResetMeasurementFields();
        AnalyzeViewModel.CurrentMeasurement = null;
        AnalyzeViewModel.CurrentPatient = patient;
        AnalyzeViewModel.RefreshPrerequisites();
        _logHelper?.Information("重新分析已切换到新建测量步骤，等待重新导入或采集视频。");
    }

    partial void OnSelectedAnalysisModeChanged(AnalysisVideoMode value)
    {
        if (value == AnalysisVideoMode.Single)
        {
            ClearFrontVideoState();
        }

        RefreshVideoValidationState();
    }

    partial void OnWalkwayLengthChanged(double value)
    {
        WalkwayLengthError = value > 0 ? null : L("MA.Step1.Validation.WalkwayLengthRequired");
    }

    #region 步骤导航命令

    /// <summary>
    /// 从封面进入新建测量步骤。
    /// </summary>
    [RelayCommand]
    private void StartMeasurement()
    {
        ResetAllState();
        CurrentStep = 1;
    }

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

        if (stepIndex < 0 || stepIndex > 3) return;

        // 检查步骤可进入规则
        switch (stepIndex)
        {
            case 2 when !CanGoToStep2:
                _logHelper?.Warning("无法进入回放检查：未完成测量创建或未准备视频");
                return;
            case 3 when !CanGoToStep3:
                _logHelper?.Warning("无法进入分析结果：未完成测量创建或未准备视频");
                return;
        }

        CurrentStep = stepIndex;

        // 进入分析结果时同步上下文到子 VM
        if (stepIndex == 3)
        {
            SyncContextToAnalyzeViewModel();
        }

        _logHelper?.Information($"切换到步骤 {stepIndex}");
    }

    /// <summary>
    /// 从回放检查进入分析流程。
    /// </summary>
    [RelayCommand]
    private async Task BeginAnalyzeAsync()
    {
        if (!CanGoToStep3)
        {
            _logHelper?.Warning("无法启动分析：未完成测量创建或未准备视频");
            return;
        }

        GoToStep(3);
        await AnalyzeViewModel.StartAnalyzeCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// 同步当前测量和患者信息到 Step4AnalyzeViewModel
    /// </summary>
    private void SyncContextToAnalyzeViewModel()
    {
        AnalyzeViewModel.CurrentMeasurement = CurrentMeasurement;
        AnalyzeViewModel.CurrentPatient = CurrentPatient;
        AnalyzeViewModel.RefreshPrerequisites();
    }

    public async Task LoadExistingMeasurementAsync(MeasurementRecord record, MeasurementResumeDecision decision)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(decision);

        if (record.Patient is not null && _sessionService.CurrentPatient?.Id != record.Patient.Id)
        {
            _sessionService.SetCurrentPatient(record.Patient);
        }

        CurrentMeasurement = record;
        HasMeasurementRecord = true;

        MeasurementName = string.IsNullOrWhiteSpace(record.MeasurementName)
            ? L("MA.Step1.DefaultMeasurementNameFormat", record.MeasurementDate)
            : record.MeasurementName;
        SelectedMeasurementType = record.MeasurementType;
        Remark = record.Remark ?? string.Empty;
        SelectedVideoSpec = record.VideoSpec;
        WalkwayLength = record.WalkwayLength > 0 ? record.WalkwayLength : 6.0;
        SelectedVideoImportMode = record.VideoImportMode;
        SelectedImportStrategy = record.ImportStrategy;

        FrontVideoPath = record.FrontVideoPath;
        SideVideoPath = record.SideVideoPath;
        HasFrontVideo = !string.IsNullOrWhiteSpace(FrontVideoPath);
        HasSideVideo = !string.IsNullOrWhiteSpace(SideVideoPath);
        SelectedAnalysisMode = HasFrontVideo && HasSideVideo
            ? AnalysisVideoMode.Dual
            : AnalysisVideoMode.Single;

        FrontVideoInfo.Clear();
        SideVideoInfo.Clear();

        var loadTasks = new List<Task>();
        if (!string.IsNullOrWhiteSpace(SideVideoPath))
        {
            loadTasks.Add(LoadVideoInfoAsync(SideVideoInfo, SideVideoPath));
        }

        if (!string.IsNullOrWhiteSpace(FrontVideoPath))
        {
            loadTasks.Add(LoadVideoInfoAsync(FrontVideoInfo, FrontVideoPath));
        }

        if (loadTasks.Count > 0)
        {
            await Task.WhenAll(loadTasks);
        }

        RefreshVideoValidationState();

        var targetStep = Math.Clamp(decision.TargetStep, 1, 3);
        if (targetStep == 2 && !CanGoToStep2)
        {
            targetStep = 1;
        }

        CurrentStep = targetStep;

        if (CurrentStep == 3)
        {
            await AnalyzeViewModel.RestoreExistingMeasurementAsync(record, CurrentPatient, decision);
        }
        else
        {
            SyncContextToAnalyzeViewModel();
        }

        if (!string.IsNullOrWhiteSpace(decision.Message))
        {
            _logHelper?.Information($"恢复测量流程：MeasurementId={record.Id}, Step={CurrentStep}, Message={decision.Message}");
        }
    }

    /// <summary>
    /// 下一步
    /// </summary>
    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStep < 3)
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

            _logHelper?.Information("测量记录已创建，可继续导入或采集视频");
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
    private async Task ResetMeasurementAsync()
    {
        if (HasMeasurementInput())
        {
            var result = await DialogHost.Show(
                new Views.Dialogs.ConfirmDialog
                {
                    DataContext = new ConfirmDialogViewModel
                    {
                        Title = L("MA.Step1.Dialog.ResetMeasurementTitle"),
                        Message = L("MA.Step1.Dialog.ResetMeasurementMessage"),
                        ConfirmText = L("Confirm"),
                        CancelText = L("Cancel"),
                        IconKind = "Refresh"
                    }
                },
                "RootDialog");

            if (result is not true)
            {
                return;
            }
        }

        ResetMeasurementDraft();
        _logHelper?.Information("重置新建测量表单");
    }

    private bool HasMeasurementInput()
    {
        return HasMeasurementRecord
            || HasSideVideo
            || HasFrontVideo
            || !string.IsNullOrWhiteSpace(SideVideoPath)
            || !string.IsNullOrWhiteSpace(FrontVideoPath)
            || SideVideoInfo.HasFile
            || FrontVideoInfo.HasFile
            || !string.IsNullOrWhiteSpace(Remark)
            || !string.Equals(MeasurementName, _lastDefaultMeasurementName, StringComparison.Ordinal)
            || SelectedMeasurementType != MeasurementType.NormalWalk
            || Math.Abs(WalkwayLength - 6.0) > 0.0001
            || SelectedAnalysisMode != AnalysisVideoMode.Dual;
    }

    private void ResetMeasurementDraft()
    {
        CurrentStep = 1;
        HasMeasurementRecord = false;
        HasFrontVideo = false;
        HasSideVideo = false;
        CurrentMeasurement = null;
        FrontVideoPath = null;
        SideVideoPath = null;
        FrontVideoInfo.Clear();
        SideVideoInfo.Clear();
        ResetMeasurementFields();
        AnalyzeViewModel.CurrentMeasurement = null;
        AnalyzeViewModel.CurrentPatient = CurrentPatient;
        AnalyzeViewModel.RefreshPrerequisites();
        RefreshVideoValidationState();
    }

    #endregion

    #region Step2 命令

    /// <summary>
    /// 选择正面视频
    /// </summary>
    [RelayCommand]
    private async Task SelectFrontVideoAsync()
    {
        var fileName = TrySelectVideoFile();
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            FrontVideoPath = fileName;
            HasFrontVideo = true;
            await LoadVideoInfoAsync(FrontVideoInfo, fileName);
            ShowVideoValidationFailureIfNeeded(L("MA.Step2.FrontVideo"), FrontVideoInfo);

            if (CurrentMeasurement != null)
            {
                CurrentMeasurement.FrontVideoPath = fileName;
            }

            _logHelper?.Information($"选择正面视频: {fileName}");
        }
    }

    /// <summary>
    /// 选择侧面视频
    /// </summary>
    [RelayCommand]
    private async Task SelectSideVideoAsync()
    {
        var fileName = TrySelectVideoFile();
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            SideVideoPath = fileName;
            HasSideVideo = true;
            await LoadVideoInfoAsync(SideVideoInfo, fileName);
            ShowVideoValidationFailureIfNeeded(L("MA.Step2.SideVideo"), SideVideoInfo);

            if (CurrentMeasurement != null)
            {
                CurrentMeasurement.SideVideoPath = fileName;
            }

            _logHelper?.Information($"选择侧面视频: {fileName}");
        }
    }

    private void ShowVideoValidationFailureIfNeeded(string title, VideoFileInfoViewModel videoInfo)
    {
        RefreshVideoValidationState();
        if (videoInfo.Status != VideoValidationStatus.Failed
            || string.IsNullOrWhiteSpace(videoInfo.ValidationMessage))
        {
            return;
        }

        AppDialog.Show(
            L("MA.Step1.Dialog.VideoValidationFailedMessage", title, videoInfo.ValidationMessage),
            L("MA.Step1.Dialog.VideoValidationFailedTitle"),
            AppDialogButtons.Ok,
            AppDialogIcon.Warning);
    }

    private string? TrySelectVideoFile()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = _localizationService.GetString("MA.Step2.SelectVideoTitle"),
                Filter = $"{_localizationService.GetString("MA.Step2.VideoFilter")}|*.mp4;*.avi;*.mov;*.mkv;*.wmv|All Files|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };

            return dialog.ShowDialog(Application.Current.MainWindow) == true
                ? dialog.FileName
                : null;
        }
        catch (Exception ex)
        {
            _logHelper?.Error("打开视频文件选择框失败", ex);
            AppDialog.Show(
                L("MA.Dialog.Error.OpenVideoFileDialogFailed", ex.Message),
                L("Error"),
                AppDialogButtons.Ok,
                AppDialogIcon.Error);
            return null;
        }
    }

    /// <summary>
    /// 清除正面视频
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanClearFrontVideo))]
    private void ClearFrontVideo()
    {
        ClearFrontVideoState();

        if (CurrentMeasurement != null)
        {
            CurrentMeasurement.FrontVideoPath = null;
        }

        _logHelper?.Information("清除正面视频");
    }

    private bool CanClearFrontVideo() => HasFrontVideo;

    private void ClearFrontVideoState()
    {
        FrontVideoPath = null;
        HasFrontVideo = false;
        FrontVideoInfo.Clear();
        RefreshVideoValidationState();
    }

    /// <summary>
    /// 清除侧面视频
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanClearSideVideo))]
    private void ClearSideVideo()
    {
        SideVideoPath = null;
        HasSideVideo = false;
        SideVideoInfo.Clear();
        RefreshVideoValidationState();

        if (CurrentMeasurement != null)
        {
            CurrentMeasurement.SideVideoPath = null;
        }

        _logHelper?.Information("清除侧面视频");
    }

    private bool CanClearSideVideo() => HasSideVideo;

    /// <summary>
    /// 打开相机采集测试弹窗，关闭后将输出视频回填到本次测量。
    /// </summary>
    [RelayCommand]
    private async Task OpenCameraRecordingDialogAsync()
    {
        try
        {
            SelectedVideoImportMode = VideoImportMode.Capture;

            var dialog = App.Services.GetRequiredService<BTFX.Views.Dialogs.CameraCaptureDialog>();
            dialog.Initialize(IsDualVideoMode
                ? BTFX.Models.Camera.CameraCaptureMode.Dual
                : BTFX.Models.Camera.CameraCaptureMode.Single);
            var dialogResult = await DialogHost.Show(dialog, "RootDialog");

            var captureResult = dialogResult as BTFX.Models.Camera.CameraCaptureDialogResult;
            if (captureResult == null)
            {
                _logHelper?.Information("相机采集弹窗关闭，未检测到输出视频。");
                return;
            }

            await ApplyCapturedVideoFilesAsync(captureResult);

            if (CurrentMeasurement != null)
            {
                CurrentMeasurement.VideoImportMode = VideoImportMode.Capture;
                CurrentMeasurement.FrontVideoPath = FrontVideoPath;
                CurrentMeasurement.SideVideoPath = SideVideoPath;
                CurrentMeasurement.UpdatedAt = DateTime.Now;
                await _measurementService.UpdateMeasurementAsync(CurrentMeasurement);
            }

            _logHelper?.Information($"采集视频已回填：Front={FrontVideoPath}, Side={SideVideoPath}");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("打开相机采集弹窗失败", ex);
            AppDialog.Show(
                L("MA.Dialog.Error.OpenCameraCaptureFailed", ex.Message),
                L("Error"),
                AppDialogButtons.Ok,
                AppDialogIcon.Error);
        }
    }

    private async Task ApplyCapturedVideoFilesAsync(BTFX.Models.Camera.CameraCaptureDialogResult result)
    {
        var loadTasks = new List<Task>();

        if (!string.IsNullOrWhiteSpace(result.SideVideoPath))
        {
            SideVideoPath = result.SideVideoPath;
            HasSideVideo = true;
            SideVideoInfo.BeginLoad(result.SideVideoPath);
            loadTasks.Add(LoadVideoInfoAsync(SideVideoInfo, result.SideVideoPath));
        }

        if (!string.IsNullOrWhiteSpace(result.FrontVideoPath))
        {
            SelectedAnalysisMode = AnalysisVideoMode.Dual;
            FrontVideoPath = result.FrontVideoPath;
            HasFrontVideo = true;
            FrontVideoInfo.BeginLoad(result.FrontVideoPath);
            loadTasks.Add(LoadVideoInfoAsync(FrontVideoInfo, result.FrontVideoPath));
        }
        else
        {
            SelectedAnalysisMode = AnalysisVideoMode.Single;
            ClearFrontVideoState();
        }

        RefreshVideoValidationState();
        await Task.WhenAll(loadTasks);
        RefreshVideoValidationState();
    }

    /// <summary>
    /// 确认导入并进入下一步
    /// </summary>
    [RelayCommand]
    private async Task ConfirmImportAsync()
    {
        RefreshVideoValidationState();

        if (!CanEnterReview)
        {
            _logHelper?.Warning($"进入回放检查失败：{EntryFailureReason}");
            return;
        }

        if (HasReviewWarning)
        {
            var result = await DialogHost.Show(
                new Views.Dialogs.ConfirmDialog
                {
                    DataContext = new
                    {
                        Title = L("MA.Step1.Dialog.VideoConsistencyWarningTitle"),
                        Message = L("MA.Step1.Dialog.VideoConsistencyWarningMessage", VideoConsistencyMessage)
                    }
                },
                "RootDialog");

            if (result is not true)
            {
                return;
            }
        }

        if (!HasMeasurementRecord)
        {
            await CreateMeasurementAsync();
            if (!HasMeasurementRecord)
            {
                return;
            }
        }

        // 更新数据库
        if (CurrentMeasurement != null)
        {
            CurrentMeasurement.FrontVideoPath = FrontVideoPath;
            CurrentMeasurement.SideVideoPath = SideVideoPath;
            CurrentMeasurement.VideoImportMode = SelectedVideoImportMode;
            CurrentMeasurement.ImportStrategy = SelectedImportStrategy;
            CurrentMeasurement.UpdatedAt = DateTime.Now;
            await _measurementService.UpdateMeasurementAsync(CurrentMeasurement);
        }

        // 进入回放检查
        CurrentStep = 2;
        _logHelper?.Information("确认导入视频，进入回放检查");
    }

    [RelayCommand]
    private void PreviewSideVideo()
    {
        OpenVideoForPreview(SideVideoPath);
    }

    [RelayCommand]
    private void PreviewFrontVideo()
    {
        OpenVideoForPreview(FrontVideoPath);
    }

    private static void OpenVideoForPreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private async Task LoadVideoInfoAsync(VideoFileInfoViewModel target, string path)
    {
        target.BeginLoad(path);

        try
        {
            var info = await ReadVideoMetadataAsync(path);
            target.ApplyMetadata(info);
            await LoadVideoPreviewAsync(target, path);
        }
        catch (Exception ex)
        {
            target.MarkFailed(ex.Message);
        }

        RefreshVideoValidationState();
    }

    private static async Task LoadVideoPreviewAsync(VideoFileInfoViewModel target, string path)
    {
        try
        {
            var preview = await Task.Run(() => CreateVideoPreviewImage(path));
            Application.Current.Dispatcher.Invoke(() => target.PreviewImage = preview);
        }
        catch
        {
            Application.Current.Dispatcher.Invoke(() => target.PreviewImage = null);
        }
    }

    private static ImageSource? CreateVideoPreviewImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        using var capture = new VideoCapture(path);
        if (!capture.IsOpened())
        {
            return null;
        }

        using var frame = new Mat();
        if (!capture.Read(frame) || frame.Empty())
        {
            return null;
        }

        var bitmap = frame.ToBitmapSource();
        bitmap.Freeze();
        return bitmap;
    }

    private void RefreshVideoValidationState()
    {
        VideoConsistencyMessage = string.Empty;

        if (IsDualVideoMode &&
            SideVideoInfo.Status != VideoValidationStatus.Failed &&
            FrontVideoInfo.Status != VideoValidationStatus.Failed &&
            SideVideoInfo.HasFile &&
            FrontVideoInfo.HasFile)
        {
            VideoConsistencyMessage = ValidateDualVideoConsistency(allowWarning: true);
        }

        OnPropertyChanged(nameof(CanEnterReview));
        OnPropertyChanged(nameof(EntryFailureReason));
        OnPropertyChanged(nameof(CanGoToStep2));
        OnPropertyChanged(nameof(CanGoToStep3));
        OnPropertyChanged(nameof(HasReviewWarning));
    }

    private string ValidateDualVideoConsistency(bool allowWarning)
    {
        if (!SideVideoInfo.HasMetadata || !FrontVideoInfo.HasMetadata)
        {
            return string.Empty;
        }

        var durationDiff = Math.Abs(SideVideoInfo.DurationSeconds - FrontVideoInfo.DurationSeconds);
        if (durationDiff > 1.0)
        {
            return L("MA.Step1.Validation.DualDurationFailed", durationDiff);
        }

        if (!VideoFrameRateComparer.AreEquivalent(
                SideVideoInfo.FrameRate,
                FrontVideoInfo.FrameRate))
        {
            return L("MA.Step1.Validation.DualFrameRateMismatch");
        }

        if (durationDiff > 0.2)
        {
            return allowWarning
                ? L("MA.Step1.Validation.DualDurationWarning", durationDiff)
                : string.Empty;
        }

        return string.Empty;
    }

    private async Task<VideoMetadata> ReadVideoMetadataAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(L("MA.Step1.Validation.VideoPathEmpty"));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(L("MA.Step1.Validation.FileMissing"), path);
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mov", ".mkv", ".wmv"
        };

        if (!supportedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(L("MA.Step1.Validation.UnsupportedFormat"));
        }

        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
        }

        var ffprobePath = ResolveFfprobePath();
        if (string.IsNullOrWhiteSpace(ffprobePath))
        {
            throw new InvalidOperationException(L("MA.Step1.Validation.FfprobeMissing"));
        }

        var json = await RunFfprobeAsync(ffprobePath, path);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var streams = root.GetProperty("streams");
        var videoStream = streams.EnumerateArray().FirstOrDefault(stream =>
            stream.TryGetProperty("codec_type", out var codecType) &&
            codecType.GetString() == "video");

        if (videoStream.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException(L("MA.Step1.Validation.NoVideoStream"));
        }

        var width = videoStream.GetProperty("width").GetInt32();
        var height = videoStream.GetProperty("height").GetInt32();
        var frameRateText = videoStream.TryGetProperty("avg_frame_rate", out var avgFrameRate)
            ? avgFrameRate.GetString()
            : null;
        var frameRate = ParseFrameRate(frameRateText);

        var duration = 0d;
        if (root.TryGetProperty("format", out var format) &&
            format.TryGetProperty("duration", out var durationProperty) &&
            double.TryParse(durationProperty.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDuration))
        {
            duration = parsedDuration;
        }

        if (duration <= 0)
        {
            throw new InvalidOperationException(L("MA.Step1.Validation.InvalidDuration"));
        }

        if (frameRate <= 0)
        {
            throw new InvalidOperationException(L("MA.Step1.Validation.InvalidFrameRate"));
        }

        const double minimumDurationSeconds = 1.0;
        if (duration < minimumDurationSeconds)
        {
            throw new InvalidOperationException(L("MA.Step1.Validation.MinimumDuration", minimumDurationSeconds));
        }

        var fileInfo = new FileInfo(path);
        return new VideoMetadata(
            $"{width}x{height}",
            frameRate,
            duration,
            fileInfo.Length);
    }

    private static string? ResolveFfprobePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffprobe.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffprobe.exe"),
            @"D:\ffmpeg\bin\ffprobe.exe",
            @"C:\ffmpeg\bin\ffprobe.exe",
            "ffprobe.exe"
        };

        return candidates.FirstOrDefault(candidate =>
        {
            if (Path.IsPathRooted(candidate))
            {
                return File.Exists(candidate);
            }

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
                process?.WaitForExit(1000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        });
    }

    private async Task<string> RunFfprobeAsync(string ffprobePath, string videoPath)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = ffprobePath,
            Arguments = $"-v error -select_streams v:0 -show_entries stream=codec_type,width,height,avg_frame_rate -show_entries format=duration -of json \"{videoPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                ? L("MA.Step1.Validation.FfprobeFailed")
                : error.Trim());
        }

        return output;
    }

    private static double ParseFrameRate(string? frameRateText)
    {
        if (string.IsNullOrWhiteSpace(frameRateText))
        {
            return 0;
        }

        var parts = frameRateText.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) &&
            denominator > 0)
        {
            return numerator / denominator;
        }

        return double.TryParse(frameRateText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    #endregion

    #region 采集相关

    /// <summary>
    /// 采集画面布局（单视频/双视频）
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDualCapture))]
    private CaptureLayout _selectedCaptureLayout = CaptureLayout.Dual;

    /// <summary>
    /// 是否双视频采集模式
    /// </summary>
    public bool IsDualCapture => SelectedCaptureLayout == CaptureLayout.Dual;

    /// <summary>
    /// 录制时长选项
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordDurationSeconds))]
    private RecordDuration _selectedRecordDuration = RecordDuration.Seconds30;

    /// <summary>
    /// 录制总秒数
    /// </summary>
    public int RecordDurationSeconds => (int)SelectedRecordDuration;

    /// <summary>
    /// FFmpeg 路径。
    /// </summary>
    [ObservableProperty]
    private string _captureFfmpegPath = @"C:\ffmpeg\bin\ffmpeg.exe";

    /// <summary>
    /// 采集保存目录。
    /// </summary>
    [ObservableProperty]
    private string _captureSaveDirectory = @"D:\ZUT_test\video";

    /// <summary>
    /// 相机名称列表，换行或逗号分隔。
    /// </summary>
    [ObservableProperty]
    private string _captureCameraNamesText = "Y-CAM-25310455\r\nY-CAM-25310080";

    /// <summary>
    /// 采集分辨率。
    /// </summary>
    [ObservableProperty]
    private string _captureVideoSize = "3840x2160";

    /// <summary>
    /// 采集帧率。
    /// </summary>
    [ObservableProperty]
    private int _captureFrameRate = 59;

    /// <summary>
    /// 采集日志。
    /// </summary>
    public ObservableCollection<string> CaptureLogLines { get; } = new();

    /// <summary>
    /// 采集状态
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCapturing))]
    [NotifyPropertyChangedFor(nameof(IsCaptureIdle))]
    [NotifyPropertyChangedFor(nameof(IsCaptureCompleted))]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCaptureCommand))]
    private CaptureState _captureState = CaptureState.Idle;

    /// <summary>
    /// 是否正在录制
    /// </summary>
    public bool IsCapturing => CaptureState == CaptureState.Recording;

    /// <summary>
    /// 是否采集待机
    /// </summary>
    public bool IsCaptureIdle => CaptureState == CaptureState.Idle;

    /// <summary>
    /// 是否采集完成
    /// </summary>
    public bool IsCaptureCompleted => CaptureState == CaptureState.Completed;

    /// <summary>
    /// 录制已用时间（秒）
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordingElapsedDisplay))]
    [NotifyPropertyChangedFor(nameof(RecordingCountdownDisplay))]
    [NotifyPropertyChangedFor(nameof(RecordingProgressPercent))]
    private int _recordingElapsedSeconds;

    /// <summary>
    /// 录制已用时间显示
    /// </summary>
    public string RecordingElapsedDisplay
    {
        get
        {
            var ts = TimeSpan.FromSeconds(RecordingElapsedSeconds);
            return ts.ToString(@"mm\:ss");
        }
    }

    /// <summary>
    /// 录制倒计时显示
    /// </summary>
    public string RecordingCountdownDisplay
    {
        get
        {
            var remaining = Math.Max(0, RecordDurationSeconds - RecordingElapsedSeconds);
            var ts = TimeSpan.FromSeconds(remaining);
            return ts.ToString(@"mm\:ss");
        }
    }

    /// <summary>
    /// 录制进度百分比 (0-100)
    /// </summary>
    public double RecordingProgressPercent =>
        RecordDurationSeconds > 0 ? (double)RecordingElapsedSeconds / RecordDurationSeconds * 100 : 0;

    /// <summary>
    /// 开始录制
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartRecording))]
    private async Task StartRecordingAsync()
    {
        if (!HasMeasurementRecord)
        {
            await CreateMeasurementAsync();
            if (!HasMeasurementRecord)
            {
                return;
            }
        }

        CaptureState = CaptureState.Recording;
        RecordingElapsedSeconds = 0;
        CaptureLogLines.Clear();
        _captureCancellation?.Dispose();
        _captureCancellation = new CancellationTokenSource();
        AppendCaptureLog($"开始采集，时长: {RecordDurationSeconds}s, 模式: {SelectedCaptureLayout}");

        try
        {
            var cameraNames = ParseCameraNames(CaptureCameraNamesText);
            if (SelectedCaptureLayout == CaptureLayout.Single)
            {
                cameraNames = cameraNames.Take(1).ToList();
            }

            if (cameraNames.Count == 0)
            {
                throw new InvalidOperationException("请至少输入一个相机名称。");
            }

            var service = App.Services.GetRequiredService<ICameraRecordingService>();
            var options = new CameraRecordingOptions
            {
                FfmpegPath = CaptureFfmpegPath.Trim(),
                SaveDirectory = CaptureSaveDirectory.Trim(),
                CameraNames = cameraNames,
                VideoSize = CaptureVideoSize.Trim(),
                FrameRate = CaptureFrameRate,
                DurationSeconds = RecordDurationSeconds,
                TranscodeToMp4 = true,
                DeleteAviAfterMp4 = true
            };

            var progress = new Progress<string>(AppendCaptureLog);
            var results = await service.RecordAsync(options, progress, _captureCancellation.Token);

            if (results.Count > 0)
            {
                FrontVideoPath = results[0].Mp4File ?? results[0].AviFile;
                HasFrontVideo = true;
                if (CurrentMeasurement != null)
                {
                    CurrentMeasurement.FrontVideoPath = FrontVideoPath;
                }
            }

            if (results.Count > 1)
            {
                SideVideoPath = results[1].Mp4File ?? results[1].AviFile;
                HasSideVideo = true;
                if (CurrentMeasurement != null)
                {
                    CurrentMeasurement.SideVideoPath = SideVideoPath;
                }
            }

            if (CurrentMeasurement != null)
            {
                CurrentMeasurement.VideoImportMode = VideoImportMode.Capture;
                CurrentMeasurement.UpdatedAt = DateTime.Now;
                await _measurementService.UpdateMeasurementAsync(CurrentMeasurement);
            }

            CaptureState = CaptureState.Completed;
            RecordingElapsedSeconds = RecordDurationSeconds;
            AppendCaptureLog("采集完成，视频已回填到本次测量。");
        }
        catch (OperationCanceledException)
        {
            CaptureState = CaptureState.Idle;
            AppendCaptureLog("采集已取消。");
        }
        catch (Exception ex)
        {
            CaptureState = CaptureState.Idle;
            AppendCaptureLog($"采集失败: {ex.Message}");
            _logHelper?.Error("视频采集失败", ex);
        }
        finally
        {
            _captureCancellation?.Dispose();
            _captureCancellation = null;
        }
    }

    private bool CanStartRecording() => CaptureState != CaptureState.Recording;

    /// <summary>
    /// 停止录制（提前结束）
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopRecording))]
    private void StopRecording()
    {
        _captureCancellation?.Cancel();
        CaptureState = CaptureState.Completed;
        _logHelper?.Information($"停止录制，已录制: {RecordingElapsedSeconds}s");
    }

    private bool CanStopRecording() => CaptureState == CaptureState.Recording;

    /// <summary>
    /// 重新录制
    /// </summary>
    [RelayCommand]
    private void RetakeCapture()
    {
        CaptureState = CaptureState.Idle;
        RecordingElapsedSeconds = 0;
        _logHelper?.Information("重新录制");
    }

    /// <summary>
    /// 确认采集并进入下一步
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConfirmCapture))]
    private void ConfirmCapture()
    {
        if (!HasFrontVideo)
        {
            HasFrontVideo = true;
            FrontVideoPath = "[采集] 正面视频";
        }

        if (IsDualCapture && !HasSideVideo)
        {
            HasSideVideo = true;
            SideVideoPath = "[采集] 侧面视频";
        }

        if (CurrentMeasurement != null)
        {
            CurrentMeasurement.FrontVideoPath = FrontVideoPath;
            CurrentMeasurement.SideVideoPath = SideVideoPath;
        }

        CurrentStep = 2;
        _logHelper?.Information("确认采集视频，进入回放检查");
    }

    private bool CanConfirmCapture() => CaptureState == CaptureState.Completed;

    private void AppendCaptureLog(string message)
    {
        CaptureLogLines.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        while (CaptureLogLines.Count > 300)
        {
            CaptureLogLines.RemoveAt(0);
        }
    }

    private static IReadOnlyList<string> ParseCameraNames(string cameraNamesText)
    {
        return cameraNamesText
            .Split(new[] { "\r\n", "\n", "\r", ",", ";", "，", "；" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    #endregion
}

public enum AnalysisVideoMode
{
    Single,
    Dual
}

public enum VideoValidationStatus
{
    Empty,
    Checking,
    Passed,
    Warning,
    Failed
}

public sealed partial class VideoFileInfoViewModel : ObservableObject
{
    private readonly Func<string, string> _getString;
    private readonly string _titleKey;

    public VideoFileInfoViewModel(Func<string, string> getString, string titleKey)
    {
        _getString = getString;
        _titleKey = titleKey;
        ValidationMessage = _getString("MA.Step1.Video.Validation.NotSelected");
    }

    public string Title => _getString(_titleKey);

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private string _fileName = "--";

    [ObservableProperty]
    private string _resolution = "--";

    [ObservableProperty]
    private double _frameRate;

    [ObservableProperty]
    private double _durationSeconds;

    [ObservableProperty]
    private long _fileSizeBytes;

    [ObservableProperty]
    private VideoValidationStatus _status = VideoValidationStatus.Empty;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private ImageSource? _previewImage;

    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    public bool HasMetadata => Status is VideoValidationStatus.Passed or VideoValidationStatus.Warning;

    public string FrameRateDisplay => FrameRate > 0 ? $"{FrameRate:F2} fps" : "--";

    public string DurationDisplay => DurationSeconds > 0 ? TimeSpan.FromSeconds(DurationSeconds).ToString(@"mm\:ss\.f") : "--";

    public string FileSizeDisplay
    {
        get
        {
            if (FileSizeBytes <= 0)
            {
                return "--";
            }

            var mb = FileSizeBytes / 1024d / 1024d;
            return $"{mb:F1} MB";
        }
    }

    public string StatusDisplay => Status switch
    {
        VideoValidationStatus.Checking => _getString("MA.Step1.Video.Status.Checking"),
        VideoValidationStatus.Passed => _getString("MA.Step1.Video.Status.Passed"),
        VideoValidationStatus.Warning => _getString("MA.Step1.Video.Status.Warning"),
        VideoValidationStatus.Failed => _getString("MA.Step1.Video.Status.Failed"),
        _ => _getString("MA.Step1.Video.Status.Empty")
    };

    public void BeginLoad(string path)
    {
        FilePath = path;
        FileName = Path.GetFileName(path);
        Resolution = "--";
        FrameRate = 0;
        DurationSeconds = 0;
        FileSizeBytes = File.Exists(path) ? new FileInfo(path).Length : 0;
        Status = VideoValidationStatus.Checking;
        ValidationMessage = _getString("MA.Step1.Video.Validation.Checking");
        PreviewImage = null;
        NotifyComputedProperties();
    }

    public void ApplyMetadata(VideoMetadata metadata)
    {
        Resolution = metadata.Resolution;
        FrameRate = metadata.FrameRate;
        DurationSeconds = metadata.DurationSeconds;
        FileSizeBytes = metadata.FileSizeBytes;
        Status = VideoValidationStatus.Passed;
        ValidationMessage = _getString("MA.Step1.Video.Validation.Passed");
        NotifyComputedProperties();
    }

    public void MarkFailed(string message)
    {
        Status = VideoValidationStatus.Failed;
        ValidationMessage = message;
        NotifyComputedProperties();
    }

    public void Clear()
    {
        FilePath = null;
        FileName = "--";
        Resolution = "--";
        FrameRate = 0;
        DurationSeconds = 0;
        FileSizeBytes = 0;
        Status = VideoValidationStatus.Empty;
        ValidationMessage = _getString("MA.Step1.Video.Validation.NotSelected");
        PreviewImage = null;
        NotifyComputedProperties();
    }

    public void RefreshLocalizedText()
    {
        ValidationMessage = Status switch
        {
            VideoValidationStatus.Empty => _getString("MA.Step1.Video.Validation.NotSelected"),
            VideoValidationStatus.Checking => _getString("MA.Step1.Video.Validation.Checking"),
            VideoValidationStatus.Passed => _getString("MA.Step1.Video.Validation.Passed"),
            _ => ValidationMessage
        };

        OnPropertyChanged(nameof(Title));
        NotifyComputedProperties();
    }

    private void NotifyComputedProperties()
    {
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(HasMetadata));
        OnPropertyChanged(nameof(FrameRateDisplay));
        OnPropertyChanged(nameof(DurationDisplay));
        OnPropertyChanged(nameof(FileSizeDisplay));
        OnPropertyChanged(nameof(StatusDisplay));
    }
}

public sealed record VideoMetadata(
    string Resolution,
    double FrameRate,
    double DurationSeconds,
    long FileSizeBytes);
