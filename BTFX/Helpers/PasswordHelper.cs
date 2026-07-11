using System.Security.Cryptography;
using System.Text;

namespace BTFX.Helpers;

/// <summary>
/// 密码处理工具类
/// 提供 SHA-256 密码哈希和验证功能
/// </summary>
public static class PasswordHelper
{
    /// <summary>
    /// 盐值长度（字节）
    /// </summary>
    private const int SaltSize = 16;
    private const int Pbkdf2Iterations = 210_000;
    private const int Pbkdf2HashSize = 32;
    private const string Pbkdf2Prefix = "pbkdf2-sha256$";

    /// <summary>
    /// 生成随机盐值
    /// </summary>
    /// <returns>Base64 编码的盐值</returns>
    public static string GenerateSalt()
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        return Convert.ToBase64String(saltBytes);
    }

    /// <summary>
    /// 使用 SHA-256 对密码进行哈希
    /// </summary>
    /// <param name="password">原始密码</param>
    /// <param name="salt">盐值</param>
    /// <returns>Base64 编码的哈希值</returns>
    public static string HashPassword(string password, string salt)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentNullException(nameof(password));
        if (string.IsNullOrEmpty(salt))
            throw new ArgumentNullException(nameof(salt));

        var saltBytes = Convert.FromBase64String(salt);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            Pbkdf2HashSize);
        return $"{Pbkdf2Prefix}{Pbkdf2Iterations}${Convert.ToBase64String(hashBytes)}";
    }

    /// <summary>
    /// 验证密码
    /// </summary>
    /// <param name="password">待验证的密码</param>
    /// <param name="hashedPassword">存储的哈希密码</param>
    /// <param name="salt">盐值</param>
    /// <returns>密码是否匹配</returns>
    public static bool VerifyPassword(string password, string hashedPassword, string salt)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(salt))
            return false;

        if (hashedPassword.StartsWith(Pbkdf2Prefix, StringComparison.Ordinal))
        {
            return VerifyPbkdf2(password, hashedPassword, salt);
        }

        var combined = password + salt;
        var computedHash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromBase64String(hashedPassword);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    public static bool NeedsRehash(string hashedPassword)
    {
        if (!hashedPassword.StartsWith(Pbkdf2Prefix, StringComparison.Ordinal))
        {
            return true;
        }

        var parts = hashedPassword.Split('$');
        return parts.Length != 3
            || !int.TryParse(parts[1], out var iterations)
            || iterations < Pbkdf2Iterations;
    }

    private static bool VerifyPbkdf2(string password, string hashedPassword, string salt)
    {
        var parts = hashedPassword.Split('$');
        if (parts.Length != 3 || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        try
        {
            var expectedHash = Convert.FromBase64String(parts[2]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                Convert.FromBase64String(salt),
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// 使用旧方式哈希密码（不带盐值，用于兼容旧数据）
    /// </summary>
    /// <param name="password">原始密码</param>
    /// <returns>Base64 编码的哈希值</returns>
    public static string HashPasswordLegacy(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentNullException(nameof(password));

        var bytes = Encoding.UTF8.GetBytes(password);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// 验证旧格式密码（不带盐值）
    /// </summary>
    /// <param name="password">待验证的密码</param>
    /// <param name="hashedPassword">存储的哈希密码</param>
    /// <returns>密码是否匹配</returns>
    public static bool VerifyPasswordLegacy(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
            return false;

        var computedHash = HashPasswordLegacy(password);
        return string.Equals(computedHash, hashedPassword, StringComparison.Ordinal);
    }
}
