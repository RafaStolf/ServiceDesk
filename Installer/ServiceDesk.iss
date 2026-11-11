#ifndef AppVersion
  #error AppVersion deve ser informado pelo workflow.
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

[Setup]
AppId={{8EB83B1F-CE63-48F5-B7E0-5179A5B79D51}
AppName=ServiceDesk
AppVersion={#AppVersion}
AppPublisher=Rafael Stolf Silva
AppPublisherURL=https://github.com/RafaStolf/ServiceDesk
AppSupportURL=https://github.com/RafaStolf/ServiceDesk/issues
AppUpdatesURL=https://github.com/RafaStolf/ServiceDesk/releases/latest
DefaultDirName={localappdata}\Programs\ServiceDesk
DefaultGroupName=ServiceDesk
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\installer-output
OutputBaseFilename=ServiceDesk-Setup-win-x64
SetupIconFile=..\Assets\AppIcon.ico
UninstallDisplayIcon={app}\ServiceDesk.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
AppMutex=Local\ServiceDesk
CloseApplications=yes
RestartApplications=no
#ifdef SignedBuild
SignTool=ServiceDeskSign
SignedUninstaller=yes
#endif

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\ServiceDesk"; Filename: "{app}\ServiceDesk.exe"; WorkingDir: "{app}"; AppUserModelID: "Centrium.ServiceDesk"
Name: "{userdesktop}\ServiceDesk"; Filename: "{app}\ServiceDesk.exe"; WorkingDir: "{app}"; AppUserModelID: "Centrium.ServiceDesk"; Tasks: desktopicon

[Run]
Filename: "{app}\ServiceDesk.exe"; Description: "Abrir ServiceDesk"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupCommand: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Não remover a entrada de outra cópia portátil ou uma configuração do usuário. }
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run',
      'ServiceDesk', StartupCommand) then
      if CompareText(StartupCommand, '"' + ExpandConstant('{app}\ServiceDesk.exe') + '" --background') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ServiceDesk');
  end;
end;
