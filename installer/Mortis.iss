; Inno Setup script. Run installer\build.cmd rather than compiling this directly: it publishes first.
#define AppExe "..\publish\Mortis.exe"
#define AppVer GetVersionNumbersString(AppExe)

[Setup]
AppId={{222A2F30-5EC0-4419-83DA-8CEED6E7797A}
AppName=Mortis
AppVersion={#AppVer}
AppPublisher=E-5Manny
; per-user install under %LOCALAPPDATA%\Programs: no admin prompt
PrivilegesRequired=lowest
DefaultDirName={autopf}\Mortis
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=MortisSetup-{#AppVer}
SetupIconFile=..\assets\icon.ico
UninstallDisplayIcon={app}\Mortis.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: desktopicon; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Mortis"; Filename: "{app}\Mortis.exe"
Name: "{autodesktop}\Mortis"; Filename: "{app}\Mortis.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Mortis.exe"; Description: "{cm:LaunchProgram,Mortis}"; Flags: nowait postinstall skipifsilent

; The save lives in %APPDATA%\Mortis, outside {app}, so uninstalling never touches it.
