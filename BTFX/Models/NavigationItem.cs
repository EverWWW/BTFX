using CommunityToolkit.Mvvm.ComponentModel;

namespace BTFX.Models;

/// <summary>
/// 导航菜单项模型
/// </summary>
public partial class NavigationItem : ObservableObject
{
    /// <summary>
    /// 菜单项标识
    /// </summary>
    [ObservableProperty]
    private string _key = string.Empty;

    /// <summary>
    /// 显示标题
    /// </summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>
    /// 图标名称（Material Design Icon）
    /// </summary>
    [ObservableProperty]
    private string _iconKind = string.Empty;

    /// <summary>
    /// 是否选中
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// 是否启用
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>
    /// 对应的ViewModel类型名称
    /// </summary>
    public string ViewModelName { get; set; } = string.Empty;
}
