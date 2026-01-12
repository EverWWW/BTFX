using System.Windows.Controls;
using BTFX.ViewModels;

namespace BTFX.Views.Dialogs;

/// <summary>
/// DepartmentEditDialog.xaml 的交互逻辑
/// </summary>
public partial class DepartmentEditDialog : UserControl
{
    public DepartmentEditDialog(DepartmentEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
