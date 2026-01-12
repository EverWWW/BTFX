using BTFX.Models;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 设置服务接口
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// 当前设置
    /// </summary>
    AppSettings CurrentSettings { get; }

    /// <summary>
    /// 加载设置
    /// </summary>
    void LoadSettings();

    /// <summary>
    /// 保存设置
    /// </summary>
    void SaveSettings();

    /// <summary>
    /// 获取配置值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">配置键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>配置值</returns>
    T GetValue<T>(string key, T defaultValue = default!);

    /// <summary>
    /// 设置配置值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">配置键</param>
    /// <param name="value">配置值</param>
    void SetValue<T>(string key, T value);

    /// <summary>
    /// 保存登录凭据（记住密码）
    /// </summary>
    /// <param name="username">账号</param>
    /// <param name="password">密码</param>
    Task SaveCredentialsAsync(string username, string password);

    /// <summary>
    /// 加载登录凭据
    /// </summary>
    /// <returns>账号和密码元组，如果没有保存则返回null</returns>
    Task<(string Username, string Password)?> LoadCredentialsAsync();

    /// <summary>
    /// 清除登录凭据
    /// </summary>
    Task ClearCredentialsAsync();

    /// <summary>
    /// 获取单位名称
    /// </summary>
    string GetUnitName();

    /// <summary>
    /// 设置单位名称
    /// </summary>
    void SetUnitName(string name);

        /// <summary>
        /// 获取Logo路径
        /// </summary>
        string? GetLogoPath();

        /// <summary>
        /// 设置Logo路径
        /// </summary>
        void SetLogoPath(string? path);

        /// <summary>
        /// 导出设置到文件
        /// </summary>
        /// <param name="filePath">导出文件路径</param>
        /// <returns>是否成功</returns>
        Task<bool> ExportSettingsAsync(string filePath);

        /// <summary>
        /// 从文件导入设置
        /// </summary>
        /// <param name="filePath">导入文件路径</param>
        /// <returns>是否成功</returns>
        Task<bool> ImportSettingsAsync(string filePath);
    }
