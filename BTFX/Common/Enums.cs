using System.ComponentModel;

namespace BTFX.Common;

/// <summary>
/// 用户角色
/// </summary>
public enum UserRole
{
    /// <summary>
    /// 管理员 - 拥有系统最高权限
    /// </summary>
    [Description("管理员")]
    Administrator = 0,

    /// <summary>
    /// 操作员 - 拥有核心业务操作权限
    /// </summary>
    [Description("操作员")]
    Operator = 1,

    /// <summary>
    /// 游客 - 受限功能
    /// </summary>
    [Description("游客")]
    Guest = 2
}

/// <summary>
/// 性别
/// </summary>
public enum Gender
{
    /// <summary>
    /// 男
    /// </summary>
    [Description("男")]
    Male = 0,

    /// <summary>
    /// 女
    /// </summary>
    [Description("女")]
    Female = 1
}

/// <summary>
/// 患者状态（逻辑删除）
/// </summary>
public enum PatientStatus
{
    /// <summary>
    /// 正常
    /// </summary>
    [Description("正常")]
    Active = 0,

    /// <summary>
    /// 已删除
    /// </summary>
    [Description("已删除")]
    Deleted = 1
}

/// <summary>
/// 测量状态
/// </summary>
public enum MeasurementStatus
{
    /// <summary>
    /// 待测量
    /// </summary>
    [Description("待测量")]
    Pending = 0,

    /// <summary>
    /// 测量中
    /// </summary>
    [Description("测量中")]
    InProgress = 1,

    /// <summary>
    /// 已完成
    /// </summary>
    [Description("已完成")]
    Completed = 2,

    /// <summary>
    /// 已取消
    /// </summary>
    [Description("已取消")]
    Cancelled = 3,

    /// <summary>
    /// 测量失败
    /// </summary>
    [Description("测量失败")]
    Failed = 4
}

/// <summary>
/// 报告状态
/// </summary>
public enum ReportStatus
{
    /// <summary>
    /// 草稿
    /// </summary>
    [Description("草稿")]
    Draft = 0,

    /// <summary>
    /// 已完成
    /// </summary>
    [Description("已完成")]
    Completed = 1,

    /// <summary>
    /// 已打印
    /// </summary>
    [Description("已打印")]
    Printed = 2
}

/// <summary>
/// 设备连接状态
/// </summary>
public enum DeviceConnectionStatus
{
    /// <summary>
    /// 未连接
    /// </summary>
    [Description("未连接")]
    Disconnected = 0,

    /// <summary>
    /// 连接中
    /// </summary>
    [Description("连接中")]
    Connecting = 1,

    /// <summary>
    /// 已连接
    /// </summary>
    [Description("已连接")]
    Connected = 2,

    /// <summary>
    /// 连接失败
    /// </summary>
    [Description("连接失败")]
    Failed = 3
}

/// <summary>
/// 应用主题
/// </summary>
public enum AppTheme
{
    /// <summary>
    /// 浅色主题
    /// </summary>
    [Description("浅色")]
    Light = 0,

    /// <summary>
    /// 深色主题
    /// </summary>
    [Description("深色")]
    Dark = 1
}

/// <summary>
/// 应用语言
/// </summary>
public enum AppLanguage
{
    /// <summary>
    /// 简体中文
    /// </summary>
    [Description("简体中文")]
    ChineseSimplified = 0,

    /// <summary>
    /// 英文
    /// </summary>
    [Description("English")]
    English = 1
}

/// <summary>
/// 导出格式
/// </summary>
public enum ExportFormat
{
    /// <summary>
    /// Excel格式
    /// </summary>
    [Description("Excel")]
    Excel = 0,

    /// <summary>
    /// CSV格式
    /// </summary>
    [Description("CSV")]
    CSV = 1,

    /// <summary>
    /// PDF格式
    /// </summary>
    [Description("PDF")]
    PDF = 2
}

