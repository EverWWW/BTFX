**完整可交付的《测量评估模块 UI 设计与开发说明书（终版）》**：

* **WPF (.NET 10) + UserControl（区域导航）**
* **CommunityToolkit.Mvvm**
* **MaterialDesignInXaml**
* **中英文切换：ResourceDictionary 资源字典加载字符串**
* 所有涉及显示文字的地方，均提供 **中文（zh-CN）/英文（en-US）** 两套文本（以资源键形式给出）

> 说明：本文档为“交给他人照着搭 UI”的规格文档；不包含算法、相机接入实现要求。播放器在UI阶段允许占位，后续替换为 LibVLCSharp.WPF 等组件不影响本UI规范。

---

# 测量评估模块 UI 设计与开发说明书（终版）

## 1. 模块信息

* 模块名称：测量评估（Measurement Assessment）
* 承载方式：WPF `UserControl`，区域导航加载
* UI风格：MaterialDesignInXaml
* MVVM：CommunityToolkit.Mvvm
* 多语言：资源字典（ResourceDictionary）动态切换（zh-CN / en-US）

---

## 2. 名词定义（UI语义）

### 2.1 测量记录（Measurement Record）

* 定义：一次完整测量流程对应的一条数据记录（导入/采集视频、回放检查、分析、报告入口）。
* UI统一称呼：**本次测量 / 测量记录**（避免术语 Trial）

### 2.2 分析阶段（Analysis Stage）

* None（未分析）
* Keypoints（关键点）
* Events（步态事件）
* Kinematics（运动学）

---

## 3. 总体布局与信息架构

## 3.1 页面四区布局（固定）

使用 Grid（三列 + 底部抽屉）：

* 左列：StepBar（四步向导）
* 中列：StepContent（步骤内容区）
* 右列：InfoPanel（固定信息栏）
* 底部：TaskDrawer（任务与日志抽屉，DrawerHost）

### 3.2 StepBar（四步向导）

固定四步：

1. 新建测量
2. 导入/采集
3. 回放检查
4. 分析结果

每步显示：

* 状态图标（PackIcon）
* 步骤标题（多语言）
* 副提示（多语言，可为空）

### 3.3 StepContent（内容切换）

StepContent 使用 `ContentControl`，绑定 `CurrentStep` 或 `CurrentStepViewModel`，通过 DataTemplate 映射：

* Step1CreateMeasurementView
* Step2VideoSourceView
* Step3ReviewView
* Step4AnalyzeView

### 3.4 InfoPanel（右侧信息栏卡片）

固定卡片（Card）：

1. 当前患者
2. 本次测量
3. 视频状态
4. 分析阶段
5. 质量提示（占位）

### 3.5 TaskDrawer（底部任务抽屉）

建议使用 MaterialDesign 的 `DrawerHost` 实现底部抽屉，包含：

* 当前任务（进度、耗时、取消）
* 日志（最近N行）
* 历史任务（占位）

---

## 4. 状态模型与交互规则（统一）

### 4.1 顶层状态字段（MeasurementAssessmentViewModel）

* `CurrentStep : int`（1..4）
* `HasMeasurementRecord : bool`
* `HasFrontVideo : bool`
* `HasSideVideo : bool`
* `IsAnalyzing : bool`
* `CompletedStage : AnalysisStage`（None/Keypoints/Events/Kinematics）
* `SelectedStage : AnalysisStage`

### 4.2 Step 可进入规则

* Step1：始终可进入
* Step2：`HasMeasurementRecord == true`
* Step3：`HasFrontVideo || HasSideVideo`
* Step4：`HasFrontVideo && HasSideVideo`

### 4.3 按钮使能规则（关键）

* 创建测量：患者上下文存在即可
* 导入/清除视频：`HasMeasurementRecord && !IsAnalyzing`
* 进入分析：`HasFrontVideo && HasSideVideo`
* 开始分析：`HasFrontVideo && HasSideVideo && !IsAnalyzing`
* 取消分析：`IsAnalyzing == true`

