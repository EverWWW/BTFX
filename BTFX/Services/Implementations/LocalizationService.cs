using System.Windows;
using BTFX.Common;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

/// <summary>
/// 多语言服务实现
/// </summary>
public class LocalizationService : ILocalizationService
{
    private const string LocalizationResourcePrefix = "Resources/Localization/Strings.";
    private const string LocalizationResourceSuffix = ".xaml";

    private readonly Dictionary<AppLanguage, string> _languageResources = new()
    {
        { AppLanguage.ChineseSimplified, "zh" },
        { AppLanguage.English, "en" }
    };

    /// <summary>
    /// 当前语言
    /// </summary>
    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.ChineseSimplified;

    /// <summary>
    /// 语言变更事件
    /// </summary>
    public event EventHandler<AppLanguage>? LanguageChanged;

    /// <summary>
    /// 应用指定语言
    /// </summary>
    /// <param name="language">语言</param>
    public void ApplyLanguage(AppLanguage language)
    {
        if (!_languageResources.TryGetValue(language, out var cultureName))
        {
            return;
        }

        // 构建资源字典URI
        var resourceUri = new Uri($"{LocalizationResourcePrefix}{cultureName}{LocalizationResourceSuffix}", UriKind.Relative);

        // 查找并移除旧的语言资源字典
        var existingDict = Application.Current.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.Contains("Localization/Strings.") == true);

        if (existingDict != null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(existingDict);
        }

        // 加载新的语言资源字典
        try
        {
            var newDict = new ResourceDictionary { Source = resourceUri };
            Application.Current.Resources.MergedDictionaries.Add(newDict);

            CurrentLanguage = language;
            LanguageChanged?.Invoke(this, language);
        }
        catch (Exception ex)
        {
            // 记录日志
            System.Diagnostics.Debug.WriteLine($"加载语言资源失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="key">资源键</param>
    /// <returns>本地化字符串</returns>
    public string GetString(string key)
    {
        try
        {
            var value = Application.Current.FindResource(key);
            return value?.ToString() ?? key;
        }
        catch
        {
            return key;
        }
    }

    /// <summary>
    /// 获取本地化字符串（带格式化参数）
    /// </summary>
    /// <param name="key">资源键</param>
    /// <param name="args">格式化参数</param>
    /// <returns>本地化字符串</returns>
    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }

    /// <summary>
    /// 获取支持的语言列表
    /// </summary>
    /// <returns>语言列表</returns>
    public IEnumerable<AppLanguage> GetSupportedLanguages()
    {
        return _languageResources.Keys;
    }
}
