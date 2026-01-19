using System.Collections.ObjectModel;
using BTFX.Models;
using BTFX.Services.Interfaces;
using BTFX.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using ToolHelper.LoggingDiagnostics.Abstractions;
using BtfxConstants = BTFX.Common.Constants;

namespace BTFX.ViewModels.Settings;

/// <summary>
/// 用户管理视图模型
/// </summary>
public partial class UserManagementViewModel : ObservableObject
{
    private readonly IUserService _userService;
    private readonly IDepartmentService _departmentService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;

    [ObservableProperty]
    private ObservableCollection<UserItem> _users = [];

    [ObservableProperty]
    private UserItem? _selectedUser;

    [ObservableProperty]
    private bool _isLoading;

    public UserManagementViewModel(
        IUserService userService,
        IDepartmentService departmentService,
        ILocalizationService localizationService)
    {
        _userService = userService;
        _departmentService = departmentService;
        _localizationService = localizationService;

        try { _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper; } catch { }

        _ = LoadUsersAsync();
    }

    [RelayCommand]
    private async Task LoadUsersAsync()
    {
        try
        {
            IsLoading = true;
            var users = await _userService.GetAllUsersAsync();

            Users.Clear();
            int rowNumber = 1;
            foreach (var user in users)
            {
                Users.Add(new UserItem(user, rowNumber++));
            }

            _logHelper?.Information($"加载用户列表：共{users.Count}个");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载用户列表失败", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddUserAsync()
    {
        try
        {
            var vm = new UserEditViewModel(_userService, _departmentService);
            var dialog = new UserEditDialog(vm);
            var result = await DialogHost.Show(dialog, "RootDialog");

            if (result is true)
            {
                await LoadUsersAsync();
                _logHelper?.Information("添加用户成功");
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("添加用户失败", ex);
        }
    }

    [RelayCommand]
    private async Task EditUserAsync(UserItem? item)
    {
        if (item == null) return;

        try
        {
            var vm = new UserEditViewModel(_userService, _departmentService, item.User);
            var dialog = new UserEditDialog(vm);
            var result = await DialogHost.Show(dialog, "RootDialog");

            if (result is true)
            {
                await LoadUsersAsync();
                _logHelper?.Information($"更新用户成功: {item.User.Username}");
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"编辑用户失败: {item.User.Username}", ex);
        }
    }

    [RelayCommand]
    private async Task ResetPasswordAsync(UserItem? item)
    {
        if (item == null) return;

        var result = System.Windows.MessageBox.Show(
            _localizationService.GetString("ConfirmResetPassword"),
            _localizationService.GetString("Confirm"),
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                item.User.PasswordHash = BtfxConstants.DEFAULT_PASSWORD;
                var success = await _userService.UpdateUserAsync(item.User);
                if (success)
                {
                    System.Windows.MessageBox.Show(
                        _localizationService.GetString("OperationSuccess"), 
                        _localizationService.GetString("Information"),
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    _logHelper?.Information($"重置用户密码: {item.User.Username}");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"{_localizationService.GetString("OperationFailed")}: {ex.Message}", 
                    _localizationService.GetString("Error"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

        [RelayCommand]
        private async Task ToggleUserStatusAsync(UserItem? item)
        {
            if (item == null) return;

            string action = item.User.IsEnabled 
                ? _localizationService.GetString("DisableUser") 
                : _localizationService.GetString("EnableUser");
            var result = System.Windows.MessageBox.Show(
                _localizationService.GetString("ConfirmToggleUserStatus", action, item.User.Username),
                _localizationService.GetString("Confirm"),
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    item.User.IsEnabled = !item.User.IsEnabled;
                    var success = await _userService.UpdateUserAsync(item.User);
                    if (success)
                    {
                        await LoadUsersAsync();
                        _logHelper?.Information($"{action}用户: {item.User.Username}");
                    }
                    else
                    {
                        item.User.IsEnabled = !item.User.IsEnabled;
                        System.Windows.MessageBox.Show(
                            _localizationService.GetString("OperationFailed"), 
                            _localizationService.GetString("Error"),
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    item.User.IsEnabled = !item.User.IsEnabled;
                    System.Windows.MessageBox.Show(
                        $"{_localizationService.GetString("OperationFailed")}: {ex.Message}", 
                        _localizationService.GetString("Error"),
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
