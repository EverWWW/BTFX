using BTFX.Common;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 多语言服务接口
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// 当前语言
    /// </summary>
    AppLanguage CurrentLanguage { get; }

    /// <summary>
    /// 语言变更事件
    /// </summary>
    event EventHandler<AppLanguage>? LanguageChanged;

    /// <summary>
    /// 应用指定语言
    /// </summary>
    /// <param name="language">语言</param>
    void ApplyLanguage(AppLanguage language);

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="key">资源键</param>
    /// <returns>本地化字符串</returns>
    string GetString(string key);

    /// <summary>
    /// 获取本地化字符串（带格式化参数）
    /// </summary>
    /// <param name="key">资源键</param>
    /// <param name="args">格式化参数</param>
    /// <returns>本地化字符串</returns>
    string GetString(string key, params object[] args);
}
