using BTFX.Common;

namespace BTFX.Models;

/// <summary>
/// 报告模型
/// </summary>
public class Report
{
    /// <summary>
    /// 报告ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 报告编号
    /// </summary>
    public string ReportNumber { get; set; } = string.Empty;

    /// <summary>
    /// 测量记录ID
    /// </summary>
    public int MeasurementRecordId { get; set; }

    /// <summary>
    /// 测量记录
    /// </summary>
    public MeasurementRecord? MeasurementRecord { get; set; }

    /// <summary>
    /// 患者ID
    /// </summary>
    public int PatientId { get; set; }

    /// <summary>
    /// 患者信息
    /// </summary>
    public Patient? Patient { get; set; }

    /// <summary>
    /// 报告标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 主诉
    /// </summary>
    public string? ChiefComplaint { get; set; }

    /// <summary>
    /// 现病史
    /// </summary>
    public string? PresentIllness { get; set; }

    /// <summary>
    /// 检查所见
    /// </summary>
    public string? Findings { get; set; }

    /// <summary>
    /// 检查结论
    /// </summary>
    public string? Conclusion { get; set; }

    /// <summary>
    /// 医生建议
    /// </summary>
    public string? DoctorAdvice { get; set; }

    /// <summary>
    /// 报告医生姓名
    /// </summary>
    public string? DoctorName { get; set; }

    /// <summary>
    /// 报告状态
    /// </summary>
    public ReportStatus Status { get; set; } = ReportStatus.Draft;

    /// <summary>
    /// 报告生成时间
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 报告完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 打印时间
    /// </summary>
    public DateTime? PrintedAt { get; set; }

    /// <summary>
    /// PDF文件路径
    /// </summary>
    public string? PdfFilePath { get; set; }

    /// <summary>
    /// 创建人ID
    /// </summary>
    public int CreatedBy { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public User? CreatedByUser { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
