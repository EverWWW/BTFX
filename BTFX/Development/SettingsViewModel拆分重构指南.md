# SettingsViewModel 拆分重构指南

## 概述

将原来的大型 SettingsViewModel (1129行) 拆分为多个独立的 ViewModel，提高代码可维护性和可测试性。

## 拆分结构

```
BTFX/ViewModels/
├── Settings/                              # 新文件夹
│   ├── GeneralSettingsViewModel.cs       ✅ 已创建
│   ├── UserManagementViewModel.cs        ⏳ 待创建
│   ├── DepartmentManagementViewModel.cs  ⏳ 待创建
│   ├── UnitSettingsViewModel.cs          ⏳ 待创建
│   ├── DataManagementSettingsViewModel.cs⏳ 待创建
│   └── SystemInfoViewModel.cs            ⏳ 待创建
├── SettingsViewModel.cs                   🔄 需重构
├── UserEditViewModel.cs                   ✅ 已存在
└── DepartmentEditViewModel.cs             ✅ 已存在
```

## 已完成

### 1. GeneralSettingsViewModel ✅

**职责**：
- 语言切换
- 主题切换
- 设置导入/导出

**依赖服务**：
- `ISettingsService`
- `ILocalizationService`
- `IThemeService`
- `ILogHelper`

**属性**：
- `LanguageOptions` - 语言选项
- `SelectedLanguage` - 选中的语言
- `ThemeOptions` - 主题选项
- `SelectedTheme` - 选中的主题
- `IsSaving` - 是否正在保存

**命令**：
- `SaveGeneralSettingsCommand`
- `ExportSettingsCommand`
- `ImportSettingsCommand`

## 待创建的 ViewModel

### 2. UserManagementViewModel

**职责**：
- 用户列表显示
- 添加/编辑/删除用户
- 重置密码
- 启用/禁用用户

**需要提取的属性**：
```csharp
// 从 SettingsViewModel 提取：
- Users (ObservableCollection<UserItem>)
- SelectedUser (UserItem?)
- IsLoading (bool)
```

**需要提取的命令**：
```csharp
- LoadUsersCommand
- AddUserCommand  
- EditUserCommand
- ResetPasswordCommand
- ToggleUserStatusCommand
```

**依赖服务**：
- `IUserService`
- `IDepartmentService` (用于编辑对话框)
- `ILocalizationService`
- `ILogHelper`

### 3. DepartmentManagementViewModel

**职责**：
- 科室列表显示
- 添加/编辑/删除科室

**需要提取的属性**：
```csharp
- Departments (ObservableCollection<DepartmentItem>)
- SelectedDepartment (DepartmentItem?)
- IsLoading (bool)
```

**需要提取的命令**：
```csharp
- LoadDepartmentsCommand
- AddDepartmentCommand
- EditDepartmentCommand
- DeleteDepartmentCommand
```

**依赖服务**：
- `IDepartmentService`
- `ILocalizationService`
- `ILogHelper`

### 4. UnitSettingsViewModel

**职责**：
- 单位名称设置
- Logo上传/清除

**需要提取的属性**：
```csharp
- UnitName (string)
- LogoPath (string)
- HasLogo (bool)
- IsSaving (bool)
```

**需要提取的命令**：
```csharp
- SelectLogoCommand
- ClearLogoCommand
- SaveUnitSettingsCommand
```

**依赖服务**：
- `ISettingsService`
- `ILogHelper`

### 5. DataManagementSettingsViewModel

**职责**：
- 自动备份设置
- 手动备份/恢复
- 备份历史管理

**需要提取的属性**：
```csharp
- AutoBackupEnabled (bool)
- BackupTime (string)
- BackupRetainCount (int)
- BackupHistory (ObservableCollection<BackupHistoryItem>)
- IsSaving (bool)
- IsLoading (bool)
```

**需要提取的命令**：
```csharp
- BackupNowCommand
- LoadBackupHistoryCommand
- RestoreBackupCommand
- SaveBackupSettingsCommand
```

**依赖服务**：
- `IBackupService`
- `ISettingsService`
- `ILocalizationService`
- `ILogHelper`

### 6. SystemInfoViewModel

**职责**：
- 显示应用信息
- 显示系统信息
- 日志管理
- 显示关于对话框

**需要提取的属性**：
```csharp
- AppVersion (string)
- AppName (string)
- DatabasePath (string)
- DatabaseSize (string)
- LogDirectory (string)
- CurrentUsername (string)
- CurrentUserRole (string)
- LogTotalCount (int)
- LogTodayCount (int)
- LogCleanupDays (int)
- IsSaving (bool)
- IsLoading (bool)
```

**需要提取的命令**：
```csharp
- ShowAboutDialogCommand
- OpenLogDirectoryCommand
- ExportLogsCommand
- CleanupLogsCommand
- LoadLogStatisticsCommand
- RefreshLogStatisticsCommand
```

**依赖服务**：
- `ISessionService`
- `ILocalizationService`
- `ILogHelper`

## 重构后的 SettingsViewModel

保留为主协调器，负责：

**职责**：
- Tab显示控制（根据用户角色）
- 管理子 ViewModel 的生命周期
- 协调子 ViewModel 之间的交互

**保留的属性**：
```csharp
- SelectedTabIndex (int)
- ShowUserManagementTab (bool)
- ShowDataManagementTab (bool)
- ShowUnitSettingsTab (bool)
- ShowDepartmentTab (bool)
- ShowDeviceConfigTab (bool)
```

