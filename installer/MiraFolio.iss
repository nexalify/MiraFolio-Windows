#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "MiraFolio"
#define MyAppExeName "MiraFolio.exe"

[Setup]
AppId={{C70010EC-3A5E-402D-A9F0-53BB2B1DF401}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=MiraFolio contributors
AppPublisherURL=https://github.com/luogreen/MiraFolio-Windows
AppSupportURL=https://github.com/luogreen/MiraFolio-Windows/issues
AppUpdatesURL=https://github.com/luogreen/MiraFolio-Windows/releases
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename={#MyAppName}-Setup-{#MyAppVersion}-win-x64
SetupIconFile=..\src\MiraFolio.App\Resources\mirafolio-icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
AppMutex=MiraFolio-Windows-SingleInstance
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
#ifdef EnableSigning
SignTool=MiraFolioAuthenticode
SignedUninstaller=yes
SignToolRetryCount=3
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
; The app owns startup registration. Remove it on uninstall without touching user data.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "MiraFolio"; Flags: uninsdeletevalue dontcreatekey

[Run]
Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
