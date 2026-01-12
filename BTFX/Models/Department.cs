using SqlSugar;

namespace BTFX.Models;

/// <summary>
/// 科室模型
/// </summary>
[SugarTable("Departments")]
public class Department
{
    /// <summary>
    /// 科室ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 科室名称
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 科室电话
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? Phone { get; set; }

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
}
