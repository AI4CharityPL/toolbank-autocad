; Inno Setup script for the in-app AutoCAD AI Assistant.
; Compiled by scripts/build-companion-installer.ps1, which stages the bundle and passes
; /DStagingDir and /DAppVersion. The installer is per-user (no admin needed) and drops the
; ApplicationPlugins bundle so AutoCAD auto-loads it on next launch. The client enters their
; own API key (BYOK) inside the palette after install.

#ifndef StagingDir
  #define StagingDir "..\dist\AcadMcpCompanion.bundle"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{A7E5C9B2-8D14-49F1-AB73-2C9E1F0A6B55}
AppName=AutoCAD AI Assistant
AppVersion={#AppVersion}
AppPublisher=ToolBank AutoCAD contributors
AppPublisherURL=https://github.com/KrzysztofAugiewicz/ToolBank-AutoCAD
AppSupportURL=https://github.com/KrzysztofAugiewicz/ToolBank-AutoCAD/issues
AppUpdatesURL=https://github.com/KrzysztofAugiewicz/ToolBank-AutoCAD/releases
LicenseFile=..\LICENSE
DefaultDirName={userappdata}\Autodesk\ApplicationPlugins\AcadMcpCompanion.bundle
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=AcadMcpCompanion-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=AutoCAD AI Assistant

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#StagingDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Messages]
polish.WelcomeLabel2=Ten kreator zainstaluje wbudowanego asystenta AI dla AutoCAD.%n%nPo instalacji uruchom ponownie AutoCAD, wpisz polecenie ACADAI, a nastepnie w zakladce Ustawienia wprowadz swoj wlasny klucz API.

[Run]
Filename: "{app}\Contents\README-pierwsze-kroki.txt"; Description: "Otworz instrukcje pierwszego uruchomienia"; Flags: postinstall shellexec skipifsilent
