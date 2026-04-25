# 步态分析详情与报告界面 Copilot 实现说明

## 1. 文档目的

本文档用于指导 WPF 上位机软件实现“分析详情界面”和“报告界面”。

软件在每次步态测量和算法分析结束后，应进入分析详情界面。分析详情界面用于完整展示本次分析得到的全部结果，包括步态参数、运动学参数、曲线、视频、文件、质量控制信息等。报告界面用于从分析结果中选择部分内容，生成适合查看、打印和归档的步态分析报告。

本文档内容可作为 Copilot 生成界面、ViewModel、数据模型和交互逻辑时的参考。

## 2. 页面整体关系

系统中与本功能相关的页面包括：

1. 分析详情界面
2. 报告配置界面
3. 报告预览界面
4. 历史报告查看界面，可后续扩展

页面跳转关系如下：

```text
测量完成
  ↓
算法分析完成
  ↓
分析详情界面
  ↓
点击“生成报告”
  ↓
报告配置界面
  ↓
点击“预览报告”
  ↓
报告预览界面
  ↓
导出 PDF / Word / 打印
```

## 3. 分析详情界面定位

分析详情界面用于展示一次测量任务的完整分析结果。

该界面内容应完整，便于医生查看、工程人员复核、算法人员调试和用户导出数据。

分析详情界面不只展示报告内容，还应展示所有算法输出数据、文件路径、质量控制结果、视频回放和曲线数据。

## 4. 报告界面定位

报告界面用于从分析结果中提取核心内容，形成正式报告。

报告内容应简洁，重点展示受试者信息、测量信息、核心步态时空参数、主要运动学参数、关键曲线、质量评价和分析结论。

报告内容应支持勾选。用户可选择是否将部分图表、详细参数、关键帧截图、质量控制信息加入报告。

## 5. 算法分析模块可输出数据

算法模块完成一次分析后，可输出以下几类数据。

### 5.1 任务执行数据

用于判断本次分析是否完成，以及结果是否可用。

字段包括：

| 字段 | 说明 | 示例 |
|---|---|---|
| TaskId | 分析任务编号 | GAIT_20260424_001 |
| RequestId | 请求编号 | REQ_001 |
| ProtocolVersion | 通讯协议版本 | v1.0 |
| AlgorithmVersion | 算法版本 | v1.0.0 |
| ModelVersion | 模型版本 | pose_v1.2 |
| TaskStatus | 任务状态 | Completed |
| Success | 是否成功 | true |
| ErrorCode | 错误码 | 空或 E001 |
| ErrorMessage | 错误说明 | 视频文件不存在 |
| StartTime | 分析开始时间 | 2026-04-24 10:30:00 |
| EndTime | 分析结束时间 | 2026-04-24 10:31:20 |
| GenerateTime | 结果生成时间 | 2026-04-24 10:31:25 |
| OutputDirectory | 本次结果目录 | D:\GaitResults\GAIT_001 |

### 5.2 受试者信息

由上位机传入，分析结果中应保留一份，用于详情展示和报告生成。

字段包括：

| 字段 | 说明 |
|---|---|
| PatientId | 受试者编号 |
| Name | 姓名 |
| Gender | 性别 |
| Age | 年龄 |
| Height | 身高，单位 cm |
| Weight | 体重，单位 kg |
| TestDate | 测量日期 |
| Remark | 备注 |

### 5.3 视频与采集数据

用于展示采集条件、视频来源和分析范围。

字段包括：

| 字段 | 说明 |
|---|---|
| SideVideoPath | 侧向视频路径 |
| FrontVideoPath | 正向视频路径 |
| SideAnnotatedVideoPath | 侧向标记视频路径 |
| FrontAnnotatedVideoPath | 正向标记视频路径 |
| FrameRate | 视频帧率 |
| Resolution | 视频分辨率 |
| Duration | 视频时长 |
| AnalysisStartTime | 分析起始时间 |
| AnalysisDuration | 分析时长 |
| SideCameraDistance | 侧向相机与步道距离 |
| CameraInfo | 相机编号、相机类型、采集参数 |

