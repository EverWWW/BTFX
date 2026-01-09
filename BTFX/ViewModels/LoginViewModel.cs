using BTFX.Common;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.ViewModels;

/// <summary>
/// 登录界面ViewModel
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly INavigationService _navigationService;
    private readonly ISessionService _sessionService;
    private readonly ISettingsService _settingsService;
    private readonly ILogHelper? _logHelper;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _rememberPassword;

    [ObservableProperty]
    private bool _isPasswordHidden = true;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoggingIn;

    [ObservableProperty]
    private string _version = Constants.VERSION_DISPLAY;

    /// <summary>
    /// 构造函数
    /// </summary>
    public LoginViewModel(
        IAuthenticationService authenticationService,
        INavigationService navigationService,
        ISessionService sessionService,
        ISettingsService settingsService)
    {
        _authenticationService = authenticationService;
        _navigationService = navigationService;
        _sessionService = sessionService;
        _settingsService = settingsService;

        // 尝试获取日志服务（可选）
        try
        {
            _logHelper = App.Services?.GetService(typeof(ToolHelper.LoggingDiagnostics.Abstractions.ILogHelper)) 
                as ToolHelper.LoggingDiagnostics.Abstractions.ILogHelper;
        }
        catch { }

        // 加载记住的密码
        LoadRememberedCredentials();
    }

    /// <summary>
    /// 加载记住的凭据
    /// </summary>
    private void LoadRememberedCredentials()
    {
        var settings = _settingsService.CurrentSettings;
        if (settings.Credentials.RememberPassword && !string.IsNullOrEmpty(settings.Credentials.Username))
        {
            Username = settings.Credentials.Username;
            Password = settings.Credentials.PasswordHash; // 注意：这里应该是解密后的密码
            RememberPassword = true;
        }
    }

    /// <summary>
    /// 是否可以登录
    /// </summary>
    private bool CanLogin() => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && !IsLoggingIn;

    /// <summary>
    /// 登录命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            IsLoggingIn = true;

            // 验证账号密码格式
            if (!ValidateInput())
            {
                return;
            }

            // 调用认证服务
            var user = await _authenticationService.LoginAsync(Username.Trim(), Password.Trim());

            if (user == null)
            {
                ErrorMessage = "账号或密码不正确";
                _logHelper?.Warning($"登录失败：账号或密码不正确 - 账号: {Username}");
                return;
            }

            if (!user.IsEnabled)
            {
                ErrorMessage = "该账户已被禁用";
                _logHelper?.Warning($"登录失败：账户已禁用 - 账号: {Username}");
                return;
            }

            // 登录成功
            _sessionService.SetCurrentUser(user);

            // 保存记住密码
            if (RememberPassword)
            {
                var settings = _settingsService.CurrentSettings;
                settings.Credentials.RememberPassword = true;
                settings.Credentials.Username = Username.Trim();
                settings.Credentials.PasswordHash = _authenticationService.HashPassword(Password.Trim());
                _settingsService.SaveSettings();
            }
            else
            {
                var settings = _settingsService.CurrentSettings;
                settings.Credentials.RememberPassword = false;
                settings.Credentials.Username = string.Empty;
                settings.Credentials.PasswordHash = string.Empty;
                _settingsService.SaveSettings();
            }

            _logHelper?.Information($"用户登录成功", new Dictionary<string, object>
            {
                ["UserId"] = user.Id,
                ["Username"] = user.Username,
                ["Role"] = user.Role.ToString()
            });

            // 导航到患者选择界面
            // 注意：游客模式将在游客登录命令中直接导航到主界面
            _navigationService.NavigateTo("PatientSelectionViewModel");
        }
        catch (Exception ex)
        {
            ErrorMessage = "登录失败，请稍后重试";
            _logHelper?.Error("登录异常", ex, new Dictionary<string, object>
            {
                ["Username"] = Username
            });
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    /// <summary>
    /// 游客登录命令
    /// </summary>
    [RelayCommand]
    private async Task GuestLoginAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            IsLoggingIn = true;

            var guestUser = await _authenticationService.GuestLoginAsync();
            _sessionService.SetCurrentUser(guestUser);

            _logHelper?.Information("游客登录成功");

            // 游客直接进入主界面，跳过患者选择
            // TODO: 当 MainContainerView 创建后，改为导航到 MainContainerViewModel
            _navigationService.NavigateTo("PatientSelectionViewModel");
        }
        catch (Exception ex)
        {
            ErrorMessage = "游客登录失败，请稍后重试";
            _logHelper?.Error("游客登录异常", ex);
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    /// <summary>
    /// 切换密码显隐命令
    /// </summary>
    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordHidden = !IsPasswordHidden;
    }

    /// <summary>
    /// 验证输入
    /// </summary>
    private bool ValidateInput()
    {
        var username = Username.Trim();
        var password = Password.Trim();

        // 检查前后空格
        if (Username != username || Password != password)
        {
            ErrorMessage = "账号或密码不正确";
            return false;
        }

        // 验证账号长度
        if (username.Length < Constants.USERNAME_MIN_LENGTH || username.Length > Constants.USERNAME_MAX_LENGTH)
        {
            ErrorMessage = "账号或密码不正确";
            return false;
        }

        // 验证密码长度
        if (password.Length < Constants.PASSWORD_MIN_LENGTH || password.Length > Constants.PASSWORD_MAX_LENGTH)
        {
            ErrorMessage = "账号或密码不正确";
            return false;
        }

                return true;
            }
        }
