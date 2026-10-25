; Windows setup for Intuime. install-windows.ps1 compiles this with Inno Setup.

#define AppName "Intuime"
#define AppVersion "1.0.0"
#define PublishDir "..\publish\win-x64"

[Setup]
AppId={{C3A91E24-7B58-4F0D-9E6A-2D4B8F1C0A77}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\publish
OutputBaseFilename=Intuime-Setup
SetupIconFile=..\src\Intuime\Assets\intuime.ico
UninstallDisplayIcon={app}\Intuime.exe
UninstallDisplayName={#AppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\Intuime.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Intuime.exe"

[Run]
Filename: "{app}\Intuime.exe"; Description: "Open Intuime"; Flags: nowait postinstall skipifsilent
