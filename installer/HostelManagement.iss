; Installer for the Hostel Management System (Inno Setup 6).
; Built by .github/workflows/installer.yml:
;   dotnet publish -> publish\   then   ISCC /DAppVersion=1.0.0 installer\HostelManagement.iss
;
; - Installs to C:\HostelManagement (not Program Files), because the database, photos, documents, invoices
;   and backups are kept next to the program and must be writable.
; - .NET 8 is included (self-contained publish), so nothing else is needed except the Access Database Engine.
; - Reinstalling or updating never replaces the database or any data; uninstalling keeps the data folders.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

#define AppName "Hostel Management System"
#define AppPublisher "Shri Balaji Hostel"
#define AppExe "HostelManagement.exe"

[Setup]
AppId={{6F3B2A8E-4C1D-4E2B-9A57-0B3C8D1E2F40}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName=C:\HostelManagement
DisableDirPage=no
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=HostelManagement-Setup-{#AppVersion}
SetupIconFile=..\HostelManagement\Resources\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Creating C:\HostelManagement and giving the admin's Windows account write access needs administrator rights.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"

[Dirs]
; The application writes its data here, so every user of this PC may change files in these folders.
Name: "{app}"; Permissions: users-modify
Name: "{app}\Database"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{app}\Photos"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{app}\Documents"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{app}\Invoices"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{app}\Receipts"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{app}\Reports"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{app}\Agreements"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{app}\Backups"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{app}\Logs"; Permissions: users-modify; Flags: uninsneveruninstall

[Files]
; Program files (everything published except the database).
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "Database\*,*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
; The empty database is copied only on the first installation: an update never replaces the hostel's data.
Source: "{#PublishDir}\Database\HostelManagement.accdb"; DestDir: "{app}\Database"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "..\docs\Installation.md"; DestDir: "{app}"; DestName: "Installation.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\User Guide"; Filename: "{app}\UserGuide.pdf"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Program logs only; the database, photos, documents, invoices, receipts, reports, agreements and backups are kept.
Type: filesandordirs; Name: "{app}\Logs"

[Code]
const
  AceDownloadUrl = 'https://www.microsoft.com/en-us/download/details.aspx?id=54920';

{ The 64 bit Microsoft Access Database Engine (ACE OLE DB provider) registers one of these names. }
function AccessEngineInstalled(): Boolean;
begin
  Result := RegKeyExists(HKEY_CLASSES_ROOT_64, 'Microsoft.ACE.OLEDB.16.0') or
            RegKeyExists(HKEY_CLASSES_ROOT_64, 'Microsoft.ACE.OLEDB.12.0');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ErrorCode: Integer;
begin
  if (CurStep = ssPostInstall) and not AccessEngineInstalled() then
  begin
    if SuppressibleMsgBox(
      'The Microsoft Access Database Engine (64 bit) is not installed on this computer.' + #13#10 + #13#10 +
      'The Hostel Management System needs it to open its database. Please download and install ' +
      '"Microsoft Access Database Engine 2016 Redistributable", file accessdatabaseengine_X64.exe.' + #13#10 + #13#10 +
      'If 32 bit Microsoft Office is installed on this PC, the 64 bit engine cannot be installed; ' +
      'see Installation.txt in the program folder.' + #13#10 + #13#10 +
      'Open the Microsoft download page now?',
      mbInformation, MB_YESNO, IDYES) = IDYES then
    begin
      ShellExec('open', AceDownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
  end;
end;
