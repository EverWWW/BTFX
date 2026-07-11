using System.Windows.Controls;
using BTFX.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BTFX.Views.Dialogs;

/// <summary>
/// AboutDialog.xaml 的交互逻辑。
/// </summary>
public partial class AboutDialog : UserControl
{
    public AboutDialog()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<AboutDialogViewModel>();
        Unloaded += (_, _) => (DataContext as IDisposable)?.Dispose();
    }
}
