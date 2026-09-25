; Ping Runner per-user installer (Inno Setup 6), from the CodePrint .NET profile.
; Built by build-installer.ps1, which passes the version and the publish folder.

#ifndef MyAppVersion
  #error MyAppVersion must be supplied by build-installer.ps1
#endif
#ifndef PublishDir
  #error PublishDir must be supplied by build-installer.ps1
#endif
#ifndef MyVersionInfoVersion
  #error MyVersionInfoVersion must be supplied by build-installer.ps1
#endif

#define MyAppName "Ping Runner"
#define MyAppPublisher "Lukáš Krejčí"
#define MyAppExeName "PingRunner.exe"
#define MyAppUrl "https://github.com/lukr-99/ping-runner"

[Setup]
; Never change AppId: it is how Windows and later installers recognize an existing install.
AppId={{7E8B1E79-C1E6-530E-9CF0-C1A8CBD95A58}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=dist
OutputBaseFilename=PingRunner-{#MyAppVersion}-setup
SetupIconFile=..\src\PingRunner.App\Assets\PingRunner.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Restart Manager closes a running Ping Runner before files are replaced.
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no
VersionInfoVersion={#MyVersionInfoVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
LicenseFile=..\LICENSE.md

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[InstallDelete]
; Ping Runner 1.x installed itself with install.ps1 into versioned folders under this path. It kept no
; user data there, only program files, so the new install replaces it. Its Start menu shortcut has the
; same name as this one and is overwritten below.
Type: filesandordirs; Name: "{localappdata}\Programs\PingRunner"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Settings live in %LOCALAPPDATA%\PingRunner and stay after uninstalling; only program files go.
Type: filesandordirs; Name: "{app}"
