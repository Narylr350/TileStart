#define AppName "TileStart"
#ifndef AppVersion
#define AppVersion "0.1.0"
#endif
#define AppPublisher "Narylr350"
#define AppExeName "TileStart.Host.exe"

[Setup]
AppId={{A42394D4-9E18-46F2-9DBA-D391397EE12F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
AppendDefaultDirName=yes
DirExistsWarning=yes
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
; Autostart and Explorer context menus intentionally remain per-user.
UsedUserAreasWarning=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=TileStart-Setup-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}
; Explorer hosts TileStart.ShellHook.dll. Never let Restart Manager close the
; desktop shell just to replace an in-use hook; PrepareToInstall stops the Host
; and waits for the hook to unload instead.
CloseApplications=no
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked

[Files]
Source: "..\artifacts\package\TileStart\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[UninstallDelete]
; This marker is the only installer-created file outside Inno Setup's normal
; install log. Never add a recursive {app} deletion rule here: foreign files
; in the directory must survive uninstall.
Type: files; Name: "{app}\.tilestart-installation"

[Icons]
Name: "{group}\TileStart"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\TileStart"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TileStart"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\*\shell\TileStart.AddToAppList"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\TileStart.PinTile"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\TileStart.AddToAppList"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\TileStart.PinTile"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.exe\shell\TileStart.AddToAppList"; Flags: deletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.exe\shell\TileStart.PinTile"; Flags: deletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.lnk\shell\TileStart.AddToAppList"; Flags: deletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.lnk\shell\TileStart.PinTile"; Flags: deletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.appref-ms\shell\TileStart.AddToAppList"; Flags: deletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.appref-ms\shell\TileStart.PinTile"; Flags: deletekey

[Run]
Filename: "{app}\{#AppExeName}"; Parameters: "--configure-nvidia-overlay"; Flags: runhidden waituntilterminated; StatusMsg: "正在配置 NVIDIA Overlay 兼容性..."
Filename: "{app}\{#AppExeName}"; Description: "启动 TileStart"; Flags: nowait postinstall skipifsilent

[Code]
function DirectoryIsEmpty(const Directory: String): Boolean;
var
  FindRec: TFindRec;
begin
  Result := True;
  if not DirExists(Directory) then
    exit;

  if FindFirst(AddBackslash(Directory) + '*', FindRec) then
  begin
    repeat
      if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
      begin
        Result := False;
        break;
      end;
    until not FindNext(FindRec);
    FindClose(FindRec);
  end;
end;

function IsRootDirectory(const Directory: String): Boolean;
var
  NormalizedDirectory: String;
begin
  NormalizedDirectory := RemoveBackslashUnlessRoot(Directory);
  Result := (Length(NormalizedDirectory) > 0) and
    (NormalizedDirectory[Length(NormalizedDirectory)] = '\');
end;

function IsExistingTileStartDirectory(const Directory: String): Boolean;
var
  BasePath: String;
begin
  BasePath := AddBackslash(Directory);
  Result := FileExists(BasePath + 'TileStart.Host.exe') and
    (FileExists(BasePath + '.tilestart-installation') or
      (FileExists(BasePath + 'unins000.exe') and FileExists(BasePath + 'unins000.dat')));
end;

function ValidateInstallDirectory(var ErrorMessage: String): Boolean;
var
  InstallDirectory: String;
begin
  InstallDirectory := WizardDirValue;
  ErrorMessage := '';

  if IsRootDirectory(InstallDirectory) then
    ErrorMessage := '不能将 TileStart 直接安装到磁盘或共享根目录。请选择专用的 TileStart 子目录。'
  else if DirExists(InstallDirectory) and not DirectoryIsEmpty(InstallDirectory) and
    not IsExistingTileStartDirectory(InstallDirectory) then
    ErrorMessage := '所选目录已包含其他内容，并且不是现有的 TileStart 安装目录。' + #13#10 +
      '为防止覆盖或卸载其他文件，请选择空目录或新建 TileStart 专用子目录。';

  Result := ErrorMessage = '';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ErrorMessage: String;
begin
  Result := True;
  if (CurPageID = wpSelectDir) and not ValidateInstallDirectory(ErrorMessage) then
  begin
    MsgBox(ErrorMessage, mbError, MB_OK);
    Result := False;
  end;
end;

function WaitForTileStartHost(TimeoutMs: Integer): Boolean;
var
  ElapsedMs: Integer;
begin
  ElapsedMs := 0;
  while CheckForMutexes('Local\TileStart.Host') and (ElapsedMs < TimeoutMs) do
  begin
    Sleep(250);
    ElapsedMs := ElapsedMs + 250;
  end;

  Result := not CheckForMutexes('Local\TileStart.Host');
end;

function StopTileStart: Boolean;
var
  AppPath: String;
  ResultCode: Integer;
begin
  AppPath := ExpandConstant('{app}\TileStart.Host.exe');
  if FileExists(AppPath) then
  begin
    Exec(AppPath, '--shutdown', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;

  if not WaitForTileStartHost(10000) then
  begin
    { Killing only the Host still lets its watcher observe the process exit and
      unload the Explorer hook. Never kill the Injector directly. }
    Exec(ExpandConstant('{cmd}'), '/C taskkill /IM TileStart.Host.exe /F >NUL 2>&1', '', SW_HIDE,
      ewWaitUntilTerminated, ResultCode);
  end;

  Result := WaitForTileStartHost(10000);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  if not ValidateInstallDirectory(Result) then
    exit;

  if StopTileStart then
    Result := ''
  else
    Result := '无法安全停止 TileStart。为避免影响 Windows 资源管理器，安装已中止。请重启 Windows 后重试。';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    SaveStringToFile(ExpandConstant('{app}\.tilestart-installation'),
      'TileStart installation directory', False);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    StopTileStart;
    if FileExists(ExpandConstant('{app}\TileStart.Host.exe')) then
    begin
      Exec(ExpandConstant('{app}\TileStart.Host.exe'), '--remove-nvidia-overlay-configuration', '', SW_HIDE,
        ewWaitUntilTerminated, ResultCode);
    end;
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'TileStart');
  end;
end;
