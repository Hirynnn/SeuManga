#define AppName "SeuManga"
#define AppVersion "0.2.0"
#define PublishDir "..\src\SeuManga.Desktop\bin\Release\net10.0-windows\win-x64\publish"

[Setup]
AppId={{8C9B7420-5F01-42CB-B78A-D312C450038F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=SeuManga
DefaultDirName={localappdata}\Programs\SeuManga
DefaultGroupName=SeuManga
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=SeuManga-Setup-{#AppVersion}-win-x64
SetupIconFile=..\src\SeuManga.Desktop\Assets\SeuManga.ico
UninstallDisplayIcon={app}\SeuManga.exe
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
Compression=lzma2
SolidCompression=yes

[Languages]
Name: "brazilian_portuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SeuManga"; Filename: "{app}\SeuManga.exe"
Name: "{autodesktop}\SeuManga"; Filename: "{app}\SeuManga.exe"; Tasks: desktopicon

[Code]
const
  RuntimeKey = 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  CurrentUserKey = 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function RuntimeVersionAt(Root: Integer; Key: string): string;
var Value: string;
begin
  Value := '';
  if RegQueryStringValue(Root, Key, 'pv', Value) then
    Result := Value
  else
    Result := '';
end;

function HasWebView2Runtime: Boolean;
var Version: string;
begin
  Version := RuntimeVersionAt(HKLM64, RuntimeKey);
  if (Version = '') or (Version = '0.0.0.0') then
    Version := RuntimeVersionAt(HKCU, CurrentUserKey);
  Result := (Version <> '') and (Version <> '0.0.0.0');
end;

function InitializeSetup: Boolean;
var ErrorCode: Integer;
begin
  Result := True;
  if not HasWebView2Runtime then begin
    MsgBox('O Runtime Evergreen do Microsoft Edge WebView2 é necessário para o leitor. Instale-o pelo site oficial da Microsoft e execute este instalador novamente.', mbInformation, MB_OK);
    ShellExec('open', 'https://developer.microsoft.com/microsoft-edge/webview2/', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    Result := False;
  end;
end;
