using BTFX.Common;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BTFX.ViewModels;

/// <summary>
/// 主窗口ViewModel
/// </summary>
public class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;

    private string _title = Constants.APP_DISPLAY_NAME;
    private object? _currentView;
    private string _version = Constants.VERSION_DISPLAY;
    private bool _isFullscreen;


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
    /// 构造函数
    /// </summary>
    public MainWindowViewModel(
        INavigationService navigationService,
        ISettingsService settingsService,
        ILocalizationService localizationService)
    {
            _navigationService = navigationService;
            _settingsService = settingsService;
            _localizationService = localizationService;

            // 初始化命令
            ToggleFullscreenCommand = new RelayCommand(ToggleFullscreen);
            ExitFullscreenCommand = new RelayCommand(ExitFullscreen);

            // 监听导航服务的视图变化
            if (_navigationService is ObservableObject observableNavigation)
            {
                observableNavigation.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(INavigationService.CurrentView))
                    {
                        CurrentView = _navigationService.CurrentView;
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
}
