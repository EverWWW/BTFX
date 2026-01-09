using BTFX.Common;

namespace BTFX.Models;

/// <summary>
/// 用户模型
/// </summary>
public class User
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 账号
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码哈希
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 密码盐值
    /// </summary>
    public string PasswordSalt { get; set; } = string.Empty;

    /// <summary>
    /// 姓名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 电话
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// 角色
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Operator;

    /// <summary>
    /// 科室ID
    /// </summary>
    public int? DepartmentId { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否内置账号（不可删除）
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
