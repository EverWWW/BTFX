using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

/// <summary>
/// 会话服务实现
/// </summary>
public class SessionService : ISessionService
{
    /// <summary>
    /// 当前登录用户
    /// </summary>
    public User? CurrentUser { get; private set; }

    /// <summary>
    /// 当前选中的患者
    /// </summary>
    public Patient? CurrentPatient { get; private set; }

    /// <summary>
    /// 是否已登录
    /// </summary>
    public bool IsLoggedIn => CurrentUser != null;

    /// <summary>
    /// 是否游客模式
    /// </summary>
    public bool IsGuestMode => CurrentUser?.Role == UserRole.Guest;

    /// <summary>
    /// 当前用户角色
    /// </summary>
    public UserRole CurrentUserRole => CurrentUser?.Role ?? UserRole.Guest;

    /// <summary>
    /// 用户变更事件
    /// </summary>
    public event EventHandler<User?>? CurrentUserChanged;

    /// <summary>
    /// 患者变更事件
    /// </summary>
    public event EventHandler<Patient?>? CurrentPatientChanged;

    /// <summary>
    /// 设置当前用户
    /// </summary>
    /// <param name="user">用户</param>
    public void SetCurrentUser(User? user)
    {
        CurrentUser = user;
        CurrentUserChanged?.Invoke(this, user);
    }

    /// <summary>
    /// 设置当前患者
    /// </summary>
    /// <param name="patient">患者</param>
    public void SetCurrentPatient(Patient? patient)
    {
        CurrentPatient = patient;
        CurrentPatientChanged?.Invoke(this, patient);
    }

    /// <summary>
    /// 清除会话
    /// </summary>
    public void ClearSession()
    {
        CurrentUser = null;
        CurrentPatient = null;
        CurrentUserChanged?.Invoke(this, null);
        CurrentPatientChanged?.Invoke(this, null);
    }

    /// <summary>
    /// 检查是否有指定权限
    /// </summary>
    /// <param name="permission">权限名称</param>
    /// <returns>是否有权限</returns>
    public bool HasPermission(string permission)
    {
        if (CurrentUser == null) return false;

        // 根据角色和权限名称判断
        return permission.ToLower() switch
        {
            // 用户管理权限 - 仅管理员
            "usermanagement" => CurrentUserRole == UserRole.Administrator,

            // 备份恢复权限 - 仅管理员
            "backup" => CurrentUserRole == UserRole.Administrator,
            "restore" => CurrentUserRole == UserRole.Administrator,

            // 单位设置权限 - 仅管理员
            "unitsettings" => CurrentUserRole == UserRole.Administrator,

            // 科室管理权限 - 仅管理员
            "departmentmanagement" => CurrentUserRole == UserRole.Administrator,

            // 删除患者（全部）权限 - 仅管理员
            "deleteanypatient" => CurrentUserRole == UserRole.Administrator,

            // 删除患者（自己创建）权限 - 管理员和操作员
            "deleteownpatient" => CurrentUserRole == UserRole.Administrator ||
                                  CurrentUserRole == UserRole.Operator,

            // 删除测量数据权限 - 仅管理员
            "deletemeasurement" => CurrentUserRole == UserRole.Administrator,

            // 删除报告权限 - 仅管理员
            "deletereport" => CurrentUserRole == UserRole.Administrator,

            // 患者管理权限 - 管理员和操作员
            "patientmanagement" => CurrentUserRole == UserRole.Administrator ||
                                   CurrentUserRole == UserRole.Operator,

            // 测量权限 - 所有用户
            "measurement" => true,

            // 数据管理权限 - 管理员和操作员
            "datamanagement" => CurrentUserRole == UserRole.Administrator ||
                                CurrentUserRole == UserRole.Operator,

            // 报告管理权限 - 管理员和操作员
            "reportmanagement" => CurrentUserRole == UserRole.Administrator ||
                                  CurrentUserRole == UserRole.Operator,

            // 导出权限 - 管理员和操作员
            "export" => CurrentUserRole == UserRole.Administrator ||
                        CurrentUserRole == UserRole.Operator,

            // 设置权限 - 所有用户（部分受限）
            "settings" => true,

            // 设备配置权限 - 管理员和操作员
            "devicesettings" => CurrentUserRole == UserRole.Administrator ||
                                CurrentUserRole == UserRole.Operator,

            // 默认无权限
            _ => false
        };
    }
}
