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
