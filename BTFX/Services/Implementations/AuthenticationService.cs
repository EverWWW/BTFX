using BTFX.Helpers;
using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

/// <summary>
/// 身份验证服务实现
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IUserService _userService;

    public AuthenticationService(IUserService userService)
    {
        _userService = userService;
    }

    /// <inheritdoc/>
    public async Task<User?> LoginAsync(string username, string password)
    {
        var user = await _userService.GetUserByUsernameAsync(username);
        if (user == null || !user.IsEnabled)
        {
            return null;
        }

        // 验证密码（支持带盐值的新格式）
        bool passwordValid;
        if (!string.IsNullOrEmpty(user.PasswordSalt))
        {
            // 新格式：使用盐值
            passwordValid = PasswordHelper.VerifyPassword(password, user.PasswordHash, user.PasswordSalt);
        }
        else
        {
            // 旧格式：不使用盐值（兼容）
            passwordValid = PasswordHelper.VerifyPasswordLegacy(password, user.PasswordHash);
        }

        if (passwordValid)
        {
            user.LastLoginAt = DateTime.Now;
            await _userService.UpdateUserAsync(user);
            return user;
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<User> GuestLoginAsync()
    {
        var guestUser = await _userService.GetUserByUsernameAsync(Common.Constants.GUEST_USERNAME);
        if (guestUser != null)
        {
            return guestUser;
        }

        // 如果游客账号不存在，创建一个临时的
        return new User
        {
            Id = -1,
            Username = Common.Constants.GUEST_USERNAME,
            Name = "游客",
            Role = Common.UserRole.Guest,
            IsEnabled = true
        };
    }

    /// <inheritdoc/>
    public Task LogoutAsync()
    {
        // 清理会话信息
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return false;
        }

        // 验证旧密码
        bool oldPasswordValid;
        if (!string.IsNullOrEmpty(user.PasswordSalt))
        {
            oldPasswordValid = PasswordHelper.VerifyPassword(oldPassword, user.PasswordHash, user.PasswordSalt);
        }
        else
        {
            oldPasswordValid = PasswordHelper.VerifyPasswordLegacy(oldPassword, user.PasswordHash);
        }

        if (!oldPasswordValid)
        {
            return false;
        }

        // 生成新的盐值和密码哈希
        var newSalt = PasswordHelper.GenerateSalt();
        var newHash = PasswordHelper.HashPassword(newPassword, newSalt);

        // 使用 UserService 的密码更新方法
        if (_userService is UserService userService)
        {
            return await userService.UpdatePasswordAsync(userId, newHash, newSalt);
        }

        // 备用方案：直接更新 User 对象
        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;
        user.UpdatedAt = DateTime.Now;
        return await _userService.UpdateUserAsync(user);
    }

    /// <inheritdoc/>
    public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return false;
        }

        // 生成新的盐值和密码哈希
        var newSalt = PasswordHelper.GenerateSalt();
        var newHash = PasswordHelper.HashPassword(newPassword, newSalt);

        // 使用 UserService 的密码更新方法
        if (_userService is UserService userService)
        {
            return await userService.UpdatePasswordAsync(userId, newHash, newSalt);
        }

        // 备用方案：直接更新 User 对象
        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;
        user.UpdatedAt = DateTime.Now;
        return await _userService.UpdateUserAsync(user);
    }

    /// <inheritdoc/>
    public bool VerifyPassword(string password, string hashedPassword)
    {
        // 旧格式兼容
        return PasswordHelper.VerifyPasswordLegacy(password, hashedPassword);
    }

    /// <inheritdoc/>
    public string HashPassword(string password)
    {
        // 旧格式兼容
        return PasswordHelper.HashPasswordLegacy(password);
    }
}
