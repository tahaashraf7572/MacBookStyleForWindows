; Inno Setup script — install with Inno Setup 6 (https://jrsoftware.org/isinfo.php)
; Build order:
;   1. dotnet publish MacBookStyleForWindows.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o publish
;   2. Compile this script with Inno Setup -> outputs "MacBook Style for Windows Setup.exe"

#define MyAppName "MacBook Style for Windows"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "MacBookStyleForWindows"
#define MyAppExeName "MacBookStyleForWindows.exe"

[Setup]
AppId={{9F2C1E2A-4B7D-4C1E-9A2D-1F5C6E7A8B90}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=MacBook Style for Windows Setup
OutputDir=output
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=lowest
; Runs per-user, no admin rights needed — matches the "easy install/uninstall" requirement.

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "startupicon"; Description: "Start automatically when Windows starts"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--silent"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Restore the user's original wallpaper and remove the startup entry before files are deleted.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--restore-and-exit"; Flags: waituntilterminated runhidden; RunOnceId: "RestoreOriginal"
