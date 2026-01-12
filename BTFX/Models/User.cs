using BTFX.Common;
using SqlSugar;

namespace BTFX.Models;

/// <summary>
/// 用户模型
/// </summary>
[SugarTable("Users")]
public class User
{
    /// <summary>
    /// 用户ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 账号
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码哈希
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 密码盐值
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string PasswordSalt { get; set; } = string.Empty;

    /// <summary>
    /// 姓名
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 电话
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// 角色
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public UserRole Role { get; set; } = UserRole.Operator;

    /// <summary>
    /// 科室ID
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public int? DepartmentId { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否内置账号（不可删除）
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// 最后登录时间
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? LastLoginAt { get; set; }

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
