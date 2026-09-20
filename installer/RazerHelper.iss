; Inno Setup script for RazerHelper. Build it with installer\build.ps1.
;
; Setup refuses to install until the .NET Desktop Runtime the app needs is
; present, and says where to get it. It installs for the current user only
; (no administrator prompt); the app asks for administrator approval itself,
; later, only for the one action that needs it (stopping Razer's services).

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

; The oldest .NET major version the app runs on. Newer ones are accepted too.
#ifndef RequiredDotNetMajor
  #define RequiredDotNetMajor 10
#endif

; Where "dotnet publish" put the app.
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

[Setup]
AppId={{CE01BA03-F232-44FD-B752-AF65F2ECA437}
AppName=RazerHelper
AppVersion={#AppVersion}
AppPublisher=Paul Rodriguez
AppPublisherURL=https://github.com/Paulrod20/razer-helper
DefaultDirName={autopf}\RazerHelper
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 11.
MinVersion=10.0.22000
; If RazerHelper is running, Setup asks the user to close it instead of failing on a locked file.
AppMutex=Local\RazerHelper.SingleInstance
SetupIconFile=..\src\RazerHelper\Assets\RazerHelper.ico
UninstallDisplayIcon={app}\RazerHelper.exe
OutputDir=..\dist
OutputBaseFilename=RazerHelper-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\RazerHelper"; Filename: "{app}\RazerHelper.exe"

[Run]
Filename: "{app}\RazerHelper.exe"; Description: "Start RazerHelper"; Flags: nowait postinstall skipifsilent

[Code]
const
  DotNetDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/10.0';
  RequiredMajor = {#RequiredDotNetMajor};
  // .NET records the runtimes it installs here. Its installer writes to the
  // 32-bit view of the registry even for the 64-bit runtime, so that is the view to read.
  RuntimeRegistryKey = 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';

// "10.0.12" or "10.0.0-rc.1" -> 10. Anything that does not start with a number gives 0.
function MajorVersionOf(const Version: String): Integer;
var
  DotPosition: Integer;
begin
  DotPosition := Pos('.', Version);
  if DotPosition = 0 then
    DotPosition := Length(Version) + 1;
  Result := StrToIntDef(Copy(Version, 1, DotPosition - 1), 0);
end;

function RegistryHasRuntime(): Boolean;
var
  Versions: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(HKLM32, RuntimeRegistryKey, Versions) then
    for I := 0 to GetArrayLength(Versions) - 1 do
      if MajorVersionOf(Versions[I]) >= RequiredMajor then
        Result := True;
end;

// A second opinion, in case the registry entry is missing: the runtime's own folder.
function FolderHasRuntime(): Boolean;
var
  Search: TFindRec;
  Folder: String;
begin
  Result := False;
  Folder := ExpandConstant('{commonpf64}') + '\dotnet\shared\Microsoft.WindowsDesktop.App\';

  if FindFirst(Folder + '*', Search) then
  try
    repeat
      if (Search.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) and (MajorVersionOf(Search.Name) >= RequiredMajor) then
        Result := True;
    until Result or not FindNext(Search);
  finally
    FindClose(Search);
  end;
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := RegistryHasRuntime() or FolderHasRuntime();

  if Result then
    Exit;

  Log('.NET ' + IntToStr(RequiredMajor) + ' Desktop Runtime was not found; refusing to install.');

  if not WizardSilent() then
    if MsgBox(
         'RazerHelper needs the .NET ' + IntToStr(RequiredMajor) + ' Desktop Runtime, which is not installed on this PC.' + #13#10 + #13#10 +
         'Setup will close now. Install the runtime (choose ".NET Desktop Runtime" for x64), then run this installer again.' + #13#10 + #13#10 +
         'Open the download page now?',
         mbError, MB_YESNO) = IDYES then
    begin
      ShellExecAsOriginalUser('open', DotNetDownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    end;
end;

// The app's own start-at-login entry is not something Setup created, so remove it by hand.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'RazerHelper');
end;
