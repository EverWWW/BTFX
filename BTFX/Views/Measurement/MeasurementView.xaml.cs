using System.Windows.Controls;
using BTFX.ViewModels.Measurement;

namespace BTFX.Views.Measurement;

/// <summary>
/// MeasurementView.xaml 的交互逻辑
/// </summary>
public partial class MeasurementView : UserControl
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public MeasurementView()
    {
        InitializeComponent();

        // 从 DI 容器获取 ViewModel
        DataContext = App.Services?.GetService(typeof(MeasurementViewModel));
    }
}