### 5.4 步态事件数据

用于展示脚跟触地、脚趾离地和步态周期分割结果。

字段包括：

| 字段 | 说明 |
|---|---|
| EventId | 事件编号 |
| Side | 左侧或右侧 |
| EventType | 事件类型，HeelStrike 或 ToeOff |
| FrameIndex | 所在帧号 |
| Time | 事件时间，单位 s |
| Confidence | 事件置信度 |
| RelatedCycleId | 所属步态周期编号 |

事件类型建议在界面上显示为中文：

| 内部值 | 界面显示 |
|---|---|
| HeelStrike | 脚跟触地 / 初始触地 |
| ToeOff | 脚趾离地 |

### 5.5 步态周期数据

用于展示每个有效步态周期的起止时间和分期结果。

字段包括：

| 字段 | 说明 |
|---|---|
| CycleId | 周期编号 |
| Side | 左侧或右侧 |
| StartEventTime | 起始事件时间 |
| EndEventTime | 结束事件时间 |
| Duration | 周期时长，单位 s |
| StanceTime | 站立相时间，单位 s |
| SwingTime | 摆动相时间，单位 s |
| DoubleSupportTime | 双支撑相时间，单位 s |
| SingleSupportTime | 单支撑相时间，单位 s |
| IsValid | 是否有效周期 |
| InvalidReason | 无效原因 |

### 5.6 步态时空参数

步态时空参数是分析详情和报告中的核心数据。

字段包括：

| 参数 | 字段名 | 单位 | 说明 |
|---|---|---|---|
| 步态周期时长 | GaitCycleDuration | s | 同侧足连续两次触地之间的时间 |
| 步长 | StepLength | m | 左右足相邻落地点之间的距离 |
| 步幅 | StrideLength | m | 同侧足连续两次落地点之间的距离 |
| 步频 | Cadence | step/min | 每分钟步数 |
| 步速 | GaitSpeed | m/s | 行走速度 |
| 站立相时间 | StanceTime | s 或 %GC | 足部接触地面的时间 |
| 摆动相时间 | SwingTime | s 或 %GC | 足部离地摆动的时间 |
| 双支撑相时间 | DoubleSupportTime | s 或 %GC | 双足同时支撑的时间 |
| 单支撑相时间 | SingleSupportTime | s 或 %GC | 单足支撑的时间 |

每个参数应尽量支持左侧值、右侧值、平均值和单位。

推荐数据结构：

```csharp
public class GaitParameterItem
{
    public string Name { get; set; }
    public string Code { get; set; }
    public double? LeftValue { get; set; }
    public double? RightValue { get; set; }
    public double? AverageValue { get; set; }
    public string Unit { get; set; }
    public bool IncludeInReport { get; set; }
}
```

### 5.7 运动学汇总参数

运动学汇总参数用于展示主要关节和节段的角度活动范围、最大值、最小值等。

字段包括：

| 类别 | 参数 | 单位 |
|---|---|---|
| 髋关节 | 最大屈曲角 | ° |
| 髋关节 | 最大伸展角 | ° |
| 髋关节 | ROM | ° |
| 膝关节 | 最大屈曲角 | ° |
| 膝关节 | 最大伸展角 | ° |
| 膝关节 | ROM | ° |
| 踝关节 | 最大背屈角 | ° |
| 踝关节 | 最大跖屈角 | ° |
| 踝关节 | ROM | ° |
| 骨盆 | 冠状面最大左倾 | ° |
| 骨盆 | 冠状面最大右倾 | ° |
| 骨盆 | 冠状面 ROM | ° |
| 躯干 | 冠状面侧屈范围 | ° |

推荐数据结构：

