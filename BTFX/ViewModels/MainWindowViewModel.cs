using BTFX.Common;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Threading;

namespace BTFX.ViewModels;

/// <summary>
/// 主窗口ViewModel
/// </summary>
public class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly ISessionService _sessionService;
    private readonly IGaitAnalysisService _analysisService;
    private readonly DispatcherTimer _analysisStateTimer;

    private string _title = Constants.APP_DISPLAY_NAME;
    private object? _currentView;
    private string _version = Constants.VERSION_DISPLAY;
    private bool _isFullscreen;
    private string _userDisplayName = "游客";


    /// <summary>
    /// 窗口标题
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// 当前视图
    /// </summary>
    public object? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    /// <summary>
    /// 是否全屏
    /// </summary>
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => SetProperty(ref _isFullscreen, value);
    }

    /// <summary>
    /// 切换全屏命令
    /// </summary>
    public IRelayCommand ToggleFullscreenCommand { get; }

    /// <summary>
    /// 退出全屏命令
    /// </summary>
    public IRelayCommand ExitFullscreenCommand { get; }

    /// <summary>
    /// 退出登录命令
    /// </summary>
    public IAsyncRelayCommand LogoutCommand { get; }

    /// <summary>
    /// 当前显示的账号名，登录后自动更新
    /// </summary>
    public string UserDisplayName
    {
        get => _userDisplayName;
        set => SetProperty(ref _userDisplayName, value);
    }

    /// <summary>
    /// 是否正在显示登录页（登录页时隐藏标题栏）
    /// </summary>
    public bool IsLoginView => _navigationService.CurrentViewKey == "LoginViewModel"
                               || _navigationService.CurrentViewKey == "ActivationViewModel"
                               || string.IsNullOrEmpty(_navigationService.CurrentViewKey);

    /// <summary>
    /// 构造函数
    /// </summary>
    public MainWindowViewModel(
        INavigationService navigationService,
        ISettingsService settingsService,
        ILocalizationService localizationService,
        ISessionService sessionService,
        IGaitAnalysisService analysisService)
    {
            _navigationService = navigationService;
            _settingsService = settingsService;
            _localizationService = localizationService;
            _sessionService = sessionService;
            _analysisService = analysisService;

            // 初始化命令
            ToggleFullscreenCommand = new RelayCommand(ToggleFullscreen);
            ExitFullscreenCommand = new RelayCommand(ExitFullscreen);
            LogoutCommand = new AsyncRelayCommand(LogoutAsync, () => !IsLoginView && !_analysisService.IsAnalysisRunning);
            _analysisStateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _analysisStateTimer.Tick += (_, _) => LogoutCommand.NotifyCanExecuteChanged();
            _analysisStateTimer.Start();

            // 监听导航服务的视图变化
            if (_navigationService is ObservableObject observableNavigation)
            {
                observableNavigation.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(INavigationService.CurrentView))
                    {
                        CurrentView = _navigationService.CurrentView;
                    }
                    if (e.PropertyName == nameof(INavigationService.CurrentViewKey))
                    {
                        OnPropertyChanged(nameof(IsLoginView));
                        LogoutCommand.NotifyCanExecuteChanged();
                        RefreshUserDisplayName();
                    }
                };
            }

            // 监听语言变化，更新标题
            _localizationService.LanguageChanged += (s, e) =>
            {
                Title = _localizationService.GetString("AppName");
            };

            // 初始化时立即应用当前语言的标题
            Title = _localizationService.GetString("AppName");
        }

    /// <summary>
    /// 切换全屏
    /// </summary>
    private void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
    }

    /// <summary>
    /// 退出全屏
    /// </summary>
    private void ExitFullscreen()
    {
        IsFullscreen = false;
    }

    /// <summary>
    /// 从全局标题栏退出当前账号。
    /// </summary>
    private async Task LogoutAsync()
    {
        var result = await MaterialDesignThemes.Wpf.DialogHost.Show(
            new Views.Dialogs.ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel
                {
                    Title = "退出登录",
                    Message = "退出登录前请确保当前工作已保存。是否确认退出？",
                    ConfirmText = "确定",
                    CancelText = "取消",
                    IsCancelVisible = true
                }
            },
            "RootDialog");

        if (result is not true)
        {
            return;
        }

        _sessionService.ClearSession();
        if (_navigationService is Services.Implementations.NavigationService navigationService)
        {
            navigationService.ClearNavigationStack();
        }

        _navigationService.NavigateTo("LoginViewModel");
        RefreshUserDisplayName();
    }

    /// <summary>
    /// 从 SessionService 刷新当前账号显示名称
    /// </summary>
    private void RefreshUserDisplayName()
    {
        var user = _sessionService.CurrentUser;
        UserDisplayName = !string.IsNullOrWhiteSpace(user?.Name)
            ? user.Name
            : !string.IsNullOrWhiteSpace(user?.Username)
                ? user.Username
                : "游客";
    }
}
