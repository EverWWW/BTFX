using SqlSugar;

namespace BTFX.Models.Analysis;

/// <summary>
/// 质量控制信息（数据库实体）
/// </summary>
[SugarTable("QualityControls")]
public class QualityControlInfo
{
    /// <summary>
    /// 主键
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 关联分析结果 ID
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public int AnalysisResultId { get; set; }

    /// <summary>
    /// 关键点识别平均置信度
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? MeanKeypointConfidence { get; set; }

    /// <summary>
    /// 有效帧比例
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? ValidFrameRatio { get; set; }

    /// <summary>
    /// 是否存在遮挡
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool OcclusionWarning { get; set; }

    /// <summary>
    /// 是否存在丢点
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool MissingPointWarning { get; set; }

    /// <summary>
    /// 原始质量控制 JSON
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? RawDataJson { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
