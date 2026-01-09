using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using BTFX.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BTFX;

/// <summary>
/// MainWindow.xaml 的交互逻辑
/// </summary>
public partial class MainWindow : Window
{
    #region Win32 API

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private const int WM_SYSCOMMAND = 0x112;
    private const int SC_SIZE = 0xF000;

    // 窗口大小调整方向
    private enum ResizeDirection
    {
        Left = 1,
        Right = 2,
        Top = 3,
        TopLeft = 4,
        TopRight = 5,
        Bottom = 6,
        BottomLeft = 7,
        BottomRight = 8
    }

    #endregion

    private WindowState _previousWindowState = WindowState.Normal;

    /// <summary>
    /// 构造函数
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        // 从DI容器获取ViewModel
        DataContext = App.Services.GetRequiredService<MainWindowViewModel>();

        // 订阅状态变化
        StateChanged += MainWindow_StateChanged;
    }

    /// <summary>
    /// 窗口状态变化处理
    /// </summary>
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        // 更新最大化图标
        MaximizeIcon.Kind = WindowState == WindowState.Maximized
            ? MaterialDesignThemes.Wpf.PackIconKind.WindowRestore
            : MaterialDesignThemes.Wpf.PackIconKind.WindowMaximize;
    }

    /// <summary>
    /// 标题栏拖动
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 双击切换最大化/还原
            ToggleMaximize();
        }
        else
        {
            // 拖动窗口
            if (WindowState == WindowState.Maximized)
            {
                // 从最大化状态拖动时，先还原窗口
                var point = e.GetPosition(this);
                _previousWindowState = WindowState.Normal;
                WindowState = WindowState.Normal;

                // 调整窗口位置，使鼠标保持在相对位置
                Left = point.X - Width / 2;
                Top = point.Y - 20;
            }

            DragMove();
        }
    }

    /// <summary>
    /// 最小化按钮点击
    /// </summary>
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// 最大化/还原按钮点击
    /// </summary>
    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    /// <summary>
    /// 关闭按钮点击
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // 显示确认对话框
        var result = MessageBox.Show(
            FindResource("ExitConfirmMessage")?.ToString() ?? "确认退出系统？",
            FindResource("ConfirmExit")?.ToString() ?? "确认退出",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            Application.Current.Shutdown();
        }
    }

    /// <summary>
    /// 切换最大化/还原
    /// </summary>
    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
        else
        {
            _previousWindowState = WindowState;
            WindowState = WindowState.Maximized;
        }
    }

    /// <summary>
    /// 窗口边框拖动调整大小
    /// </summary>
    private void Resize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (WindowState == WindowState.Maximized) return;

        var element = sender as FrameworkElement;
        if (element == null) return;

        var direction = element.Name switch
        {
            "ResizeLeft" => ResizeDirection.Left,
            "ResizeRight" => ResizeDirection.Right,
            "ResizeTop" => ResizeDirection.Top,
            "ResizeBottom" => ResizeDirection.Bottom,
            "ResizeTopLeft" => ResizeDirection.TopLeft,
            "ResizeTopRight" => ResizeDirection.TopRight,
            "ResizeBottomLeft" => ResizeDirection.BottomLeft,
            "ResizeBottomRight" => ResizeDirection.BottomRight,
            _ => ResizeDirection.Right
        };

        ResizeWindow(direction);
    }

    /// <summary>
    /// 调整窗口大小
    /// </summary>
    private void ResizeWindow(ResizeDirection direction)
    {
        var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        if (hwndSource != null)
        {
            SendMessage(hwndSource.Handle, WM_SYSCOMMAND, (IntPtr)(SC_SIZE + direction), IntPtr.Zero);
        }
    }

    /// <summary>
    /// 键盘按下事件处理
    /// </summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // ESC键退出全屏
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel vm && vm.IsFullscreen)
        {
            vm.ExitFullscreenCommand.Execute(null);
            e.Handled = true;
        }
    }
}