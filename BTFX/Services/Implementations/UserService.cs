using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

/// <summary>
/// 用户服务实现（占位实现，第四阶段完善）
/// </summary>
public class UserService : IUserService
{
    // TODO: 注入数据库服务

    /// <inheritdoc/>
    public Task<List<User>> GetAllUsersAsync()
    {
        // TODO: 从数据库读取
        return Task.FromResult(new List<User>());
    }

    /// <inheritdoc/>
    public Task<User?> GetUserByIdAsync(int id)
    {
        // TODO: 从数据库读取
        return Task.FromResult<User?>(null);
    }

    /// <inheritdoc/>
    public Task<User?> GetUserByUsernameAsync(string username)
    {
        // TODO: 从数据库读取
        return Task.FromResult<User?>(null);
    }

    /// <inheritdoc/>
    public Task<int> AddUserAsync(User user)
    {
        // TODO: 插入数据库
        return Task.FromResult(0);
    }

    /// <inheritdoc/>
    public Task<bool> UpdateUserAsync(User user)
    {
        // TODO: 更新数据库
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteUserAsync(int id)
    {
        // TODO: 删除数据库记录
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<bool> IsUsernameExistsAsync(string username, int? excludeId = null)
    {
        // TODO: 检查数据库
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<bool> InitializeDefaultUsersAsync()
    {
        // TODO: 初始化默认用户（admin, user, guest）
        return Task.FromResult(false);
    }
}