```csharp
public class KinematicSummaryItem
{
    public string Category { get; set; }
    public string Name { get; set; }
    public double? LeftValue { get; set; }
    public double? RightValue { get; set; }
    public double? AverageValue { get; set; }
    public string Unit { get; set; }
    public bool IncludeInReport { get; set; }
}
```

### 5.8 运动学时间序列数据

运动学时间序列数据通常不直接放入 JSON 汇总结果中，可保存为 CSV 文件，并在 JSON 中返回文件路径。

可输出文件包括：

| 文件 | 内容 |
|---|---|
| joint_angles.csv | 髋、膝、踝、骨盆角度时间序列 |
| keypoints.csv | 人体关键点坐标时间序列 |
| keypoint_velocity.csv | 关键点速度时间序列 |
| angular_velocity.csv | 关节角速度时间序列 |

关节角度 CSV 表头示例：

```csv
frame_index,time,left_hip_angle,right_hip_angle,left_knee_angle,right_knee_angle,left_ankle_angle,right_ankle_angle,pelvis_angle
0,0.000,12.3,11.8,5.6,6.1,3.2,3.5,1.2
1,0.033,12.5,12.0,5.9,6.3,3.3,3.6,1.3
```

关键点轨迹 CSV 表头示例：

```csv
frame_index,time,left_hip_x,left_hip_y,left_knee_x,left_knee_y,left_ankle_x,left_ankle_y,right_hip_x,right_hip_y,right_knee_x,right_knee_y,right_ankle_x,right_ankle_y
```

关键点速度 CSV 表头示例：

```csv
frame_index,time,left_ankle_vx,left_ankle_vy,left_ankle_speed,right_ankle_vx,right_ankle_vy,right_ankle_speed
```

关节角速度 CSV 表头示例：

```csv
frame_index,time,left_hip_angular_velocity,right_hip_angular_velocity,left_knee_angular_velocity,right_knee_angular_velocity,left_ankle_angular_velocity,right_ankle_angular_velocity
```

### 5.9 曲线与图表数据

用于在界面中绘制曲线。

曲线类型包括：

| 曲线 | 数据来源 |
|---|---|
| 髋关节角度时间曲线 | joint_angles.csv |
| 膝关节角度时间曲线 | joint_angles.csv |
| 踝关节角度时间曲线 | joint_angles.csv |
| 骨盆冠状面角度曲线 | joint_angles.csv |
| 关键点轨迹曲线 | keypoints.csv |
| 关键点速度曲线 | keypoint_velocity.csv |
| 关节角速度曲线 | angular_velocity.csv |
| 步态事件时间轴 | events 数据 |
| 步态周期分割图 | cycles 数据 |

### 5.10 视频输出数据

视频输出用于界面回放和复核。

输出内容包括：

| 文件 | 说明 |
|---|---|
| 原始侧向视频 | 采集得到的侧向视频 |
| 原始正向视频 | 采集得到的正向视频 |
| 侧向关键点标记视频 | 叠加骨架、关键点、事件标记的视频 |
| 正向关键点标记视频 | 叠加骨架、关键点、事件标记的视频 |
| 关键帧截图 | 可用于报告展示 |

### 5.11 质量控制数据

质量控制数据用于判断结果是否可靠。

字段包括：

| 字段 | 说明 |
|---|---|
| EffectiveFrameRatio | 有效帧比例 |
| AverageKeypointConfidence | 关键点平均置信度 |
| LowConfidenceFrameCount | 低置信度帧数 |
| LostKeypointFrameCount | 关键点丢失帧数 |
| InterpolatedFrameCount | 插值修正帧数 |
| ValidCycleCount | 有效步态周期数 |
| InvalidCycleCount | 无效步态周期数 |
| EventDetectionQuality | 事件检测质量 |
| CurveSmoothness | 曲线平滑性 |
| OcclusionWarning | 遮挡提示 |
| QualityLevel | 质量等级 |
| QualityMessage | 质量说明 |

质量等级建议使用以下枚举：

