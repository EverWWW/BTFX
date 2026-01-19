# AboutDialog 和 ConfirmDialog 集成指南

## 概述

本文档说明如何在 BTFX 项目中使用已实现的 AboutDialog（关于我们）和 ConfirmDialog（确认对话框）。

## 已完成的修改

### 1. SystemInfoView - 添加"关于"按钮

**文件：** `BTFX/Views/Settings/SystemInfoView.xaml`

**修改内容：** 在应用信息标题旁边添加了"关于我们"按钮

### 2. SettingsViewModel - 添加显示对话框的命令

**文件：** `BTFX/ViewModels/SettingsViewModel.cs`

**新增命令：**
- `ShowAboutDialogCommand` - 显示关于对话框
- `ShowConfirmDialogAsync` - 辅助方法，简化确认对话框的使用

## 需要添加的本地化资源

在 `Strings.zh.xaml` 和 `Strings.en.xaml` 中添加以下资源键：

```xml
<!-- 确认对话框相关 -->
<system:String x:Key="ConfirmImportSettings">导入设置将覆盖当前的通用设置和单位设置，是否继续？</system:String>
<system:String x:Key="ConfirmDeleteUser">确定要删除用户 {0} 吗？此操作无法撤销。</system:String>
<system:String x:Key="ConfirmToggleUserStatus">确定要{0}用户 {1} 吗？</system:String>
<system:String x:Key="ConfirmDeleteDepartment">确定要删除科室 {0} 吗？此操作无法撤销。</system:String>
<system:String x:Key="ConfirmRestoreBackup">恢复备份将覆盖当前数据，此操作不可撤销！确定要继续吗？</system:String>
<system:String x:Key="ConfirmCleanupLogs">确定要清理 {0} 天前的日志吗？此操作不可撤销！</system:String>
<system:String x:Key="Enable">启用</system:String>
<system:String x:Key="DisableUser">禁用</system:String>
<system:String x:Key="InternalVersionLabel">内部版本：</system:String>

<!-- DataManagement相关 -->
<system:String x:Key="ToggleSelectAll">全选/取消全选</system:String>
<system:String x:Key="ConfirmDelete">确认删除</system:String>
<system:String x:Key="MeasurementDate">测量日期</system:String>
<system:String x:Key="BatchExport">批量导出</system:String>
<system:String x:Key="BatchDelete">批量删除</system:String>
<system:String x:Key="Actions">操作</system:String>
<system:String x:Key="Total">共</system:String>
<system:String x:Key="RecordsSelected">条，已选</system:String>
<system:String x:Key="Items">项</system:String>
<system:String x:Key="Page">页</system:String>
<system:String x:Key="Pages">页</system:String>
<system:String x:Key="GoToPage">跳转到</system:String>
<system:String x:Key="Go">跳转</system:String>
```

英文版本（`Strings.en.xaml`）：

```xml
<!-- Confirm Dialog Related -->
<system:String x:Key="ConfirmImportSettings">Importing settings will overwrite current general and unit settings. Continue?</system:String>
<system:String x:Key="ConfirmDeleteUser">Are you sure you want to delete user {0}? This action cannot be undone.</system:String>
<system:String x:Key="ConfirmToggleUserStatus">Are you sure you want to {0} user {1}?</system:String>
<system:String x:Key="ConfirmDeleteDepartment">Are you sure you want to delete department {0}? This action cannot be undone.</system:String>
<system:String x:Key="ConfirmRestoreBackup">Restoring backup will overwrite current data. This action cannot be undone! Continue?</system:String>
<system:String x:Key="ConfirmCleanupLogs">Are you sure you want to cleanup logs older than {0} days? This action cannot be undone!</system:String>
<system:String x:Key="Enable">enable</system:String>
<system:String x:Key="DisableUser">disable</system:String>
<system:String x:Key="InternalVersionLabel">Internal Version:</system:String>

<!-- DataManagement Related -->
<system:String x:Key="ToggleSelectAll">Select All/Deselect All</system:String>
<system:String x:Key="ConfirmDelete">Confirm Delete</system:String>
<system:String x:Key="MeasurementDate">Measurement Date</system:String>
<system:String x:Key="BatchExport">Batch Export</system:String>
<system:String x:Key="BatchDelete">Batch Delete</system:String>
<system:String x:Key="Actions">Actions</system:String>
<system:String x:Key="Total">Total</system:String>
<system:String x:Key="RecordsSelected">records, selected</system:String>
<system:String x:Key="Items">items</system:String>
<system:String x:Key="Page">Page</system:String>
<system:String x:Key="Pages">pages</system:String>
<system:String x:Key="GoToPage">Go to page</system:String>
<system:String x:Key="Go">Go</system:String>
```

