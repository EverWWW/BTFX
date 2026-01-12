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
    /// 科室电话
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
