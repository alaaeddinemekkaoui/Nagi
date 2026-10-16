Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!ifndef SourceDir
 !error "Pass /DSourceDir=the published Nagi directory"
!endif
Name "Nagi"
OutFile "${SetupOutput}"
InstallDir "$LOCALAPPDATA\Programs\Nagi"
RequestExecutionLevel user
SetCompressor zlib
SetDatablockOptimize on
CRCCheck on
BrandingText "Nagi · Calm, within reach."
VIProductVersion "${AppVersion}.0"
VIAddVersionKey /LANG=1033 "ProductName" "Nagi"
VIAddVersionKey /LANG=1033 "FileDescription" "Nagi Setup"
VIAddVersionKey /LANG=1033 "FileVersion" "${AppVersion}"
VIAddVersionKey /LANG=1033 "ProductVersion" "${AppVersion}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Nagi contributors"
!define MUI_ICON "${SourceDir}\Assets\Nagi.ico"
!define MUI_UNICON "${SourceDir}\Assets\Nagi.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "Welcome to Nagi"
!define MUI_WELCOMEPAGE_TEXT "Your calm desktop companion.$\r$\n$\r$\nSetup installs Nagi for your Windows account. After installation, choose your name, theme, dock layout, optional OBS connection and weather city.$\r$\n$\r$\nYour existing preferences and scripts are preserved."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\Nagi.exe"
!define MUI_FINISHPAGE_RUN_PARAMETERS "--setup"
!define MUI_FINISHPAGE_RUN_TEXT "Open Nagi and personalize your space"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function .onInit
 SetRegView 64
 ${IfNot} ${RunningX64}
  MessageBox MB_ICONSTOP "Nagi requires Windows 11 x64."
  Abort
 ${EndIf}
 ReadRegStr $0 HKLM "Software\Microsoft\Windows NT\CurrentVersion" "CurrentBuildNumber"
 IntCmp $0 22621 supported unsupported supported
 unsupported:
  MessageBox MB_ICONSTOP "Nagi requires Windows 11 version 22H2 or newer."
  Abort
 supported:
 StrCmp $INSTDIR "$LOCALAPPDATA\Programs\Nagi" +3
 MessageBox MB_ICONSTOP "Nagi installs only in the current user's Programs\Nagi directory."
 Abort
FunctionEnd

Function un.onInit
 SetRegView 64
 StrCmp $INSTDIR "$LOCALAPPDATA\Programs\Nagi" +3
 MessageBox MB_ICONSTOP "The Nagi installation path could not be verified."
 Abort
FunctionEnd

Section "Nagi"
 InitPluginsDir
 SetOutPath "$PLUGINSDIR"
 File /oname=Close-Nagi.ps1 "${SourceDir}\Close-Nagi.ps1"
 nsExec::ExecToStack '"$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\Close-Nagi.ps1" -ExecutablePath "$INSTDIR\Nagi.exe"'
 Pop $0
 Pop $1
 StrCmp $0 "0" +3
 MessageBox MB_ICONSTOP "Please quit Nagi from the tray, then run Setup again."
 Abort
 nsExec::ExecToStack '"$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\Close-Nagi.ps1" -ExecutablePath "$LOCALAPPDATA\Programs\Pulse\Pulse.App.exe"'
 Pop $0
 Pop $1
 StrCmp $0 "0" +3
 MessageBox MB_ICONSTOP "Please quit the previous Pulse installation, then run Setup again."
 Abort
 SetOutPath "$INSTDIR"
 SetOverwrite on
 File /r "${SourceDir}\*.*"
 nsExec::ExecToStack '"$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$INSTDIR\Complete-NagiInstall.ps1" -InstallDirectory "$INSTDIR"'
 Pop $0
 Pop $1
 StrCmp $0 "0" +3
 MessageBox MB_ICONSTOP "Nagi files were copied, but the Start menu entry could not be created. Run Setup again."
 Abort
 WriteUninstaller "$INSTDIR\Uninstall.exe"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nagi" "DisplayName" "Nagi"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nagi" "DisplayVersion" "${AppVersion}"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nagi" "InstallLocation" "$INSTDIR"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nagi" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nagi" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nagi" "DisplayIcon" "$INSTDIR\Nagi.exe,0"
 WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nagi" "NoModify" 1
 WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nagi" "NoRepair" 1
SectionEnd

Section "Uninstall"
 nsExec::ExecToStack '"$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$INSTDIR\Close-Nagi.ps1" -ExecutablePath "$INSTDIR\Nagi.exe"'
 Pop $0
 Pop $1
 StrCmp $0 "0" +3
 MessageBox MB_ICONSTOP "Please quit Nagi from the tray, then retry uninstalling."
 Abort
 DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Nagi"
 DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nagi"
 Delete "$SMPROGRAMS\Nagi.lnk"
 !include "${OwnedFiles}"
 Delete "$INSTDIR\Uninstall.exe"
 RMDir "$INSTDIR"
SectionEnd
