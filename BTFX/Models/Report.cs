using BTFX.Common;
using SqlSugar;

namespace BTFX.Models;

/// <summary>
/// 报告模型
/// </summary>
[SugarTable("Reports")]
public class Report
{
    /// <summary>
    /// 报告ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 测量记录ID
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public int MeasurementId { get; set; }

    /// <summary>
    /// 测量记录（导航属性）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public MeasurementRecord? MeasurementRecord { get; set; }

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
    /// 创建者ID（对应数据库 UserId 字段）
    /// </summary>
    [SugarColumn(ColumnName = "UserId", IsNullable = false)]
    public int CreatedBy { get; set; }

    /// <summary>
    /// 创建者（导航属性）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public User? CreatedByUser { get; set; }

    /// <summary>
    /// 报告编号
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string ReportNumber { get; set; } = string.Empty;

    /// <summary>
    /// 报告日期
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime ReportDate { get; set; } = DateTime.Now;

    /// <summary>
    /// 医生意见
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? DoctorOpinion { get; set; }

    /// <summary>
    /// 报告状态
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public ReportStatus Status { get; set; } = ReportStatus.Draft;

    /// <summary>
    /// PDF文件路径
    /// </summary>
    [SugarColumn(ColumnName = "FilePath", Length = 500, IsNullable = true)]
    public string? PdfFilePath { get; set; }

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

    // ============ 以下字段不在数据库中，用 IsIgnore 标记 ============

    /// <summary>
    /// 测量记录ID（兼容旧属性名）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public int MeasurementRecordId
    {
        get => MeasurementId;
        set => MeasurementId = value;
    }

    /// <summary>
    /// 报告标题（忽略）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 主诉（忽略）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public string? ChiefComplaint { get; set; }

    /// <summary>
    /// 现病史（忽略）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public string? PresentIllness { get; set; }

    /// <summary>
    /// 检查结果（忽略）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public string? Findings { get; set; }

    /// <summary>
    /// 结论（忽略）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public string? Conclusion { get; set; }

    /// <summary>
    /// 医生建议（忽略）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public string? DoctorAdvice { get; set; }

    /// <summary>
    /// 报告医生姓名（忽略）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public string? DoctorName { get; set; }

    /// <summary>
    /// 报告生成时间（兼容旧属性名）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public DateTime GeneratedAt
    {
        get => ReportDate;
        set => ReportDate = value;
    }

    /// <summary>
    /// 报告完成时间（忽略）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 打印时间（忽略）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public DateTime? PrintedAt { get; set; }
}
