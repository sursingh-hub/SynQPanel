; SynQPanel Installer Script
; Public Preview – GPL v3
; NOTE: Administrator privileges are required for AIDA64 Shared Memory access

#define MyAppName "SynQPanel"
#define MyAppVersion GetFileVersion("..\SynQPanel\bin\publish\win-x64\SynQPanel.exe")
#define MyAppPublisher "SynQPanel Contributors"
#define MyAppURL "https://github.com/sursingh-hub/SynQPanel"
#define MyAppExeName "SynQPanel.exe"

[Setup]
; IMPORTANT: Unique AppId for SynQPanel
AppId={{B8A0A5F3-9F3A-4D2E-9C5A-9D8E2C7F9A11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright={#MyAppPublisher}

DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no

; Admin required for shared memory access
PrivilegesRequired=admin

OutputBaseFilename=SynQPanel_Setup_v1.0.1
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

SetupIconFile=..\SynQPanel\Resources\Images\favicon.ico
UninstallDisplayIcon={app}\SynQPanel.exe
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional options";

[Files]
Source: "..\SynQPanel\bin\publish\win-x64\*"; Excludes: "*.pdb"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs


[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch SynQPanel"; Flags: nowait postinstall runascurrentuser