```csharp
public enum AnalysisQualityLevel
{
    Good,
    ReviewRequired,
    Unavailable
}
```

界面显示：

| 内部值 | 显示文本 |
|---|---|
| Good | 良好 |
| ReviewRequired | 需复核 |
| Unavailable | 不可用 |

### 5.12 文件输出数据

每次分析应生成单独结果目录。

目录中可包含：

```text
GAIT_20260424_001/
  result.json
  joint_angles.csv
  keypoints.csv
  keypoint_velocity.csv
  angular_velocity.csv
  annotated_side.mp4
  annotated_front.mp4
  side_keyframe.png
  front_keyframe.png
  analysis.log
  report.pdf
  report.docx
```

## 6. 分析详情界面布局

### 6.1 页面顶部区域

顶部显示本次测量的核心信息和主要操作按钮。

显示内容：

| 内容 | 控件形式 |
|---|---|
| 受试者姓名 | TextBlock |
| 受试者编号 | TextBlock |
| 测量时间 | TextBlock |
| 分析状态 | 状态标签 |
| 质量评价 | 状态标签 |
| 平均步速 | 指标卡片 |
| 平均步频 | 指标卡片 |
| 平均步长 | 指标卡片 |
| 有效周期数 | 指标卡片 |

操作按钮：

| 按钮 | 功能 |
|---|---|
| 生成报告 | 进入报告配置界面 |
| 导出数据 | 导出本次分析目录或 CSV 数据 |
| 打开结果目录 | 打开本地输出目录 |
| 返回列表 | 返回历史记录或测量列表 |

### 6.2 左侧导航

左侧采用垂直导航菜单。

导航项包括：

1. 结果概览
2. 时空参数
3. 周期与事件
4. 运动学参数
5. 曲线图
6. 视频回放
7. 质量控制
8. 文件管理
9. 报告生成

### 6.3 右侧内容区域

右侧根据左侧导航切换内容。

可以使用 UserControl 拆分每个模块，降低单个 XAML 文件复杂度。

推荐控件命名：

| 模块 | UserControl 名称 |
|---|---|
| 结果概览 | GaitResultOverviewView |
| 时空参数 | GaitSpatiotemporalView |
| 周期与事件 | GaitCycleEventView |
| 运动学参数 | GaitKinematicView |
| 曲线图 | GaitChartView |
| 视频回放 | GaitVideoReviewView |
| 质量控制 | GaitQualityControlView |
| 文件管理 | GaitResultFileView |
| 报告生成 | GaitReportConfigView |

## 7. 结果概览模块

结果概览模块显示本次分析的基本信息和核心摘要。

### 7.1 任务信息

显示字段：

| 字段 | 显示名称 |
|---|---|
| TaskId | 任务编号 |
| TaskStatus | 分析状态 |
| Success | 是否成功 |
| GenerateTime | 生成时间 |
| AlgorithmVersion | 算法版本 |
| ModelVersion | 模型版本 |
| ProtocolVersion | 协议版本 |

### 7.2 受试者信息

显示字段：

| 字段 | 显示名称 |
|---|---|
| PatientId | 受试者编号 |
| Name | 姓名 |
| Gender | 性别 |
| Age | 年龄 |
| Height | 身高 |
| Weight | 体重 |
| TestDate | 测量日期 |

### 7.3 采集信息

显示字段：

| 字段 | 显示名称 |
|---|---|
| SideVideoPath | 侧向视频 |
| FrontVideoPath | 正向视频 |
| FrameRate | 视频帧率 |
| Resolution | 视频分辨率 |
| AnalysisDuration | 分析时长 |
| SideCameraDistance | 侧向相机距离 |

### 7.4 结果摘要

显示字段：

| 字段 | 显示名称 |
|---|---|
| AverageGaitSpeed | 平均步速 |
| AverageCadence | 平均步频 |
| AverageStepLength | 平均步长 |
| AverageStrideLength | 平均步幅 |
| ValidCycleCount | 有效周期数 |
| QualityLevel | 质量评价 |

