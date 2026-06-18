; ============================================================================
; BTFX - Inno Setup installer script
; App: 步态信息采集系统 / Gait Information Collection System
; ============================================================================

#define MyAppName "步态信息采集系统"
#define MyAppNameEn "Gait Information Collection System"
#define MyAppVersion "1.0.0.1"
#define MyAppPublisher "BTFX Team"
#define MyAppURL "https://github.com/EverWWW/BTFX"
#define MyAppExeName "BTFX.exe"
#define MyAppId "{{B7F8E9D2-3A4C-5B6E-7F8A-9B0C1D2E3F4A}"

; Publish output relative to this installer script.
#define PublishDir "..\..\publish\win-x64"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; The app writes Data, video, and analysis output under the install directory.
; Install under the user profile by default to avoid Program Files write limits.
DefaultDirName={localappdata}\Programs\BTFX
DefaultGroupName={#MyAppName}

LicenseFile=license.txt
InfoBeforeFile=readme.txt

OutputDir=Output
OutputBaseFilename=BTFX_Setup_{#MyAppVersion}

Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; SetupIconFile=Assets\installer.ico

PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

WizardStyle=modern
WizardSizePercent=120

VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoCopyright=Copyright 2024-2026 {#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
chinesesimplified.CreateDesktopIcon=创建桌面快捷方式(&D)
chinesesimplified.LaunchProgram=立即启动 %1
chinesesimplified.AdditionalIcons=附加图标：
chinesesimplified.DeleteUserDataPrompt=是否删除用户数据（数据库、日志、报告等）？

english.CreateDesktopIcon=Create a &desktop shortcut
english.LaunchProgram=Launch %1
english.AdditionalIcons=Additional icons:
english.DeleteUserDataPrompt=Delete user data (database, logs, reports, etc.)?

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "license.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "readme.txt"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{app}\Data"
Name: "{app}\Data\Database"
Name: "{app}\Data\Logs"
Name: "{app}\Data\Reports"
Name: "{app}\Data\Backups"
Name: "{app}\Data\Videos"
Name: "{app}\Data\Temp"
Name: "{app}\Data\Config"
Name: "{app}\video"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{#MyAppNameEn}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "{#MyAppNameEn}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; User data is kept by default. Deletion is handled by the uninstall prompt below.

[Code]
function IsUpgrade(): Boolean;
var
  UninstallKey: String;
begin
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}_is1';
  Result := RegKeyExists(HKLM, UninstallKey) or RegKeyExists(HKCU, UninstallKey);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if IsUpgrade() then
  begin
    Log('Upgrade installation detected');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{app}\Data');
    if DirExists(DataDir) then
    begin
      if MsgBox(ExpandConstant('{cm:DeleteUserDataPrompt}'), mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
