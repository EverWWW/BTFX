using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace BTFX.Testing;

public partial class CameraRecordingTestDialog : UserControl
{
    public CameraRecordingTestDialog()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<CameraRecordingTestViewModel>();
    }
}
