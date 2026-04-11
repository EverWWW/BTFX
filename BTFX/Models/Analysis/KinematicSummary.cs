using SqlSugar;

namespace BTFX.Models.Analysis;

/// <summary>
/// 运动学汇总参数（数据库实体）
/// </summary>
[SugarTable("KinematicSummaries")]
public class KinematicSummary
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
    /// 髋关节 ROM（°）
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? HipRomDeg { get; set; }

    /// <summary>
    /// 膝关节 ROM（°）
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? KneeRomDeg { get; set; }

    /// <summary>
    /// 踝关节 ROM（°）
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? AnkleRomDeg { get; set; }

    /// <summary>
    /// 骨盆冠状面 ROM（°）
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? PelvisCoronalRomDeg { get; set; }

    /// <summary>
    /// 原始运动学 JSON（便于扩展）
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? RawDataJson { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