/// <summary>
/// 备份频率
/// </summary>
public enum BackupFrequency
{
    /// <summary>
    /// 每天
    /// </summary>
    [Description("每天")]
    Daily = 0,

    /// <summary>
    /// 每周
    /// </summary>
    [Description("每周")]
    Weekly = 1,

    /// <summary>
    /// 每月
    /// </summary>
    [Description("每月")]
    Monthly = 2
}

/// <summary>
/// 操作日志级别
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// 信息
    /// </summary>
    [Description("信息")]
    Info = 0,

    /// <summary>
    /// 警告
    /// </summary>
    [Description("警告")]
    Warning = 1,

    /// <summary>
    /// 错误
    /// </summary>
    [Description("错误")]
    Error = 2
}

/// <summary>
/// 用户状态
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// 启用
    /// </summary>
    [Description("启用")]
    Enabled = 0,

    /// <summary>
    /// 禁用
    /// </summary>
    [Description("禁用")]
    Disabled = 1
}

#region 测量评估模块枚举

/// <summary>
/// 测量类型
/// </summary>
public enum MeasurementType
{
    /// <summary>
    /// 自然步行
    /// </summary>
    [Description("自然步行")]
    NormalWalk = 0,

    /// <summary>
    /// 快走
    /// </summary>
    [Description("快走")]
    FastWalk = 1,

    /// <summary>
    /// 慢走
    /// </summary>
    [Description("慢走")]
    SlowWalk = 2,

    /// <summary>
    /// 其他
    /// </summary>
    [Description("其他")]
    Other = 3
}

/// <summary>
/// 分析阶段
/// </summary>
public enum AnalysisStage
{
    /// <summary>
    /// 未分析
    /// </summary>
    [Description("未分析")]
    None = 0,

    /// <summary>
    /// 关键点识别
    /// </summary>
    [Description("关键点")]
    Keypoints = 1,

    /// <summary>
    /// 步态事件检测
    /// </summary>
    [Description("步态事件")]
    Events = 2,

    /// <summary>
    /// 运动学参数
    /// </summary>
    [Description("运动学")]
    Kinematics = 3
}

/// <summary>
/// 视频规格
/// </summary>
public enum VideoSpec
{
    /// <summary>
    /// 1080P 30fps
    /// </summary>
    [Description("1080P / 30 FPS")]
    P1080_30fps = 0,

    /// <summary>
    /// 1440P 30fps
    /// </summary>
    [Description("1440P / 30 FPS")]
    P1440_30fps = 1
}

/// <summary>
/// 导入策略
/// </summary>
public enum ImportStrategy
{
    /// <summary>
    /// 复制到测量目录
    /// </summary>
    [Description("复制到测量目录")]
    CopyToFolder = 0,

    /// <summary>
    /// 仅引用原路径
    /// </summary>
    [Description("仅引用原路径")]
    ReferenceOnly = 1
}

/// <summary>
/// 分析任务状态
/// </summary>
public enum AnalysisTaskStatus
{
    /// <summary>
    /// 未运行
    /// </summary>
    [Description("未运行")]
    NotRun = 0,

    /// <summary>
    /// 运行中
    /// </summary>
    [Description("运行中")]
    Running = 1,

    /// <summary>
    /// 已完成
    /// </summary>
    [Description("已完成")]
    Completed = 2,

    /// <summary>
    /// 失败
    /// </summary>
    [Description("失败")]
    Failed = 3
}

/// <summary>
/// 视频导入模式
/// </summary>
public enum VideoImportMode
{
    /// <summary>
    /// 导入已有视频
    /// </summary>
    [Description("导入")]
    Import = 0,

    /// <summary>
    /// 实时采集
    /// </summary>
    [Description("采集")]
    Capture = 1
}

/// <summary>
/// 采集画面布局
/// </summary>
public enum CaptureLayout
{
    /// <summary>
    /// 单视频模式（仅正面或侧面）
    /// </summary>
    [Description("单视频")]
    Single = 0,

