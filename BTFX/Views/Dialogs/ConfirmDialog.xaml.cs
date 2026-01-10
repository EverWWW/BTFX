using System.Windows.Controls;

namespace BTFX.Views.Dialogs;

/// <summary>
/// ConfirmDialog.xaml 的交互逻辑
/// </summary>
public partial class ConfirmDialog : UserControl
{
    /// <summary>
    /// 返回true的静态值
    /// </summary>
    public static readonly object TrueValue = true;

    /// <summary>
    /// 返回false的静态值
    /// </summary>
    public static readonly object FalseValue = false;

    public ConfirmDialog()
    {
        InitializeComponent();
    }
}
