using BTFX.Models;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 身份验证服务接口
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// 登录
    /// </summary>
    /// <param name="username">账号</param>
    /// <param name="password">密码</param>
    /// <returns>登录成功返回用户信息，失败返回null</returns>
    Task<User?> LoginAsync(string username, string password);

    /// <summary>
    /// 游客登录
    /// </summary>
    /// <returns>游客用户信息</returns>
    Task<User> GuestLoginAsync();

    /// <summary>
    /// 登出
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// 修改密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="oldPassword">旧密码</param>
    /// <param name="newPassword">新密码</param>
    /// <returns>是否成功</returns>
    Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);

    /// <summary>
    /// 重置密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="newPassword">新密码</param>
    /// <returns>是否成功</returns>
    Task<bool> ResetPasswordAsync(int userId, string newPassword);

    /// <summary>
    /// 验证密码
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <param name="hashedPassword">哈希密码</param>
    /// <returns>是否匹配</returns>
    bool VerifyPassword(string password, string hashedPassword);

    /// <summary>
    /// 哈希密码
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <returns>哈希后的密码</returns>
    string HashPassword(string password);
}
