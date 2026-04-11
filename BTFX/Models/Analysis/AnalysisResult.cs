using SqlSugar;

namespace BTFX.Models.Analysis;

/// <summary>
/// 分析结果主表模型
/// </summary>
[SugarTable("AnalysisResults")]
public class AnalysisResult
{
    /// <summary>
    /// 主键
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 关联测量记录 ID
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public int MeasurementId { get; set; }

    /// <summary>
    /// 请求编号（如 GAIT_20260324_001）
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// 协议版本
    /// </summary>
    [SugarColumn(Length = 10, IsNullable = false)]
    public string ProtocolVersion { get; set; } = string.Empty;

    /// <summary>
    /// 算法版本
    /// </summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string AlgorithmVersion { get; set; } = string.Empty;

    /// <summary>
    /// 模型版本
    /// </summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string ModelVersion { get; set; } = string.Empty;

    /// <summary>
    /// 任务状态（completed/failed）
    /// </summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string TaskStatus { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool Success { get; set; }

    /// <summary>
    /// 错误码
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public int? ErrorCode { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 输出目录路径
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = false)]
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 输入配置文件路径
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ConfigFilePath { get; set; }

    /// <summary>
    /// summary.json 路径
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? SummaryFilePath { get; set; }

    /// <summary>
    /// 标注视频路径
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? AnnotatedVideoPath { get; set; }

    /// <summary>
    /// 标注视频时长（秒）
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? AnnotatedVideoDurationS { get; set; }

    /// <summary>
    /// 分析耗时（秒）
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? AnalysisDurationSeconds { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    #region 导航属性

    /// <summary>
    /// 运动学汇总（导航属性）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public KinematicSummary? KinematicSummary { get; set; }

    /// <summary>
    /// CSV 文件列表（导航属性）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public List<AnalysisCsvFile>? CsvFiles { get; set; }

    /// <summary>
    /// 质量控制信息（导航属性）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public QualityControlInfo? QualityControl { get; set; }

    /// <summary>
    /// 步态周期时长（秒）（导航属性，由 BuildSuccessResult 填充）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public double? GaitCycleDurationS { get; set; }

    /// <summary>
    /// 站立相时长（秒）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public double? StanceTimeS { get; set; }

    /// <summary>
    /// 摆动相时长（秒）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public double? SwingTimeS { get; set; }

    /// <summary>
    /// 双支撑相时长（秒）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public double? DoubleSupportTimeS { get; set; }

    /// <summary>
    /// 单支撑相时长（秒）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public double? SingleSupportTimeS { get; set; }

    /// <summary>
    /// 步长（米）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public double? StepLengthM { get; set; }

    /// <summary>
    /// 步幅（米）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public double? StrideLengthM { get; set; }

    /// <summary>
    /// 步速（米/秒）
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public double? GaitSpeedMPerS { get; set; }

    #endregion
}
