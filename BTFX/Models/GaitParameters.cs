using SqlSugar;

namespace BTFX.Models;

/// <summary>
/// 步态参数模型
/// </summary>
[SugarTable("GaitParameters")]
public class GaitParameters
{
    /// <summary>
    /// 参数ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 测量记录ID
    /// </summary>
    [SugarColumn(ColumnName = "MeasurementId", IsNullable = false)]
    public int MeasurementRecordId { get; set; }

    /// <summary>
    /// 步长 (cm) - 左脚
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? StrideLengthLeft { get; set; }

    /// <summary>
    /// 步长 (cm) - 右脚
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? StrideLengthRight { get; set; }

    /// <summary>
    /// 跨步长 - 左脚（对应数据库 StepLengthLeft）
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? StepLengthLeft { get; set; }

    /// <summary>
    /// 跨步长 - 右脚（对应数据库 StepLengthRight）
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? StepLengthRight { get; set; }

    /// <summary>
    /// 步频 (步/分钟)
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? Cadence { get; set; }

    /// <summary>
    /// 步速 (m/s)
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? Velocity { get; set; }

    /// <summary>
    /// 支撑相时间百分比 - 左脚 (%)
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? StancePhaseLeft { get; set; }

    /// <summary>
    /// 支撑相时间百分比 - 右脚 (%)
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? StancePhaseRight { get; set; }

    /// <summary>
    /// 摆动相时间百分比 - 左脚 (%)
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? SwingPhaseLeft { get; set; }

    /// <summary>
    /// 摆动相时间百分比 - 右脚 (%)
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? SwingPhaseRight { get; set; }

    /// <summary>
    /// 双支撑相时间百分比 (%)
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? DoubleSupport { get; set; }

    /// <summary>
    /// 单支撑相时间百分比 (%)
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? SingleSupport { get; set; }

    /// <summary>
    /// 步态对称性指数
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? SymmetryIndex { get; set; }

    /// <summary>
    /// 原始数据JSON (用于存储详细的时间序列数据)
    /// </summary>
    [SugarColumn(ColumnName = "ParametersJson", ColumnDataType = "text", IsNullable = true)]
    public string? RawDataJson { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 步宽 (cm)（忽略，不存在于当前数据库）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public double? StepWidth { get; set; }

    /// <summary>
    /// 步态周期时间 - 左脚 (秒)（忽略）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public double? GaitCycleTimeLeft { get; set; }

    /// <summary>
    /// 步态周期时间 - 右脚 (秒)（忽略）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public double? GaitCycleTimeRight { get; set; }

    /// <summary>
    /// 步态变异系数（忽略）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public double? VariabilityCoefficient { get; set; }

    /// <summary>
    /// 更新时间（忽略，当前数据库无此字段）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
