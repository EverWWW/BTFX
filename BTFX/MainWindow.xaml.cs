using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
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

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const int WM_SYSCOMMAND = 0x112;
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int SC_SIZE = 0xF000;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

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

        // 初始化最大化图标状态
        MaximizeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.WindowRestore;

        // 订阅状态变化
        StateChanged += MainWindow_StateChanged;
        SourceInitialized += MainWindow_SourceInitialized;
    }

    /// <summary>
    /// 窗口句柄初始化后挂载 WndProc，确保最大化时不遮挡任务栏
    /// </summary>
    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    var work = monitorInfo.rcWork;
                    var mon = monitorInfo.rcMonitor;
                    mmi.ptMaxPosition = new POINT { X = work.Left - mon.Left, Y = work.Top - mon.Top };
                    mmi.ptMaxSize = new POINT { X = work.Right - work.Left, Y = work.Bottom - work.Top };
                }
            }
            Marshal.StructureToPtr(mmi, lParam, true);
            handled = true;
        }
        return IntPtr.Zero;
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