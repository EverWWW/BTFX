using BTFX.Models;
using BTFX.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

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

        if (VerifyPassword(password, user.PasswordHash))
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

        if (!VerifyPassword(oldPassword, user.PasswordHash))
        {
            return false;
        }

        user.PasswordHash = HashPassword(newPassword);
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

        user.PasswordHash = HashPassword(newPassword);
        user.UpdatedAt = DateTime.Now;
        return await _userService.UpdateUserAsync(user);
    }

    /// <inheritdoc/>
    public bool VerifyPassword(string password, string hashedPassword)
    {
        return HashPassword(password) == hashedPassword;
    }

    /// <inheritdoc/>
    public string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
