using System.Security.Cryptography;
using System.Text;
using BTFX.Helpers;
using Xunit;

namespace BTFX.Tests;

public sealed class CredentialSecurityTests
{
    [Fact]
    public void CredentialProtector_RoundTripsWithDpapiFormat()
    {
        var encrypted = CredentialProtector.Protect("688626");

        Assert.StartsWith("dpapi:v1:", encrypted, StringComparison.Ordinal);
        Assert.True(CredentialProtector.TryUnprotect(encrypted, out var password, out var requiresMigration));
        Assert.Equal("688626", password);
        Assert.False(requiresMigration);
    }

    [Fact]
    public void CredentialProtector_ReadsLegacyAesAndRequestsMigration()
    {
        var encrypted = EncryptLegacyAes("688626");

        Assert.True(CredentialProtector.TryUnprotect(encrypted, out var password, out var requiresMigration));
        Assert.Equal("688626", password);
        Assert.True(requiresMigration);
    }

    [Fact]
    public void PasswordHelper_CreatesAndVerifiesPbkdf2Hash()
    {
        var salt = PasswordHelper.GenerateSalt();
        var hash = PasswordHelper.HashPassword("688626", salt);

        Assert.StartsWith("pbkdf2-sha256$", hash, StringComparison.Ordinal);
        Assert.True(PasswordHelper.VerifyPassword("688626", hash, salt));
        Assert.False(PasswordHelper.VerifyPassword("wrong", hash, salt));
        Assert.False(PasswordHelper.NeedsRehash(hash));
    }

    [Fact]
    public void PasswordHelper_VerifiesOldSaltedSha256AndRequestsMigration()
    {
        var salt = PasswordHelper.GenerateSalt();
        var oldHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("688626" + salt)));

        Assert.True(PasswordHelper.VerifyPassword("688626", oldHash, salt));
        Assert.True(PasswordHelper.NeedsRehash(oldHash));
    }

    private static string EncryptLegacyAes(string password)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes("BTFX2026SecretK!");
        aes.IV = Encoding.UTF8.GetBytes("BTFX2026InitVec!");
        using var encryptor = aes.CreateEncryptor();
        using var output = new MemoryStream();
        using (var crypto = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
        using (var writer = new StreamWriter(crypto))
        {
            writer.Write(password);
        }

        return Convert.ToBase64String(output.ToArray());
    }
}
