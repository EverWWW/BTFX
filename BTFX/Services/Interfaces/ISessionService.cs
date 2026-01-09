using BTFX.Common;
using BTFX.Models;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 会话服务接口
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// 当前登录用户
    /// </summary>
    User? CurrentUser { get; }

    /// <summary>
    /// 当前选中的患者
    /// </summary>
    Patient? CurrentPatient { get; }

    /// <summary>
    /// 是否已登录
    /// </summary>
    bool IsLoggedIn { get; }

    /// <summary>
    /// 是否游客模式
    /// </summary>
    bool IsGuestMode { get; }

    /// <summary>
    /// 当前用户角色
    /// </summary>
    UserRole CurrentUserRole { get; }

    /// <summary>
    /// 设置当前用户
    /// </summary>
    /// <param name="user">用户</param>
    void SetCurrentUser(User? user);

    /// <summary>
    /// 设置当前患者
    /// </summary>
    /// <param name="patient">患者</param>
    void SetCurrentPatient(Patient? patient);

    /// <summary>
    /// 清除会话
    /// </summary>
    void ClearSession();

    /// <summary>
    /// 检查是否有指定权限
    /// </summary>
    /// <param name="permission">权限名称</param>
    /// <returns>是否有权限</returns>
    bool HasPermission(string permission);

    /// <summary>
    /// 用户变更事件
    /// </summary>
    event EventHandler<User?>? CurrentUserChanged;

    /// <summary>
    /// 患者变更事件
    /// </summary>
    event EventHandler<Patient?>? CurrentPatientChanged;
}