**新增的属性**（子 ViewModel）：
```csharp
- GeneralSettingsViewModel (GeneralSettingsViewModel)
- UserManagementViewModel (UserManagementViewModel)
- DepartmentManagementViewModel (DepartmentManagementViewModel)
- UnitSettingsViewModel (UnitSettingsViewModel)
- DataManagementSettingsViewModel (DataManagementSettingsViewModel)
- SystemInfoViewModel (SystemInfoViewModel)
```

**精简后的构造函数**：
```csharp
public SettingsViewModel(
    ISessionService sessionService,
    GeneralSettingsViewModel generalSettingsViewModel,
    UserManagementViewModel userManagementViewModel,
    DepartmentManagementViewModel departmentManagementViewModel,
    UnitSettingsViewModel unitSettingsViewModel,
    DataManagementSettingsViewModel dataManagementSettingsViewModel,
    SystemInfoViewModel systemInfoViewModel)
{
    _sessionService = sessionService;
    
    GeneralSettingsViewModel = generalSettingsViewModel;
    UserManagementViewModel = userManagementViewModel;
    DepartmentManagementViewModel = departmentManagementViewModel;
    UnitSettingsViewModel = unitSettingsViewModel;
    DataManagementSettingsViewModel = dataManagementSettingsViewModel;
    SystemInfoViewModel = systemInfoViewModel;
    
    InitializeTabVisibility();
}
```

## 更新 View 的 DataContext

### GeneralSettingsView.xaml
```xaml
<!-- 原来 -->
d:DataContext="{d:DesignInstance Type=vm:SettingsViewModel}"

<!-- 改为 -->
d:DataContext="{d:DesignInstance Type=vmSettings:GeneralSettingsViewModel}"

<!-- 绑定改为 -->
DataContext="{Binding GeneralSettingsViewModel}"
```

### UserManagementView.xaml
```xaml
d:DataContext="{d:DesignInstance Type=vmSettings:UserManagementViewModel}"
DataContext="{Binding UserManagementViewModel}"
```

### 其他 View 类似修改

## 依赖注入配置 (App.xaml.cs)

在 `ConfigureServices` 方法中添加：

```csharp
// Settings ViewModels
services.AddTransient<GeneralSettingsViewModel>();
services.AddTransient<UserManagementViewModel>();
services.AddTransient<DepartmentManagementViewModel>();
services.AddTransient<UnitSettingsViewModel>();
services.AddTransient<DataManagementSettingsViewModel>();
services.AddTransient<SystemInfoViewModel>();
services.AddTransient<SettingsViewModel>();
```

## 迁移步骤

### Step 1: 创建基础 ViewModel 文件
1. ✅ 创建 `ViewModels/Settings` 文件夹
2. ✅ 创建 `GeneralSettingsViewModel.cs`
3. ⏳ 创建其他5个 ViewModel 文件

### Step 2: 提取代码
1. 从 `SettingsViewModel.cs` 复制对应区域的代码到新 ViewModel
2. 移除不需要的依赖服务
3. 调整命名空间
4. 确保所有属性和命令都正确迁移

### Step 3: 重构 SettingsViewModel
1. 移除已迁移的代码
2. 添加子 ViewModel 属性
3. 更新构造函数
4. 保留 Tab 控制逻辑

### Step 4: 更新 View
1. 在每个 View 的 XAML 中添加命名空间：
   ```xaml
   xmlns:vmSettings="clr-namespace:BTFX.ViewModels.Settings"
   ```
2. 更新 `d:DataContext`
3. 在 SettingsView.xaml 中为每个子 View 设置 DataContext

### Step 5: 更新依赖注入
1. 在 `App.xaml.cs` 中注册所有新的 ViewModel
2. 确保注册顺序正确（先注册依赖，后注册使用者）

### Step 6: 测试
1. 编译项目
2. 测试每个设置页面的功能
3. 验证 Tab 切换
4. 验证用户权限控制

## 优点

1. **单一职责**：每个 ViewModel 只负责一个功能区域
2. **代码可读性**：每个文件更短，更容易理解
3. **可维护性**：修改某个功能不影响其他功能
4. **可测试性**：更容易编写单元测试
5. **团队协作**：多人可同时编辑不同的 ViewModel

## 注意事项

1. **数据共享**：如果子 ViewModel 之间需要共享数据，通过 SettingsViewModel 协调
2. **事件通知**：子 ViewModel 可以通过事件通知父 ViewModel
3. **生命周期**：使用 Transient 生命周期，每次打开设置页面创建新实例
4. **内存管理**：确保正确释放资源，特别是事件订阅

## 后续优化建议

1. **创建基类**：`SettingsViewModelBase` 包含通用属性和方法
2. **消息传递**：使用 WeakReferenceMessenger 在 ViewModel 间通信
3. **验证框架**：统一的输入验证机制
4. **对话框服务**：封装 DialogHost 调用

## 相关文件

- `BTFX/ViewModels/Settings/` - 所有拆分后的 ViewModel
- `BTFX/ViewModels/SettingsViewModel.cs` - 主协调器
- `BTFX/Views/Settings/` - 所有设置子视图
- `BTFX/Views/SettingsView.xaml` - 设置主视图
- `BTFX/App.xaml.cs` - 依赖注入配置
