using BTFX.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BTFX.ViewModels;

/// <summary>
/// 患者列表行包装（含显示序号和选中状态）
/// </summary>
public partial class PatientRowItem : ObservableObject
{
    /// <summary>
    /// 界面显示序号（跨页连续）
    /// </summary>
    public int DisplayIndex { get; init; }

    /// <summary>
    /// 原始患者数据
    /// </summary>
    public Patient Patient { get; init; } = null!;

    /// <summary>
    /// 是否被勾选
    /// </summary>
    [ObservableProperty]
    private bool _isChecked;
}