### 4.4 弹窗与提示（MaterialDesign）

* 确认类弹窗：`DialogHost`
* 轻提示：`Snackbar`
* 错误提示：优先页面内红字 + 可点详情（占位）

---

## 5. 视图规范（逐步详细规格）

---

# 5.1 Step1：新建测量（CreateMeasurement）

## 5.1.1 UI构成（中间内容区）

**Card A：本次测量信息**

* Label：测量名称
* TextBox：测量名称（默认自动填：日期时间）
* Label：测量类型
* ComboBox：自然步行/快走/慢走/其他
* Label：备注
* TextBox（多行）：备注

**Card B：测量条件（UI保留）**

* Label：视频规格
* ComboBox：1080P 30fps / 1440P 30fps
* Label：步道长度（m）
* TextBox：默认 6
* Label：模式
* RadioButton：导入 / 采集（默认导入）

**底部操作栏**

* Primary Button：创建本次测量
* Secondary Button：重置
* Next Button：下一步（创建成功后启用）

## 5.1.2 交互逻辑

* 点击“创建本次测量”：

  * `HasMeasurementRecord=true`
  * `CurrentStep=2`
  * InfoPanel 刷新显示测量记录信息
* 再次创建（已有记录）：

  * DialogHost 确认覆盖：

    * 覆盖并新建 / 取消

## 5.1.3 校验

* 步道长度必须为正数；非法时输入框下显示错误文本，禁止创建。

---

# 5.2 Step2：导入/采集（VideoSource）

## 5.2.1 UI构成

使用 TabControl：

### Tab1：导入视频

**双视频卡（并排）**

* Front Video Card：

  * 标题：正面视频
  * 路径显示（只读）
  * 按钮：选择文件 / 清除
  * 元信息行：分辨率 / 帧率 / 时长（UI阶段可显示“—”）
* Side Video Card：

  * 标题：侧面视频
  * 同上

**导入策略 Card**

* Radio：复制到测量目录（默认）/ 仅引用原路径
* 目标路径显示（占位）

**同步检查 Card（UI占位）**

* fps一致性、分辨率一致性、时长差（—）
* 总体提示条（—）

**底部操作栏**

* Primary：确认导入
* Secondary：上一步
* Next：下一步（至少一个视频就绪启用）

### Tab2：实时采集（占位）

* 设备选择下拉（Front/Side）
* 双预览区域占位
* 录制按钮禁用并显示提示文本（未配置）

## 5.2.2 交互逻辑

* 选择Front视频成功 → `HasFrontVideo=true`
* 选择Side视频成功 → `HasSideVideo=true`
* 清除对应视频 → HasXXXVideo=false
* 点击下一步：

  * 无视频：阻止并显示红字提示
  * 单视频：DialogHost 确认继续进入Step3
  * 双视频：进入Step3

---

# 5.3 Step3：回放检查（Review）

> UI阶段播放器允许占位；控件布局必须支持后续替换为真实播放器。

## 5.3.1 UI构成

**上半区：双画面回放（左右并排）**

* Front 播放区域（占位容器 Card/Border）

  * 中心图标：PlayCircleOutline
  * 文案：未加载/已加载（取决于 HasFrontVideo）
* Side 播放区域（同上）

**播放控制条 Card**

* 播放/暂停按钮
* 上一帧/下一帧
* 倍速下拉（0.5x/1x/2x）
* 进度滑条
* 当前时间/总时长（00:00 / 00:00）

**下半区：叠加与事件（左右）**

* Overlay Card：

  * CheckBox：显示关键点
  * CheckBox：显示骨架（依赖显示关键点）
  * CheckBox：显示置信度（占位）
  * Combo：关键点组（下肢/躯干/全部，占位）
  * 空状态提示：未生成关键点时提示
