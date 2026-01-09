namespace BTFX.Models;

/// <summary>
/// 步态参数模型
/// </summary>
public class GaitParameters
{
    /// <summary>
    /// 参数ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 测量记录ID
    /// </summary>
    public int MeasurementRecordId { get; set; }

    /// <summary>
    /// 步长 (cm) - 左脚
    /// </summary>
    public double? StrideLengthLeft { get; set; }

    /// <summary>
    /// 步长 (cm) - 右脚
    /// </summary>
    public double? StrideLengthRight { get; set; }

    /// <summary>
    /// 步宽 (cm)
    /// </summary>
    public double? StepWidth { get; set; }

    /// <summary>
    /// 步频 (步/分钟)
    /// </summary>
    public double? Cadence { get; set; }

    /// <summary>
    /// 步速 (m/s)
    /// </summary>
    public double? Velocity { get; set; }

    /// <summary>
    /// 支撑相时间百分比 - 左脚 (%)
    /// </summary>
    public double? StancePhaseLeft { get; set; }

    /// <summary>
    /// 支撑相时间百分比 - 右脚 (%)
    /// </summary>
    public double? StancePhaseRight { get; set; }

    /// <summary>
    /// 摆动相时间百分比 - 左脚 (%)
    /// </summary>
    public double? SwingPhaseLeft { get; set; }

    /// <summary>
    /// 摆动相时间百分比 - 右脚 (%)
    /// </summary>
    public double? SwingPhaseRight { get; set; }

    /// <summary>
    /// 双支撑相时间百分比 (%)
    /// </summary>
    public double? DoubleSupport { get; set; }

    /// <summary>
    /// 步态周期时间 - 左脚 (秒)
    /// </summary>
    public double? GaitCycleTimeLeft { get; set; }

    /// <summary>
    /// 步态周期时间 - 右脚 (秒)
    /// </summary>
    public double? GaitCycleTimeRight { get; set; }

    /// <summary>
    /// 步态对称性指数
    /// </summary>
    public double? SymmetryIndex { get; set; }

    /// <summary>
    /// 步态变异系数
    /// </summary>
    public double? VariabilityCoefficient { get; set; }

    /// <summary>
    /// 原始数据JSON (用于存储详细的时间序列数据)
    /// </summary>
    public string? RawDataJson { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
