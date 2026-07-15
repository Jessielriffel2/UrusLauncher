#define AppName "Urus Launcher"
#define AppExeName "UrusLauncher.App.exe"
#define AppPublisher "Urus Launcher"
#define AppVersion GetEnv("URUS_LAUNCHER_VERSION")
#define AppFileVersion GetEnv("URUS_LAUNCHER_FILE_VERSION")
#define RepositoryRoot AddBackslash(SourcePath) + ".."
#define DistributionRoot RepositoryRoot + "\artifacts\urus-distribution"
#define PublishRoot DistributionRoot + "\portable\UrusLauncher"
#define BrandingIcon RepositoryRoot + "\src\LegendLauncher.App\Assets\Branding\urus-launcher.ico"

#if AppVersion == ""
  #undef AppVersion
  #define AppVersion "1.0.0"
#endif

#if AppFileVersion == ""
  #undef AppFileVersion
  #define AppFileVersion "1.0.0.0"
#endif

[Setup]
AppId={{7B1BD240-F9E4-4F44-9C6A-0C8B6B1A95A2}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoVersion={#AppFileVersion}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#DistributionRoot}
OutputBaseFilename=UrusLauncher-Setup-{#AppVersion}-win-x64
SetupIconFile={#BrandingIcon}
UninstallDisplayIcon={app}\urus-launcher.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardResizable=no
DisableWelcomePage=no
ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UsePreviousAppDir=yes
UsePreviousLanguage=yes
UsePreviousTasks=yes
AllowNoIcons=yes
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\urus-launcher.ico"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\urus-launcher.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Flags: nowait; Check: RelaunchRequested

[Code]
function RelaunchRequested: Boolean;
var
  Index: Integer;
begin
  Result := False;
  for Index := 1 to ParamCount do
  begin
    if CompareText(ParamStr(Index), '/RELAUNCH') = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;
