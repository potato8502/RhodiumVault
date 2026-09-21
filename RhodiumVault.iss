[Setup]
AppId={{42EBBF0C-5DAD-4891-8B35-159FBD854524}
AppName=Rhodium Vault
AppVersion=1.3.0
AppPublisher=Rhodium Software
AppPublisherURL=
DefaultDirName={localappdata}\Programs\Rhodium Vault
DefaultGroupName=Rhodium Vault
UninstallDisplayIcon={app}\RhodiumVault.exe
OutputDir=installer
OutputBaseFilename=RhodiumVault-Setup
Compression=lzma2
SolidCompression=yes
SetupIconFile=app.ico
PrivilegesRequired=lowest
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
german.AppDescription=Lokaler, verschluesselter Passwort-Tresor - keine Cloud, kein Account
english.AppDescription=Local, encrypted password vault - no cloud, no account

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Rhodium Vault"; Filename: "{app}\RhodiumVault.exe"
Name: "{group}\{cm:UninstallProgram,Rhodium Vault}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\Rhodium Vault"; Filename: "{app}\RhodiumVault.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\RhodiumVault.exe"; Description: "{cm:LaunchProgram,Rhodium Vault}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
