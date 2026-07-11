using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BTFX.Helpers;

internal static class CredentialProtector
{
    private const string DpapiPrefix = "dpapi:v1:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BTFX.Credentials.v1");
    private static readonly byte[] LegacyKey = Encoding.UTF8.GetBytes("BTFX2026SecretK!");
    private static readonly byte[] LegacyIv = Encoding.UTF8.GetBytes("BTFX2026InitVec!");

    public static string Protect(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(value),
            Entropy,
            DataProtectionScope.CurrentUser);
        return DpapiPrefix + Convert.ToBase64String(encrypted);
    }

    public static bool TryUnprotect(
        string protectedValue,
        out string value,
        out bool requiresMigration)
    {
        value = string.Empty;
        requiresMigration = false;
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return false;
        }

        try
        {
            if (protectedValue.StartsWith(DpapiPrefix, StringComparison.Ordinal))
            {
                var encrypted = Convert.FromBase64String(protectedValue[DpapiPrefix.Length..]);
                var decrypted = ProtectedData.Unprotect(
                    encrypted,
                    Entropy,
                    DataProtectionScope.CurrentUser);
                value = Encoding.UTF8.GetString(decrypted);
                return true;
            }

            value = DecryptLegacyAes(protectedValue);
            requiresMigration = true;
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string DecryptLegacyAes(string protectedValue)
    {
        using var aes = Aes.Create();
        aes.Key = LegacyKey;
        aes.IV = LegacyIv;
        using var decryptor = aes.CreateDecryptor();
        using var input = new MemoryStream(Convert.FromBase64String(protectedValue));
        using var crypto = new CryptoStream(input, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(crypto);
        return reader.ReadToEnd();
    }
}