* Timeline Card：

  * 空状态提示：暂无事件数据
  * 时间轴示意控件（占位）

**底部操作栏**

* 返回导入
* 进入分析（双视频就绪才启用）

## 5.3.2 交互逻辑

* `进入分析`：

  * 双视频存在 → `CurrentStep=4`
  * 否则禁用并提示原因
* Overlay开关：

  * 若 `CompletedStage < Keypoints`，Snackbar提示“尚未生成关键点”

---

# 5.4 Step4：分析结果（Analyze）

## 5.4.1 UI构成

**阶段选择卡（三张卡片）**

* Keypoints / Events / Kinematics
* 展示状态：未运行/完成/失败
* 点击卡片设置 `SelectedStage`

**参数设置区（Expander/ExpansionPanel）**

* 后处理开关与输入框（占位）
* 事件阈值（占位）
* IK开关（占位）

**运行控制区**

* 开始分析（Primary）
* 取消（分析中启用）
* 重跑（阶段完成后显示）
* 打开输出目录（占位）

**结果摘要区（卡片网格，占位但结构固定）**

* 时空参数
* 相位参数
* ROM
* 质量指标

**联动入口**

* 查看回放叠加（回Step3）
* 生成报告（导航到报告模块并携带测量记录ID）

## 5.4.2 交互逻辑（UI模拟）

* 点击开始分析：

  * `IsAnalyzing=true`
  * TaskDrawer展开
  * 进度模拟到100
  * 完成后：`IsAnalyzing=false`、`CompletedStage=SelectedStage`
* 点击取消：

  * `IsAnalyzing=false`，任务停止

---

## 6. InfoPanel（右侧信息栏）规格

### 6.1 当前患者 Card

* 姓名、编号、性别/年龄（可选）
* 按钮：切换患者

### 6.2 本次测量 Card

* 测量名称、创建时间、记录ID、当前步骤
* 按钮：打开测量目录（UI阶段可隐藏/占位）

### 6.3 视频状态 Card

* 正面：已选择/未选择
* 侧面：已选择/未选择
* 单视角时黄色提示“分析不可用”

### 6.4 分析阶段 Card

* Keypoints/Events/Kinematics 三行状态
* 快捷按钮：去分析（Step4）

### 6.5 质量提示 Card（占位）

* 默认“—”
* 文案提示：完成关键点分析后显示质量信息

---

## 7. TaskDrawer（任务抽屉）规格（DrawerHost）

### 7.1 UI构成

* 当前任务卡：

  * 任务名
  * 进度条
  * 耗时
  * 取消按钮
* 日志卡：

  * 最近N行日志（只读）
* 历史任务（占位）

### 7.2 展开规则

* 分析开始自动展开
* 分析完成/失败保持展开，用户手动收起

---

# 8. ViewModel 绑定与命令规范（CommunityToolkit.Mvvm）

## 8.1 顶层 VM 字段（建议用 [ObservableProperty]）

* `CurrentStep`
* `HasMeasurementRecord`
* `HasFrontVideo`
* `HasSideVideo`
* `IsAnalyzing`
* `CompletedStage`
* `SelectedStage`

## 8.2 顶层 VM 命令（建议用 [RelayCommand]）

* `GoToStep(int stepIndex)`
* `CreateMeasurement()`
* `ResetMeasurement()`
* `SelectFrontVideo()`
* `SelectSideVideo()`
* `ClearFrontVideo()`
* `ClearSideVideo()`
* `ConfirmImport()`
* `StartAnalyze()`
* `CancelAnalyze()`
* `GoToReport()`
* `BackToPatientSelect()`
* `ToggleTaskDrawer()`

> DialogHost 与 Snackbar 通过服务接口（IDialogService/INotificationService）调用，VM不直接依赖UI控件。

---

# 9. 多语言资源字典规范（zh-CN / en-US）

## 9.1 资源键命名规则

