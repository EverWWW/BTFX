using BTFX.Models;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 用户服务接口
/// </summary>
public interface IUserService
{
    /// <summary>
    /// 获取所有用户
    /// </summary>
    /// <returns>用户列表</returns>
    Task<List<User>> GetAllUsersAsync();

    /// <summary>
    /// 根据ID获取用户
    /// </summary>
    /// <param name="id">用户ID</param>
    /// <returns>用户信息</returns>
    Task<User?> GetUserByIdAsync(int id);

    /// <summary>
    /// 根据账号获取用户
    /// </summary>
    /// <param name="username">账号</param>
    /// <returns>用户信息</returns>
    Task<User?> GetUserByUsernameAsync(string username);

    /// <summary>
    /// 添加用户
    /// </summary>
    /// <param name="user">用户信息</param>
    /// <returns>新增的用户ID</returns>
    Task<int> AddUserAsync(User user);

    /// <summary>
    /// 更新用户信息
    /// </summary>
    /// <param name="user">用户信息</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateUserAsync(User user);

    /// <summary>
    /// 删除用户
    /// </summary>
    /// <param name="id">用户ID</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteUserAsync(int id);

    /// <summary>
    /// 检查账号是否存在
    /// </summary>
    /// <param name="username">账号</param>
    /// <param name="excludeId">排除的用户ID（用于更新时检查）</param>
    /// <returns>是否存在</returns>
    Task<bool> IsUsernameExistsAsync(string username, int? excludeId = null);

    /// <summary>
    /// 初始化默认用户
    /// </summary>
    /// <returns>是否成功</returns>
    Task<bool> InitializeDefaultUsersAsync();
}