## 推荐的使用位置

### AboutDialog

1. ✅ **系统信息页面**（已实现）- 点击"关于我们"按钮显示
2. **主菜单** - 在设置或帮助菜单中添加
3. **登录界面** - 可选，在页脚添加"关于"链接

### ConfirmDialog

建议替换以下位置的 MessageBox 确认对话框：

#### 用户管理（UserManagement）
- ✅ 删除用户
- ✅ 重置密码
- ✅ 启用/禁用用户

#### 科室管理（DepartmentManagement）
- ✅ 删除科室

#### 数据管理（DataManagement）
- 批量删除测量记录
- 删除单条测量记录

#### 患者管理（PatientManagement）
- 删除患者

#### 备份与恢复（Backup & Restore）
- ✅ 恢复备份
- ✅ 清理日志

#### 设置管理（Settings）
- ✅ 导入设置
- 重置设置到默认值

#### 其他
- 退出登录确认
- 关闭程序确认
- 覆盖文件确认

## 使用示例

### 1. 显示 AboutDialog

```csharp
/// <summary>
/// 显示关于对话框
/// </summary>
[RelayCommand]
private async Task ShowAboutDialogAsync()
{
    try
    {
        var dialog = new AboutDialog();
        await DialogHost.Show(dialog, "RootDialog");
    }
    catch (Exception ex)
    {
        _logHelper?.Error("显示关于对话框失败", ex);
    }
}
```

在 XAML 中绑定：
```xaml
<Button
    Command="{Binding ShowAboutDialogCommand}"
    Content="{DynamicResource AboutUs}"
    Style="{StaticResource MaterialDesignFlatButton}" />
```

### 2. 使用 ConfirmDialog

**辅助方法（已添加到 SettingsViewModel）：**

```csharp
/// <summary>
/// 显示确认对话框
/// </summary>
private async Task<bool> ShowConfirmDialogAsync(string title, string message)
{
    try
    {
        var dialog = new ConfirmDialog
        {
            DataContext = new { Title = title, Message = message }
        };
        
        var result = await DialogHost.Show(dialog, "RootDialog");
        return result is bool boolResult && boolResult;
    }
    catch (Exception ex)
    {
        _logHelper?.Error("显示确认对话框失败", ex);
        return false;
    }
}
```

**使用示例：**

```csharp
/// <summary>
/// 删除用户
/// </summary>
[RelayCommand]
private async Task DeleteUserAsync(UserItem? item)
{
    if (item == null) return;

    var confirmed = await ShowConfirmDialogAsync(
        _localizationService.GetString("ConfirmDelete"),
        string.Format(_localizationService.GetString("ConfirmDeleteUser"), item.User.Username));

    if (confirmed)
    {
        try
        {
            var success = await _userService.DeleteUserAsync(item.User.Id);
            if (success)
            {
                await LoadUsersAsync();
                _logHelper?.Information($"删除用户: {item.User.Username}");
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除用户失败: {item.User.Username}", ex);
        }
    }
}
```

### 3. 在其他 ViewModel 中使用

对于其他 ViewModel（如 DataManagementViewModel、MainContainerViewModel），需要：

