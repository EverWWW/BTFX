using System.Windows.Controls;
using BTFX.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BTFX.Views.Dialogs;

public partial class DahengCameraTestDialog : UserControl
{
    private bool _loadedOnce;

    public DahengCameraTestDialog()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<DahengCameraTestDialogViewModel>();
    }

    private async void DahengCameraTestDialog_OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_loadedOnce)
        {
            return;
        }

        _loadedOnce = true;
        if (DataContext is DahengCameraTestDialogViewModel viewModel)
        {
            await viewModel.RefreshDevicesCommand.ExecuteAsync(null);
        }
    }

    private void DahengCameraTestDialog_OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