* 前缀统一：`MA.`（Measurement Assessment）
* 子模块：`Step1/Step2/Step3/Step4/Info/Task/Common/Dialog`

例如：

* `MA.Step1.Title`
* `MA.Common.Next`
* `MA.Dialog.VideoIncomplete.Title`

## 9.2 字符资源清单（中文/英文）

> 以下为**完整UI文字**建议集合。项目可按需裁剪，但建议保留键名一致性。

### 9.2.1 公共（Common）

* `MA.Common.MeasurementAssessment`

  * zh-CN：测量评估
  * en-US：Measurement Assessment
* `MA.Common.Step`

  * zh-CN：步骤
  * en-US：Step
* `MA.Common.Next`

  * zh-CN：下一步
  * en-US：Next
* `MA.Common.Back`

  * zh-CN：上一步
  * en-US：Back
* `MA.Common.Confirm`

  * zh-CN：确认
  * en-US：Confirm
* `MA.Common.Cancel`

  * zh-CN：取消
  * en-US：Cancel
* `MA.Common.Close`

  * zh-CN：关闭
  * en-US：Close
* `MA.Common.Reset`

  * zh-CN：重置
  * en-US：Reset
* `MA.Common.Save`

  * zh-CN：保存
  * en-US：Save
* `MA.Common.OpenFolder`

  * zh-CN：打开文件夹
  * en-US：Open Folder
* `MA.Common.Optional`

  * zh-CN：（可选）
  * en-US：(Optional)
* `MA.Common.NotAvailable`

  * zh-CN：—
  * en-US：—

### 9.2.2 StepBar（四步标题）

* `MA.StepBar.Step1`

  * zh-CN：新建测量
  * en-US：Create Measurement
* `MA.StepBar.Step2`

  * zh-CN：导入/采集
  * en-US：Import / Capture
* `MA.StepBar.Step3`

  * zh-CN：回放检查
  * en-US：Review
* `MA.StepBar.Step4`

  * zh-CN：分析结果
  * en-US：Analyze

### 9.2.3 Step1（新建测量）

* `MA.Step1.Title`

  * zh-CN：新建本次测量
  * en-US：Create a New Measurement
* `MA.Step1.Card.BasicInfo`

  * zh-CN：本次测量信息
  * en-US：Measurement Info
* `MA.Step1.MeasurementName`

  * zh-CN：测量名称
  * en-US：Measurement Name
* `MA.Step1.MeasurementType`

  * zh-CN：测量类型
  * en-US：Measurement Type
* `MA.Step1.Type.NormalWalk`

  * zh-CN：自然步行
  * en-US：Normal Walk
* `MA.Step1.Type.FastWalk`

  * zh-CN：快走
  * en-US：Fast Walk
* `MA.Step1.Type.SlowWalk`

  * zh-CN：慢走
  * en-US：Slow Walk
* `MA.Step1.Type.Other`

  * zh-CN：其他
  * en-US：Other
* `MA.Step1.Remark`

  * zh-CN：备注
  * en-US：Notes
* `MA.Step1.Card.Conditions`

  * zh-CN：测量条件
  * en-US：Measurement Settings
* `MA.Step1.VideoSpec`

  * zh-CN：视频规格
  * en-US：Video Preset
* `MA.Step1.VideoSpec.1080p30`

  * zh-CN：1080P / 30 FPS
  * en-US：1080p / 30 FPS
* `MA.Step1.VideoSpec.1440p30`

  * zh-CN：1440P / 30 FPS
  * en-US：1440p / 30 FPS
* `MA.Step1.WalkwayLength`

  * zh-CN：步道长度（m）
  * en-US：Walkway Length (m)
* `MA.Step1.Mode`

  * zh-CN：模式
  * en-US：Mode
* `MA.Step1.Mode.Import`

  * zh-CN：导入
  * en-US：Import
* `MA.Step1.Mode.Capture`

  * zh-CN：采集
  * en-US：Capture
