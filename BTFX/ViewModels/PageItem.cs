using CommunityToolkit.Mvvm.ComponentModel;

namespace BTFX.ViewModels;

/// <summary>
/// 分页页码项（数字或省略号）
/// </summary>
public partial class PageItem : ObservableObject
{
    /// <summary>
    /// 页码数字，省略号时为 -1
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// 显示文本
    /// </summary>
    public string DisplayText { get; init; } = string.Empty;

    /// <summary>
    /// 是否为省略号（不可点击）
    /// </summary>
    public bool IsEllipsis { get; init; }

    /// <summary>
    /// 是否为当前选中页
    /// </summary>
    [ObservableProperty]
    private bool _isCurrent;
}
