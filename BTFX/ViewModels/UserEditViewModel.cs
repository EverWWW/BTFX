using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;

namespace BTFX.ViewModels;

public class RoleOption
{
    public UserRole Value { get; set; }
    public string Display { get; set; } = string.Empty;
}

public class UserEditViewModel : ObservableObject
{
    private readonly IUserService _userService;
    private readonly IDepartmentService _departmentService;
    private readonly User? _originalUser;

    public bool IsNewUser => _originalUser == null;

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _phone = string.Empty;
    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    private ObservableCollection<RoleOption> _roleOptions = new();
    public ObservableCollection<RoleOption> RoleOptions
    {
        get => _roleOptions;
        set => SetProperty(ref _roleOptions, value);
    }

    private RoleOption? _selectedRole;
    public RoleOption? SelectedRole
    {
        get => _selectedRole;
        set => SetProperty(ref _selectedRole, value);
    }

    private ObservableCollection<Department> _departments = new();
    public ObservableCollection<Department> Departments
    {
        get => _departments;
        set => SetProperty(ref _departments, value);
    }

    private Department? _selectedDepartment;
    public Department? SelectedDepartment
    {
        get => _selectedDepartment;
        set => SetProperty(ref _selectedDepartment, value);
    }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private string _confirmPassword = string.Empty;
    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    private string _validationError = string.Empty;
    public string ValidationError
    {
        get => _validationError;
        set
        {
            if (SetProperty(ref _validationError, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        set => SetProperty(ref _isSaving, value);
    }

    public bool HasValidationError => !string.IsNullOrEmpty(ValidationError);

    public bool ShowStatusToggle => !IsNewUser;

    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }

    public UserEditViewModel(IUserService userService, IDepartmentService departmentService, User? user = null)
    {
        _userService = userService;
        _departmentService = departmentService;
        _originalUser = user;

        CancelCommand = new RelayCommand(Cancel);
        SaveCommand = new AsyncRelayCommand(SaveAsync);

        InitializeOptions();
        LoadData();
    }

    private void InitializeOptions()
    {
        RoleOptions.Add(new RoleOption { Value = UserRole.Administrator, Display = "管理员" });
        RoleOptions.Add(new RoleOption { Value = UserRole.Operator, Display = "操作员" });
        RoleOptions.Add(new RoleOption { Value = UserRole.Guest, Display = "访客" });
    }

    private void LoadData()
    {
        Task.Run(async () =>
        {
            try
            {
                var depts = await _departmentService.GetEnabledDepartmentsAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Departments.Clear();
                    foreach (var dept in depts)
                    {
                        Departments.Add(dept);
                    }

                    if (_originalUser != null)
                    {
                        Username = _originalUser.Username;
                        Name = _originalUser.Name;
                        Phone = _originalUser.Phone;
                        IsEnabled = _originalUser.IsEnabled;

                        SelectedRole = RoleOptions.FirstOrDefault(r => r.Value == _originalUser.Role);
                        SelectedDepartment = Departments.FirstOrDefault(d => d.Id == _originalUser.DepartmentId);
                    }
                    else
                    {
                        SelectedRole = RoleOptions.FirstOrDefault(r => r.Value == UserRole.Operator);
                        IsEnabled = true;
                    }
                });
            }
                    catch
                    {
                        // Ignore errors during init
                    }
                });
            }

            private void Cancel()
    {
        DialogHost.CloseDialogCommand.Execute(false, null);
    }

    private async Task SaveAsync()
    {
        if (IsSaving) return;

        if (!await ValidateAsync()) return;

        IsSaving = true;
        try
        {
            var user = _originalUser ?? new User();
            user.Username = Username.Trim();
            user.Name = Name.Trim();
            user.Phone = Phone?.Trim() ?? string.Empty;
            user.Role = SelectedRole?.Value ?? UserRole.Operator;
            user.DepartmentId = SelectedDepartment?.Id;
            user.IsEnabled = IsEnabled;

            bool success;

            if (IsNewUser)
            {
                user.PasswordHash = Password; // Service should handle hashing
                var newId = await _userService.AddUserAsync(user);
                success = newId > 0;
            }
            else
            {
                success = await _userService.UpdateUserAsync(user);
            }

            if (success)
            {
                DialogHost.CloseDialogCommand.Execute(true, null);
            }
            else
            {
                ValidationError = "保存失败，请重试";
            }
        }
        catch (Exception ex)
        {
            ValidationError = $"保存出错: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task<bool> ValidateAsync()
    {
        ValidationError = string.Empty;

        if (string.IsNullOrWhiteSpace(Username))
        {
            ValidationError = "账号不能为空";
            return false;
        }

        if (Username.Length < 3 || Username.Length > 20)
        {
            ValidationError = "账号长度应在3-20位之间";
            return false;
        }

        bool isExists = await _userService.IsUsernameExistsAsync(Username, _originalUser?.Id);
        if (isExists)
        {
            ValidationError = "该账号已存在";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationError = "姓名不能为空";
            return false;
        }

        if (SelectedRole == null)
        {
            ValidationError = "请选择角色";
            return false;
        }

        if (IsNewUser)
        {
            if (string.IsNullOrWhiteSpace(Password))
            {
                ValidationError = "密码不能为空";
                return false;
            }

            if (Password.Length < 6)
            {
                ValidationError = "密码长度至少为6位";
                return false;
            }

            if (Password != ConfirmPassword)
            {
                ValidationError = "两次输入的密码不一致";
                return false;
            }
        }

        return true;
    }
}
