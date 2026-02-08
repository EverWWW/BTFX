using BTFX.Common;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 主题服务接口
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// 当前主题
    /// </summary>
    AppTheme CurrentTheme { get; }

    /// <summary>
    /// 当前主题色（十六进制）
    /// </summary>
    string CurrentPrimaryColor { get; }

    /// <summary>
    /// 主题变更事件
    /// </summary>
    event EventHandler<AppTheme>? ThemeChanged;

    /// <summary>
    /// 主题色变更事件
    /// </summary>
    event EventHandler<string>? PrimaryColorChanged;

    /// <summary>
    /// 应用指定主题
    /// </summary>
    /// <param name="theme">主题</param>
    void ApplyTheme(AppTheme theme);

    /// <summary>
    /// 切换主题（亮色/深色）
    /// </summary>
    void ToggleTheme();

    /// <summary>
    /// 设置主题色
    /// </summary>
    /// <param name="primaryColor">主题色</param>
    void SetPrimaryColor(System.Windows.Media.Color primaryColor);
}
