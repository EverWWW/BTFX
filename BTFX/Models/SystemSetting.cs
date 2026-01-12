using SqlSugar;

namespace BTFX.Models;

/// <summary>
/// 系统设置模型
/// </summary>
[SugarTable("SystemSettings")]
public class SystemSetting
{
    /// <summary>
    /// 设置键
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, ColumnName = "Key", Length = 100)]
    public string SettingKey { get; set; } = string.Empty;

    /// <summary>
    /// 设置值
    /// </summary>
    [SugarColumn(ColumnName = "Value", ColumnDataType = "text", IsNullable = true)]
    public string? SettingValue { get; set; }

    /// <summary>
    /// 值类型
    /// </summary>
    [SugarColumn(ColumnName = "ValueType", Length = 50, IsNullable = false)]
    public string ValueType { get; set; } = "string";

    /// <summary>
    /// 更新时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
