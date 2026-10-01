#ifndef AppVersion
  #error AppVersion obrigatoria
#endif

[Setup]
AppId={{36454C2A-E5A2-498B-9EC1-30E3AC6C3700}
AppName=Comanda Demonstração
AppVersion={#AppVersion}
AppPublisher=Varthex
DefaultDirName={localappdata}\Programs\VarthexComandaDemo
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputPath}
OutputBaseFilename=VarthexComanda-Demo-{#AppVersion}-Setup-win-x64
SetupIconFile=..\backend\src\VarthexComanda.Desktop\Assets\VarthexComanda.ico
UninstallDisplayIcon={app}\VarthexComanda.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UninstallDisplayName=Comanda Demonstração

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho Comanda Demonstração"; GroupDescription: "Atalhos:"

[Files]
Source: "{#PublishPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\demo\catalogo.json"; DestDir: "{app}\demo"; Flags: ignoreversion
Source: "..\demo\README.md"; DestDir: "{app}\demo"; Flags: ignoreversion
Source: "..\demo\fotos\*.png"; DestDir: "{app}\demo\fotos"; Flags: ignoreversion
Source: "..\demo\edicao-demonstracao.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\Comanda Demonstração"; Filename: "{app}\VarthexComanda.exe"; Parameters: "--demonstracao"
Name: "{userdesktop}\Comanda Demonstração"; Filename: "{app}\VarthexComanda.exe"; Parameters: "--demonstracao"; Tasks: desktopicon

[Run]
Filename: "{app}\VarthexComanda.exe"; Parameters: "--demonstracao"; Description: "Abrir Comanda Demonstração"; Flags: nowait postinstall skipifsilent