* `MA.Step1.CreateButton`

  * zh-CN：创建本次测量
  * en-US：Create Measurement
* `MA.Step1.Validation.WalkwayLengthInvalid`

  * zh-CN：请输入有效的步道长度（正数）。
  * en-US：Please enter a valid walkway length (positive number).

### 9.2.4 Step2（导入/采集）

* `MA.Step2.Title`

  * zh-CN：导入/采集视频
  * en-US：Import / Capture Videos
* `MA.Step2.Tab.Import`

  * zh-CN：导入视频
  * en-US：Import
* `MA.Step2.Tab.Capture`

  * zh-CN：实时采集
  * en-US：Capture
* `MA.Step2.FrontVideo`

  * zh-CN：正面视频
  * en-US：Front Video
* `MA.Step2.SideVideo`

  * zh-CN：侧面视频
  * en-US：Side Video
* `MA.Step2.SelectFile`

  * zh-CN：选择文件
  * en-US：Select File
* `MA.Step2.Clear`

  * zh-CN：清除
  * en-US：Clear
* `MA.Step2.Meta.Resolution`

  * zh-CN：分辨率
  * en-US：Resolution
* `MA.Step2.Meta.Fps`

  * zh-CN：帧率
  * en-US：FPS
* `MA.Step2.Meta.Duration`

  * zh-CN：时长
  * en-US：Duration
* `MA.Step2.ImportStrategy`

  * zh-CN：导入策略
  * en-US：Import Strategy
* `MA.Step2.Strategy.Copy`

  * zh-CN：复制到测量目录（推荐）
  * en-US：Copy to Measurement Folder (Recommended)
* `MA.Step2.Strategy.Reference`

  * zh-CN：仅引用原路径
  * en-US：Reference Original Path Only
* `MA.Step2.SyncCheck`

  * zh-CN：同步检查
  * en-US：Sync Check
* `MA.Step2.ImportConfirm`

  * zh-CN：确认导入
  * en-US：Confirm Import
* `MA.Step2.Capture.NotConfigured`

  * zh-CN：设备未配置，采集功能暂不可用。
  * en-US：Device not configured. Capture is unavailable.
* `MA.Step2.Validation.NoVideo`

  * zh-CN：请至少导入一个视频。
  * en-US：Please import at least one video.

### 9.2.5 Step3（回放检查）

* `MA.Step3.Title`

  * zh-CN：回放检查
  * en-US：Review
* `MA.Step3.FrontView`

  * zh-CN：正面视角
  * en-US：Front View
* `MA.Step3.SideView`

  * zh-CN：侧面视角
  * en-US：Side View
* `MA.Step3.Player.Placeholder`

  * zh-CN：视频区域（待接入播放器）
  * en-US：Video Area (Player Pending)
* `MA.Step3.Controls.Play`

  * zh-CN：播放
  * en-US：Play
* `MA.Step3.Controls.Pause`

  * zh-CN：暂停
  * en-US：Pause
* `MA.Step3.Controls.PrevFrame`

  * zh-CN：上一帧
  * en-US：Prev Frame
* `MA.Step3.Controls.NextFrame`

  * zh-CN：下一帧
  * en-US：Next Frame
* `MA.Step3.Controls.Speed`

  * zh-CN：倍速
  * en-US：Speed
* `MA.Step3.Overlay.Title`

  * zh-CN：叠加显示
  * en-US：Overlay
* `MA.Step3.Overlay.ShowKeypoints`

  * zh-CN：显示关键点
  * en-US：Show Keypoints
* `MA.Step3.Overlay.ShowSkeleton`

  * zh-CN：显示骨架
  * en-US：Show Skeleton
* `MA.Step3.Overlay.ShowConfidence`

  * zh-CN：显示置信度
  * en-US：Show Confidence
* `MA.Step3.Overlay.KeypointGroup`

  * zh-CN：关键点组
  * en-US：Keypoint Group