## 8. 时空参数模块

该模块使用表格展示步态时空参数。

表格列：

| 列名 | 说明 |
|---|---|
| 参数名称 | 中文名称 |
| 左侧 | 左侧结果 |
| 右侧 | 右侧结果 |
| 平均值 | 平均结果 |
| 单位 | 单位 |
| 是否进入报告 | CheckBox |

默认参数：

1. 步态周期时长
2. 步长
3. 步幅
4. 步频
5. 步速
6. 站立相时间
7. 摆动相时间
8. 双支撑相时间
9. 单支撑相时间

## 9. 周期与事件模块

该模块包含两个区域。

### 9.1 步态事件列表

表格列：

| 列名 | 说明 |
|---|---|
| 事件编号 | EventId |
| 侧别 | 左侧或右侧 |
| 事件类型 | 脚跟触地或脚趾离地 |
| 帧号 | FrameIndex |
| 时间 | Time |
| 置信度 | Confidence |
| 所属周期 | RelatedCycleId |

### 9.2 步态周期列表

表格列：

| 列名 | 说明 |
|---|---|
| 周期编号 | CycleId |
| 侧别 | 左侧或右侧 |
| 起始时间 | StartEventTime |
| 结束时间 | EndEventTime |
| 周期时长 | Duration |
| 站立相 | StanceTime |
| 摆动相 | SwingTime |
| 双支撑相 | DoubleSupportTime |
| 单支撑相 | SingleSupportTime |
| 是否有效 | IsValid |
| 备注 | InvalidReason |

## 10. 运动学参数模块

运动学参数模块使用表格展示主要关节和节段参数。

表格列：

| 列名 | 说明 |
|---|---|
| 类别 | 髋关节、膝关节、踝关节、骨盆、躯干 |
| 参数名称 | ROM、最大角、最小角等 |
| 左侧 | 左侧值 |
| 右侧 | 右侧值 |
| 平均值 | 平均值 |
| 单位 | ° |
| 是否进入报告 | CheckBox |

默认显示：

1. 髋关节矢状面 ROM
2. 膝关节矢状面 ROM
3. 踝关节矢状面 ROM
4. 骨盆冠状面 ROM
5. 躯干冠状面侧屈范围

## 11. 曲线图模块

曲线图模块用于绘制主要运动学曲线和步态事件图。

### 11.1 曲线列表

可展示曲线：

1. 髋关节角度时间曲线
2. 膝关节角度时间曲线
3. 踝关节角度时间曲线
4. 骨盆冠状面角度曲线
5. 关键点轨迹曲线
6. 关键点速度曲线
7. 关节角速度曲线
8. 步态事件时间轴
9. 步态周期分割图

### 11.2 曲线卡片操作

每张曲线卡片包含：

| 操作 | 说明 |
|---|---|
| 加入报告 | 将该图加入报告 |
| 导出图片 | 导出当前曲线为图片 |
| 查看数据 | 打开对应 CSV 数据 |
| 放大查看 | 弹窗显示大图 |

## 12. 视频回放模块

视频回放模块用于回放原始视频和标记视频。

### 12.1 显示内容

| 内容 | 说明 |
|---|---|
| 侧向视频 | 显示侧向视角视频 |
| 正向视频 | 显示正向视角视频 |
| 关键点骨架 | 叠加显示人体关键点和骨架 |
| 事件标记 | 显示脚跟触地、脚趾离地事件 |
| 当前帧号 | 显示当前播放帧 |
| 当前时间 | 显示当前播放时间 |
| 当前周期 | 显示当前所属步态周期 |

### 12.2 操作按钮

| 按钮 | 功能 |
|---|---|
| 播放 | 播放视频 |
| 暂停 | 暂停视频 |
| 上一帧 | 回退一帧 |
| 下一帧 | 前进一帧 |
| 倍速 | 0.5x、1x、2x |
| 跳转事件 | 跳转到指定 HS 或 TO 事件 |
| 截图 | 导出当前帧截图 |
| 加入报告 | 将当前截图加入报告 |

