using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        AttachNumericPasswordInputFilter(PasswordBox);
        AttachNumericPasswordInputFilter(ConfirmPasswordBox);
    }

    public UserEditDialog(UserEditViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UserEditViewModel vm)
        {
            vm.Password = PasswordBox.Password;
        }
    }

    private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UserEditViewModel vm)
        {
            vm.ConfirmPassword = ConfirmPasswordBox.Password;
        }
    }

    private static void AttachNumericPasswordInputFilter(PasswordBox passwordBox)
    {
        InputMethod.SetIsInputMethodEnabled(passwordBox, false);
        passwordBox.PreviewTextInput += PasswordBox_PreviewTextInput;
        passwordBox.PreviewKeyDown += PasswordBox_PreviewKeyDown;
        DataObject.AddPastingHandler(passwordBox, PasswordBox_OnPaste);
    }

    private static void PasswordBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(c => !char.IsDigit(c));
    }

    private static void PasswordBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private static void PasswordBox_OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var text = e.DataObject.GetData(DataFormats.Text) as string;
        if (string.IsNullOrEmpty(text) || text.Any(c => !char.IsDigit(c)))
        {
            e.CancelCommand();
        }
    }
}
