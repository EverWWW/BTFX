; ============================================================================
; BTFX 步态智能分析系统 - Inno Setup 安装脚本
; Gait Intelligent Analysis System - Inno Setup Script
; 版本：1.0.0.1
; ============================================================================

#define MyAppName "步态智能分析系统"
#define MyAppNameEn "Gait Intelligent Analysis System"
#define MyAppVersion "1.0.0.1"
#define MyAppPublisher "BTFX Team"
#define MyAppURL "https://github.com/EverWWW/BTFX"
#define MyAppExeName "BTFX.exe"
#define MyAppId "{{B7F8E9D2-3A4C-5B6E-7F8A-9B0C1D2E3F4A}"

; 发布目录（相对于此脚本文件）
#define PublishDir "..\..\publish\win-x64"

[Setup]
; 应用程序信息
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; 默认安装路径
DefaultDirName={autopf}\BTFX
DefaultGroupName={#MyAppName}

; 许可协议和自述文件
LicenseFile=license.txt
InfoBeforeFile=readme.txt

; 输出设置
OutputDir=Output
OutputBaseFilename=BTFX_Setup_{#MyAppVersion}

; 压缩设置
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; 安装程序图标（如果有 ico 文件则取消注释）
; SetupIconFile=Assets\installer.ico

; 权限设置
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; 向导样式
WizardStyle=modern
WizardSizePercent=120

; 安装程序版本信息
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoCopyright=Copyright © 2024-2026 {#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

; 架构
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; 卸载设置
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
; 语言支持 / Language Support
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
; 中文自定义消息
chinesesimplified.CreateDesktopIcon=创建桌面快捷方式(&D)
chinesesimplified.LaunchProgram=立即启动 %1
chinesesimplified.DataDirInfo=数据目录将创建在安装目录下

; 英文自定义消息
english.CreateDesktopIcon=Create a &desktop shortcut
english.LaunchProgram=Launch %1
english.DataDirInfo=Data directory will be created in the installation folder

[Tasks]
; 附加任务
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 主程序文件（从发布目录复制所有文件）
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; 许可协议和自述文件
Source: "license.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "readme.txt"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
; 创建数据目录
Name: "{app}\Data"
Name: "{app}\Data\Database"
Name: "{app}\Data\Logs"
Name: "{app}\Data\Reports"
Name: "{app}\Data\Backups"
Name: "{app}\Data\Videos"
Name: "{app}\Data\Temp"
Name: "{app}\Data\Config"

[Icons]
; 开始菜单快捷方式
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{#MyAppNameEn}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

; 桌面快捷方式
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "{#MyAppNameEn}"

[Run]
; 安装完成后运行
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 卸载时删除数据目录（可选，默认注释掉以保留用户数据）
; Type: filesandordirs; Name: "{app}\Data"

[Code]
// Pascal Script 代码

// 检查是否已安装旧版本
function IsUpgrade(): Boolean;
var
  UninstallKey: String;
begin
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}_is1';
  Result := RegKeyExists(HKLM, UninstallKey) or RegKeyExists(HKCU, UninstallKey);
end;

// 初始化安装向导
function InitializeSetup(): Boolean;
begin
  Result := True;
  
  // 如果是升级安装，可以在这里添加额外的提示
  if IsUpgrade() then
  begin
    // 升级安装
    Log('Upgrade installation detected');
  end;
end;

// 卸载时询问是否保留数据
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
  ResultCode: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{app}\Data');
    if DirExists(DataDir) then
    begin
      if MsgBox('是否删除用户数据（数据库、日志、报告等）？' + #13#10 + 
                'Delete user data (database, logs, reports, etc.)?', 
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
