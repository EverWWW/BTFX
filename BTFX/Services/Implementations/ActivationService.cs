using System.IO;
using System.IO.Compression;
using System.Management;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BTFX.Common;
using BTFX.Models.Activation;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 参考公司通用 WPF 框架的激活服务。
/// </summary>
public class ActivationService : IActivationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ILogHelper? _logHelper;
    private readonly string _licenseFilePath;

    public ActivationService(ILogHelper? logHelper = null)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _logHelper = logHelper;

        var licenseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Constants.APP_NAME,
            "License");
        Directory.CreateDirectory(licenseDirectory);
        _licenseFilePath = Path.Combine(licenseDirectory, $"{Constants.ACTIVATION_PRODUCT_MODEL}.lic");
    }

    public bool IsActivated
    {
        get
        {
            var saved = ReadLicenseFile();
            if (saved == null)
            {
                return false;
            }

            var current = GetCurrentMachineInfo();
            return string.Equals(saved.LicenseKey, GenerateLicenseKey(current), StringComparison.OrdinalIgnoreCase);
        }
    }

    public SoftKey GetCurrentMachineInfo()
    {
        var cpuId = QueryFirst("Win32_Processor", "ProcessorId");
        var diskId = QueryFirst("Win32_DiskDrive", "Model");
        var biosId = QueryFirst("Win32_BaseBoard", "SerialNumber");

        return new SoftKey
        {
            EquipmentName = Constants.APP_DISPLAY_NAME,
            EquipmentModel = Constants.ACTIVATION_PRODUCT_MODEL,
            EquipmentVersion = Constants.VERSION_FULL,
            CpuId = NormalizeHardwareValue(cpuId, "CPU"),
            HdId = NormalizeHardwareValue(diskId, "DISK"),
            BiosId = NormalizeHardwareValue(biosId, "BIOS"),
            MacAddress = NormalizeHardwareValue(GetMacAddress(), "MAC"),
            OsVersion = GetOsVersion(),
            UniqCode = CreateMachineCode(cpuId, diskId),
            Connect = true
        };
    }

    public string GenerateLicenseKey(SoftKey softKey)
    {
        var data =
            $"biosId={softKey.BiosId}&cpuId={softKey.CpuId}&equipmentModel={softKey.EquipmentModel}&equipmentName={softKey.EquipmentName}&hdId={softKey.HdId}&uniqCode={softKey.UniqCode}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(data));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return SplitEvery(hex, 4);
    }

    public async Task<ActivationResult> ActivateOnlineAsync(string productCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productCode))
        {
            return ActivationResult.Failed("请输入产品编号。");
        }

        var softKey = GetCurrentMachineInfo();
        softKey.EquipCode = productCode.Trim();
        softKey.Connect = true;

        try
        {
            var json = JsonSerializer.Serialize(softKey, JsonOptions);
            _logHelper?.Information("开始在线激活", new Dictionary<string, object> { ["ProductCode"] = softKey.EquipCode });
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(Constants.ACTIVATION_ENDPOINT, content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ActivationResult.Failed($"激活服务器响应异常：{(int)response.StatusCode}");
            }

            var serverResult = ParseOnlineActivationResponse(responseText);
            if (!serverResult.IsSuccess)
            {
                return serverResult;
            }

            softKey.LicenseKey = GenerateLicenseKey(softKey);
            WriteLicenseFile(softKey);
            return ActivationResult.Success("激活成功。");
        }
        catch (TaskCanceledException)
        {
            return ActivationResult.Failed("连接激活服务器超时，请检查网络后重试。");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("在线激活失败", ex);
            return ActivationResult.Failed("在线激活失败，请检查网络或稍后重试。");
        }
    }

    public ActivationResult ActivateOffline(string productCode, string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return ActivationResult.Failed("请输入激活码。");
        }

        var softKey = GetCurrentMachineInfo();
        softKey.EquipCode = productCode?.Trim();
        softKey.LicenseKey = licenseKey.Trim();
        softKey.Connect = false;

        var expected = GenerateLicenseKey(softKey);
        if (!string.Equals(softKey.LicenseKey, expected, StringComparison.OrdinalIgnoreCase))
        {
            return ActivationResult.Failed("离线激活失败：请确认激活码是否正确。");
        }

        WriteLicenseFile(softKey);
        return ActivationResult.Success("离线激活成功。");
    }

    private static ActivationResult ParseOnlineActivationResponse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return ActivationResult.Failed("激活服务器未返回有效数据。");
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (root.TryGetProperty("code", out var code) && IsSuccessCode(code))
            {
                return ActivationResult.Success("在线激活成功。");
            }

            if (root.TryGetProperty("msg", out var message))
            {
                return ActivationResult.Failed(message.GetString() ?? "在线激活失败。");
            }
        }
        catch
        {
            // 服务器返回非 JSON 时按失败处理。
        }

        return ActivationResult.Failed("在线激活失败，请确认产品编号是否正确。");
    }

    private static bool IsSuccessCode(JsonElement code)
    {
        return code.ValueKind switch
        {
            JsonValueKind.Number => code.TryGetInt32(out var value) && value == 200,
            JsonValueKind.String => string.Equals(code.GetString(), "200", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private SoftKey? ReadLicenseFile()
    {
        try
        {
            if (!File.Exists(_licenseFilePath))
            {
                return null;
            }

            var compressed = File.ReadAllBytes(_licenseFilePath);
            var json = DecodeBase62(UnzipToString(compressed));
            return JsonSerializer.Deserialize<SoftKey>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logHelper?.Warning($"读取授权文件失败：{ex.Message}");
            return null;
        }
    }

    private void WriteLicenseFile(SoftKey softKey)
    {
        var json = JsonSerializer.Serialize(softKey, JsonOptions);
        File.WriteAllBytes(_licenseFilePath, ZipString(EncodeBase62(json)));
    }

    private static string QueryFirst(string wmiClass, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
            foreach (var item in searcher.Get())
            {
                var value = item[property]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static string GetMacAddress()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT MACAddress, IPEnabled FROM Win32_NetworkAdapterConfiguration");
            foreach (var item in searcher.Get())
            {
                if (item["IPEnabled"] is true && item["MACAddress"] is string mac && !string.IsNullOrWhiteSpace(mac))
                {
                    return mac.Trim();
                }
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static string GetOsVersion()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption, OSArchitecture FROM Win32_OperatingSystem");
            foreach (var item in searcher.Get())
            {
                return $"{item["Caption"]},{item["OSArchitecture"]}";
            }
        }
        catch
        {
        }

        return Environment.OSVersion.VersionString;
    }

    private static string CreateMachineCode(string cpuId, string diskId)
    {
        var source = $"{NormalizeHardwareValue(cpuId, "CPU")}{NormalizeHardwareValue(diskId, "DISK")}";
        if (source.Length < 24)
        {
            source = source.PadRight(24, '0');
        }

        var chars = source.Take(24).ToArray();
        var rotated = new StringBuilder(24);
        for (var i = 0; i < 24; i++)
        {
            rotated.Append(chars[i + 3 >= 24 ? 0 : i + 3]);
        }

        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(rotated.ToString())));
    }

    private static string NormalizeHardwareValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string SplitEvery(string value, int length)
    {
        return string.Join("-", Enumerable.Range(0, (value.Length + length - 1) / length)
            .Select(i => value.Substring(i * length, Math.Min(length, value.Length - i * length))));
    }

    private static byte[] ZipString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress))
        {
            input.CopyTo(gzip);
        }

        return output.ToArray();
    }

    private static string UnzipToString(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(input, CompressionMode.Decompress))
        {
            gzip.CopyTo(output);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private static string EncodeBase62(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string DecodeBase62(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
