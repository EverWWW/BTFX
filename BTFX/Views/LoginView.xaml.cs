using System.Windows;
using System.Windows.Controls;
using BTFX.ViewModels;
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
        // 自动聚焦到账号输入框（如果没有记住密码）
        // 或密码输入框（如果已记住密码）
        if (DataContext is LoginViewModel vm)
        {
            if (string.IsNullOrEmpty(vm.Username))
            {
                // 聚焦账号输入框
                var usernameTextBox = FindName("UsernameTextBox") as TextBox;
                usernameTextBox?.Focus();
            }
            else if (vm.IsPasswordHidden)
            {
                // 聚焦密码框
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
        }
    }
}
