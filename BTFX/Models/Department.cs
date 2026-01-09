namespace BTFX.Models;

/// <summary>
/// 科室模型
/// </summary>
public class Department
{
    /// <summary>
    /// 科室ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 科室名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 科室代码
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// 科室描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
