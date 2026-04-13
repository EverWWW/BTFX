using System.Windows;
using System.Windows.Controls;

namespace BTFX.Controls;

/// <summary>
/// 顶部标题栏公共组件，供登录页之外所有页面共用。
/// 通过 <see cref="UserDisplayName"/> 依赖属性显示当前登录账号名；
/// 未赋值时默认显示"游客"。
/// 最小化 / 关闭事件通过冒泡路由向上传递给宿主 Window 处理。
/// </summary>
public partial class TitleBarControl : UserControl
{
    /// <summary>
    /// 当前显示的账号名依赖属性。默认值"游客"。
    /// </summary>
    public static readonly DependencyProperty UserDisplayNameProperty =
        DependencyProperty.Register(
            nameof(UserDisplayName),
            typeof(string),
            typeof(TitleBarControl),
            new PropertyMetadata("游客"));

    /// <summary>
    /// 当前账号名。可在 XAML 中通过 Binding 或在代码中直接设置。
    /// 未赋值时显示"游客"。
    /// </summary>
    public string UserDisplayName
    {
        get => (string)GetValue(UserDisplayNameProperty);
        set => SetValue(UserDisplayNameProperty, value);
    }

    /// <summary>
    /// 最小化按钮点击路由事件，宿主 Window 可订阅或由组件内部直接查找父窗口处理。
    /// </summary>
    public static readonly RoutedEvent MinimizeClickedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(MinimizeClicked),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(TitleBarControl));

    /// <summary>
    /// 最小化按钮点击事件。
    /// </summary>
    public event RoutedEventHandler MinimizeClicked
    {
        add => AddHandler(MinimizeClickedEvent, value);
        remove => RemoveHandler(MinimizeClickedEvent, value);
    }

    /// <summary>
    /// 关闭按钮点击路由事件。
    /// </summary>
    public static readonly RoutedEvent CloseClickedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(CloseClicked),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(TitleBarControl));

    /// <summary>
    /// 关闭按钮点击事件。
    /// </summary>
    public event RoutedEventHandler CloseClicked
    {
        add => AddHandler(CloseClickedEvent, value);
        remove => RemoveHandler(CloseClickedEvent, value);
    }

    public TitleBarControl()
    {
        InitializeComponent();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        // 先触发路由事件，允许宿主监听
        RaiseEvent(new RoutedEventArgs(MinimizeClickedEvent, this));

        // 同时直接操作父窗口（兜底，无需宿主额外订阅）
        if (Window.GetWindow(this) is { } window)
            window.WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // 先触发路由事件，允许宿主监听（例如弹确认框）
        var routedArgs = new RoutedEventArgs(CloseClickedEvent, this);
        RaiseEvent(routedArgs);

        // 若宿主未处理（Handled=false），则直接操作父窗口
        if (!routedArgs.Handled)
        {
            var window = Window.GetWindow(this);
            window?.Close();
        }
    }
}
