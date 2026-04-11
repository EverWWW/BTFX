using BTFX.Common;
using SqlSugar;

namespace BTFX.Models.Analysis;

/// <summary>
/// CSV 文件记录（数据库实体）
/// </summary>
[SugarTable("AnalysisCsvFiles")]
public class AnalysisCsvFile
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
    /// 文件类型标识
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public CsvFileType FileType { get; set; }

    /// <summary>
    /// 文件绝对路径
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = false)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 文件是否存在
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool FileExists { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
