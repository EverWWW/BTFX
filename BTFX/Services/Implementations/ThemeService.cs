using System.Windows;
using System.Windows.Media;
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
    /// 当前主题色（十六进制）
    /// </summary>
    public string CurrentPrimaryColor { get; private set; } = "#FF009EDB";

    /// <summary>
    /// 主题变更事件
    /// </summary>
    public event EventHandler<AppTheme>? ThemeChanged;

    /// <summary>
    /// 主题色变更事件
    /// </summary>
    public event EventHandler<string>? PrimaryColorChanged;

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

        RefreshWindows();
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
    public void SetPrimaryColor(Color primaryColor)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetPrimaryColor(primaryColor);
        paletteHelper.SetTheme(theme);

        // 同步更新 Application 级别的 Brush 资源
        Application.Current.Dispatcher.Invoke(() =>
        {
            Application.Current.Resources["MaterialDesign.Brush.Primary"] = new SolidColorBrush(primaryColor);
        });

        CurrentPrimaryColor = primaryColor.ToString();
        PrimaryColorChanged?.Invoke(this, CurrentPrimaryColor);

        RefreshWindows();
    }

    /// <summary>
    /// 设置副色调
    /// </summary>
    /// <param name="secondaryColor">副色调</param>
    public void SetSecondaryColor(Color secondaryColor)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetSecondaryColor(secondaryColor);
        paletteHelper.SetTheme(theme);
    }

    /// <summary>
    /// 强制刷新所有打开的窗口
    /// </summary>
    private static void RefreshWindows()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (Window window in Application.Current.Windows)
            {
                try
                {
                    window.UpdateLayout();
                    window.InvalidateVisual();
                }
                catch
                {
                    // 忽略单个窗口刷新失败
                }
            }
        });
    }
}
