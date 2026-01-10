using System.Collections.ObjectModel;
using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.ViewModels;

/// <summary>
/// 设置视图模型
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;
    private readonly ISessionService _sessionService;
    private readonly IUserService _userService;
    private readonly IDepartmentService _departmentService;
    private readonly IBackupService _backupService;
    private readonly ILogHelper? _logHelper;

    #region Tab显示控制

    /// <summary>
    /// 当前选中的Tab索引
    /// </summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>
    /// 是否显示用户管理Tab（仅管理员）
    /// </summary>
    [ObservableProperty]
    private bool _showUserManagementTab;

    /// <summary>
    /// 是否显示数据管理Tab（仅管理员）
    /// </summary>
    [ObservableProperty]
    private bool _showDataManagementTab;

    /// <summary>
    /// 是否显示单位设置Tab（仅管理员）
    /// </summary>
    [ObservableProperty]
    private bool _showUnitSettingsTab;

    /// <summary>
    /// 是否显示科室管理Tab（仅管理员）
    /// </summary>
    [ObservableProperty]
    private bool _showDepartmentTab;

    /// <summary>
    /// 是否显示设备配置Tab（管理员和操作员）
    /// </summary>
    [ObservableProperty]
    private bool _showDeviceConfigTab;

    #endregion

    #region 通用设置

    /// <summary>
    /// 语言选项列表
    /// </summary>
    public ObservableCollection<LanguageOption> LanguageOptions { get; } = new()
    {
        new LanguageOption { Value = AppLanguage.ChineseSimplified, Display = "简体中文" },
        new LanguageOption { Value = AppLanguage.English, Display = "English" }
    };

    /// <summary>
    /// 选中的语言
    /// </summary>
    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    /// <summary>
    /// 主题选项列表
    /// </summary>
    public ObservableCollection<ThemeOption> ThemeOptions { get; } = new()
    {
        new ThemeOption { Value = AppTheme.Light, Display = "浅色主题", IconKind = "WhiteBalanceSunny" },
        new ThemeOption { Value = AppTheme.Dark, Display = "深色主题", IconKind = "WeatherNight" }
    };

    /// <summary>
    /// 选中的主题
    /// </summary>
    [ObservableProperty]
    private ThemeOption? _selectedTheme;

    #endregion

    #region 用户管理

    /// <summary>
    /// 用户列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<UserItem> _users = new();

    /// <summary>
    /// 选中的用户
    /// </summary>
    [ObservableProperty]
    private UserItem? _selectedUser;

    #endregion

    #region 科室管理

    /// <summary>
    /// 科室列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DepartmentItem> _departments = new();

    /// <summary>
    /// 选中的科室
    /// </summary>
    [ObservableProperty]
    private DepartmentItem? _selectedDepartment;

    #endregion

    #region 单位设置

    /// <summary>
    /// 单位名称
    /// </summary>
    [ObservableProperty]
    private string _unitName = string.Empty;

    /// <summary>
    /// Logo路径
    /// </summary>
    [ObservableProperty]
    private string _logoPath = string.Empty;

    /// <summary>
    /// 是否有Logo
    /// </summary>
    public bool HasLogo => !string.IsNullOrEmpty(_logoPath) && System.IO.File.Exists(_logoPath);

    #endregion

    #region 数据管理

    /// <summary>
    /// 自动备份启用
    /// </summary>
    [ObservableProperty]
    private bool _autoBackupEnabled;

    /// <summary>
    /// 备份时间
    /// </summary>
    [ObservableProperty]
    private string _backupTime = Constants.BACKUP_DEFAULT_TIME;

    /// <summary>
    /// 保留备份数量
    /// </summary>
    [ObservableProperty]
    private int _backupRetainCount = Constants.BACKUP_DEFAULT_RETAIN_COUNT;

    /// <summary>
    /// 备份历史列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<BackupHistoryItem> _backupHistory = new();

    #endregion

    #region 系统信息

    /// <summary>
    /// 应用版本
    /// </summary>
    public string AppVersion => Constants.VERSION_FULL;

    /// <summary>
    /// 应用名称
    /// </summary>
    public string AppName => Constants.APP_DISPLAY_NAME;

    /// <summary>
    /// 数据库路径
    /// </summary>
    [ObservableProperty]
    private string _databasePath = string.Empty;

    /// <summary>
    /// 数据库大小
    /// </summary>
    [ObservableProperty]
    private string _databaseSize = "--";

    /// <summary>
    /// 日志目录
    /// </summary>
    [ObservableProperty]
    private string _logDirectory = string.Empty;

    /// <summary>
    /// 当前用户名
    /// </summary>
    [ObservableProperty]
    private string _currentUsername = string.Empty;

    /// <summary>
    /// 当前用户角色
    /// </summary>
    [ObservableProperty]
    private string _currentUserRole = string.Empty;

    #endregion

    #region 加载状态

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 是否正在保存
    /// </summary>
    [ObservableProperty]
    private bool _isSaving;

    #endregion

    /// <summary>
    /// 构造函数
    /// </summary>
    public SettingsViewModel(
        ISettingsService settingsService,
        ILocalizationService localizationService,
        IThemeService themeService,
        ISessionService sessionService,
        IUserService userService,
        IDepartmentService departmentService,
        IBackupService backupService)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
        _themeService = themeService;
        _sessionService = sessionService;
        _userService = userService;
        _departmentService = departmentService;
        _backupService = backupService;

        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }

        // 初始化权限
        InitializePermissions();

        // 加载设置
        LoadSettings();

        // 加载系统信息
        LoadSystemInfo();
    }

    /// <summary>
    /// 初始化权限
    /// </summary>
    private void InitializePermissions()
    {
        var isAdmin = _sessionService.HasPermission("manage_users");
        var isOperator = _sessionService.HasPermission("edit");

        ShowUserManagementTab = isAdmin;
        ShowDataManagementTab = isAdmin;
        ShowUnitSettingsTab = isAdmin;
        ShowDepartmentTab = isAdmin;
        ShowDeviceConfigTab = isAdmin || isOperator;
    }

    /// <summary>
    /// 加载设置
    /// </summary>
    private void LoadSettings()
    {
        try
        {
            var settings = _settingsService.CurrentSettings;

            // 语言设置
            SelectedLanguage = LanguageOptions.FirstOrDefault(l => l.Value == settings.Application.Language)
                ?? LanguageOptions.First();

            // 主题设置
            SelectedTheme = ThemeOptions.FirstOrDefault(t => t.Value == settings.Application.Theme)
                ?? ThemeOptions.First();

            // 单位设置
            UnitName = settings.Unit.Name;
            LogoPath = settings.Unit.LogoPath;

            _logHelper?.Information("设置加载完成");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载设置失败", ex);
        }
    }

    /// <summary>
    /// 加载系统信息
    /// </summary>
    private void LoadSystemInfo()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 数据库路径
            DatabasePath = System.IO.Path.Combine(baseDir, Constants.DATABASE_DIRECTORY, Constants.DATABASE_FILENAME);
            if (System.IO.File.Exists(DatabasePath))
            {
                var fileInfo = new System.IO.FileInfo(DatabasePath);
                DatabaseSize = $"{fileInfo.Length / 1024.0:F2} KB";
            }

            // 日志目录
            LogDirectory = System.IO.Path.Combine(baseDir, Constants.LOG_DIRECTORY);

            // 当前用户
            var user = _sessionService.CurrentUser;
            CurrentUsername = user?.Username ?? "未登录";
            CurrentUserRole = user?.Role switch
            {
                UserRole.Administrator => "管理员",
                UserRole.Operator => "操作员",
                UserRole.Guest => "游客",
                _ => "未知"
            };
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载系统信息失败", ex);
        }
    }

    #region 属性变化处理

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value != null)
        {
            _localizationService.ApplyLanguage(value.Value);
            _settingsService.CurrentSettings.Application.Language = value.Value;
            _settingsService.SaveSettings();
            _logHelper?.Information($"切换语言: {value.Display}");
        }
    }

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (value != null)
        {
            _themeService.ApplyTheme(value.Value);
            _settingsService.CurrentSettings.Application.Theme = value.Value;
            _settingsService.SaveSettings();
            _logHelper?.Information($"切换主题: {value.Display}");
        }
    }

    partial void OnLogoPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasLogo));
    }

    #endregion

    #region 通用设置命令

    /// <summary>
    /// 保存通用设置
    /// </summary>
    [RelayCommand]
    private void SaveGeneralSettings()
    {
        try
        {
            _settingsService.SaveSettings();
            System.Windows.MessageBox.Show("设置已保存！", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            _logHelper?.Information("保存通用设置");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("保存通用设置失败", ex);
            System.Windows.MessageBox.Show($"保存失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    #endregion

    #region 用户管理命令

    /// <summary>
    /// 加载用户列表
    /// </summary>
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

    /// <summary>
    /// 添加用户
    /// </summary>
    [RelayCommand]
    private void AddUser()
    {
        System.Windows.MessageBox.Show("用户添加功能开发中...", "提示",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// 编辑用户
    /// </summary>
    [RelayCommand]
    private void EditUser(UserItem? item)
    {
        if (item == null) return;
        System.Windows.MessageBox.Show($"编辑用户 {item.User.Username} 功能开发中...", "提示",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// 重置密码
    /// </summary>
    [RelayCommand]
    private void ResetPassword(UserItem? item)
    {
        if (item == null) return;

        var result = System.Windows.MessageBox.Show(
            $"确定要重置用户 {item.User.Username} 的密码为默认密码 {Constants.DEFAULT_PASSWORD} 吗？",
            "确认重置",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            System.Windows.MessageBox.Show("密码重置功能开发中...", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// 切换用户状态
    /// </summary>
    [RelayCommand]
    private void ToggleUserStatus(UserItem? item)
    {
        if (item == null) return;
        System.Windows.MessageBox.Show("用户状态切换功能开发中...", "提示",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    #endregion

    #region 科室管理命令

    /// <summary>
    /// 加载科室列表
    /// </summary>
    [RelayCommand]
    private async Task LoadDepartmentsAsync()
    {
        try
        {
            IsLoading = true;
            var departments = await _departmentService.GetAllDepartmentsAsync();

            Departments.Clear();
            int rowNumber = 1;
            foreach (var dept in departments)
            {
                Departments.Add(new DepartmentItem(dept, rowNumber++));
            }

            _logHelper?.Information($"加载科室列表：共{departments.Count}个");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载科室列表失败", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 添加科室
    /// </summary>
    [RelayCommand]
    private void AddDepartment()
    {
        System.Windows.MessageBox.Show("科室添加功能开发中...", "提示",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// 编辑科室
    /// </summary>
    [RelayCommand]
    private void EditDepartment(DepartmentItem? item)
    {
        if (item == null) return;
        System.Windows.MessageBox.Show($"编辑科室 {item.Department.Name} 功能开发中...", "提示",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// 删除科室
    /// </summary>
    [RelayCommand]
    private async Task DeleteDepartmentAsync(DepartmentItem? item)
    {
        if (item == null) return;

        var result = System.Windows.MessageBox.Show(
            $"确定要删除科室 {item.Department.Name} 吗？此操作不可恢复！",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            var success = await _departmentService.DeleteDepartmentAsync(item.Department.Id);
            if (success)
            {
                await LoadDepartmentsAsync();
                _logHelper?.Information($"删除科室：{item.Department.Name}");
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除科室失败：{item.Department.Name}", ex);
            System.Windows.MessageBox.Show($"删除失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    #endregion

    #region 单位设置命令

    /// <summary>
    /// 选择Logo
    /// </summary>
    [RelayCommand]
    private void SelectLogo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择Logo图片",
            Filter = "图片文件 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            var fileInfo = new System.IO.FileInfo(dialog.FileName);
            if (fileInfo.Length > Constants.LOGO_MAX_SIZE_KB * 1024)
            {
                System.Windows.MessageBox.Show(
                    $"Logo文件大小不能超过{Constants.LOGO_MAX_SIZE_KB}KB",
                    "提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            LogoPath = dialog.FileName;
            _logHelper?.Information($"选择Logo：{dialog.FileName}");
        }
    }

    /// <summary>
    /// 清除Logo
    /// </summary>
    [RelayCommand]
    private void ClearLogo()
    {
        LogoPath = string.Empty;
        _logHelper?.Information("清除Logo");
    }

    /// <summary>
    /// 保存单位设置
    /// </summary>
    [RelayCommand]
    private void SaveUnitSettings()
    {
        try
        {
            _settingsService.CurrentSettings.Unit.Name = UnitName;
            _settingsService.CurrentSettings.Unit.LogoPath = LogoPath;
            _settingsService.SaveSettings();

            System.Windows.MessageBox.Show("单位设置已保存！", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            _logHelper?.Information("保存单位设置");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("保存单位设置失败", ex);
            System.Windows.MessageBox.Show($"保存失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    #endregion

    #region 数据管理命令

    /// <summary>
    /// 立即备份
    /// </summary>
    [RelayCommand]
    private async Task BackupNowAsync()
    {
        try
        {
            IsSaving = true;
            var filePath = await _backupService.CreateBackupAsync();
            if (!string.IsNullOrEmpty(filePath))
            {
                System.Windows.MessageBox.Show($"备份成功！\n文件：{filePath}", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                _logHelper?.Information($"手动备份成功：{filePath}");
                await LoadBackupHistoryAsync();
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("手动备份失败", ex);
            System.Windows.MessageBox.Show($"备份失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// 加载备份历史
    /// </summary>
    [RelayCommand]
    private async Task LoadBackupHistoryAsync()
    {
        try
        {
            // TODO: 实现备份列表获取
            BackupHistory.Clear();
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载备份历史失败", ex);
        }
    }

    /// <summary>
    /// 恢复备份
    /// </summary>
    [RelayCommand]
    private async Task RestoreBackupAsync(BackupHistoryItem? item)
    {
        if (item == null) return;

        var result = System.Windows.MessageBox.Show(
            "恢复备份将覆盖当前数据，此操作不可撤销！确定要继续吗？",
            "确认恢复",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            IsSaving = true;
            var success = await _backupService.RestoreBackupAsync(item.FilePath);
            if (success)
            {
                System.Windows.MessageBox.Show("恢复成功！程序需要重启。", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                _logHelper?.Information($"恢复备份成功：{item.FilePath}");
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"恢复备份失败：{item.FilePath}", ex);
            System.Windows.MessageBox.Show($"恢复失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// 保存备份设置
    /// </summary>
    [RelayCommand]
    private void SaveBackupSettings()
    {
        try
        {
            // 备份设置暂时存储在内存中
            System.Windows.MessageBox.Show("备份设置已保存！", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            _logHelper?.Information("保存备份设置");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("保存备份设置失败", ex);
            System.Windows.MessageBox.Show($"保存失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    #endregion

    #region 系统信息命令

    /// <summary>
    /// 打开日志目录
    /// </summary>
    [RelayCommand]
    private void OpenLogDirectory()
    {
        try
        {
            if (System.IO.Directory.Exists(LogDirectory))
            {
                System.Diagnostics.Process.Start("explorer.exe", LogDirectory);
            }
            else
            {
                System.Windows.MessageBox.Show("日志目录不存在", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("打开日志目录失败", ex);
        }
    }

    /// <summary>
    /// 显示关于对话框
    /// </summary>
    [RelayCommand]
    private void ShowAbout()
    {
        System.Windows.MessageBox.Show(
            $"{AppName}\n版本：{AppVersion}\n\n步态智能分析系统",
            "关于",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    #endregion
}

#region 辅助类

/// <summary>
/// 语言选项
/// </summary>
public class LanguageOption
{
    public AppLanguage Value { get; set; }
    public string Display { get; set; } = string.Empty;
}

/// <summary>
/// 主题选项
/// </summary>
public class ThemeOption
{
    public AppTheme Value { get; set; }
    public string Display { get; set; } = string.Empty;
    public string IconKind { get; set; } = string.Empty;
}

/// <summary>
/// 用户列表项
/// </summary>
public partial class UserItem : ObservableObject
{
    public User User { get; }
    public int RowNumber { get; }

    public string Username => User.Username;
    public string Name => User.Name ?? "--";
    public string RoleDisplay => User.Role switch
    {
        UserRole.Administrator => "管理员",
        UserRole.Operator => "操作员",
        UserRole.Guest => "游客",
        _ => "未知"
    };
    public string Phone => User.Phone ?? "--";
    public string DepartmentName => "--"; // TODO: 需要从数据库加载
    public string StatusDisplay => User.IsEnabled ? "启用" : "禁用";
    public string StatusColor => User.IsEnabled ? "#4CAF50" : "#9E9E9E";
    public string CreatedAtDisplay => User.CreatedAt.ToString(Constants.DATETIME_LIST_FORMAT);
    public string LastLoginDisplay => User.LastLoginAt?.ToString(Constants.DATETIME_LIST_FORMAT) ?? "从未登录";

    public bool IsBuiltIn => User.Username == Constants.ADMIN_USERNAME ||
                             User.Username == Constants.USER_USERNAME ||
                             User.Username == Constants.GUEST_USERNAME;

    public UserItem(User user, int rowNumber)
    {
        User = user;
        RowNumber = rowNumber;
    }
}

/// <summary>
/// 科室列表项
/// </summary>
public partial class DepartmentItem : ObservableObject
{
    public Department Department { get; }
    public int RowNumber { get; }

    public string Name => Department.Name;
    public string Description => Department.Description ?? "--";
    public string CreatedAtDisplay => Department.CreatedAt.ToString(Constants.DATETIME_LIST_FORMAT);

    public DepartmentItem(Department department, int rowNumber)
    {
        Department = department;
        RowNumber = rowNumber;
    }
}

/// <summary>
/// 备份历史项
/// </summary>
public partial class BackupHistoryItem : ObservableObject
{
    public string FilePath { get; }
    public DateTime CreatedAt { get; }
    public int RowNumber { get; }

    public string FileName => System.IO.Path.GetFileName(FilePath);
    public string CreatedAtDisplay => CreatedAt.ToString(Constants.DATETIME_FORMAT);
    public string FileSizeDisplay
    {
        get
        {
            try
            {
                if (System.IO.File.Exists(FilePath))
                {
                    var fileInfo = new System.IO.FileInfo(FilePath);
                    return $"{fileInfo.Length / 1024.0:F2} KB";
                }
            }
            catch { }
            return "--";
        }
    }

    public BackupHistoryItem(string filePath, DateTime createdAt, int rowNumber)
    {
        FilePath = filePath;
        CreatedAt = createdAt;
        RowNumber = rowNumber;
    }
}

#endregion
