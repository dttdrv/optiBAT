[Setup]
AppId={{A1B2C3D4-E5F6-7890-A1B2-C3D4E5F67890}
AppName=optiBAT
AppVersion=1.0.0
AppPublisher=optiSuite
AppPublisherURL=https://github.com/deyan
DefaultDirName={autopf}\optiBAT
DefaultGroupName=optiBAT
AllowNoIcons=yes
OutputDir=output
OutputBaseFilename=OptiBat-Setup-1.0.0
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
WizardStyle=modern
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName=optiBAT
SetupIconFile=..\src\OptiBat\Resources\app.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startonboot"; Description: "Start optiBAT when Windows starts"; GroupDescription: "Startup:"

[Files]
Source: "..\portable\OptiBat.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\optiBAT"; Filename: "{app}\OptiBat.exe"
Name: "{autodesktop}\optiBAT"; Filename: "{app}\OptiBat.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\OptiBat.exe"; Parameters: "--register-task"; StatusMsg: "Registering scheduled task..."; Flags: runhidden
Filename: "{app}\OptiBat.exe"; Parameters: "--register-task --start-at-logon"; StatusMsg: "Enabling start at logon..."; Tasks: startonboot; Flags: runhidden
Filename: "{app}\OptiBat.exe"; Description: "{cm:LaunchProgram,optiBAT}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM OptiBat.exe"; Flags: runhidden; RunOnceId: "KillApp"
Filename: "{app}\OptiBat.exe"; Parameters: "--uninstall"; Flags: runhidden; RunOnceId: "Unregister"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\optiBAT"
