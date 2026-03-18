; Inno Setup script for SekoDL
; Build after publishing the app:
;   dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

#define MyAppName "SekoDL"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Seko"
#define MyAppExeName "SekoDL.exe"
#define MyAppSourceDir "..\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{9D4B7D74-DFAB-4D92-9E36-7ABF7B3D5D4D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=SekoDL-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

[Tasks]
Name: "registerprotocol"; Description: "Register sekodl:// custom protocol"; Flags: unchecked
Name: "registerchrome"; Description: "Register Chrome native host"; Flags: unchecked
Name: "registeredge"; Description: "Register Edge native host"; Flags: unchecked
Name: "registerfirefox"; Description: "Register Firefox native host"; Flags: unchecked

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\Install\register-sekodl-protocol.ps1"; DestDir: "{app}\Install"; Flags: ignoreversion
Source: "..\Install\register-native-host-chrome.ps1"; DestDir: "{app}\Install"; Flags: ignoreversion
Source: "..\Install\register-native-host-edge.ps1"; DestDir: "{app}\Install"; Flags: ignoreversion
Source: "..\Install\register-native-host-firefox.ps1"; DestDir: "{app}\Install"; Flags: ignoreversion
Source: "..\Install\sekodl.nativehost.chrome.json"; DestDir: "{app}\Install"; Flags: ignoreversion
Source: "..\Install\sekodl.nativehost.edge.json"; DestDir: "{app}\Install"; Flags: ignoreversion
Source: "..\Install\sekodl.nativehost.firefox.json"; DestDir: "{app}\Install"; Flags: ignoreversion
Source: "..\BrowserExtension\chrome-edge\*"; DestDir: "{app}\BrowserExtension\chrome-edge"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\BrowserExtension\firefox\*"; DestDir: "{app}\BrowserExtension\firefox"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Install\register-sekodl-protocol.ps1"""; Description: "Register sekodl:// protocol"; Flags: runhidden postinstall skipifsilent; Tasks: registerprotocol
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Install\register-native-host-chrome.ps1"""; Description: "Register Chrome native host"; Flags: runhidden postinstall skipifsilent; Tasks: registerchrome
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Install\register-native-host-edge.ps1"""; Description: "Register Edge native host"; Flags: runhidden postinstall skipifsilent; Tasks: registeredge
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Install\register-native-host-firefox.ps1"""; Description: "Register Firefox native host"; Flags: runhidden postinstall skipifsilent; Tasks: registerfirefox
Filename: "{app}\{#MyAppExeName}"; Description: "Launch SekoDL"; Flags: nowait postinstall skipifsilent