## 13. 质量控制模块

质量控制模块用于展示本次分析质量。

显示字段：

| 字段 | 显示名称 |
|---|---|
| EffectiveFrameRatio | 有效帧比例 |
| AverageKeypointConfidence | 关键点平均置信度 |
| LowConfidenceFrameCount | 低置信度帧数 |
| LostKeypointFrameCount | 关键点丢失帧数 |
| InterpolatedFrameCount | 插值修正帧数 |
| ValidCycleCount | 有效周期数 |
| InvalidCycleCount | 无效周期数 |
| EventDetectionQuality | 事件检测质量 |
| CurveSmoothness | 曲线平滑性 |
| OcclusionWarning | 遮挡提示 |
| QualityLevel | 质量等级 |
| QualityMessage | 质量说明 |

质量等级显示规则：

| 等级 | 显示文本 | 界面处理 |
|---|---|---|
| Good | 良好 | 可以生成报告 |
| ReviewRequired | 需复核 | 生成报告前提示复核 |
| Unavailable | 不可用 | 禁止生成正式报告或提示重新采集 |

## 14. 文件管理模块

文件管理模块显示本次分析生成的全部文件。

表格列：

| 列名 | 说明 |
|---|---|
| 文件类型 | JSON、CSV、视频、日志、报告 |
| 文件名称 | 文件名 |
| 文件说明 | 文件用途 |
| 文件路径 | 本地路径 |
| 操作 | 打开、导出、复制路径 |

文件类型包括：

1. result.json
2. joint_angles.csv
3. keypoints.csv
4. keypoint_velocity.csv
5. angular_velocity.csv
6. annotated_side.mp4
7. annotated_front.mp4
8. analysis.log
9. report.pdf
10. report.docx

## 15. 报告配置界面

报告配置界面用于选择报告内容。

### 15.1 报告基本信息

字段包括：

| 字段 | 控件 |
|---|---|
| 报告编号 | TextBox |
| 报告名称 | TextBox |
| 检查人员 | TextBox |
| 审核人员 | TextBox |
| 报告日期 | DatePicker |
| 备注 | TextBox |
| 报告格式 | ComboBox，PDF、Word |

### 15.2 默认进入报告的内容

默认勾选：

1. 受试者基本信息
2. 测量信息
3. 有效步态周期数
4. 步态周期时长
5. 步长
6. 步幅
7. 步频
8. 步速
9. 站立相时间
10. 摆动相时间
11. 双支撑相时间
12. 单支撑相时间
13. 髋关节 ROM
14. 膝关节 ROM
15. 踝关节 ROM
16. 骨盆冠状面 ROM
17. 髋关节角度曲线
18. 膝关节角度曲线
19. 踝关节角度曲线
20. 质量评价
21. 分析结论

### 15.3 可选进入报告的内容

可选勾选：

1. 左右侧详细周期参数表
2. 步态事件时间轴
3. 骨盆角度曲线
4. 躯干侧屈曲线
5. 关键点轨迹图
6. 关键点速度曲线
7. 关节角速度曲线
8. 侧向视频关键帧
9. 正向视频关键帧
10. 算法版本
11. 模型版本
12. 协议版本
13. 详细质量控制信息
14. 文件清单

### 15.4 报告配置数据结构

