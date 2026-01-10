using System.Collections.ObjectModel;
using System.Windows.Threading;
using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.ViewModels;

/// <summary>
/// 设备连接状态
/// </summary>
public enum DeviceConnectionStatus
{
    /// <summary>
    /// 未连接
    /// </summary>
    Disconnected,

    /// <summary>
    /// 连接中
    /// </summary>
    Connecting,

    /// <summary>
    /// 已连接
    /// </summary>
    Connected,

    /// <summary>
    /// 连接失败
    /// </summary>
    ConnectionFailed
}

/// <summary>
/// 主界面容器ViewModel
/// </summary>
public partial class MainContainerViewModel : ObservableObject, IDisposable
{
    private readonly INavigationService _navigationService;
    private readonly ISessionService _sessionService;
    private readonly ILogHelper? _logHelper;
    private readonly DispatcherTimer _timeUpdateTimer;
    private bool _disposed;

    #region 导航栏属性

    /// <summary>
    /// 导航栏是否展开
    /// </summary>
    [ObservableProperty]
    private bool _isNavigationOpen = true;

    /// <summary>
    /// 导航菜单项
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<NavigationItem> _navigationItems = new();

    /// <summary>
    /// 选中的导航项
    /// </summary>
    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    #endregion

    #region 患者信息属性

    /// <summary>
    /// 当前患者姓名
    /// </summary>
    [ObservableProperty]
    private string _currentPatientName = string.Empty;

    /// <summary>
    /// 患者性别
    /// </summary>
    [ObservableProperty]
    private string _currentPatientGender = string.Empty;

    /// <summary>
    /// 患者年龄
    /// </summary>
    [ObservableProperty]
    private int? _currentPatientAge;

    /// <summary>
    /// 是否有当前患者
    /// </summary>
    [ObservableProperty]
    private bool _hasCurrentPatient;

    /// <summary>
    /// 是否游客模式
    /// </summary>
    [ObservableProperty]
    private bool _isGuestMode;

    #endregion

    #region 状态栏属性

    /// <summary>
    /// 当前时间
    /// </summary>
    [ObservableProperty]
    private string _currentTime = string.Empty;

    /// <summary>
    /// 设备状态
    /// </summary>
    [ObservableProperty]
    private DeviceConnectionStatus _deviceStatus = DeviceConnectionStatus.Disconnected;

    /// <summary>
    /// 设备状态颜色
    /// </summary>
    public string DeviceStatusColor => DeviceStatus switch
    {
        DeviceConnectionStatus.Connected => "#4CAF50",      // 绿色
        DeviceConnectionStatus.Connecting => "#FF9800",     // 橙色
        DeviceConnectionStatus.ConnectionFailed => "#F44336", // 红色
        _ => "#9E9E9E"                                       // 灰色
    };

    /// <summary>
    /// 设备状态文字
    /// </summary>
    public string DeviceStatusText => DeviceStatus switch
    {
        DeviceConnectionStatus.Connected => "已连接",
        DeviceConnectionStatus.Connecting => "连接中...",
        DeviceConnectionStatus.ConnectionFailed => "连接失败",
        _ => "未连接"
    };

    #endregion

    #region 内容区域属性

    /// <summary>
    /// 当前内容视图
    /// </summary>
    [ObservableProperty]
    private object? _currentContent;

    /// <summary>
    /// 当前用户名
    /// </summary>
    [ObservableProperty]
    private string _currentUsername = string.Empty;

    #endregion

    /// <summary>
    /// 构造函数
    /// </summary>
    public MainContainerViewModel(
        INavigationService navigationService,
        ISessionService sessionService)
    {
        _navigationService = navigationService;
        _sessionService = sessionService;

        // 尝试获取日志服务
        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }

        // 初始化导航菜单
        InitializeNavigationItems();

        // 初始化时间更新定时器
        _timeUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timeUpdateTimer.Tick += OnTimeUpdateTick;
        _timeUpdateTimer.Start();

        // 更新当前时间
        UpdateCurrentTime();

