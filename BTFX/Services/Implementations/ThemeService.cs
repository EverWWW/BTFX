using System.Windows;
using BTFX.Common;
using BTFX.Services.Interfaces;
using MaterialDesignThemes.Wpf;

namespace BTFX.Services.Implementations;

/// <summary>
/// 主题服务实现
/// </summary>
public class ThemeService : IThemeService
{
    private readonly PaletteHelper _paletteHelper = new();

    /// <summary>
    /// 当前主题
    /// </summary>
    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    /// <summary>
    /// 主题变更事件
    /// </summary>
    public event EventHandler<AppTheme>? ThemeChanged;

    /// <summary>
    /// 应用指定主题
    /// </summary>
    /// <param name="theme">主题</param>
    public void ApplyTheme(AppTheme theme)
    {
        CurrentTheme = theme;

        var paletteHelper = new PaletteHelper();
        var currentTheme = paletteHelper.GetTheme();

        // 设置基础主题
        currentTheme.SetBaseTheme(theme == AppTheme.Dark ? BaseTheme.Dark : BaseTheme.Light);

        // 应用主题
        paletteHelper.SetTheme(currentTheme);

        // 触发事件
        ThemeChanged?.Invoke(this, theme);
    }

    /// <summary>
    /// 切换主题
    /// </summary>
    public void ToggleTheme()
    {
        var newTheme = CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        ApplyTheme(newTheme);
    }

    /// <summary>
    /// 设置主色调
    /// </summary>
    /// <param name="primaryColor">主色调</param>
    public void SetPrimaryColor(System.Windows.Media.Color primaryColor)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetPrimaryColor(primaryColor);
        paletteHelper.SetTheme(theme);
    }

    /// <summary>
    /// 设置副色调
    /// </summary>
    /// <param name="secondaryColor">副色调</param>
    public void SetSecondaryColor(System.Windows.Media.Color secondaryColor)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetSecondaryColor(secondaryColor);
        paletteHelper.SetTheme(theme);
    }
}
