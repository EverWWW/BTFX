using BTFX.Common;
using BTFX.Data;
using BTFX.Helpers;
using BTFX.Models;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 用户服务实现（使用 SqlSugar）
/// </summary>
public class UserService : IUserService
{
    private readonly ILogHelper? _logHelper;

    /// <summary>
    /// 构造函数
    /// </summary>
    public UserService()
    {
        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }
    }

    /// <inheritdoc/>
    public async Task<List<User>> GetAllUsersAsync()
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            return await db.GetListAsync<User>(u => true);
        }
        catch (Exception ex)
        {
            _logHelper?.Error("获取用户列表失败", ex);
            return new List<User>();
        }
    }

    /// <inheritdoc/>
    public async Task<User?> GetUserByIdAsync(int id)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            return await db.GetByIdAsync<User>(id);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"获取用户失败: Id={id}", ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            // SqlSugar 默认不区分大小写（SQLite）
            return await db.GetFirstAsync<User>(u => u.Username == username);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"获取用户失败: Username={username}", ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<int> AddUserAsync(User user)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var now = DateTime.Now;

            // 生成盐值和密码哈希
            var salt = PasswordHelper.GenerateSalt();
            var passwordHash = PasswordHelper.HashPassword(
                string.IsNullOrEmpty(user.PasswordHash) ? Constants.DEFAULT_PASSWORD : user.PasswordHash, 
                salt);

            user.PasswordHash = passwordHash;
            user.PasswordSalt = salt;
            user.CreatedAt = now;
            user.UpdatedAt = now;

            var id = await db.InsertReturnIdentityAsync(user);

            _logHelper?.Information($"添加用户成功: Id={id}, Username={user.Username}");
            return (int)id;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"添加用户失败: Username={user.Username}", ex);
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateUserAsync(User user)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            user.UpdatedAt = DateTime.Now;

            // 使用条件更新，不更新密码字段
            var count = await db.UpdateAsync<User>(
                u => new User 
                { 
                    Name = user.Name,
                    Phone = user.Phone,
                    Role = user.Role,
                    DepartmentId = user.DepartmentId,
                    IsEnabled = user.IsEnabled,
                    LastLoginAt = user.LastLoginAt,
                    UpdatedAt = user.UpdatedAt
                },
                u => u.Id == user.Id);

            if (count > 0)
            {
                _logHelper?.Information($"更新用户成功: Id={user.Id}");
            }

            return count > 0;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"更新用户失败: Id={user.Id}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteUserAsync(int id)
    {
        try
        {
            // 检查是否为内置账号
            var user = await GetUserByIdAsync(id);
            if (user == null)
            {
                return false;
            }

            if (user.IsBuiltIn)
            {
                _logHelper?.Warning($"无法删除内置账号: Id={id}, Username={user.Username}");
                return false;
            }

            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            var count = await db.DeleteAsync<User>(u => u.Id == id && !u.IsBuiltIn);

            if (count > 0)
            {
                _logHelper?.Information($"删除用户成功: Id={id}");
            }

            return count > 0;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除用户失败: Id={id}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsUsernameExistsAsync(string username, int? excludeId = null)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            if (excludeId.HasValue)
            {
                return await db.AnyAsync<User>(u => u.Username == username && u.Id != excludeId.Value);
            }
            else
            {
                return await db.AnyAsync<User>(u => u.Username == username);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"检查用户名失败: Username={username}", ex);
            return true; // 出错时返回 true，防止重复添加
        }
    }

    /// <inheritdoc/>
    public Task<bool> InitializeDefaultUsersAsync()
    {
        // 数据库初始化时已经创建了内置用户
        return Task.FromResult(true);
    }

    /// <summary>
    /// 更新用户密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="newPasswordHash">新密码哈希</param>
    /// <param name="newSalt">新盐值</param>
    /// <returns>是否成功</returns>
    public async Task<bool> UpdatePasswordAsync(int userId, string newPasswordHash, string newSalt)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var now = DateTime.Now;

            var count = await db.UpdateAsync<User>(
                u => new User 
                { 
                    PasswordHash = newPasswordHash,
                    PasswordSalt = newSalt,
                    UpdatedAt = now
                },
                u => u.Id == userId);

            if (count > 0)
            {
                _logHelper?.Information($"更新用户密码成功: Id={userId}");
            }

            return count > 0;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"更新用户密码失败: Id={userId}", ex);
            return false;
        }
    }
}
