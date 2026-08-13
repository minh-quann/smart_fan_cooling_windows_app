; =====================================================================
; Inno Setup Script for Universal Smart Fan Cooling Hub
; Generates a professional single-file Setup EXE installer with Wizard, 
; Desktop/Start Menu shortcuts, Auto-start with Windows, and Uninstaller.
;
; Version is passed from MSBuild via /DMyAppVersion="x.y.z"
; =====================================================================

#define MyAppName "Smart Fan Cooling Hub"
; Allow version override from command-line: ISCC /DMyAppVersion="1.0.1" installer.iss
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "Smart Cooling Technology"
#define MyAppURL "https://github.com/minh-quann/smart_fan_cooling_windows_app"
#define MyAppExeName "smart_fan_cooling_windows_app.exe"

[Setup]
AppId={{8F24C9A0-3B1B-4E8A-9A88-12D3C4E5F6A7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\SmartFanCooling
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\OutputInstaller
OutputBaseFilename=Smart_Fan_Cooling_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
CloseApplicationsFilter=*smart_fan_cooling_windows_app*
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\bin\x64\Release\net9.0-windows10.0.22621.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Gỡ cài đặt {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; Auto-start is now managed in-app via Windows Task Scheduler (not via Registry Run key)
; Only HKLM AppCompatFlags entry remains for admin elevation compatibility
[Registry]
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; ValueType: string; ValueName: "{app}\{#MyAppExeName}"; ValueData: "~ RUNASADMIN"; Flags: uninsdeletevalue

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: postinstall skipifsilent shellexec

[Code]
// Clean up stuck WinRing0 kernel driver service before install/uninstall
// to prevent LibreHardwareMonitor from failing to load Ring-0 driver
procedure CleanupKernelDriver();
var
  ResultCode: Integer;
begin
  // Kill any running instance of the app first
  Exec('taskkill.exe', '/F /IM smart_fan_cooling_windows_app.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
  // Stop the WinRing0 kernel driver service (may be in STOP_PENDING state)
  Exec('sc.exe', 'stop R0smart_fan_cooling_windows_app', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
  // Delete the driver service entry so LibreHardwareMonitor can create a fresh one
  Exec('sc.exe', 'delete R0smart_fan_cooling_windows_app', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
  // Also try to delete the .sys file left behind in the app directory
  DeleteFile(ExpandConstant('{app}\smart_fan_cooling_windows_app.sys'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CleanupKernelDriver();
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    CleanupKernelDriver();
  end;
end;
