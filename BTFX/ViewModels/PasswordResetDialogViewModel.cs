using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;

namespace BTFX.ViewModels;

public partial class PasswordResetDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "重置密码";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    private string _validationError = string.Empty;

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.CloseDialogCommand.Execute(null, null);
    }

    [RelayCommand]
    private void Confirm()
    {
        ValidationError = string.Empty;

        if (string.IsNullOrWhiteSpace(Password))
        {
            ValidationError = "登录密码不能为空";
            return;
        }

        if (!Password.All(char.IsDigit))
        {
            ValidationError = "密码由纯数字组成";
            return;
        }

        if (Password.Length < 6)
        {
            ValidationError = "密码长度至少为6位";
            return;
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ValidationError = "密码不一致！";
            return;
        }

        DialogHost.CloseDialogCommand.Execute(Password, null);
    }
}
