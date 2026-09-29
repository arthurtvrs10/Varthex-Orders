#ifndef AppVersion
  #error AppVersion obrigatoria
#endif

[Setup]
AppId={{7C6D4F08-6BE2-4DCC-A515-D574AC1C2901}
AppName=Varthex Comanda
AppVersion={#AppVersion}
AppPublisher=Varthex
DefaultDirName={localappdata}\Programs\VarthexComanda
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputPath}
OutputBaseFilename=VarthexComanda-{#AppVersion}-Setup-win-x64
SetupIconFile=..\backend\src\VarthexComanda.Desktop\Assets\VarthexComanda.ico
UninstallDisplayIcon={app}\VarthexComanda.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UninstallDisplayName=Varthex Comanda

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"

[Files]
Source: "{#PublishPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\Varthex Comanda"; Filename: "{app}\VarthexComanda.exe"
Name: "{userdesktop}\Varthex Comanda"; Filename: "{app}\VarthexComanda.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VarthexComanda.exe"; Description: "Abrir Varthex Comanda"; Flags: nowait postinstall skipifsilent
