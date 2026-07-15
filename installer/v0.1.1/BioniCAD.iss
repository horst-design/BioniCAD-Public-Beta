; BioniCAD Installer-Skript (Inno Setup)
; Baut ein Setup.exe, das die self-contained Release-Version (dotnet publish)
; zusammen mit dem ui/- und wiki/-Ordner installiert und einen Start-Shortcut anlegt.
; Keine Admin-Rechte nötig (Installation nach %LocalAppData%).

#define MyAppName "BioniCAD"
#define MyAppVersion "0.1.1"
#define MyAppPublisher "Horst"
#define MyAppExeName "BioniCAD-Start.bat"

[Setup]
AppId={{A4C9E2D1-6F3B-4E8A-9C2D-1F7B3A9E5D6C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=BioniCAD-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Files]
Source: "..\..\src\LatticeFraktal\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\ui\*"; DestDir: "{app}\ui"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\wiki\*"; DestDir: "{app}\wiki"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "BioniCAD-Start.bat"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknüpfung erstellen"; GroupDescription: "Zusätzliche Symbole:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "BioniCAD jetzt starten"; Flags: nowait postinstall skipifsilent
