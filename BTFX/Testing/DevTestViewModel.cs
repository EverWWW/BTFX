using System.Collections.ObjectModel;
using System.Windows;
using BTFX.Common;
using BTFX.Models;
using BTFX.Models.Analysis;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;
using BTFX.ViewModels.Measurement;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OxyPlot;

namespace BTFX.Testing;

/// <summary>
/// 开发测试入口 ViewModel
/// 提供按钮将评估/报告模块切换到各种 UI 状态，用于界面预览和美化
/// </summary>
public partial class DevTestViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 当前嵌入的内容视图（MeasurementView 或 ReportView）
    /// </summary>
    [ObservableProperty]
    private object? _previewContent;

    /// <summary>
    /// 状态说明文字
    /// </summary>
    [ObservableProperty]
    private string _statusText = "选择一个场景进入预览";

    /// <summary>
    /// 当前场景名称
    /// </summary>
    [ObservableProperty]
    private string _currentScenario = "无";

    public DevTestViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    // ========== Step4 评估模块场景 ==========

    /// <summary>
    /// Step4 Ready 状态（初始就绪）
    /// </summary>
    [RelayCommand]
    private void ShowStep4Ready()
    {
        var (measurementView, measurementVm, analyzeVm) = CreateMeasurementViewAtStep4();

        // 设置前置条件 - 全部满足
        analyzeVm.RefreshPrerequisites();

        CurrentScenario = "Step4 - 就绪状态 (Ready)";
        StatusText = "展示：前置条件检查列表 + 开始分析按钮";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step4 Running 状态（分析进行中）
    /// </summary>
    [RelayCommand]
    private void ShowStep4Running()
    {
        var (measurementView, measurementVm, analyzeVm) = CreateMeasurementViewAtStep4();

        // 模拟运行中状态
        analyzeVm.AnalysisState = AnalysisState.Running;
        analyzeVm.Progress = 58;
        analyzeVm.CurrentStage = "检测步态事件";
        analyzeVm.StatusMessage = "正在分析侧面视频关键点...";
        analyzeVm.ElapsedTime = TimeSpan.FromSeconds(32);

        // 添加模拟日志
        analyzeVm.TaskLogs = new ObservableCollection<string>
        {
            "[14:30:01] 分析任务已启动",
            "[14:30:02] 正在初始化算法引擎...",
            "[14:30:05] 算法引擎启动完成 (v2.1.0)",
            "[14:30:06] 开始检测正面视频关键点...",
            "[14:30:18] 正面视频关键点检测完成 (共 255 帧)",
            "[14:30:19] 开始检测侧面视频关键点...",
            "[14:30:28] 侧面视频关键点检测完成 (共 255 帧)",
            "[14:30:29] 开始检测步态事件...",
        };

        CurrentScenario = "Step4 - 运行中 (Running, 58%)";
        StatusText = "展示：进度条 + 阶段指示 + 实时日志 + 取消按钮";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step4 Previewing - A 级质量（视频+曲线+参数全可用）
    /// </summary>
    [RelayCommand]
    private void ShowStep4PreviewingGradeA()
    {
        var (measurementView, measurementVm, analyzeVm) = CreateMeasurementViewAtStep4();
        var result = MockDataGenerator.CreateGradeAResult();

        PopulatePreviewState(analyzeVm, result, hasVideo: false, hasCharts: true);

        CurrentScenario = "Step4 - 预览 A 级（优秀质量 + 曲线可用）";
        StatusText = "展示：曲线图 + 参数概览 + 质量等级 A（绿色）+ 底部质量栏";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step4 Previewing - B 级质量
    /// </summary>
    [RelayCommand]
    private void ShowStep4PreviewingGradeB()
    {
        var (measurementView, measurementVm, analyzeVm) = CreateMeasurementViewAtStep4();
        var result = MockDataGenerator.CreateGradeBResult();

        PopulatePreviewState(analyzeVm, result, hasVideo: false, hasCharts: true);

        CurrentScenario = "Step4 - 预览 B 级（良好质量）";
        StatusText = "展示：质量等级 B（橙色）+ 部分指标⚠️警告";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step4 Previewing - C 级质量
    /// </summary>
    [RelayCommand]
    private void ShowStep4PreviewingGradeC()
    {
        var (measurementView, measurementVm, analyzeVm) = CreateMeasurementViewAtStep4();
        var result = MockDataGenerator.CreateGradeCResult();

        PopulatePreviewState(analyzeVm, result, hasVideo: false, hasCharts: true);

        CurrentScenario = "Step4 - 预览 C 级（较差质量）";
        StatusText = "展示：质量等级 C（红色）+ 多项指标不达标";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step4 Previewing - 无视频降级
    /// </summary>
    [RelayCommand]
    private void ShowStep4NoVideo()
    {
        var (measurementView, measurementVm, analyzeVm) = CreateMeasurementViewAtStep4();
        var result = MockDataGenerator.CreateGradeAResult();

        PopulatePreviewState(analyzeVm, result, hasVideo: false, hasCharts: true);
        analyzeVm.HasVideoError = true;
        analyzeVm.VideoErrorMessage = "标注视频文件缺失，仅展示参数数据";
        analyzeVm.IsShowingParams = false; // 让用户看到视频降级覆盖层

        CurrentScenario = "Step4 - 视频不可用降级";
        StatusText = "展示：视频区域降级覆盖层（VideoOff 图标 + 提示文字）";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step4 Previewing - 无曲线降级
    /// </summary>
    [RelayCommand]
    private void ShowStep4NoChart()
    {
        var (measurementView, measurementVm, analyzeVm) = CreateMeasurementViewAtStep4();
        var result = MockDataGenerator.CreateGradeBResult();

        PopulatePreviewState(analyzeVm, result, hasVideo: false, hasCharts: false);

        CurrentScenario = "Step4 - 曲线不可用降级";
        StatusText = "展示：曲线图区域降级提示（ChartLineVariant 图标 + 提示文字）";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step4 Previewing - 全降级（无视频 + 无曲线）
    /// </summary>
    [RelayCommand]
    private void ShowStep4FullDegradation()
    {
        var (measurementView, measurementVm, analyzeVm) = CreateMeasurementViewAtStep4();
        var result = MockDataGenerator.CreateGradeCResult();

        PopulatePreviewState(analyzeVm, result, hasVideo: false, hasCharts: false);
        analyzeVm.HasVideoError = true;
        analyzeVm.VideoErrorMessage = "标注视频文件缺失，仅展示参数数据";

        CurrentScenario = "Step4 - 全降级（无视频 + 无曲线）";
        StatusText = "展示：视频和曲线均降级，自动切到参数概览";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step4 Failed 状态
    /// </summary>
    [RelayCommand]
    private void ShowStep4Failed()
    {
        var (measurementView, measurementVm, analyzeVm) = CreateMeasurementViewAtStep4();

        analyzeVm.AnalysisState = AnalysisState.Failed;
        analyzeVm.ErrorCode = 4;
        analyzeVm.ErrorDescription = "算法内部错误：关键点检测模型加载失败，请确认模型文件完整性。";
        analyzeVm.ErrorSuggestion = "算法内部错误，请重新分析或联系技术支持";
        analyzeVm.ElapsedTime = TimeSpan.FromSeconds(12);
        analyzeVm.IsLogExpanded = true;

        analyzeVm.TaskLogs = new ObservableCollection<string>
        {
            "[14:30:01] 分析任务已启动",
            "[14:30:02] 正在初始化算法引擎...",
            "[14:30:05] 算法引擎启动完成 (v2.1.0)",
            "[14:30:06] 开始检测正面视频关键点...",
            "[14:30:10] ❌ 错误: 模型文件加载失败",
            "[14:30:10] stderr: RuntimeError: Failed to load model weights",
            "[14:30:12] 分析失败: [4] 算法内部错误"
        };

        CurrentScenario = "Step4 - 分析失败 (Failed)";
        StatusText = "展示：错误图标 + 错误码/描述/建议 + 可展开日志面板 + 重试/返回按钮";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step4 参数概览子视图
    /// </summary>
    [RelayCommand]
    private void ShowStep4ParamsView()
    {
        var (measurementView, measurementVm, analyzeVm) = CreateMeasurementViewAtStep4();
        var result = MockDataGenerator.CreateGradeAResult();

        PopulatePreviewState(analyzeVm, result, hasVideo: false, hasCharts: true);
        analyzeVm.IsShowingParams = true;

        CurrentScenario = "Step4 - 参数概览子视图";
        StatusText = "展示：步态事件参数卡片 + 运动学参数卡片 + 质量评估卡片（含综合等级）";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step4 单视频（仅正面）Ready 状态
    /// </summary>
    [RelayCommand]
    private void ShowStep4SingleFrontReady()
    {
        var (measurementView, measurementVm, analyzeVm) = CreateMeasurementViewAtStep4SingleFront();
        analyzeVm.RefreshPrerequisites();

        CurrentScenario = "Step4 - 单视频（仅正面）就绪";
        StatusText = "展示：正面视频✓ + 侧面视频可选(未提供) + 至少一个视频✓ + 开始分析按钮可用";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step4 单视频（仅侧面）Ready 状态
    /// </summary>
    [RelayCommand]
    private void ShowStep4SingleSideReady()
    {
        var (measurementView, measurementVm, analyzeVm) = CreateMeasurementViewAtStep4SingleSide();
        analyzeVm.RefreshPrerequisites();

        CurrentScenario = "Step4 - 单视频（仅侧面）就绪";
        StatusText = "展示：正面视频可选(未提供) + 侧面视频✓ + 至少一个视频✓ + 开始分析按钮可用";
        PreviewContent = measurementView;
    }

    // ========== Step1 新建测量场景 ==========

    /// <summary>
    /// Step1 空表单（未选患者）
    /// </summary>
    [RelayCommand]
    private void ShowStep1Empty()
    {
        var (measurementView, measurementVm) = CreateMeasurementViewAtStep(1);

        // 保持默认空状态，不设置患者
        measurementVm.HasMeasurementRecord = false;

        CurrentScenario = "Step1 - 空表单（未选患者）";
        StatusText = "展示：测量名称/类型/备注表单 + 测量条件 + 创建按钮（禁用，因未选择患者）";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step1 已填表单（有患者 + 已填信息）
    /// </summary>
    [RelayCommand]
    private void ShowStep1Filled()
    {
        var (measurementView, measurementVm) = CreateMeasurementViewAtStep(1);

        // 模拟已填充数据
        measurementVm.MeasurementName = $"常规步态测量_{DateTime.Now:yyyyMMdd_HHmm}";
        measurementVm.SelectedMeasurementType = Common.MeasurementType.NormalWalk;
        measurementVm.Remark = "测试备注：患者行走时略有右偏，建议关注";
        measurementVm.WalkwayLength = 6.0;
        measurementVm.SelectedVideoSpec = Common.VideoSpec.P1080_30fps;
        measurementVm.SelectedVideoImportMode = Common.VideoImportMode.Import;

        CurrentScenario = "Step1 - 已填表单（有患者）";
        StatusText = "展示：填充的测量信息表单 + 测量条件选择 + 创建按钮可用";
        PreviewContent = measurementView;
    }

    // ========== Step2 导入/采集场景 ==========

    /// <summary>
    /// Step2 导入模式 - 无视频（空状态）
    /// </summary>
    [RelayCommand]
    private void ShowStep2ImportEmpty()
    {
        var (measurementView, measurementVm) = CreateMeasurementViewAtStep(2);

        // 已有测量记录但未选视频
        measurementVm.HasMeasurementRecord = true;
        measurementVm.HasFrontVideo = false;
        measurementVm.HasSideVideo = false;
        measurementVm.FrontVideoPath = null;
        measurementVm.SideVideoPath = null;

        CurrentScenario = "Step2 - 导入模式（空状态）";
        StatusText = "展示：双视频空卡片 + 选择文件按钮 + 导入策略 + 同步检查占位";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step2 导入模式 - 已选视频
    /// </summary>
    [RelayCommand]
    private void ShowStep2ImportWithVideo()
    {
        var (measurementView, measurementVm) = CreateMeasurementViewAtStep(2);
        var patient = MockDataGenerator.CreateMockPatient();
        var measurement = MockDataGenerator.CreateMockMeasurement(patient);

        measurementVm.HasMeasurementRecord = true;
        measurementVm.CurrentMeasurement = measurement;
        measurementVm.HasFrontVideo = true;
        measurementVm.HasSideVideo = true;
        measurementVm.FrontVideoPath = measurement.FrontVideoPath;
        measurementVm.SideVideoPath = measurement.SideVideoPath;
        measurementVm.SelectedImportStrategy = Common.ImportStrategy.CopyToFolder;

        CurrentScenario = "Step2 - 导入模式（双视频已选择）";
        StatusText = "展示：双视频路径显示 + 清除按钮可用 + 确认导入按钮可用";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step2 采集 - 双视频待机状态
    /// </summary>
    [RelayCommand]
    private void ShowStep2CaptureDualIdle()
    {
        var (measurementView, measurementVm) = CreateMeasurementViewAtStep(2);

        measurementVm.HasMeasurementRecord = true;
        measurementVm.SelectedCaptureLayout = Common.CaptureLayout.Dual;
        measurementVm.SelectedRecordDuration = Common.RecordDuration.Seconds30;
        measurementVm.CaptureState = Common.CaptureState.Idle;
        measurementVm.RecordingElapsedSeconds = 0;

        CurrentScenario = "Step2 - 采集（双视频 · 待机）";
        StatusText = "展示：双画面预览框 + 配置栏（双视频/30秒）+ 开始录制按钮";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step2 采集 - 单视频待机状态
    /// </summary>
    [RelayCommand]
    private void ShowStep2CaptureSingleIdle()
    {
        var (measurementView, measurementVm) = CreateMeasurementViewAtStep(2);

        measurementVm.HasMeasurementRecord = true;
        measurementVm.SelectedCaptureLayout = Common.CaptureLayout.Single;
        measurementVm.SelectedRecordDuration = Common.RecordDuration.Seconds60;
        measurementVm.CaptureState = Common.CaptureState.Idle;
        measurementVm.RecordingElapsedSeconds = 0;

        CurrentScenario = "Step2 - 采集（单视频 · 待机）";
        StatusText = "展示：单画面大预览框 + 配置栏（单视频/1分钟）+ 开始录制按钮";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step2 采集 - 录制中状态
    /// </summary>
    [RelayCommand]
    private void ShowStep2CaptureRecording()
    {
        var (measurementView, measurementVm) = CreateMeasurementViewAtStep(2);

        measurementVm.HasMeasurementRecord = true;
        measurementVm.SelectedCaptureLayout = Common.CaptureLayout.Dual;
        measurementVm.SelectedRecordDuration = Common.RecordDuration.Seconds30;
        measurementVm.CaptureState = Common.CaptureState.Recording;
        measurementVm.RecordingElapsedSeconds = 12;

        CurrentScenario = "Step2 - 采集（双视频 · 录制中 12s/30s）";
        StatusText = "展示：REC红点指示 + 停止按钮 + 进度条(40%) + 倒计时 00:18";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step2 采集 - 录制完成状态
    /// </summary>
    [RelayCommand]
    private void ShowStep2CaptureCompleted()
    {
        var (measurementView, measurementVm) = CreateMeasurementViewAtStep(2);

        measurementVm.HasMeasurementRecord = true;
        measurementVm.SelectedCaptureLayout = Common.CaptureLayout.Dual;
        measurementVm.SelectedRecordDuration = Common.RecordDuration.Seconds30;
        measurementVm.CaptureState = Common.CaptureState.Completed;
        measurementVm.RecordingElapsedSeconds = 30;

        CurrentScenario = "Step2 - 采集（双视频 · 录制完成）";
        StatusText = "展示：绿勾完成提示 + 确认采集按钮 + 重新录制按钮 + 进度条100%";
        PreviewContent = measurementView;
    }

    // ========== Step3 回放检查场景 ==========

    /// <summary>
    /// Step3 回放检查 - 双视频已加载
    /// </summary>
    [RelayCommand]
    private void ShowStep3Review()
    {
        var (measurementView, measurementVm) = CreateMeasurementViewAtStep(3);
        var patient = MockDataGenerator.CreateMockPatient();
        var measurement = MockDataGenerator.CreateMockMeasurement(patient);

        measurementVm.HasMeasurementRecord = true;
        measurementVm.CurrentMeasurement = measurement;
        measurementVm.HasFrontVideo = true;
        measurementVm.HasSideVideo = true;
        measurementVm.FrontVideoPath = measurement.FrontVideoPath;
        measurementVm.SideVideoPath = measurement.SideVideoPath;

        CurrentScenario = "Step3 - 回放检查（双视频）";
        StatusText = "展示：双画面播放器 + 播放控制条 + 叠加选项 + 事件时间轴 + 进入分析按钮";
        PreviewContent = measurementView;
    }

    /// <summary>
    /// Step3 回放检查 - 仅单视频
    /// </summary>
    [RelayCommand]
    private void ShowStep3SingleVideo()
    {
        var (measurementView, measurementVm) = CreateMeasurementViewAtStep(3);

        measurementVm.HasMeasurementRecord = true;
        measurementVm.HasFrontVideo = true;
        measurementVm.HasSideVideo = false;
        measurementVm.FrontVideoPath = @"D:\Videos\patient_front_20260409.mp4";
        measurementVm.SideVideoPath = null;

        CurrentScenario = "Step3 - 回放检查（仅正面视频）";
        StatusText = "展示：正面视频已加载 + 侧面视频未加载提示 + 进入分析按钮禁用";
        PreviewContent = measurementView;
    }

    // ========== 报告模块场景 ==========

    /// <summary>
    /// 报告列表模式
    /// </summary>
    [RelayCommand]
    private void ShowReportList()
    {
        var reportView = CreateReportView();
        if (reportView == null) return;

        var vm = reportView.DataContext as ViewModels.ReportViewModel;
        if (vm == null) return;

        // 注入模拟报告列表
        vm.CurrentModeIndex = 0;
        vm.Reports = new ObservableCollection<ReportItem>(MockDataGenerator.CreateMockReportItems());
        vm.CanGenerateReport = true;
        vm.CanEditReport = true;
        vm.CanDeleteReport = true;
        vm.CanExportReport = true;

        CurrentScenario = "报告 - 列表模式";
        StatusText = "展示：报告列表表格 + 筛选器 + 操作按钮（查看/编辑/删除/导出）";
        PreviewContent = reportView;
    }

    /// <summary>
    /// 报告生成模式
    /// </summary>
    [RelayCommand]
    private void ShowReportGenerate()
    {
        var reportView = CreateReportView();
        if (reportView == null) return;

        var vm = reportView.DataContext as ViewModels.ReportViewModel;
        if (vm == null) return;

        vm.CurrentModeIndex = 1;
        vm.MeasurementRecords = new ObservableCollection<MeasurementRecordItem>(
            MockDataGenerator.CreateMockMeasurementRecordItems());
        vm.CanGenerateReport = true;

        CurrentScenario = "报告 - 生成模式";
        StatusText = "展示：测量数据选择列表 + 筛选器 + 生成报告按钮";
        PreviewContent = reportView;
    }

    /// <summary>
    /// 清除预览
    /// </summary>
    [RelayCommand]
    private void ClearPreview()
    {
        PreviewContent = null;
        CurrentScenario = "无";
        StatusText = "选择一个场景进入预览";
    }

    // ========== 私有辅助方法 ==========

    /// <summary>
    /// 创建 MeasurementView 并导航到 Step4
    /// </summary>
    private (FrameworkElement view, MeasurementViewModel mvm, Step4AnalyzeViewModel avm)
        CreateMeasurementViewAtStep4()
    {
        var (view, mvm) = CreateMeasurementViewAtStep(4);
        var analyzeVm = mvm.AnalyzeViewModel;

        // 设置模拟患者和测量记录
        var patient = MockDataGenerator.CreateMockPatient();
        var measurement = MockDataGenerator.CreateMockMeasurement(patient);

        mvm.CurrentMeasurement = measurement;
        mvm.HasMeasurementRecord = true;
        mvm.HasFrontVideo = true;
        mvm.HasSideVideo = true;
        mvm.FrontVideoPath = measurement.FrontVideoPath;
        mvm.SideVideoPath = measurement.SideVideoPath;

        analyzeVm.CurrentMeasurement = measurement;
        analyzeVm.CurrentPatient = patient;

        return (view, mvm, analyzeVm);
    }

    /// <summary>
    /// 创建 MeasurementView 并导航到 Step4（仅正面视频）
    /// </summary>
    private (FrameworkElement view, MeasurementViewModel mvm, Step4AnalyzeViewModel avm)
        CreateMeasurementViewAtStep4SingleFront()
    {
        var (view, mvm) = CreateMeasurementViewAtStep(4);
        var analyzeVm = mvm.AnalyzeViewModel;

        var patient = MockDataGenerator.CreateMockPatient();
        var measurement = MockDataGenerator.CreateMockMeasurement(patient);

        // 仅保留正面视频
        measurement.SideVideoPath = null;

        mvm.CurrentMeasurement = measurement;
        mvm.HasMeasurementRecord = true;
        mvm.HasFrontVideo = true;
        mvm.HasSideVideo = false;
        mvm.FrontVideoPath = measurement.FrontVideoPath;
        mvm.SideVideoPath = null;

        analyzeVm.CurrentMeasurement = measurement;
        analyzeVm.CurrentPatient = patient;

        return (view, mvm, analyzeVm);
    }

    /// <summary>
    /// 创建 MeasurementView 并导航到 Step4（仅侧面视频）
    /// </summary>
    private (FrameworkElement view, MeasurementViewModel mvm, Step4AnalyzeViewModel avm)
        CreateMeasurementViewAtStep4SingleSide()
    {
        var (view, mvm) = CreateMeasurementViewAtStep(4);
        var analyzeVm = mvm.AnalyzeViewModel;

        var patient = MockDataGenerator.CreateMockPatient();
        var measurement = MockDataGenerator.CreateMockMeasurement(patient);

        // 仅保留侧面视频
        measurement.FrontVideoPath = null;

        mvm.CurrentMeasurement = measurement;
        mvm.HasMeasurementRecord = true;
        mvm.HasFrontVideo = false;
        mvm.HasSideVideo = true;
        mvm.FrontVideoPath = null;
        mvm.SideVideoPath = measurement.SideVideoPath;

        analyzeVm.CurrentMeasurement = measurement;
        analyzeVm.CurrentPatient = patient;

        return (view, mvm, analyzeVm);
    }

    /// <summary>
    /// 创建 MeasurementView 并导航到指定步骤
    /// </summary>
    private (FrameworkElement view, MeasurementViewModel mvm)
        CreateMeasurementViewAtStep(int step)
    {
        var measurementView = _serviceProvider.GetService(typeof(Views.Measurement.MeasurementView))
            as FrameworkElement ?? throw new InvalidOperationException("无法创建 MeasurementView");

        var measurementVm = measurementView.DataContext as MeasurementViewModel
            ?? throw new InvalidOperationException("无法获取 MeasurementViewModel");

        // 跳转到指定步骤（需要满足前置条件才能切换）
        if (step >= 2) measurementVm.HasMeasurementRecord = true;
        if (step >= 3) { measurementVm.HasFrontVideo = true; measurementVm.HasSideVideo = true; }
        measurementVm.CurrentStep = step;
        // 恢复步骤2/3场景调用方需要的状态
        if (step < 4) { measurementVm.HasFrontVideo = false; measurementVm.HasSideVideo = false; }

        return (measurementView, measurementVm);
    }

    /// <summary>
    /// 填充 Previewing 状态的全部数据
    /// </summary>
    private static void PopulatePreviewState(
        Step4AnalyzeViewModel vm, AnalysisResult result,
        bool hasVideo, bool hasCharts)
    {
        vm.AnalysisResult = result;
        vm.AnalysisDurationDisplay = "00:45";
        vm.ElapsedTime = TimeSpan.FromSeconds(45);

        // 步态事件参数
        vm.GaitEventResult = new GaitEventParametersDisplay
        {
            GaitCycleDuration = $"{result.GaitCycleDurationS:F2}s",
            StanceTime = $"{result.StanceTimeS:F2}s",
            SwingTime = $"{result.SwingTimeS:F2}s",
            DoubleSupportTime = $"{result.DoubleSupportTimeS:F2}s",
            SingleSupportTime = $"{result.SingleSupportTimeS:F2}s",
            StepLength = result.StepLengthM.HasValue ? $"{result.StepLengthM.Value * 100:F1}cm" : "--",
            StrideLength = result.StrideLengthM.HasValue ? $"{result.StrideLengthM.Value * 100:F1}cm" : "--",
            GaitSpeed = $"{result.GaitSpeedMPerS:F2}m/s"
        };

        // 运动学参数
        if (result.KinematicSummary is { } ks)
        {
            vm.KinematicResult = new KinematicSummaryDisplay
            {
                HipRom = $"{ks.HipRomDeg:F2}°",
                KneeRom = $"{ks.KneeRomDeg:F2}°",
                AnkleRom = $"{ks.AnkleRomDeg:F2}°",
                PelvisRom = $"{ks.PelvisCoronalRomDeg:F2}°"
            };
        }

        // 质量评估
        if (result.QualityControl is { } qc)
        {
            var confidenceGrade = qc.MeanKeypointConfidence switch
            {
                >= 0.85 => QualityGrade.Excellent,
                >= 0.70 => QualityGrade.Good,
                _ => QualityGrade.Poor
            };

            var frameRatioGrade = qc.ValidFrameRatio switch
            {
                >= 0.95 => QualityGrade.Excellent,
                >= 0.80 => QualityGrade.Good,
                _ => QualityGrade.Poor
            };

            var occlusionGrade = qc.OcclusionWarning ? QualityGrade.Poor : QualityGrade.Excellent;
            var missingGrade = qc.MissingPointWarning ? QualityGrade.Poor : QualityGrade.Excellent;

            var worstGrade = (QualityGrade)Math.Max(
                Math.Max((int)confidenceGrade, (int)frameRatioGrade),
                Math.Max((int)occlusionGrade, (int)missingGrade));

            var (gradeDisplay, gradeColor, gradeDesc) = worstGrade switch
            {
                QualityGrade.Excellent => ("A", "#4CAF50", "数据质量优秀"),
                QualityGrade.Good => ("B", "#FF9800", "数据质量良好，部分指标需关注"),
                _ => ("C", "#F44336", "数据质量较差，建议重新采集")
            };

            vm.QualityResult = new QualityControlDisplay
            {
                Confidence = qc.MeanKeypointConfidence ?? 0,
                ConfidenceDisplay = qc.MeanKeypointConfidence?.ToString("F2") ?? "--",
                ConfidenceOk = confidenceGrade != QualityGrade.Poor,
                ConfidenceGrade = confidenceGrade,
                ValidFrameRatio = qc.ValidFrameRatio ?? 0,
                ValidFrameRatioDisplay = qc.ValidFrameRatio.HasValue ? $"{qc.ValidFrameRatio.Value * 100:F0}%" : "--",
                ValidFrameRatioOk = frameRatioGrade != QualityGrade.Poor,
                ValidFrameRatioGrade = frameRatioGrade,
                HasOcclusion = qc.OcclusionWarning,
                HasMissingPoints = qc.MissingPointWarning,
                OverallGrade = worstGrade,
                GradeDisplay = gradeDisplay,
                GradeColor = gradeColor,
                GradeDescription = gradeDesc
            };
        }

        // 视频
        vm.HasVideoError = !hasVideo;
        if (!hasVideo)
        {
            vm.AnnotatedVideoPath = null;
        }

        // 曲线图
        if (hasCharts)
        {
            var (hip, knee, ankle, pelvis) = MockDataGenerator.CreateMockPlotModels();
            vm.HipAnglePlotModel = hip;
            vm.KneeAnglePlotModel = knee;
            vm.AnkleAnglePlotModel = ankle;
            vm.PelvisAnglePlotModel = pelvis;
            vm.HasChartData = true;
        }
        else
        {
            vm.HipAnglePlotModel = null;
            vm.KneeAnglePlotModel = null;
            vm.AnkleAnglePlotModel = null;
            vm.PelvisAnglePlotModel = null;
            vm.HasChartData = false;
        }

        // 设为预览状态
        vm.AnalysisState = AnalysisState.Previewing;
        if (!hasVideo)
        {
            vm.IsShowingParams = true;
        }
    }

    /// <summary>
    /// 创建 ReportView
    /// </summary>
    private FrameworkElement? CreateReportView()
    {
        return _serviceProvider.GetService(typeof(Views.ReportView)) as FrameworkElement;
    }
}
