; OptiMaxing installer script (Inno Setup 6).
;
; Build order:
;   1. dotnet publish src\OptiMaxing.App\OptiMaxing.App.csproj -c Release -r win-x64 --self-contained true
;      (this already produces a single-file, self-extracting OptiMaxing.exe per the csproj settings)
;   2. Compile this script with ISCC.exe (Inno Setup 6, https://jrsoftware.org/isinfo.php):
;        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\OptiMaxing.iss
;   3. Output lands in installer\output\OptiMaxing-Setup-<version>.exe
;
; Code signing: NOT wired into this script. OptiMaxing modifies system registry/services/
; scheduled tasks and requests admin elevation (see src\OptiMaxing.App\app.manifest), so an
; unsigned installer will trigger SmartScreen warnings on first run. A proper release needs
; either an EV code-signing certificate (removes SmartScreen friction near-immediately) or a
; standard OV certificate (builds reputation over time via repeated downloads). Until a
; certificate is purchased, ship as-is with a README note telling users to expect and dismiss
; the SmartScreen "unrecognized app" prompt. If/when a cert + signtool are available, sign both
; publish\OptiMaxing.exe and the compiled Setup.exe before distribution:
;   signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 /a publish\OptiMaxing.exe
;   signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 /a installer\output\OptiMaxing-Setup-*.exe

#define MyAppName "OptiMaxing"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "OptiMaxing"
#define MyAppURL "https://github.com/IvanOstroumov/OptiMaxing"
#define MyAppExeName "OptiMaxing.exe"

[Setup]
AppId={{6C8B6A6E-6E0B-4B0E-9E60-4C3F1E9C4E10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; The app itself requires admin (app.manifest requestedExecutionLevel=requireAdministrator);
; the installer must run elevated too so it can write to Program Files.
PrivilegesRequired=admin
OutputDir=output
OutputBaseFilename=OptiMaxing-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
; Not signed yet — see header comment. Remove/replace once a certificate is available.
;SignTool=signtool $p

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Single self-contained exe produced by dotnet publish (see header). No other files expected
; because PublishSingleFile + IncludeNativeLibrariesForSelfExtract bundle the runtime inside it.
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; OptiMaxing writes its JSON backup/restore-point-tracking state under %LOCALAPPDATA%;
; deliberately NOT deleted on uninstall so a user who reinstalls later can still revert
; tweaks applied by a previous install. Documented here so this isn't mistaken for an oversight.
