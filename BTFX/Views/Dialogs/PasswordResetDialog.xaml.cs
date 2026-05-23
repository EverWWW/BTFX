using System.Windows.Controls;
using BTFX.ViewModels;

namespace BTFX.Views.Dialogs;

public partial class PasswordResetDialog : UserControl
{
    public PasswordResetDialog()
    {
        InitializeComponent();
    }

    public PasswordResetDialog(PasswordResetDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void PasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is PasswordResetDialogViewModel vm && sender is PasswordBox box)
        {
            vm.Password = box.Password;
        }
    }

    private void ConfirmPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is PasswordResetDialogViewModel vm && sender is PasswordBox box)
        {
            vm.ConfirmPassword = box.Password;
        }
    }
}
