#ifndef AppVersion
  #error Specify the app version with /DAppVersion.
#endif

#ifndef PublishDir
  #define PublishDir "..\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
#endif

[Setup]
AppId={{918C6E84-A3F9-4DD5-9F41-B0632147CF3B}
AppName=Nibbles
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\Nibbles
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\Nibbles.exe
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\..\artifacts\Nibbles\{#AppVersion}
OutputBaseFilename=Nibbles-{#AppVersion}-Windows-x64-Setup
SetupIconFile={#PublishDir}\appicon.ico
WizardStyle=modern
DisableWelcomePage=no
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Nibbles"; Filename: "{app}\Nibbles.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Nibbles"; Filename: "{app}\Nibbles.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Nibbles.exe"; Description: "Launch Nibbles"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
