using BTFX.Common;
using SqlSugar;

namespace BTFX.Models;

/// <summary>
/// 测量记录模型
/// </summary>
[SugarTable("MeasurementRecords")]
public class MeasurementRecord
{
    /// <summary>
    /// 记录ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 患者ID
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public int PatientId { get; set; }

    /// <summary>
    /// 患者信息（导航属性）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public Patient? Patient { get; set; }

    /// <summary>
    /// 操作员ID（对应数据库 UserId 字段）
    /// </summary>
    [SugarColumn(ColumnName = "UserId", IsNullable = false)]
    public int OperatorId { get; set; }

    /// <summary>
    /// 操作员信息（导航属性）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public User? Operator { get; set; }

    /// <summary>
    /// 测量日期时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime MeasurementDate { get; set; } = DateTime.Now;

    /// <summary>
    /// 测量状态
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public MeasurementStatus Status { get; set; } = MeasurementStatus.Pending;

    /// <summary>
    /// 视频文件路径（对应数据库 VideoPath 字段）
    /// </summary>
    [SugarColumn(ColumnName = "VideoPath", Length = 500, IsNullable = true)]
    public string? VideoFilePath { get; set; }

    /// <summary>
    /// 测量持续时间 (秒)（对应数据库 Duration 字段）
    /// </summary>
    [SugarColumn(ColumnName = "Duration", IsNullable = true)]
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// 是否为游客数据
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool IsGuestData { get; set; } = false;

    /// <summary>
    /// 备注
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Remark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 步态参数ID（忽略，不存在于数据库）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public int? GaitParametersId { get; set; }

    /// <summary>
    /// 步态参数（导航属性）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public GaitParameters? GaitParameters { get; set; }
}