    /// <summary>
    /// 双视频模式（正面 + 侧面）
    /// </summary>
    [Description("双视频")]
    Dual = 1
}

/// <summary>
/// 录制时长选项
/// </summary>
public enum RecordDuration
{
    /// <summary>
    /// 30 秒
    /// </summary>
    [Description("30秒")]
    Seconds30 = 30,

    /// <summary>
    /// 1 分钟
    /// </summary>
    [Description("1分钟")]
    Seconds60 = 60
}

/// <summary>
/// 采集录制状态
/// </summary>
public enum CaptureState
{
    /// <summary>
    /// 待机 - 等待开始录制
    /// </summary>
    Idle = 0,

    /// <summary>
    /// 录制中
    /// </summary>
    Recording = 1,

    /// <summary>
    /// 录制完成
    /// </summary>
    Completed = 2
}

/// <summary>
/// 评估模块 UI 状态
/// </summary>
public enum AnalysisState
{
    /// <summary>
    /// 就绪，等待开始分析
    /// </summary>
    [Description("就绪")]
    Ready = 0,

    /// <summary>
    /// 分析进行中
    /// </summary>
    [Description("分析中")]
    Running = 1,

    /// <summary>
    /// 分析完成，视频预览中
    /// </summary>
    [Description("预览中")]
    Previewing = 2,

    /// <summary>
    /// 分析失败
    /// </summary>
    [Description("失败")]
    Failed = 3
}

/// <summary>
/// 算法退出错误码
/// </summary>
public enum AnalysisErrorCode
{
    /// <summary>
    /// 成功
    /// </summary>
    [Description("成功")]
    Success = 0,

    /// <summary>
    /// 配置文件错误
    /// </summary>
    [Description("配置文件错误")]
    ConfigError = 1,

    /// <summary>
    /// 输入文件不存在
    /// </summary>
    [Description("输入文件不存在")]
    InputFileNotFound = 2,

    /// <summary>
    /// 视频读取失败
    /// </summary>
    [Description("视频读取失败")]
    VideoReadFailed = 3,

    /// <summary>
    /// 分析处理失败
    /// </summary>
    [Description("分析处理失败")]
    AnalysisFailed = 4,

    /// <summary>
    /// 结果导出失败
    /// </summary>
    [Description("结果导出失败")]
    ExportFailed = 5,

    /// <summary>
    /// 未知错误
    /// </summary>
    [Description("未知错误")]
    Unknown = 9
}

/// <summary>
/// 视频播放速度
/// </summary>
public enum PlaybackSpeed
{
    /// <summary>
    /// 0.25 倍速
    /// </summary>
    [Description("0.25x")]
    Quarter = 0,

    /// <summary>
    /// 0.5 倍速
    /// </summary>
    [Description("0.5x")]
    Half = 1,

    /// <summary>
    /// 1.0 倍速（正常）
    /// </summary>
    [Description("1x")]
    Normal = 2,

    /// <summary>
    /// 1.5 倍速
    /// </summary>
    [Description("1.5x")]
    OneAndHalf = 3,

    /// <summary>
    /// 2.0 倍速
    /// </summary>
    [Description("2x")]
    Double = 4
}

/// <summary>
/// CSV 文件类型标识
/// </summary>
public enum CsvFileType
{
    /// <summary>
    /// 关节角度时间序列
    /// </summary>
    [Description("关节角度")]
    JointAngle,

    /// <summary>
    /// 关键点运动轨迹
    /// </summary>
    [Description("关键点轨迹")]
    KeypointTrajectory,

    /// <summary>
    /// 关键点速度
    /// </summary>
    [Description("关键点速度")]
    KeypointVelocity,

    /// <summary>
    /// 关节角速度
    /// </summary>
    [Description("关节角速度")]
    JointAngularVelocity
}

#endregion
