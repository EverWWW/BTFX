using BTFX.Common;

namespace BTFX.Models;

/// <summary>
/// 测量记录模型
/// </summary>
public class MeasurementRecord
{
    /// <summary>
    /// 记录ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 患者ID
    /// </summary>
    public int PatientId { get; set; }

    /// <summary>
    /// 患者信息
    /// </summary>
    public Patient? Patient { get; set; }

    /// <summary>
    /// 测量日期时间
    /// </summary>
    public DateTime MeasurementDate { get; set; } = DateTime.Now;

    /// <summary>
    /// 测量状态
    /// </summary>
    public MeasurementStatus Status { get; set; } = MeasurementStatus.Pending;

    /// <summary>
    /// 视频文件路径
    /// </summary>
    public string? VideoFilePath { get; set; }

    /// <summary>
    /// 步态参数ID
    /// </summary>
    public int? GaitParametersId { get; set; }

    /// <summary>
    /// 步态参数
    /// </summary>
    public GaitParameters? GaitParameters { get; set; }

    /// <summary>
    /// 测量持续时间 (秒)
    /// </summary>
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// 操作员ID
    /// </summary>
    public int OperatorId { get; set; }

    /// <summary>
    /// 操作员信息
    /// </summary>
    public User? Operator { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
