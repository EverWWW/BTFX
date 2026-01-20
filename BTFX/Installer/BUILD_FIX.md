# 构建错误解决方案

## 问题已解决 ✅

PowerShell 脚本语法错误已修复。现在脚本可以正常运行。

## 当前状态

1. ✅ dotnet publish 成功 (生成了 309.85 MB 的发布文件)
2. ❌ Inno Setup 未安装

## 下一步操作

### 安装 Inno Setup

1. 访问：https://jrsoftware.org/isinfo.php
2. 下载 Inno Setup 6.x 
3. 运行安装程序（使用默认安装路径）
4. 安装完成后，再次运行 `build-installer.bat`

### 验证安装

安装完成后，确认以下路径之一存在：
- `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`
- `C:\Program Files\Inno Setup 6\ISCC.exe`

### 跳过 Inno Setup（仅测试发布）

如果只想测试 dotnet publish，可以运行：

```cmd
.\build-installer.bat -SkipInnoSetup
```

或

```powershell
.\build-installer.ps1 -SkipInnoSetup
```

## 文件说明

| 文件 | 说明 |
|------|------|
| `build-installer.bat` | 批处理入口（双击运行） |
| `build-installer.ps1` | PowerShell 构建脚本 |
| `BTFX.iss` | Inno Setup 安装脚本 |

## 已修复的问题

1. ✅ PowerShell 脚本语法错误
2. ✅ 字符串编码问题
3. ✅ 中文字符显示问题

---

**更新时间**: 2025-01-20
