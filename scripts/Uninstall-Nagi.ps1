$ErrorActionPreference = 'Stop'
$pulseInstall = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs/Nagi'))
$pulsePrograms = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
if ([System.IO.Directory]::GetParent($pulseInstall).FullName -ne $pulsePrograms -or [System.IO.Path]::GetFileName($pulseInstall) -ne 'Nagi') { throw 'Unsafe uninstall target.' }
$nagiNative=Join-Path $pulseInstall 'Uninstall.exe'
if(Test-Path -LiteralPath $nagiNative){
 $nagiRemoval=Start-Process -FilePath $nagiNative -ArgumentList '/S' -WindowStyle Hidden -Wait -PassThru
 if($nagiRemoval.ExitCode -ne 0){throw 'Nagi uninstaller did not complete.'}
 Write-Output 'Nagi uninstalled. User preferences are retained.'
 return
}
& (Join-Path $PSScriptRoot 'Close-Nagi.ps1') -ExecutablePath (Join-Path $pulseInstall 'Nagi.exe')
$pulseRunKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Remove-ItemProperty -LiteralPath $pulseRunKey -Name 'Nagi' -ErrorAction SilentlyContinue
$pulseShortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Nagi.lnk'
if (Test-Path -LiteralPath $pulseShortcutPath) { Remove-Item -LiteralPath $pulseShortcutPath }
if (Test-Path -LiteralPath $pulseInstall) { Remove-Item -LiteralPath $pulseInstall -Recurse -Force }
Write-Output 'Nagi uninstalled. Preferences and diagnostic logs remain in %LOCALAPPDATA%\Pulse.'