* `MA.Step3.Overlay.Group.LowerLimb`

  * zh-CN：下肢
  * en-US：Lower Limb
* `MA.Step3.Overlay.Group.Trunk`

  * zh-CN：躯干
  * en-US：Trunk
* `MA.Step3.Overlay.Group.All`

  * zh-CN：全部
  * en-US：All
* `MA.Step3.Overlay.NoKeypoints`

  * zh-CN：尚未生成关键点，请先在“分析结果”步骤运行关键点分析。
  * en-US：Keypoints not available. Run keypoint analysis in “Analyze”.
* `MA.Step3.Timeline.Title`

  * zh-CN：步态事件
  * en-US：Gait Events
* `MA.Step3.Timeline.NoEvents`

  * zh-CN：暂无事件数据（完成事件分析后显示）。
  * en-US：No event data (available after event analysis).
* `MA.Step3.GoAnalyze`

  * zh-CN：进入分析
  * en-US：Go to Analyze
* `MA.Step3.GoAnalyze.DisabledReason`

  * zh-CN：需要同时具备正面与侧面视频才可分析。
  * en-US：Both front and side videos are required for analysis.

### 9.2.6 Step4（分析结果）

* `MA.Step4.Title`

  * zh-CN：分析结果
  * en-US：Analyze
* `MA.Step4.Stage.Title`

  * zh-CN：分析阶段
  * en-US：Analysis Stage
* `MA.Step4.Stage.Keypoints`

  * zh-CN：关键点识别
  * en-US：Keypoint Detection
* `MA.Step4.Stage.Events`

  * zh-CN：步态事件检测
  * en-US：Event Detection
* `MA.Step4.Stage.Kinematics`

  * zh-CN：运动学参数计算
  * en-US：Kinematics
* `MA.Step4.Options.Title`

  * zh-CN：参数设置
  * en-US：Options
* `MA.Step4.Run.Start`

  * zh-CN：开始分析
  * en-US：Start
* `MA.Step4.Run.Cancel`

  * zh-CN：取消分析
  * en-US：Cancel
* `MA.Step4.Run.Rerun`

  * zh-CN：重跑
  * en-US：Rerun
* `MA.Step4.Run.OpenOutput`

  * zh-CN：打开输出目录
  * en-US：Open Output Folder
* `MA.Step4.Summary.Title`

  * zh-CN：结果摘要
  * en-US：Summary
* `MA.Step4.Link.ReviewOverlay`

  * zh-CN：查看回放叠加
  * en-US：View Overlay
* `MA.Step4.Link.GenerateReport`

  * zh-CN：生成报告
  * en-US：Generate Report
* `MA.Step4.Validation.NeedDualVideo`

  * zh-CN：请先导入正面与侧面视频后再进行分析。
  * en-US：Please import both front and side videos before analysis.

### 9.2.7 InfoPanel（右侧信息栏）

* `MA.Info.Patient.Title`

  * zh-CN：当前患者
  * en-US：Current Patient
* `MA.Info.Measurement.Title`

  * zh-CN：本次测量
  * en-US：Current Measurement
* `MA.Info.Video.Title`

  * zh-CN：视频状态
  * en-US：Video Status
* `MA.Info.Analysis.Title`

  * zh-CN：分析阶段
  * en-US：Analysis Status
* `MA.Info.Quality.Title`

  * zh-CN：质量提示
  * en-US：Quality
* `MA.Info.SwitchPatient`

  * zh-CN：切换患者
  * en-US：Switch Patient
* `MA.Info.Video.FrontReady`

  * zh-CN：正面：已选择
  * en-US：Front: Selected
* `MA.Info.Video.FrontMissing`

  * zh-CN：正面：未选择
  * en-US：Front: Not Selected
* `MA.Info.Video.SideReady`

  * zh-CN：侧面：已选择
  * en-US：Side: Selected
* `MA.Info.Video.SideMissing`

  * zh-CN：侧面：未选择
  * en-US：Side: Not Selected
