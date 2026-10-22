Unicode true
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
Name "Nagi"
!ifndef BuildChannel
 !define BuildChannel "Production"
!endif
OutFile "${PortableOutput}"
RequestExecutionLevel user
SilentInstall silent
AutoCloseWindow true
SetCompressor zlib
SetDatablockOptimize on
CRCCheck on
Icon "${SourceDir}\Assets\Nagi.ico"
VIProductVersion "${AppVersion}.0"
VIAddVersionKey /LANG=1033 "ProductName" "Nagi"
VIAddVersionKey /LANG=1033 "FileDescription" "Nagi ${BuildChannel} Portable"
VIAddVersionKey /LANG=1033 "FileVersion" "${AppVersion}"
VIAddVersionKey /LANG=1033 "ProductVersion" "${AppVersion}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Nagi contributors"
Var CacheDirectory
Var LaunchArguments
Var ExtractionMutex

Section "Nagi Portable"
 ${IfNot} ${RunningX64}
  MessageBox MB_ICONSTOP "Nagi requires Windows 11 x64."
  SetErrorLevel 1
  Quit
 ${EndIf}
 ${GetParameters} $LaunchArguments
 StrCpy $CacheDirectory "$LOCALAPPDATA\Nagi\Portable\${PayloadId}"
 System::Call 'kernel32::CreateMutexW(p 0, i 0, w "Local\Nagi.Portable.${PayloadId}") p.r0'
 StrCpy $ExtractionMutex $0
 StrCmp $ExtractionMutex 0 failed
 System::Call 'kernel32::WaitForSingleObject(p $ExtractionMutex, i 60000) i.r0'
 ${If} $0 != 0
 ${AndIf} $0 != 128
  Goto failed
 ${EndIf}
 IfFileExists "$CacheDirectory\.nagi-ready" 0 extract
 IfFileExists "$CacheDirectory\Nagi.exe" launch extract
 extract:
  SetOutPath "$CacheDirectory"
  ClearErrors
  File /r "${SourceDir}\*.*"
  IfErrors failed
  FileOpen $1 "$CacheDirectory\.nagi-ready" w
  IfErrors failed
  FileWrite $1 "${PayloadId}"
  FileClose $1
 launch:
  SetOutPath "$CacheDirectory"
  ClearErrors
  Exec '"$CacheDirectory\Nagi.exe" $LaunchArguments'
  IfErrors failed
  System::Call 'kernel32::ReleaseMutex(p $ExtractionMutex)'
  System::Call 'kernel32::CloseHandle(p $ExtractionMutex)'
  SetErrorLevel 0
  Quit
 failed:
  ${If} $ExtractionMutex != 0
   System::Call 'kernel32::ReleaseMutex(p $ExtractionMutex)'
   System::Call 'kernel32::CloseHandle(p $ExtractionMutex)'
  ${EndIf}
  MessageBox MB_ICONSTOP "Nagi could not prepare or open its portable files. Check that your local application-data folder is writable, then try again."
  SetErrorLevel 1
SectionEnd
