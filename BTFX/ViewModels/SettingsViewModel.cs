using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using ToolHelper.LoggingDiagnostics.Abstractions;
using BtfxConstants = BTFX.Common.Constants;

namespace BTFX.ViewModels;

/// <summary>
/// 设置视图模型 - 作为设置页面的容器，管理子ViewModel和Tab权限控制
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISessionService _sessionService;
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

    #region 子 ViewModel

    /// <summary>
    /// 通用设置 ViewModel
    /// </summary>
    public Settings.GeneralSettingsViewModel GeneralSettingsViewModel { get; }

    /// <summary>
    /// 用户管理 ViewModel
    /// </summary>
    public Settings.UserManagementViewModel UserManagementViewModel { get; }

    /// <summary>
    /// 科室管理 ViewModel
    /// </summary>
    public Settings.DepartmentManagementViewModel DepartmentManagementViewModel { get; }

    /// <summary>
    /// 单位设置 ViewModel
    /// </summary>
    public Settings.UnitSettingsViewModel UnitSettingsViewModel { get; }

    /// <summary>
    /// 数据管理设置 ViewModel
    /// </summary>
    public Settings.DataManagementSettingsViewModel DataManagementSettingsViewModel { get; }

    /// <summary>
    /// 系统信息 ViewModel
    /// </summary>
    public Settings.SystemInfoViewModel SystemInfoViewModel { get; }

    #endregion

    /// <summary>
    /// 构造函数
    /// </summary>
    public SettingsViewModel(
        ISessionService sessionService,
        Settings.GeneralSettingsViewModel generalSettingsViewModel,
        Settings.UserManagementViewModel userManagementViewModel,
        Settings.DepartmentManagementViewModel departmentManagementViewModel,
        Settings.UnitSettingsViewModel unitSettingsViewModel,
        Settings.DataManagementSettingsViewModel dataManagementSettingsViewModel,
        Settings.SystemInfoViewModel systemInfoViewModel)
    {
        _sessionService = sessionService;

        // 注入子 ViewModel
        GeneralSettingsViewModel = generalSettingsViewModel;
        UserManagementViewModel = userManagementViewModel;
        DepartmentManagementViewModel = departmentManagementViewModel;
        UnitSettingsViewModel = unitSettingsViewModel;
        DataManagementSettingsViewModel = dataManagementSettingsViewModel;
        SystemInfoViewModel = systemInfoViewModel;

                try { _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper; } catch { }

                        // 初始化权限
                        InitializePermissions();

                        _logHelper?.Information("设置页面初始化完成");
                    }

                    /// <summary>
                    /// 初始化权限
                    /// </summary>
                    private void InitializePermissions()
                    {
                        var isAdmin = _sessionService.HasPermission("usermanagement");
                        var isOperator = _sessionService.HasPermission("patientmanagement");
                        ShowUserManagementTab = isAdmin;
                        ShowDataManagementTab = isAdmin;
                        ShowUnitSettingsTab = isAdmin;
                        ShowDepartmentTab = isAdmin;
                        ShowDeviceConfigTab = isAdmin || isOperator;
                    }
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
            public string RoleDisplay => GetLocalizedRole(User.Role);
            public string Phone => User.Phone ?? "--";
            public string DepartmentName => "--"; // TODO: 需要从数据库加载
            public string StatusDisplay => User.IsEnabled ? GetLocalizedString("Enabled") : GetLocalizedString("Disabled");
            public string StatusColor => User.IsEnabled ? "#4CAF50" : "#9E9E9E";
            public string CreatedAtDisplay => User.CreatedAt.ToString(BtfxConstants.DATETIME_LIST_FORMAT);
            public string LastLoginDisplay => User.LastLoginAt?.ToString(BtfxConstants.DATETIME_LIST_FORMAT) ?? GetLocalizedString("NeverLoggedIn");

            public bool IsBuiltIn => User.Username == BtfxConstants.ADMIN_USERNAME ||
                                     User.Username == BtfxConstants.USER_USERNAME ||
                                     User.Username == BtfxConstants.GUEST_USERNAME;

            public UserItem(User user, int rowNumber)
            {
                User = user;
                RowNumber = rowNumber;
            }

            private static string GetLocalizedString(string key)
            {
                try
                {
                    var resource = System.Windows.Application.Current.FindResource(key);
                    return resource?.ToString() ?? key;
                }
                catch
                {
                    return key;
                }
            }

            private static string GetLocalizedRole(UserRole role)
            {
                return role switch
                {
                    UserRole.Administrator => GetLocalizedString("Administrator"),
                    UserRole.Operator => GetLocalizedString("Operator"),
                    UserRole.Guest => GetLocalizedString("Guest"),
                    _ => "--"
                };
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
            public string Description => "--"; // 暂时返回默认值，等待模型添加Description字段
            public string Phone => Department.Phone ?? "--";
            public string CreatedAtDisplay => Department.CreatedAt.ToString(BtfxConstants.DATETIME_LIST_FORMAT);

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
            public string CreatedAtDisplay => CreatedAt.ToString(BtfxConstants.DATETIME_FORMAT);
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