```csharp
public class GaitReportOptions
{
    public bool IncludePatientInfo { get; set; } = true;
    public bool IncludeMeasurementInfo { get; set; } = true;
    public bool IncludeSpatiotemporalParameters { get; set; } = true;
    public bool IncludeKinematicSummary { get; set; } = true;
    public bool IncludeHipCurve { get; set; } = true;
    public bool IncludeKneeCurve { get; set; } = true;
    public bool IncludeAnkleCurve { get; set; } = true;
    public bool IncludePelvisCurve { get; set; }
    public bool IncludeEventTimeline { get; set; }
    public bool IncludeKeypointTrajectory { get; set; }
    public bool IncludeVelocityCurve { get; set; }
    public bool IncludeAngularVelocityCurve { get; set; }
    public bool IncludeSideKeyFrame { get; set; }
    public bool IncludeFrontKeyFrame { get; set; }
    public bool IncludeQualityControl { get; set; } = true;
    public bool IncludeDetailedQualityControl { get; set; }
    public bool IncludeConclusion { get; set; } = true;
    public bool IncludeFileList { get; set; }
}
```

## 16. 报告预览界面

报告预览界面按最终报告样式展示内容。

报告结构如下：

1. 步态分析报告标题
2. 基本信息
3. 测量信息
4. 步态时空参数
5. 运动学参数
6. 主要曲线
7. 质量评价
8. 分析结论
9. 签名与备注

## 17. 报告正文结构

### 17.1 标题

```text
步态分析报告
```

### 17.2 基本信息

字段：

| 字段 | 显示名称 |
|---|---|
| Name | 姓名 |
| PatientId | 编号 |
| Gender | 性别 |
| Age | 年龄 |
| Height | 身高 |
| Weight | 体重 |
| TestDate | 测量日期 |
| ReportNo | 报告编号 |

### 17.3 测量信息

字段：

| 字段 | 显示名称 |
|---|---|
| AcquisitionMode | 采集方式 |
| CameraViews | 采集视角 |
| FrameRate | 视频帧率 |
| Resolution | 视频分辨率 |
| AnalysisDuration | 分析时长 |
| ValidCycleCount | 有效周期数 |
| QualityLevel | 质量评价 |

### 17.4 步态时空参数

使用表格展示。

列：

| 参数 | 左侧 | 右侧 | 平均值 | 单位 |
|---|---|---|---|---|

### 17.5 运动学参数

使用表格展示。

列：

| 关节或节段 | 左侧 | 右侧 | 平均值 | 单位 |
|---|---|---|---|---|

### 17.6 主要曲线

默认显示：

1. 髋关节角度曲线
2. 膝关节角度曲线
3. 踝关节角度曲线

可选显示：

1. 骨盆角度曲线
2. 步态事件时间轴
3. 关键点轨迹图
4. 关键帧截图

### 17.7 质量评价

显示内容：

| 字段 | 显示名称 |
|---|---|
| EffectiveFrameRatio | 有效帧比例 |
| AverageKeypointConfidence | 关键点平均置信度 |
| ValidCycleCount | 有效周期数 |
| QualityMessage | 质量说明 |

### 17.8 分析结论

结论应使用描述性文字。

示例模板：

```text
本次步态分析共识别有效步态周期 {ValidCycleCount} 个。受试者平均步速为 {AverageGaitSpeed} m/s，平均步频为 {AverageCadence} step/min，平均步长为 {AverageStepLength} m。髋、膝、踝关节角度曲线已完成计算，主要关节活动范围见运动学参数表。本报告结果用于步态功能评估和康复训练参考。
```

当质量等级为 ReviewRequired 时：

```text
本次分析存在部分关键点短时丢失或遮挡情况，系统已进行修正处理。建议结合标记视频和曲线结果进行复核。
```

当质量等级为 Unavailable 时：

```text
本次分析结果质量不足，建议重新采集后再生成正式报告。
```

## 18. ViewModel 设计

### 18.1 主 ViewModel

```csharp
public class GaitAnalysisDetailViewModel
{
    public GaitAnalysisResult Result { get; set; }
    public ObservableCollection<GaitParameterItem> SpatiotemporalParameters { get; set; }
    public ObservableCollection<KinematicSummaryItem> KinematicSummaryItems { get; set; }
    public ObservableCollection<GaitEventItem> Events { get; set; }
    public ObservableCollection<GaitCycleItem> Cycles { get; set; }
    public ObservableCollection<ResultFileItem> ResultFiles { get; set; }
    public GaitReportOptions ReportOptions { get; set; }

    public ICommand GenerateReportCommand { get; set; }
    public ICommand ExportDataCommand { get; set; }
    public ICommand OpenResultDirectoryCommand { get; set; }
    public ICommand PreviewReportCommand { get; set; }
}
```

