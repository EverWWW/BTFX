using BTFX.Common;
using BTFX.Models;
using BTFX.Models.Camera;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using ToolHelper.LoggingDiagnostics.Abstractions;
using BtfxConstants = BTFX.Common.Constants;

namespace BTFX.ViewModels;

/// <summary>
/// 设置视图模型 - 作为设置页面的容器，管理子ViewModel和Tab权限控制
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISessionService _sessionService;
    private readonly ICameraCaptureSettingsService _cameraCaptureSettingsService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;
    private CameraCaptureSettings _cameraCaptureSettings = new();

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

    /// <summary>
    /// 侧面相机ID
    /// </summary>
    [ObservableProperty]
    private string _sideCameraName = string.Empty;

    /// <summary>
    /// 正面相机ID
    /// </summary>
    [ObservableProperty]
    private string _frontCameraName = string.Empty;

    /// <summary>
    /// 第三方相机配置软件路径
    /// </summary>
    [ObservableProperty]
    private string _externalCameraConfigToolPath = string.Empty;

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
        ICameraCaptureSettingsService cameraCaptureSettingsService,
        Settings.GeneralSettingsViewModel generalSettingsViewModel,
        Settings.UserManagementViewModel userManagementViewModel,
        Settings.DepartmentManagementViewModel departmentManagementViewModel,
        Settings.UnitSettingsViewModel unitSettingsViewModel,
        Settings.DataManagementSettingsViewModel dataManagementSettingsViewModel,
        Settings.SystemInfoViewModel systemInfoViewModel,
        ILocalizationService localizationService)
    {
        _sessionService = sessionService;
        _cameraCaptureSettingsService = cameraCaptureSettingsService;
        _localizationService = localizationService;

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
                        LoadDeviceConfig();

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

                    private void LoadDeviceConfig()
                    {
                        _cameraCaptureSettings = _cameraCaptureSettingsService.Load();
                        SideCameraName = _cameraCaptureSettings.SideCameraName;
                        FrontCameraName = _cameraCaptureSettings.FrontCameraName;
                        ExternalCameraConfigToolPath = _cameraCaptureSettings.ExternalConfigToolPath;
                    }

                    [RelayCommand]
                    private void SaveDeviceConfig()
                    {
                        _cameraCaptureSettings.SideCameraName = SideCameraName.Trim();
                        _cameraCaptureSettings.FrontCameraName = FrontCameraName.Trim();
                        _cameraCaptureSettings.ExternalConfigToolPath = ExternalCameraConfigToolPath.Trim();
                        _cameraCaptureSettingsService.Save(_cameraCaptureSettings);
                        _logHelper?.Information("保存设备配置");
                    }

                    [RelayCommand]
                    private void SelectExternalCameraConfigTool()
                    {
                        var dialog = new OpenFileDialog
                        {
                            Title = _localizationService.GetString("DeviceSettings.SelectCameraConfigTool"),
                            Filter = _localizationService.GetString("DeviceSettings.ExecutableFilter")
                        };

                        if (dialog.ShowDialog() == true)
                        {
                            ExternalCameraConfigToolPath = dialog.FileName;
                            SaveDeviceConfig();
                        }
                    }

                    [RelayCommand]
                    private void OpenExternalCameraConfigTool()
                    {
                        if (string.IsNullOrWhiteSpace(ExternalCameraConfigToolPath) || !File.Exists(ExternalCameraConfigToolPath))
                        {
                            System.Windows.MessageBox.Show(
                                _localizationService.GetString("DeviceSettings.InvalidCameraConfigToolPath"),
                                _localizationService.GetString("Tip"),
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Information);
                            return;
                        }

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = ExternalCameraConfigToolPath,
                            UseShellExecute = true
                        });
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
        /// 主题色选项
        /// </summary>
        public class ThemeColorOption : ObservableObject
        {
            public string ColorHex { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set => SetProperty(ref _isSelected, value);
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
            public string Code => string.IsNullOrWhiteSpace(Department.Code) ? "--" : Department.Code;
            public string Description => string.IsNullOrWhiteSpace(Department.Description) ? "--" : Department.Description;
            public string Phone => Department.Phone ?? "--";
            public string CreatedAtDisplay => Department.CreatedAt.ToString(BtfxConstants.DATETIME_LIST_FORMAT);

            [ObservableProperty]
            private bool _isChecked;

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
