using BTFX.Common;
using BTFX.Data;
using BTFX.Helpers;
using BTFX.Models;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 用户服务实现
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var users = await db.QueryAsync<User>(@"
                SELECT Id, Username, PasswordHash, PasswordSalt, Name, Phone, Role, 
                       DepartmentId, IsEnabled, IsBuiltIn, LastLoginAt, CreatedAt, UpdatedAt 
                FROM Users 
                ORDER BY Id
            ");

            return users.ToList();
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            return await db.QueryFirstOrDefaultAsync<User>(@"
                SELECT Id, Username, PasswordHash, PasswordSalt, Name, Phone, Role, 
                       DepartmentId, IsEnabled, IsBuiltIn, LastLoginAt, CreatedAt, UpdatedAt 
                FROM Users 
                WHERE Id = @Id
            ", new { Id = id });
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            return await db.QueryFirstOrDefaultAsync<User>(@"
                SELECT Id, Username, PasswordHash, PasswordSalt, Name, Phone, Role, 
                       DepartmentId, IsEnabled, IsBuiltIn, LastLoginAt, CreatedAt, UpdatedAt 
                FROM Users 
                WHERE Username = @Username COLLATE NOCASE
            ", new { Username = username });
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);

            // 生成盐值和密码哈希
            var salt = PasswordHelper.GenerateSalt();
            var passwordHash = PasswordHelper.HashPassword(
                string.IsNullOrEmpty(user.PasswordHash) ? Constants.DEFAULT_PASSWORD : user.PasswordHash, 
                salt);

            var id = await db.InsertAndGetIdAsync(@"
                INSERT INTO Users (Username, PasswordHash, PasswordSalt, Name, Phone, Role, 
                                   DepartmentId, IsEnabled, IsBuiltIn, CreatedAt, UpdatedAt)
                VALUES (@Username, @PasswordHash, @PasswordSalt, @Name, @Phone, @Role, 
                        @DepartmentId, @IsEnabled, @IsBuiltIn, @CreatedAt, @UpdatedAt)
            ", new
            {
                user.Username,
                PasswordHash = passwordHash,
                PasswordSalt = salt,
                user.Name,
                Phone = user.Phone ?? "",
                Role = (int)user.Role,
                user.DepartmentId,
                IsEnabled = user.IsEnabled ? 1 : 0,
                IsBuiltIn = user.IsBuiltIn ? 1 : 0,
                CreatedAt = now,
                UpdatedAt = now
            });

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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);
            var lastLoginAt = user.LastLoginAt?.ToString(Constants.DATETIME_FORMAT);

            var affected = await db.ExecuteNonQueryAsync(@"
                UPDATE Users 
                SET Name = @Name, Phone = @Phone, Role = @Role, DepartmentId = @DepartmentId,
                    IsEnabled = @IsEnabled, LastLoginAt = @LastLoginAt, UpdatedAt = @UpdatedAt
                WHERE Id = @Id
            ", new
            {
                user.Id,
                user.Name,
                Phone = user.Phone ?? "",
                Role = (int)user.Role,
                user.DepartmentId,
                IsEnabled = user.IsEnabled ? 1 : 0,
                LastLoginAt = lastLoginAt,
                UpdatedAt = now
            });

            if (affected > 0)
            {
                _logHelper?.Information($"更新用户成功: Id={user.Id}");
            }

            return affected > 0;
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

            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var affected = await db.ExecuteNonQueryAsync(@"
                DELETE FROM Users WHERE Id = @Id AND IsBuiltIn = 0
            ", new { Id = id });

            if (affected > 0)
            {
                _logHelper?.Information($"删除用户成功: Id={id}");
            }

            return affected > 0;
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            string sql;
            object parameters;

            if (excludeId.HasValue)
            {
                sql = "SELECT COUNT(*) FROM Users WHERE Username = @Username COLLATE NOCASE AND Id != @ExcludeId";
                parameters = new { Username = username, ExcludeId = excludeId.Value };
            }
            else
            {
                sql = "SELECT COUNT(*) FROM Users WHERE Username = @Username COLLATE NOCASE";
                parameters = new { Username = username };
            }

            var count = await db.ExecuteScalarAsync<int>(sql, parameters);
            return count > 0;
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);

            var affected = await db.ExecuteNonQueryAsync(@"
                UPDATE Users 
                SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt, UpdatedAt = @UpdatedAt
                WHERE Id = @Id
            ", new
            {
                Id = userId,
                PasswordHash = newPasswordHash,
                PasswordSalt = newSalt,
                UpdatedAt = now
            });

            if (affected > 0)
            {
                _logHelper?.Information($"更新用户密码成功: Id={userId}");
            }

            return affected > 0;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"更新用户密码失败: Id={userId}", ex);
            return false;
        }
    }
}
