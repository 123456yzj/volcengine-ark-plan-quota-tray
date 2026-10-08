#ifndef BuildDir
  #error BuildDir is required
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif
#ifndef AppVersion
  #error AppVersion is required
#endif

[Setup]
AppId={{658A2A59-691E-45D8-90DA-31C9376DE765}
AppName=ark_left
AppVersion={#AppVersion}
AppPublisher=ark_left contributors
AppPublisherURL=https://github.com/123456yzj/volcengine-ark-plan-quota-tray
AppSupportURL=https://github.com/123456yzj/volcengine-ark-plan-quota-tray/issues
AppUpdatesURL=https://github.com/123456yzj/volcengine-ark-plan-quota-tray/releases
DefaultDirName={localappdata}\Programs\ark_left
DefaultGroupName=ark_left
PrivilegesRequired=lowest
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=ark_left-{#AppVersion}-windows-setup
SetupIconFile={#BuildDir}\ark_left.ico
UninstallDisplayIcon={app}\ark_left.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=auto
UninstallDisplayName=ark_left {#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#BuildDir}\ark_left.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\ark_left-check.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\runtime-bootstrap\*"; DestDir: "{app}\runtime-bootstrap"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#BuildDir}\third_party\*"; DestDir: "{app}\third_party"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#BuildDir}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "docs\*.md"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "docs\requirements\*.md"; DestDir: "{app}\docs\requirements"; Flags: ignoreversion

[Icons]
Name: "{group}\ark_left"; Filename: "{app}\ark_left.exe"; Parameters: "--show"; WorkingDir: "{app}"
Name: "{group}\Uninstall ark_left"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ark_left"; Filename: "{app}\ark_left.exe"; Parameters: "--show"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\ark_left.exe"; Parameters: "--show"; Description: "Launch ark_left"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
    MsgBox('ark_left requires .NET Framework 4.8. Install it from Microsoft, then run this installer again.', mbError, MB_OK);
end;
