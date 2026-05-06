using System.Windows;
using System.Windows.Controls;
using BTFX.ViewModels;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace BTFX.Views;

/// <summary>
/// LoginView.xaml 的交互逻辑
/// </summary>
public partial class LoginView : UserControl
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public LoginView()
    {
        InitializeComponent();

        // 从DI容器获取ViewModel
        DataContext = App.Services.GetRequiredService<LoginViewModel>();

        Loaded += LoginView_Loaded;
    }

    /// <summary>
    /// 视图加载完成
    /// </summary>
    private void LoginView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            // 如果 ViewModel 中有记住的密码，同步到 PasswordBox
            if (!string.IsNullOrEmpty(vm.Password))
            {
                PasswordBox.Password = vm.Password;
            }

            // 监听密码可见性变化
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(LoginViewModel.IsPasswordHidden))
                {
                    if (vm.IsPasswordHidden)
                    {
                        // 密文模式：同步密码到PasswordBox，并更新Tag驱动水印
                        PasswordBox.Password = vm.Password ?? "";
                        PasswordBox.Tag = (vm.Password?.Length ?? 0) > 0 ? "1" : "";
                        PasswordBox.Focus();
                    }
                    else
                    {
                        // 明文模式：聚焦PasswordTextBox，将光标移到末尾
                        PasswordTextBox.Focus();
                        PasswordTextBox.CaretIndex = PasswordTextBox.Text?.Length ?? 0;
                    }
                }
            };

            // 焦点逻辑
            if (string.IsNullOrEmpty(vm.Username))
            {
                UsernameTextBox?.Focus();
            }
            else if (vm.IsPasswordHidden)
            {
                PasswordBox.Focus();
            }
        }
    }

    /// <summary>
    /// PasswordBox密码变化处理（因为PasswordBox的Password属性不支持绑定）
    /// </summary>
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is PasswordBox passwordBox)
        {
            vm.Password = passwordBox.Password;
            // 更新Tag标记，驱动水印显隐（有密码时Tag!=""?水印Collapsed）
            passwordBox.Tag = passwordBox.Password.Length > 0 ? "1" : "";
        }
    }

    /// <summary>
    /// 关闭按钮点击事件
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    /// <summary>
    /// 打开临时相机录制测试界面。
    /// </summary>
    private async void CameraRecordTestButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = App.Services.GetRequiredService<BTFX.Testing.CameraRecordingTestDialog>();
        await DialogHost.Show(dialog, "RootDialog");
    }

    /// <summary>
    /// 点击背景空白区域拖动窗口
    /// </summary>
    private void LoginBackground_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 判断点击位置是否在主卡片范围内，在卡片外才触发拖动
        if (e.OriginalSource is System.Windows.DependencyObject source)
        {
            // 向上遍历视觉树，若命中 MainCard 则说明点击在卡片内，不触发拖动
            var hit = source;
            while (hit != null)
            {
                if (ReferenceEquals(hit, MainCard))
                    return;
                hit = System.Windows.Media.VisualTreeHelper.GetParent(hit) as System.Windows.DependencyObject;
            }
        }

        var window = Window.GetWindow(this);
        if (window == null) return;

        // 最大化状态下先还原，再拖动（DragMove 在最大化时会抛异常）
        if (window.WindowState == WindowState.Maximized)
        {
            // 获取鼠标相对屏幕的位置，还原后将窗口定位到鼠标附近
            var mousePos = e.GetPosition(window);
            var screenPos = window.PointToScreen(mousePos);
            window.WindowState = WindowState.Normal;
            window.Left = screenPos.X - window.Width / 2;
            window.Top = screenPos.Y - 20;
        }

        window.DragMove();
    }
}