1. **添加辅助方法**：
```csharp
private async Task<bool> ShowConfirmDialogAsync(string title, string message)
{
    try
    {
        var dialog = new ConfirmDialog
        {
            DataContext = new { Title = title, Message = message }
        };
        
        var result = await DialogHost.Show(dialog, "RootDialog");
        return result is bool boolResult && boolResult;
    }
    catch (Exception ex)
    {
        _logHelper?.Error("显示确认对话框失败", ex);
        return false;
    }
}
```

2. **替换 MessageBox.Show 调用**：

原代码：
```csharp
var result = System.Windows.MessageBox.Show(
    "确定要删除吗？",
    "确认删除",
    System.Windows.MessageBoxButton.YesNo,
    System.Windows.MessageBoxImage.Question);

if (result == System.Windows.MessageBoxResult.Yes)
{
    // 执行删除
}
```

新代码：
```csharp
var confirmed = await ShowConfirmDialogAsync(
    _localizationService.GetString("ConfirmDelete"),
    _localizationService.GetString("ConfirmDeleteMessage"));

if (confirmed)
{
    // 执行删除
}
```

## 注意事项

1. **DialogHost Identifier**  
   - 确保使用的 DialogHost.Identifier 存在（本项目使用 "RootDialog"）
   - 通常在 MainContainerView 或 MainWindow 中定义

2. **异步操作**  
   - Dialog显示使用 `await DialogHost.Show()`，确保调用方法是异步的
   - 方法签名要有 `async Task` 或 `async Task<T>`

3. **本地化**  
   - 所有对话框文本都应该使用 `_localizationService.GetString()`
   - 确保资源键在中英文资源文件中都有定义

4. **错误处理**  
   - 总是用 try-catch 包裹对话框显示代码
   - 记录异常到日志

5. **数据上下文**  
   - ConfirmDialog 需要一个包含 `Title` 和 `Message` 属性的对象作为 DataContext
   - 可以使用匿名对象：`new { Title = ..., Message = ... }`

## 测试checklist

- [ ] AboutDialog 在系统信息页面正常显示
- [ ] AboutDialog 显示正确的应用信息和版本号
- [ ] AboutDialog 支持中英文切换
- [ ] ConfirmDialog 在删除操作前正确显示
- [ ] ConfirmDialog 的"是/否"按钮工作正常
- [ ] ConfirmDialog 支持中英文切换
- [ ] 确认对话框取消时不执行操作
- [ ] 确认对话框确认时正确执行操作
- [ ] 对话框在不同主题下显示正常
- [ ] 对话框的背景遮罩正确显示

## 后续改进建议

1. **创建 DialogService**  
   封装对话框显示逻辑，避免在每个 ViewModel 中重复代码：
   ```csharp
   public interface IDialogService
   {
       Task ShowAboutAsync();
       Task<bool> ShowConfirmAsync(string title, string message);
       Task ShowMessageAsync(string title, string message);
   }
   ```

2. **统一错误提示对话框**  
   创建 ErrorDialog/MessageDialog 用于显示错误和信息提示

3. **输入对话框**  
   创建 InputDialog 用于需要用户输入的场景

4. **进度对话框**  
   创建 ProgressDialog 用于长时间操作的进度显示

## 相关文件

- `BTFX/Views/Dialogs/AboutDialog.xaml` - 关于对话框视图
- `BTFX/Views/Dialogs/ConfirmDialog.xaml` - 确认对话框视图
- `BTFX/Views/Dialogs/ConfirmDialog.xaml.cs` - 确认对话框代码
- `BTFX/ViewModels/SettingsViewModel.cs` - 示例实现
- `BTFX/Views/Settings/SystemInfoView.xaml` - "关于"按钮示例
- `BTFX/Resources/Localization/Strings.zh.xaml` - 中文资源
- `BTFX/Resources/Localization/Strings.en.xaml` - 英文资源
