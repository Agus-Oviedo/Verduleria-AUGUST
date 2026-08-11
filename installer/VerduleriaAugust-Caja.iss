#define MyAppName "Augustu - Caja"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Augustu - Frutas y Verduras"
#define MyAppExeName "VerduleriaAugust.ScaleAgent.Setup.exe"

[Setup]
AppId={{8F4F74A3-D742-4DCE-903B-96C20E1E87E5}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Augustu Caja
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\Installer
OutputBaseFilename=Augustu-Caja-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\Configurator\{#MyAppExeName}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "..\artifacts\CajaPayload\Agent\*"; DestDir: "{app}\Agent"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\CajaPayload\Configurator\*"; DestDir: "{app}\Configurator"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Configurador Balanza Augustu"; Filename: "{app}\Configurator\{#MyAppExeName}"
Name: "{autodesktop}\Configurador Balanza Augustu"; Filename: "{app}\Configurator\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: checkedonce

[Run]
Filename: "{app}\Configurator\{#MyAppExeName}"; Description: "Abrir Configurador Balanza Augustu"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop VerduleriaAugustScaleAgent"; Flags: runhidden; RunOnceId: "StopScaleAgent"
Filename: "{sys}\sc.exe"; Parameters: "delete VerduleriaAugustScaleAgent"; Flags: runhidden; RunOnceId: "DeleteScaleAgent"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsWin64 then
  begin
    MsgBox('Este instalador requiere Windows de 64 bits.', mbError, MB_OK);
    Result := False;
  end;
end;
