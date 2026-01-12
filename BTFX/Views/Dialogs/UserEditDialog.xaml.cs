using System.Windows.Controls;
using BTFX.ViewModels;

namespace BTFX.Views.Dialogs;

/// <summary>
/// UserEditDialog.xaml 的交互逻辑
/// </summary>
public partial class UserEditDialog : UserControl
{
    public UserEditDialog()
    {
        InitializeComponent();
    }

    public UserEditDialog(UserEditViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is UserEditViewModel vm)
        {
            vm.Password = PasswordBox.Password;
        }
    }

    private void ConfirmPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is UserEditViewModel vm)
        {
            vm.ConfirmPassword = ConfirmPasswordBox.Password;
        }
    }
}
