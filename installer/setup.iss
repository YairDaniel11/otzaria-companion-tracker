; Inno Setup script. Build: ISCC /DAppVersion=1.0.0 installer\setup.iss
; Expects the assembled app in ..\dist\OtzariaCompanionTracker\
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{B0E7B3A2-5C1D-4F6E-9A55-3D2C7A1E8F10}
AppName=Otzaria Companion Tracker
AppVersion={#AppVersion}
AppPublisher=Otzaria Companion Tracker
DefaultDirName={autopf}\OtzariaCompanionTracker
DefaultGroupName=Otzaria Companion Tracker
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=OtzariaCompanionTracker-Setup
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\OtzariaCompanionGuard.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
#if FileExists(CompilerPath + "Languages\Hebrew.isl")
Name: "hebrew"; MessagesFile: "compiler:Languages\Hebrew.isl"
#else
Name: "english"; MessagesFile: "compiler:Default.isl"
#endif

[Tasks]
Name: "autostart"; Description: "הפעל את המעקב אוטומטית עם ווינדוס (מומלץ)"
Name: "desktopicon"; Description: "צור קיצור דרך בשולחן העבודה"; Flags: unchecked

[Files]
Source: "..\dist\OtzariaCompanionTracker\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
; Start-menu entry: opens the settings window (starts the background guard if needed)
Name: "{autoprograms}\מעקב תוכנות נלוות לאוצריא"; Filename: "{app}\OtzariaCompanionGuard.exe"; Parameters: "--show"
Name: "{autodesktop}\מעקב תוכנות נלוות לאוצריא"; Filename: "{app}\OtzariaCompanionGuard.exe"; Parameters: "--show"; Tasks: desktopicon

[Run]
Filename: "{app}\OtzariaCompanionGuard.exe"; Parameters: "--autostart on --show"; Description: "הפעל את התוכנה עכשיו"; Tasks: autostart; Flags: nowait postinstall skipifsilent
Filename: "{app}\OtzariaCompanionGuard.exe"; Parameters: "--autostart off --show"; Description: "הפעל את התוכנה עכשיו"; Tasks: not autostart; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM OtzariaCompanionGuard.exe"; Flags: runhidden; RunOnceId: "KillGuard"
Filename: "taskkill.exe"; Parameters: "/F /IM otzaria_companion_settings.exe"; Flags: runhidden; RunOnceId: "KillUi"
Filename: "reg.exe"; Parameters: "delete HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v OtzariaCompanionTracker /f"; Flags: runhidden; RunOnceId: "DelAutostart"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  rc: Integer;
begin
  // close a running copy so files can be replaced (upgrade)
  Exec('taskkill.exe', '/F /IM OtzariaCompanionGuard.exe', '', SW_HIDE, ewWaitUntilTerminated, rc);
  Exec('taskkill.exe', '/F /IM otzaria_companion_settings.exe', '', SW_HIDE, ewWaitUntilTerminated, rc);
  Result := '';
end;