        // 订阅会话变化事件
        _sessionService.CurrentPatientChanged += OnCurrentPatientChanged;
        _sessionService.CurrentUserChanged += OnCurrentUserChanged;

        // 初始化当前状态
        UpdateUserInfo();
        UpdatePatientInfo();

        _logHelper?.Information("MainContainerViewModel 初始化完成");
    }

    /// <summary>
    /// 初始化导航菜单项
    /// </summary>
    private void InitializeNavigationItems()
    {
        var isGuest = _sessionService.IsGuestMode;

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new NavigationItem
            {
                Key = "Measurement",
                Title = "测量评估",
                IconKind = "ChartLine",
                IsEnabled = true,
                ViewModelName = "MeasurementViewModel"
            },
            new NavigationItem
            {
                Key = "DataManagement",
                Title = "数据管理",
                IconKind = "Database",
                IsEnabled = !isGuest,
                ViewModelName = "DataManagementViewModel"
            },
            new NavigationItem
            {
                Key = "Report",
                Title = "报告",
                IconKind = "FileDocument",
                IsEnabled = !isGuest,
                ViewModelName = "ReportViewModel"
            },
            new NavigationItem
            {
                Key = "Settings",
                Title = "系统设置",
                IconKind = "Cog",
                IsEnabled = true,
                ViewModelName = "SettingsViewModel"
            }
        };

        // 默认选中第一项
        if (NavigationItems.Count > 0)
        {
            SelectedNavigationItem = NavigationItems[0];
            SelectedNavigationItem.IsSelected = true;
            // 加载默认子视图
            LoadSubView(SelectedNavigationItem.ViewModelName);
        }
    }

    /// <summary>
    /// 更新用户信息
    /// </summary>
    private void UpdateUserInfo()
    {
        var user = _sessionService.CurrentUser;
        IsGuestMode = _sessionService.IsGuestMode;
        CurrentUsername = user?.Username ?? "游客";

        // 游客模式下禁用部分菜单
        foreach (var item in NavigationItems)
        {
            if (item.Key == "DataManagement" || item.Key == "Report")
            {
                item.IsEnabled = !IsGuestMode;
            }
        }
    }

    /// <summary>
    /// 更新患者信息
    /// </summary>
    private void UpdatePatientInfo()
    {
        var patient = _sessionService.CurrentPatient;
        HasCurrentPatient = patient != null;

        if (patient != null)
        {
            CurrentPatientName = patient.Name;
            CurrentPatientGender = patient.Gender == Gender.Male ? "男" : "女";
            CurrentPatientAge = patient.Age;
        }
        else
        {
            CurrentPatientName = string.Empty;
            CurrentPatientGender = string.Empty;
            CurrentPatientAge = null;
        }
    }

    /// <summary>
    /// 时间更新事件
    /// </summary>
    private void OnTimeUpdateTick(object? sender, EventArgs e)
    {
        UpdateCurrentTime();
    }

    /// <summary>
    /// 更新当前时间
    /// </summary>
    private void UpdateCurrentTime()
    {
        var now = DateTime.Now;
        var dayOfWeek = now.DayOfWeek switch
        {
            DayOfWeek.Monday => "星期一",
            DayOfWeek.Tuesday => "星期二",
            DayOfWeek.Wednesday => "星期三",
            DayOfWeek.Thursday => "星期四",
            DayOfWeek.Friday => "星期五",
            DayOfWeek.Saturday => "星期六",
            DayOfWeek.Sunday => "星期日",
            _ => string.Empty
        };
        CurrentTime = $"{now:yyyy-MM-dd} {dayOfWeek} {now:HH:mm:ss}";
    }

    /// <summary>
    /// 当前患者变化事件
    /// </summary>
    private void OnCurrentPatientChanged(object? sender, Patient? patient)
    {
        UpdatePatientInfo();
    }

    /// <summary>
    /// 当前用户变化事件
    /// </summary>
    private void OnCurrentUserChanged(object? sender, User? user)
    {
        UpdateUserInfo();
    }

    /// <summary>
    /// 选中导航项变化
    /// </summary>
    partial void OnSelectedNavigationItemChanged(NavigationItem? oldValue, NavigationItem? newValue)
    {
        if (oldValue != null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue != null && newValue.IsEnabled)
        {
            newValue.IsSelected = true;
            _logHelper?.Information($"导航切换到: {newValue.Title}");

            // 加载对应的子视图
            LoadSubView(newValue.ViewModelName);
        }
    }

    /// <summary>
    /// 加载子视图
    /// </summary>
    private void LoadSubView(string viewModelName)
    {
        try
        {
            object? view = viewModelName switch
            {
                "MeasurementViewModel" => App.Services?.GetService(typeof(Views.MeasurementView)),
                "DataManagementViewModel" => App.Services?.GetService(typeof(Views.DataManagementView)),
                "ReportViewModel" => App.Services?.GetService(typeof(Views.ReportView)),
                "SettingsViewModel" => App.Services?.GetService(typeof(Views.SettingsView)),
                _ => null
            };

            if (view != null)
            {
                CurrentContent = view;
                _logHelper?.Information($"加载子视图: {viewModelName}");
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"加载子视图失败: {viewModelName}", ex);
        }
    }

    /// <summary>
    /// 设备状态变化时通知颜色和文字属性
    /// </summary>
    partial void OnDeviceStatusChanged(DeviceConnectionStatus value)
    {
        OnPropertyChanged(nameof(DeviceStatusColor));
        OnPropertyChanged(nameof(DeviceStatusText));
    }

    #region 命令

    /// <summary>
    /// 切换导航栏展开/折叠
    /// </summary>
    [RelayCommand]
    private void ToggleNavigation()
    {
        IsNavigationOpen = !IsNavigationOpen;
    }

    /// <summary>
    /// 导航到指定菜单项
    /// </summary>
    [RelayCommand]
    private void NavigateTo(NavigationItem? item)
    {
        if (item != null && item.IsEnabled)
        {
            SelectedNavigationItem = item;
        }
    }

    /// <summary>
    /// 切换患者
    /// </summary>
    [RelayCommand]
    private async Task SwitchPatientAsync()
    {
        // 提示保存当前工作
        var result = await DialogHost.Show(
            new Views.Dialogs.ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel
                {
                    Title = "切换患者",
                    Message = "切换患者前请确保当前工作已保存。是否继续？"
                }
            },
            "RootDialog");

        if (result is true)
        {
            _logHelper?.Information("用户选择切换患者");
            _sessionService.SetCurrentPatient(null);
            _navigationService.NavigateTo("PatientSelectionViewModel");
        }
    }

    /// <summary>
    /// 退出登录
    /// </summary>
    [RelayCommand]
    private async Task LogoutAsync()
    {
        // 提示保存当前工作
        var result = await DialogHost.Show(
            new Views.Dialogs.ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel
                {
                    Title = "退出登录",
                    Message = IsGuestMode 
                        ? "游客模式下的临时数据将被清除。是否确认退出？"
                        : "退出登录前请确保当前工作已保存。是否确认退出？"
                }
            },
            "RootDialog");

        if (result is true)
        {
            _logHelper?.Information($"用户退出登录: {CurrentUsername}");

            // 清除会话
            _sessionService.ClearSession();

            // 清除导航栈并导航到登录界面
            if (_navigationService is Services.Implementations.NavigationService navService)
            {
                navService.ClearNavigationStack();
            }
            _navigationService.NavigateTo("LoginViewModel");
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _timeUpdateTimer.Stop();
        _timeUpdateTimer.Tick -= OnTimeUpdateTick;

        _sessionService.CurrentPatientChanged -= OnCurrentPatientChanged;
        _sessionService.CurrentUserChanged -= OnCurrentUserChanged;

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// 确认对话框ViewModel
/// </summary>
public partial class ConfirmDialogViewModel : ObservableObject
{
    /// <summary>
    /// 对话框标题
    /// </summary>
    [ObservableProperty]
    private string _title = "确认";

    /// <summary>
    /// 对话框消息
    /// </summary>
    [ObservableProperty]
    private string _message = string.Empty;
}
