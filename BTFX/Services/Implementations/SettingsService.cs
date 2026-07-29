using System.IO;
using System.Text.Json;
using BTFX.Common;
using BTFX.Helpers;
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
                if (EnsureDefaultSettings())
                {
                    SaveSettings();
                }
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
            EnsureDefaultSettings();
        }
    }

    private bool EnsureDefaultSettings()
    {
        var changed = false;
        if (CurrentSettings.Application == null)
        {
            CurrentSettings.Application = new ApplicationSettings();
            changed = true;
        }
        if (CurrentSettings.Database == null)
        {
            CurrentSettings.Database = new DatabaseSettings();
            changed = true;
        }
        if (CurrentSettings.AutoBackup == null)
        {
            CurrentSettings.AutoBackup = new AutoBackupSettings();
            changed = true;
        }
        if (CurrentSettings.Unit == null)
        {
            CurrentSettings.Unit = new UnitSettings();
            changed = true;
        }
        if (CurrentSettings.Credentials == null)
        {
            CurrentSettings.Credentials = new CredentialsSettings();
            changed = true;
        }
        if (CurrentSettings.Algorithm == null)
        {
            CurrentSettings.Algorithm = new AlgorithmSettings();
            changed = true;
        }
        if (CurrentSettings.Update == null)
        {
            CurrentSettings.Update = new UpdateSettings();
            changed = true;
        }
        if (CurrentSettings.ProductInfo == null)
        {
            CurrentSettings.ProductInfo = new ProductInfoSettings();
            changed = true;
        }
        else if (CurrentSettings.ProductInfo.InternalVersion is
                 "V1.0.0.20260626_alpha01" or
                 "V1.0.0.20260714_alpha01")
        {
            CurrentSettings.ProductInfo.InternalVersion = Constants.VERSION_INTERNAL;
            changed = true;
        }

        return changed;
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
        CurrentSettings.Credentials.PasswordHash = CredentialProtector.Protect(password);
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

        if (!CredentialProtector.TryUnprotect(
                CurrentSettings.Credentials.PasswordHash,
                out var password,
                out var requiresMigration))
        {
            return null;
        }

        if (requiresMigration)
        {
            CurrentSettings.Credentials.PasswordHash = CredentialProtector.Protect(password);
            SaveSettings();
        }

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

                #region 设置导入导出

                /// <summary>
                /// 导出设置模型（用于导出，不含敏感数据）
                /// </summary>
                private class ExportableSettings
                {
                    public ApplicationSettings? Application { get; set; }
                    public UnitSettings? Unit { get; set; }
                    public ProductInfoSettings? ProductInfo { get; set; }
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
                            ProductInfo = CurrentSettings.ProductInfo,
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

                        if (importedSettings.ProductInfo != null)
                        {
                            CurrentSettings.ProductInfo = importedSettings.ProductInfo;
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
