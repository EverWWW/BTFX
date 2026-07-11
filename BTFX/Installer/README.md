# BTFX 安装包构建指南

## 概述

本目录包含 BTFX 步态智能分析系统的安装包构建脚本和相关资源。

## 目标电脑运行依赖

安装包包含主程序、.NET 运行时、FFmpeg/FFprobe、大恒托管程序集以及算法 EXE 和 TOML 配置。以下内容不进入安装包，需要在部署时单独准备：

1. **大恒相机环境**：使用 DAHENG 相机时，目标电脑需要安装与设备匹配的 Galaxy SDK/驱动。仅复制 `GxIAPINET.dll` 不能替代原生 SDK 和 USB3 Vision 驱动。
2. **算法运行环境**：将与当前算法 EXE 版本匹配的 `_internal` 文件夹复制到安装目录的 `gait_analysis_gpu` 下，最终结构应为 `gait_analysis_gpu\_internal`。
3. **显卡环境**：GPU 算法所需的 NVIDIA 驱动和 CUDA 兼容条件以算法版本交付说明为准；更换算法 EXE 时必须同步更换对应 `_internal`。

程序会在打开视频采集或开始分析前检查上述依赖，并提示缺失路径。发布安装包前仍应在一台未安装开发环境的目标电脑上完成一次录制和分析闭环测试。

## 目录结构

```
Installer/
├── BTFX.iss              # Inno Setup 安装脚本
├── build-installer.ps1   # PowerShell 构建脚本
├── build-installer.bat   # 批处理入口（双击运行）
├── license.txt           # 许可协议
├── readme.txt            # 自述文件
├── README.md             # 本文档
├── Assets/               # 安装包资源目录
│   ├── installer.ico     # 安装包图标（可选）
│   ├── header.bmp        # 向导头部图（可选）
│   └── sidebar.bmp       # 侧边栏图（可选）
└── Output/               # 输出目录（构建后生成）
    └── BTFX_Setup_x.x.x.x.exe
```

## 环境要求

### 必需

1. **Windows 10/11 x64**
2. **.NET 10 SDK**
   - 下载：https://dotnet.microsoft.com/download
   - 验证：`dotnet --version`
3. **Inno Setup 6.x**
   - 下载：https://jrsoftware.org/isinfo.php
   - 安装后确保 `ISCC.exe` 在默认路径

### 可选

- Visual Studio 2022+ （用于开发调试）
- 代码签名证书（避免 SmartScreen 警告）

## 快速开始

### 方法一：双击运行

1. 双击 `build-installer.bat`
2. 等待构建完成
3. 安装包生成在 `Output/` 目录

### 方法二：PowerShell 命令

```powershell
# 完整构建
.\build-installer.ps1

# 跳过发布（使用现有发布文件）
.\build-installer.ps1 -SkipPublish

# 仅发布，不生成安装包
.\build-installer.ps1 -SkipInnoSetup
```

### 方法三：手动步骤

```bash
# 1. 发布项目
cd BTFX
dotnet publish -c Release -r win-x64 --self-contained true -o ..\publish\win-x64

# 2. 编译安装包
cd Installer
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" BTFX.iss
```

## 输出说明

| 文件 | 说明 |
|------|------|
| `BTFX_Setup_1.0.0.1.exe` | 最终安装包（约 150-200MB） |

## 安装包功能

### 安装向导

1. 选择语言（中文/英文）
2. 欢迎页面
3. 许可协议
4. 选择安装路径
5. 创建桌面快捷方式（可选）
6. 安装进度
7. 完成（可选启动程序）

### 安装内容

- BTFX.exe 及所有依赖
- .NET 10 运行时（自包含）
- 数据目录结构
- 许可协议和自述文件

### 快捷方式

- **桌面**：步态智能分析系统（可选）
- **开始菜单**：
  - 步态智能分析系统
  - 卸载

### 卸载

- 删除程序文件
- 删除快捷方式
- 询问是否删除用户数据

## 自定义配置

### 修改版本号

1. 编辑 `BTFX\BTFX.csproj`：
   ```xml
   <Version>1.0.0.2</Version>
   <FileVersion>1.0.0.2</FileVersion>
   <AssemblyVersion>1.0.0.2</AssemblyVersion>
   ```

2. 编辑 `BTFX\Installer\BTFX.iss`：
   ```
   #define MyAppVersion "1.0.0.2"
   ```

3. 编辑 `BTFX\Common\Constants.cs`：
   ```csharp
   public const string VERSION_FULL = "V1.0.0.2";
   ```

### 添加安装包图标

1. 准备 256x256 的 ICO 文件
2. 放置到 `Assets\installer.ico`
3. 取消 `BTFX.iss` 中的注释：
   ```
   SetupIconFile=Assets\installer.ico
   ```

### 添加向导图片

| 图片 | 尺寸 | 用途 |
|------|------|------|
| `header.bmp` | 150x57 | 向导顶部横幅 |
| `sidebar.bmp` | 164x314 | 向导左侧边栏 |

## 故障排除

### 问题：找不到 ISCC.exe

**原因**：Inno Setup 未安装或不在默认路径

**解决**：
1. 下载并安装 Inno Setup 6：https://jrsoftware.org/isinfo.php
2. 或手动指定路径（修改 `build-installer.ps1`）

### 问题：dotnet publish 失败

**原因**：SDK 版本不匹配或项目错误

**解决**：
1. 确认 .NET 10 SDK 已安装：`dotnet --list-sdks`
2. 先在 Visual Studio 中确认项目可以编译

### 问题：安装包运行时被杀毒软件拦截

**原因**：未签名的可执行文件

**解决**：
1. 购买代码签名证书
2. 使用 `signtool` 签名 EXE 和安装包
3. 或在杀毒软件中添加信任

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| 1.0.0.1 | 2025-01 | 初始版本 |

---

**版权所有 © 2024-2026 BTFX Team**
