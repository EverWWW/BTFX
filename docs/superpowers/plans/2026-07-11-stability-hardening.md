# BTFX 稳定性加固实施计划

> **执行要求：** 在当前会话中按任务顺序执行；每个行为先增加失败测试，再完成最小实现并运行完整测试。

**目标：** 修复已确认的相机生命周期、导入回滚、测量删除、资源泄漏、预览并发、凭据安全和部署依赖问题。

**架构：** 保留现有 WPF、MVVM、SqlSugar 和 FFmpeg 结构。将路径边界、生命周期状态、密码格式和依赖检测提取为可测试的小型组件；数据库修改继续通过现有服务完成，并以事务包围多表操作。

**技术栈：** .NET 10、WPF、CommunityToolkit.Mvvm、SqlSugar、xUnit、FFmpeg、GxIAPINET。

## 全局约束

- 不改变现有界面布局和正常业务流程。
- 不修改双相机同步参数和视频编码参数。
- 删除测量时删除软件拥有的分析结果目录，不删除外部导入或录制的原始视频。
- 算法 `_internal` 和大恒 GalaxySDK/U3V 驱动继续由部署人员单独提供。
- 所有新增用户提示必须支持中英文。

---

### 任务一：相机弹窗关闭状态

**文件：**
- 新建：`BTFX/Services/Implementations/CameraDialogLifetime.cs`
- 修改：`BTFX/ViewModels/CameraCaptureDialogViewModel.cs`
- 修改：`BTFX/Views/Dialogs/CameraCaptureDialog.xaml.cs`
- 测试：`BTFX.Tests/CameraDialogLifetimeTests.cs`

**接口：**
- `CameraDialogLifetime.IsClosed`
- `CameraDialogLifetime.TryBeginPreview()`
- `CameraDialogLifetime.Close()`
- `CameraCaptureDialogViewModel.StopAllMediaWork()` 进入终止状态且保持幂等。

- [ ] 写测试：关闭前允许预览，关闭后永久拒绝预览，重复关闭无异常。
- [ ] 运行测试并确认因类型不存在而失败。
- [ ] 实现生命周期组件并接入所有预览启动、取消和异常恢复路径。
- [ ] 运行相机专项测试和完整测试。

### 任务二：结果包安全解压和完整性校验

**文件：**
- 新建：`BTFX/Services/Implementations/ArchiveImportFileStager.cs`
- 修改：`BTFX/Services/Implementations/ExportImportService.cs`
- 测试：`BTFX.Tests/ArchiveImportFileStagerTests.cs`

**接口：**
- `ArchiveImportFileStager.ResolveSafePath(string root, string relativePath)`
- `ArchiveImportFileStager.ExtractAndValidateAsync(...)`
- 校验文件大小和 SHA256，路径必须位于根目录内。

- [ ] 写测试：正常路径、兄弟目录前缀逃逸、缺少文件、大小不符和 SHA256 不符。
- [ ] 运行测试并确认失败。
- [ ] 实现临时目录解压和校验，替换当前直接写正式目录的逻辑。
- [ ] 运行专项测试。

### 任务三：导入事务和文件回滚

**文件：**
- 修改：`BTFX/Services/Implementations/ExportImportService.cs`
- 测试：`BTFX.Tests/ArchiveImportTransactionTests.cs`

**接口：**
- 导入开始后调用 `BeginTran()`；成功调用 `CommitTran()`；异常或取消调用 `RollbackTran()`。
- 临时目录仅在数据库准备成功后提升为正式目录。

- [ ] 写集成测试：构造第二阶段失败的归档，验证没有新增测量且没有残留正式目录。
- [ ] 运行测试并确认当前实现留下记录。
- [ ] 接入数据库事务、临时目录清理和正式目录回滚。
- [ ] 运行导入测试和完整测试。

### 任务四：测量关联记录和结果目录删除

**文件：**
- 新建：`BTFX/Services/Implementations/MeasurementResultFileQuarantine.cs`
- 修改：`BTFX/Services/Implementations/MeasurementService.cs`
- 修改：`BTFX/Services/Interfaces/IMeasurementService.cs`（仅在返回结果需要表达文件警告时修改）
- 测试：`BTFX.Tests/MeasurementDeletionTests.cs`

**接口：**
- `MeasurementResultFileQuarantine.Stage(...)`
- `Restore()`、`CommitDelete()` 和 `Dispose()` 均幂等。
- 删除顺序：报告、CSV、质量信息、运动学汇总、分析结果、步态参数、测量。

- [ ] 写测试：删除全部关联表和结果目录，保留外部源视频；数据库失败时恢复目录。
- [ ] 运行测试并确认失败。
- [ ] 实现安全路径解析、隔离移动和数据库事务删除。
- [ ] 批量删除复用同一删除实现并正确报告数量。
- [ ] 运行数据库专项测试和完整测试。

