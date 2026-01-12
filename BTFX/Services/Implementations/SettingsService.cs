using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

/// <summary>
/// 设置服务实现
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// 当前设置
    /// </summary>
    public AppSettings CurrentSettings { get; private set; } = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public SettingsService()
    {
        var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.CONFIG_DIRECTORY);
        _configFilePath = Path.Combine(configDir, Constants.CONFIG_FILENAME);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // 确保配置目录存在
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
    }

    /// <summary>
    /// 加载设置
    /// </summary>
    public void LoadSettings()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                CurrentSettings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            }
            else
            {
                // 创建默认配置
                CurrentSettings = new AppSettings();
                SaveSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
            CurrentSettings = new AppSettings();
        }
    }

    /// <summary>
    /// 保存设置
    /// </summary>
    public void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(CurrentSettings, _jsonOptions);
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取配置值
    /// </summary>
    public T GetValue<T>(string key, T defaultValue = default!)
    {
        try
        {
            var property = typeof(AppSettings).GetProperty(key);
            if (property != null)
            {
                var value = property.GetValue(CurrentSettings);
                if (value is T typedValue)
                {
                    return typedValue;
                }
            }
        }
        catch
        {
            // 忽略异常
        }

        return defaultValue;
    }

    /// <summary>
    /// 设置配置值
    /// </summary>
    public void SetValue<T>(string key, T value)
    {
        try
        {
            var property = typeof(AppSettings).GetProperty(key);
            if (property != null && property.CanWrite)
            {
                property.SetValue(CurrentSettings, value);
                SaveSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"设置配置值失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 保存登录凭据
    /// </summary>
    public async Task SaveCredentialsAsync(string username, string password)
    {
        CurrentSettings.Credentials.RememberPassword = true;
        CurrentSettings.Credentials.Username = username;
        CurrentSettings.Credentials.PasswordHash = EncryptPassword(password);
        SaveSettings();
        await Task.CompletedTask;
    }

    /// <summary>
    /// 加载登录凭据
    /// </summary>
    public async Task<(string Username, string Password)?> LoadCredentialsAsync()
    {
        if (!CurrentSettings.Credentials.RememberPassword ||
            string.IsNullOrEmpty(CurrentSettings.Credentials.Username) ||
            string.IsNullOrEmpty(CurrentSettings.Credentials.PasswordHash))
        {
            return null;
        }

        var password = DecryptPassword(CurrentSettings.Credentials.PasswordHash);
        return await Task.FromResult((CurrentSettings.Credentials.Username, password));
    }

    /// <summary>
    /// 清除登录凭据
    /// </summary>
    public async Task ClearCredentialsAsync()
    {
        CurrentSettings.Credentials.RememberPassword = false;
        CurrentSettings.Credentials.Username = string.Empty;
        CurrentSettings.Credentials.PasswordHash = string.Empty;
        SaveSettings();
        await Task.CompletedTask;
    }

    /// <summary>
    /// 获取单位名称
    /// </summary>
    public string GetUnitName()
    {
        return CurrentSettings.Unit.Name;
    }

    /// <summary>
    /// 设置单位名称
    /// </summary>
    public void SetUnitName(string name)
    {
        CurrentSettings.Unit.Name = name;
        SaveSettings();
    }

    /// <summary>
    /// 获取Logo路径
    /// </summary>
    public string? GetLogoPath()
    {
        return CurrentSettings.Unit.LogoPath;
    }

    /// <summary>
    /// 设置Logo路径
    /// </summary>
    public void SetLogoPath(string? path)
    {
        CurrentSettings.Unit.LogoPath = path;
        SaveSettings();
    }

    #region 加密解密辅助方法

    // 加密密钥（实际项目中应该使用更安全的方式存储）
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("BTFX2026SecretK!");
    private static readonly byte[] IV = Encoding.UTF8.GetBytes("BTFX2026InitVec!");

    /// <summary>
    /// 加密密码
    /// </summary>
    private static string EncryptPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return string.Empty;

        try
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(password);
            }

            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 解密密码
    /// </summary>
    private static string DecryptPassword(string encryptedPassword)
    {
        if (string.IsNullOrEmpty(encryptedPassword)) return string.Empty;

        try
        {
                        using var aes = Aes.Create();
                        aes.Key = Key;
                        aes.IV = IV;

                        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                        using var ms = new MemoryStream(Convert.FromBase64String(encryptedPassword));
                        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                        using var sr = new StreamReader(cs);

                        return sr.ReadToEnd();
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }

                #endregion

                #region 设置导入导出

                /// <summary>
                /// 导出设置模型（用于导出，不含敏感数据）
                /// </summary>
                private class ExportableSettings
                {
                    public ApplicationSettings? Application { get; set; }
                    public UnitSettings? Unit { get; set; }
                    public string ExportTime { get; set; } = string.Empty;
                    public string AppVersion { get; set; } = string.Empty;
                }

                /// <summary>
                /// 导出设置到文件
                /// </summary>
                public async Task<bool> ExportSettingsAsync(string filePath)
                {
                    try
                    {
                        // 创建可导出的设置（不含凭据等敏感信息）
                        var exportableSettings = new ExportableSettings
                        {
                            Application = CurrentSettings.Application,
                            Unit = CurrentSettings.Unit,
                            ExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            AppVersion = Constants.VERSION_FULL
                        };

                        var json = JsonSerializer.Serialize(exportableSettings, _jsonOptions);
                        await File.WriteAllTextAsync(filePath, json);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"导出设置失败: {ex.Message}");
                        return false;
                    }
                }

                /// <summary>
                /// 从文件导入设置
                /// </summary>
                public async Task<bool> ImportSettingsAsync(string filePath)
                {
                    try
                    {
                        if (!File.Exists(filePath))
                        {
                            return false;
                        }

                        var json = await File.ReadAllTextAsync(filePath);
                        var importedSettings = JsonSerializer.Deserialize<ExportableSettings>(json, _jsonOptions);

                        if (importedSettings == null)
                        {
                            return false;
                        }

                        // 应用导入的设置（保留凭据信息）
                        if (importedSettings.Application != null)
                        {
                            CurrentSettings.Application = importedSettings.Application;
                        }

                        if (importedSettings.Unit != null)
                        {
                            CurrentSettings.Unit = importedSettings.Unit;
                        }

                        SaveSettings();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"导入设置失败: {ex.Message}");
                        return false;
                    }
                }

                #endregion
            }