* `MA.Info.Video.AnalysisUnavailable`

  * zh-CN：仅单视角视频，分析不可用。
  * en-US：Single view only. Analysis unavailable.
* `MA.Info.Quality.Placeholder`

  * zh-CN：完成关键点分析后显示质量信息。
  * en-US：Quality info will be available after keypoint analysis.

### 9.2.8 TaskDrawer（任务抽屉）

* `MA.Task.Title`

  * zh-CN：任务中心
  * en-US：Tasks
* `MA.Task.CurrentTask`

  * zh-CN：当前任务
  * en-US：Current Task
* `MA.Task.Progress`

  * zh-CN：进度
  * en-US：Progress
* `MA.Task.Elapsed`

  * zh-CN：耗时
  * en-US：Elapsed
* `MA.Task.Log`

  * zh-CN：日志
  * en-US：Log
* `MA.Task.NoTask`

  * zh-CN：暂无任务。
  * en-US：No task.

### 9.2.9 Dialog（弹窗）

* `MA.Dialog.VideoIncomplete.Title`

  * zh-CN：视频不完整
  * en-US：Incomplete Video
* `MA.Dialog.VideoIncomplete.Content`

  * zh-CN：仅导入一个视角视频，可进入回放检查，但无法进行分析。是否继续？
  * en-US：Only one view video is imported. You can review, but analysis is unavailable. Continue?
* `MA.Dialog.VideoIncomplete.Continue`

  * zh-CN：继续
  * en-US：Continue
* `MA.Dialog.VideoIncomplete.BackImport`

  * zh-CN：返回导入
  * en-US：Back to Import
* `MA.Dialog.OverrideMeasurement.Title`

  * zh-CN：创建新测量
  * en-US：Create New Measurement
* `MA.Dialog.OverrideMeasurement.Content`

  * zh-CN：当前测量尚未完成。创建新测量将覆盖当前内容，是否继续？
  * en-US：Current measurement is not finished. Creating a new one will overwrite it. Continue?
* `MA.Dialog.OverrideMeasurement.Confirm`

  * zh-CN：继续
  * en-US：Continue
* `MA.Dialog.OverrideMeasurement.Cancel`

  * zh-CN：取消
  * en-US：Cancel

### 9.2.10 Snackbar（轻提示）

* `MA.Snackbar.ImportSuccess`

  * zh-CN：导入成功。
  * en-US：Import successful.
* `MA.Snackbar.AnalysisStarted`

  * zh-CN：分析已开始。
  * en-US：Analysis started.
* `MA.Snackbar.AnalysisCompleted`

  * zh-CN：分析完成。
  * en-US：Analysis completed.
* `MA.Snackbar.AnalysisCanceled`

  * zh-CN：分析已取消。
  * en-US：Analysis canceled.
* `MA.Snackbar.NoKeypoints`

  * zh-CN：尚未生成关键点。
  * en-US：Keypoints not available.

---

## 10. 资源字典文件建议结构（示例）

* `Resources/StringResources.zh-CN.xaml`
* `Resources/StringResources.en-US.xaml`

键一致，仅 Value 不同。语言切换时替换合并字典（MergedDictionaries）。

---

## 11. UI验收标准（含多语言）

1. zh-CN/en-US 切换后，测量评估页面所有文字随资源字典更新
2. StepBar/按钮/弹窗/Snackbar文字均使用资源键，不出现硬编码字符串
3. 四步交互规则正确（第4章）
4. MaterialDesign 样式统一（Card、DialogHost、DrawerHost、Snackbar、PackIcon）
5. Step4 模拟分析任务可触发 TaskDrawer 展开与进度展示

---

## 12. 可选附件（如需直接分工给开发）

* 附件A：控件与绑定字段对照表（逐控件：Name、Binding、ResourceKey）
* 附件B：状态真值表（Step与按钮使能矩阵）