### 18.2 分析结果主模型

```csharp
public class GaitAnalysisResult
{
    public TaskInfo TaskInfo { get; set; }
    public PatientInfo PatientInfo { get; set; }
    public AcquisitionInfo AcquisitionInfo { get; set; }
    public GaitSummary Summary { get; set; }
    public List<GaitParameterItem> SpatiotemporalParameters { get; set; }
    public List<KinematicSummaryItem> KinematicSummary { get; set; }
    public List<GaitEventItem> Events { get; set; }
    public List<GaitCycleItem> Cycles { get; set; }
    public QualityControlInfo QualityControl { get; set; }
    public OutputFileInfo OutputFiles { get; set; }
}
```

## 19. 按钮交互逻辑

### 19.1 生成报告

流程：

```text
点击生成报告
  ↓
检查分析状态是否 Completed
  ↓
检查质量等级是否 Unavailable
  ↓
如果不可用，提示重新采集或复核
  ↓
进入报告配置界面
```

### 19.2 预览报告

流程：

```text
点击预览报告
  ↓
读取报告配置项
  ↓
从分析结果中提取对应数据
  ↓
生成预览页面
```

### 19.3 导出报告

流程：

```text
点击导出 PDF 或 Word
  ↓
生成报告文件
  ↓
保存到本次分析结果目录
  ↓
更新 ResultFiles 列表
  ↓
提示导出成功
```

### 19.4 导出数据

流程：

```text
点击导出数据
  ↓
选择导出目录
  ↓
复制 result.json、CSV、视频、日志文件
  ↓
提示导出成功
```

## 20. 界面实现注意事项

1. 分析详情界面以完整展示为主，不压缩数据内容。
2. 报告界面以简洁输出为主，默认只展示核心结果。
3. 曲线、关键帧、详细质量控制信息均支持勾选进入报告。
4. 所有参数显示时应带单位。
5. 左右侧数据应分列显示。
6. 平均值应单独显示。
7. 视频、CSV、JSON 和日志文件应能从文件管理模块打开。
8. 质量等级为不可用时，报告生成按钮应提示风险。
9. 分析结果和报告文件都应保存到同一次任务目录中。
10. 界面字段名和数据模型字段名应尽量保持一致，便于 JSON 反序列化和后期维护。

## 21. 页面命名建议

| 页面 | 类名 |
|---|---|
| 分析详情页 | GaitAnalysisDetailPage |
| 结果概览控件 | GaitResultOverviewView |
| 时空参数控件 | GaitSpatiotemporalView |
| 周期事件控件 | GaitCycleEventView |
| 运动学参数控件 | GaitKinematicView |
| 曲线图控件 | GaitChartView |
| 视频回放控件 | GaitVideoReviewView |
| 质量控制控件 | GaitQualityControlView |
| 文件管理控件 | GaitResultFileView |
| 报告配置页 | GaitReportConfigPage |
| 报告预览页 | GaitReportPreviewPage |

## 22. 最终实现目标

完成后，软件应支持以下能力：

1. 每次测量完成后进入分析详情界面。
2. 分析详情界面完整展示步态参数、运动学参数、事件、周期、曲线、视频、文件和质量控制信息。
3. 用户可查看标记视频和曲线结果。
4. 用户可打开或导出 CSV、JSON、视频和日志文件。
5. 用户可选择报告内容。
6. 用户可预览报告。
7. 用户可导出 PDF 或 Word 报告。
8. 报告文件保存到本次分析结果目录。
9. 历史记录中可重新打开分析详情和已生成报告。
