namespace BTFX.Testing;

/// <summary>
/// DevTestView.xaml 的交互逻辑
/// </summary>
public partial class DevTestView : System.Windows.Controls.UserControl
{
    public DevTestView(DevTestViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
