using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using BTFX.Helpers;
using MaterialDesignThemes.Wpf;

namespace BTFX.Views.Dialogs;

public partial class AppMessageDialogWindow : Window
{
    private readonly AppDialogButtons _buttons;
    private bool _allowClose;

    public AppMessageDialogWindow(
        string message,
        string title,
        AppDialogButtons buttons,
        AppDialogIcon icon,
        Window? owner)
    {
        DialogMessage = message ?? string.Empty;
        DialogTitle = title ?? string.Empty;
        _buttons = buttons;

        InitializeComponent();
        DataContext = this;
        ConfigureOwner(owner);
        ConfigureIcon(icon);
        ConfigureButtons(buttons);

        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnClosing;
    }

    public string DialogTitle { get; }

    public string DialogMessage { get; }

    public AppDialogResult Result { get; private set; } = AppDialogResult.None;

    private void ConfigureOwner(Window? owner)
    {
        if (owner is null)
        {
            Overlay.Background = System.Windows.Media.Brushes.Transparent;
            Width = 720;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = owner.Left;
        Top = owner.Top;
        Width = owner.ActualWidth > 0 ? owner.ActualWidth : owner.Width;
        Height = owner.ActualHeight > 0 ? owner.ActualHeight : owner.Height;
    }

    private void ConfigureIcon(AppDialogIcon icon)
    {
        var (kind, color) = icon switch
        {
            AppDialogIcon.Warning => (PackIconKind.AlertOutline, "#FF8A00"),
            AppDialogIcon.Error => (PackIconKind.AlertCircleOutline, "#E4004A"),
            AppDialogIcon.Question => (PackIconKind.HelpCircleOutline, "#403B5B"),
            _ => (PackIconKind.InformationOutline, "#403B5B")
        };

        DialogIcon.Kind = kind;
        DialogIcon.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
    }

    private void ConfigureButtons(AppDialogButtons buttons)
    {
        switch (buttons)
        {
            case AppDialogButtons.YesNo:
                SecondaryButton.Content = ResourceText("No", "No");
                PrimaryButton.Content = ResourceText("Yes", "Yes");
                SecondaryButton.Visibility = Visibility.Visible;
                break;
            case AppDialogButtons.OkCancel:
                SecondaryButton.Content = ResourceText("Cancel", "Cancel");
                PrimaryButton.Content = ResourceText("OK", "OK");
                SecondaryButton.Visibility = Visibility.Visible;
                break;
            default:
                PrimaryButton.Content = ResourceText("OK", "OK");
                SecondaryButton.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private static string ResourceText(string key, string fallback)
    {
        return Application.Current?.TryFindResource(key)?.ToString() ?? fallback;
    }

    private void PrimaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        CloseWithResult(_buttons == AppDialogButtons.YesNo ? AppDialogResult.Yes : AppDialogResult.Ok);
    }

    private void SecondaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        CloseWithResult(_buttons == AppDialogButtons.YesNo ? AppDialogResult.No : AppDialogResult.Cancel);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        CloseWithResult(DefaultCloseResult());
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        CloseWithResult(DefaultCloseResult());
    }

    private AppDialogResult DefaultCloseResult()
    {
        return _buttons switch
        {
            AppDialogButtons.OkCancel => AppDialogResult.Cancel,
            AppDialogButtons.YesNo => AppDialogResult.No,
            _ => AppDialogResult.Ok
        };
    }

    private void CloseWithResult(AppDialogResult result)
    {
        Result = result;
        _allowClose = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        Result = DefaultCloseResult();
        _allowClose = true;
    }
}
