; =====================================================================
; Inno Setup Script for Universal Smart Fan Cooling Hub
; Generates a professional single-file Setup EXE installer with Wizard, 
; Desktop/Start Menu shortcuts, Auto-start with Windows, and Uninstaller.
; =====================================================================

#define MyAppName "Smart Fan Cooling Hub"
#define MyAppVersion "1.0.0"
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
OutputDir=OutputInstaller
OutputBaseFilename=Smart_Fan_Cooling_Setup_v1.2
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Khởi chạy ứng dụng cùng Windows (System Startup)"; GroupDescription: "Tùy chọn bổ sung:"

[Files]
Source: "bin\Release\net10.0-windows10.0.22621.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Gỡ cài đặt {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LlanoSmartFanCooling"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: runascurrentuser postinstall skipifsilent
