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
                    // 当从明文切换到密码模式时，同步密码到 PasswordBox
                    if (vm.IsPasswordHidden && !string.IsNullOrEmpty(vm.Password))
                    {
                        PasswordBox.Password = vm.Password;
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
        }
    }
}
