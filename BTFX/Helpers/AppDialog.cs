using System.Windows;
using BTFX.Views.Dialogs;

namespace BTFX.Helpers;

public static class AppDialog
{
    public static AppDialogResult Show(
        string message,
        string title,
        AppDialogButtons buttons = AppDialogButtons.Ok,
        AppDialogIcon icon = AppDialogIcon.Information)
    {
        var application = Application.Current;
        if (application?.Dispatcher is null || application.Dispatcher.CheckAccess())
        {
            return ShowCore(message, title, buttons, icon);
        }

        return application.Dispatcher.Invoke(() => ShowCore(message, title, buttons, icon));
    }

    private static AppDialogResult ShowCore(
        string message,
        string title,
        AppDialogButtons buttons,
        AppDialogIcon icon)
    {
        var owner = FindOwner();
        var dialog = new AppMessageDialogWindow(message, title, buttons, icon, owner);
        dialog.ShowDialog();
        return dialog.Result;
    }

    private static Window? FindOwner()
    {
        var application = Application.Current;
        if (application is null)
        {
            return null;
        }

        return application.Windows
                   .OfType<Window>()
                   .FirstOrDefault(window => window.IsActive && window.IsVisible) ??
               (application.MainWindow?.IsVisible == true ? application.MainWindow : null);
    }
}
