using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using BTFX.Models;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;
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
    private const double UserRowHeight = 60;
    private const double UserRowTopMargin = 8;
    private const int MinimumPageSize = 2;
    private const int MaximumPageSize = 6;

    private readonly IUserService _userService;
    private readonly IAuthenticationService _authenticationService;
    private readonly IDepartmentService _departmentService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;
    private readonly List<UserItem> _allUsers = [];
    private readonly Dictionary<int, string> _departmentLookup = [];

    private bool _isUpdatingSelection;

    [ObservableProperty]
    private ObservableCollection<UserItem> _users = [];

    private ObservableCollection<PageItem> _pageNumbers = [];

    [ObservableProperty]
    private UserItem? _selectedUser;

    [ObservableProperty]
    private bool _isLoading;

    private bool _isAllSelected;

    private int _currentPage = 1;

    private int _totalPages = 1;

    private int _pageSize = MaximumPageSize;

    public ObservableCollection<PageItem> PageNumbers => _pageNumbers;

    public bool IsAllSelected
    {
        get => _isAllSelected;
        private set => SetProperty(ref _isAllSelected, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            if (SetProperty(ref _totalPages, value))
            {
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    public bool CanGoPrevious => _currentPage > 1;

    public bool CanGoNext => _currentPage < _totalPages;

    public UserManagementViewModel(
        IUserService userService,
        IAuthenticationService authenticationService,
        IDepartmentService departmentService,
        ILocalizationService localizationService)
    {
        _userService = userService;
        _authenticationService = authenticationService;
        _departmentService = departmentService;
        _localizationService = localizationService;

        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch
        {
        }

        _ = LoadUsersAsync();
    }

    [RelayCommand]
    private async Task LoadUsersAsync()
    {
        try
        {
            IsLoading = true;
            await LoadDepartmentsAsync();

            var users = await _userService.GetAllUsersAsync();
            _allUsers.Clear();

            var rowNumber = 1;
            foreach (var user in users.OrderBy(u => u.Id))
            {
                _allUsers.Add(new UserItem(user, rowNumber++, GetDepartmentName(user.DepartmentId)));
            }

            CurrentPage = 1;
            RefreshPagedUsers();
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
        if (item == null)
        {
            return;
        }

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
        if (item == null)
        {
            return;
        }

        var result = await ShowConfirmDialogAsync(
            _localizationService.GetString("Confirm"),
            _localizationService.GetString("ConfirmResetPassword"));

        if (!result)
        {
            return;
        }

        try
        {
            var success = await _authenticationService.ResetPasswordAsync(item.User.Id, BtfxConstants.DEFAULT_PASSWORD);
            if (success)
            {
                await LoadUsersAsync();
                await ShowNoticeDialogAsync(
                    $"密码已重置为默认密码：{BtfxConstants.DEFAULT_PASSWORD}",
                    _localizationService.GetString("Information"));
                _logHelper?.Information($"重置用户密码: {item.User.Username}");
                return;
            }

            await ShowNoticeDialogAsync(
                _localizationService.GetString("OperationFailed"),
                _localizationService.GetString("Error"));
        }
        catch (Exception ex)
        {
            await ShowNoticeDialogAsync(
                $"{_localizationService.GetString("OperationFailed")}: {ex.Message}",
                _localizationService.GetString("Error"));
        }
    }

    [RelayCommand]
    private async Task ToggleUserStatusAsync(UserItem? item)
    {
        if (item == null)
        {
            return;
        }

        var action = item.User.IsEnabled
            ? _localizationService.GetString("DisableUser")
            : _localizationService.GetString("EnableUser");

        var result = await ShowConfirmDialogAsync(
            _localizationService.GetString("Confirm"),
            _localizationService.GetString("ConfirmToggleUserStatus", action, item.User.Username));

        if (!result)
        {
            return;
        }

        try
        {
            item.User.IsEnabled = !item.User.IsEnabled;
            var success = await _userService.UpdateUserAsync(item.User);
            if (success)
            {
                await LoadUsersAsync();
                _logHelper?.Information($"{action}用户: {item.User.Username}");
                return;
            }

            item.User.IsEnabled = !item.User.IsEnabled;
            await ShowNoticeDialogAsync(
                _localizationService.GetString("OperationFailed"),
                _localizationService.GetString("Error"));
        }
        catch (Exception ex)
        {
            item.User.IsEnabled = !item.User.IsEnabled;
            await ShowNoticeDialogAsync(
                $"{_localizationService.GetString("OperationFailed")}: {ex.Message}",
                _localizationService.GetString("Error"));
        }
    }

    [RelayCommand]
    private async Task DeleteUserAsync(UserItem? item)
    {
        if (item == null)
        {
            return;
        }

        if (item.IsBuiltIn)
        {
            await ShowNoticeDialogAsync(
                _localizationService.GetString("BuiltInUserDeleteNotAllowed"),
                _localizationService.GetString("Warning"));
            return;
        }

        var result = await ShowConfirmDialogAsync(
            _localizationService.GetString("DeleteUser"),
            _localizationService.GetString("ConfirmDeleteUserMessage", item.User.Username));

        if (!result)
        {
            return;
        }

        try
        {
            var success = await _userService.DeleteUserAsync(item.User.Id);
            if (success)
            {
                await LoadUsersAsync();
                await ShowNoticeDialogAsync(
                    _localizationService.GetString("DeleteSuccess"),
                    _localizationService.GetString("Information"));
                _logHelper?.Information($"删除用户成功: {item.User.Username}");
                return;
            }

            await ShowNoticeDialogAsync(
                _localizationService.GetString("DeleteFailed"),
                _localizationService.GetString("Error"));
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除用户失败: {item.User.Username}", ex);
            await ShowNoticeDialogAsync(
                $"{_localizationService.GetString("DeleteFailed")}: {ex.Message}",
                _localizationService.GetString("Error"));
        }
    }

    /// <summary>
    /// 显示确认对话框。
    /// </summary>
    private static async Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        var result = await DialogHost.Show(
            new ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel
                {
                    Title = title,
                    Message = message,
                    ConfirmText = "确定",
                    CancelText = "取消",
                    IsCancelVisible = true
                }
            },
            "RootDialog").ConfigureAwait(true);

        return result is true;
    }

    /// <summary>
    /// 显示提示对话框。
    /// </summary>
    private static Task ShowNoticeDialogAsync(string message, string title)
    {
        return DialogHost.Show(
            new ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel
                {
                    Title = title,
                    Message = message,
                    ConfirmText = "确定",
                    IsCancelVisible = false
                }
            },
            "RootDialog");
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        try
        {
            _isUpdatingSelection = true;
            var shouldSelectAll = !IsAllSelected;
            foreach (var user in Users)
            {
                user.IsChecked = shouldSelectAll;
            }

            IsAllSelected = shouldSelectAll;
            if (!shouldSelectAll)
            {
                SelectedUser = null;
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        CurrentPage--;
        RefreshPagedUsers();
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!CanGoNext)
        {
            return;
        }

        CurrentPage++;
        RefreshPagedUsers();
    }

    [RelayCommand]
    private void GoToPage(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > TotalPages || pageNumber == CurrentPage)
        {
            return;
        }

        CurrentPage = pageNumber;
        RefreshPagedUsers();
    }

    /// <summary>
    /// 根据列表可视区域高度动态更新每页条数。
    /// </summary>
    /// <param name="viewportHeight">列表可视区域高度。</param>
    public void UpdatePageSize(double viewportHeight)
    {
        if (viewportHeight <= 0)
        {
            return;
        }

        var rowFullHeight = UserRowHeight + UserRowTopMargin;
        var calculatedPageSize = Math.Clamp(
            (int)Math.Floor((viewportHeight + UserRowTopMargin) / rowFullHeight),
            MinimumPageSize,
            MaximumPageSize);

        if (calculatedPageSize == _pageSize)
        {
            return;
        }

        _pageSize = calculatedPageSize;

        if (_allUsers.Count > 0)
        {
            var maxPage = (int)Math.Ceiling(_allUsers.Count / (double)_pageSize);
            if (CurrentPage > maxPage)
            {
                CurrentPage = maxPage;
            }
        }

        RefreshPagedUsers();
        _logHelper?.Information($"用户列表每页条数已根据可视高度更新为 {_pageSize}。");
    }

    partial void OnSelectedUserChanged(UserItem? value)
    {
        if (value == null || _isUpdatingSelection)
        {
            return;
        }

        try
        {
            _isUpdatingSelection = true;
            foreach (var user in Users)
            {
                user.IsChecked = ReferenceEquals(user, value);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        UpdateSelectAllState();
    }

    private async Task LoadDepartmentsAsync()
    {
        _departmentLookup.Clear();
        var departments = await _departmentService.GetAllDepartmentsAsync();
        foreach (var department in departments)
        {
            _departmentLookup[department.Id] = department.Name;
        }
    }

    private string GetDepartmentName(int? departmentId)
    {
        if (!departmentId.HasValue)
        {
            return "--";
        }

        return _departmentLookup.TryGetValue(departmentId.Value, out var departmentName) && !string.IsNullOrWhiteSpace(departmentName)
            ? departmentName
            : "--";
    }

    private void RefreshPagedUsers()
    {
        foreach (var existingUser in Users)
        {
            existingUser.PropertyChanged -= OnUserItemPropertyChanged;
        }

        Users.Clear();

        TotalPages = _allUsers.Count == 0
            ? 1
            : (int)Math.Ceiling(_allUsers.Count / (double)_pageSize);

        if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }

        if (CurrentPage < 1)
        {
            CurrentPage = 1;
        }

        foreach (var user in _allUsers.Skip((CurrentPage - 1) * _pageSize).Take(_pageSize))
        {
            user.IsChecked = false;
            user.PropertyChanged += OnUserItemPropertyChanged;
            Users.Add(user);
        }

        if (SelectedUser != null && !Users.Contains(SelectedUser))
        {
            SelectedUser = null;
        }

        BuildPageNumbers();
        UpdateSelectAllState();
    }

    private void BuildPageNumbers()
    {
        PageNumbers.Clear();
        if (TotalPages <= 0)
        {
            return;
        }

        var pagesToShow = new SortedSet<int> { 1, TotalPages };
        for (var page = Math.Max(1, CurrentPage - 1); page <= Math.Min(TotalPages, CurrentPage + 1); page++)
        {
            pagesToShow.Add(page);
        }

        var previousPage = 0;
        foreach (var page in pagesToShow)
        {
            if (previousPage > 0 && page - previousPage > 1)
            {
                PageNumbers.Add(new PageItem
                {
                    DisplayText = "...",
                    IsEllipsis = true,
                    PageNumber = -1
                });
            }

            PageNumbers.Add(new PageItem
            {
                DisplayText = page.ToString(),
                PageNumber = page,
                IsCurrent = page == CurrentPage
            });

            previousPage = page;
        }
    }

    private void UpdateSelectAllState()
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        try
        {
            _isUpdatingSelection = true;
            IsAllSelected = Users.Count > 0 && Users.All(user => user.IsChecked);
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private void OnUserItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(UserItem.IsChecked) || sender is not UserItem userItem || _isUpdatingSelection)
        {
            return;
        }

        try
        {
            _isUpdatingSelection = true;
            if (userItem.IsChecked)
            {
                foreach (var user in Users)
                {
                    if (!ReferenceEquals(user, userItem))
                    {
                        user.IsChecked = false;
                    }
                }

                SelectedUser = userItem;
            }
            else if (ReferenceEquals(SelectedUser, userItem))
            {
                SelectedUser = null;
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        UpdateSelectAllState();
    }
}