### 任务五：临时弹窗和媒体资源释放

**文件：**
- 修改：`BTFX/ViewModels/CameraCaptureDialogViewModel.cs`
- 修改：`BTFX/ViewModels/ReportPreviewDialogViewModel.cs`
- 修改：`BTFX/ViewModels/AboutDialogViewModel.cs`
- 修改：对应三个弹窗的 `.xaml.cs`
- 修改：`BTFX/Views/Dialogs/CameraCaptureDialog.xaml.cs`
- 测试：`BTFX.Tests/DialogLifetimeSubscriptionTests.cs`

**接口：**
- 三个瞬态 ViewModel 实现 `IDisposable`，使用具名语言变更处理器。
- 弹窗卸载时只调用一次 `Dispose()`。

- [ ] 写测试：释放后触发语言变更不再更新 ViewModel。
- [ ] 运行测试并确认失败。
- [ ] 接入退订和幂等释放，删除强制 `GC.Collect()`。
- [ ] 运行测试和内存相关静态检查。

### 任务六：回放预览任务版本和取消

**文件：**
- 新建：`BTFX/Services/Implementations/PreviewGenerationCoordinator.cs`
- 修改：`BTFX/Views/Measurement/Step3ReviewView.xaml.cs`
- 测试：`BTFX.Tests/PreviewGenerationCoordinatorTests.cs`

**接口：**
- `PreviewGenerationCoordinator.Begin()` 返回包含版本号和令牌的任务租约。
- `CancelCurrent()` 使旧租约失效。
- 仅 `IsCurrent(version)` 为真时允许发布媒体源。

- [ ] 写测试：第二次生成使第一次失效，卸载取消当前任务。
- [ ] 运行测试并确认失败。
- [ ] 将取消令牌传入 `WaitForExitAsync`，取消时终止 FFmpeg，临时文件增加版本号。
- [ ] 运行预览测试和完整测试。

### 任务七：凭据与账户密码迁移

**文件：**
- 新建：`BTFX/Helpers/CredentialProtector.cs`
- 修改：`BTFX/Helpers/PasswordHelper.cs`
- 修改：`BTFX/Services/Implementations/SettingsService.cs`
- 修改：`BTFX/Services/Implementations/AuthenticationService.cs`
- 修改：`BTFX/Services/Implementations/UserService.cs`
- 测试：`BTFX.Tests/CredentialSecurityTests.cs`

**接口：**
- `CredentialProtector.Protect/Unprotect` 使用 DPAPI CurrentUser，并识别旧 AES 数据。
- `PasswordHelper.HashPasswordPbkdf2` 返回带格式版本、迭代次数、盐值和哈希的字符串。
- `PasswordHelper.VerifyPasswordWithMigration` 返回验证结果及是否需要升级。

- [ ] 写测试：DPAPI 往返、旧 AES 迁移、PBKDF2 验证、错误密码、旧 SHA256 升级。
- [ ] 运行测试并确认失败。
- [ ] 实现新格式并保持旧数据兼容。
- [ ] 登录成功后持久化升级后的哈希。
- [ ] 运行认证测试和完整测试。

### 任务八：运行依赖预检查

**文件：**
- 新建：`BTFX/Services/Interfaces/IRuntimeDependencyPreflightService.cs`
- 新建：`BTFX/Services/Implementations/RuntimeDependencyPreflightService.cs`
- 修改：`BTFX/App.xaml.cs`
- 修改：相机打开和分析启动入口 ViewModel/Service。
- 修改：`BTFX/Localization/Strings.zh-CN.xaml`
- 修改：`BTFX/Localization/Strings.en-US.xaml`
- 测试：`BTFX.Tests/RuntimeDependencyPreflightTests.cs`

**接口：**
- `CheckAnalysis()` 检查 FFmpeg、FFprobe、算法 EXE 和 `_internal`。
- `CheckDaheng()` 检查托管程序集并通过受控 SDK 初始化确认原生环境。
- 返回结构化缺失项，不在检查服务内弹窗。

- [ ] 写测试：完整环境、缺少算法、缺少 `_internal`、缺少 FFmpeg 和大恒 SDK 加载失败。
- [ ] 运行测试并确认失败。
- [ ] 注册服务并在功能入口显示本地化错误。
- [ ] 运行测试和中英文资源键检查。

### 任务九：最终验证

- [ ] 运行 `dotnet test BTFX.slnx`，要求全部通过。
- [ ] 运行 `dotnet build BTFX.slnx -c Debug`，要求零错误。
- [ ] 运行 Release `dotnet publish` 到系统临时目录。
- [ ] 运行 NuGet 漏洞扫描。
- [ ] 检查 `git diff --check`、变更范围和工作区状态。
- [ ] 汇总需要实机验证的录制中关闭、双相机重开、导入失败和换机部署场景。
