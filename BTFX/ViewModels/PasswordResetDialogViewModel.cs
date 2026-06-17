using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;

namespace BTFX.ViewModels;

public partial class PasswordResetDialogViewModel : ObservableObject
{
    private readonly ILocalizationService? _localizationService;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    private string _validationError = string.Empty;

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);

    public PasswordResetDialogViewModel()
    {
        _localizationService = App.Services?.GetService(typeof(ILocalizationService)) as ILocalizationService;
        _title = L("ResetPassword");
    }

    private string L(string key, params object[] args)
    {
        var value = args.Length == 0
            ? _localizationService?.GetString(key)
            : _localizationService?.GetString(key, args);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

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
            ValidationError = L("PasswordRequired");
            return;
        }

        if (!Password.All(char.IsDigit))
        {
            ValidationError = L("PasswordDigitsOnly");
            return;
        }

        if (Password.Length < 6)
        {
            ValidationError = L("PasswordMinLengthError");
            return;
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ValidationError = L("PasswordMismatch");
            return;
        }

        DialogHost.CloseDialogCommand.Execute(Password, null);
    }
}
