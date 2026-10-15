param([string]$SourceDirectory = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$pulseSource = (Resolve-Path -LiteralPath $SourceDirectory).Path
if (!(Test-Path -LiteralPath (Join-Path $pulseSource 'Nagi.exe'))) { throw 'Run this installer from the extracted Nagi package directory.' }
$pulseInstall = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs/Nagi'))
$pulseExpected = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs/Nagi'))
if ($pulseInstall -ne $pulseExpected -or $pulseSource -eq $pulseInstall) { throw 'Invalid source or install destination.' }
& (Join-Path $PSScriptRoot 'Close-Nagi.ps1') -ExecutablePath (Join-Path $pulseInstall 'Nagi.exe')
# Close the previous product before sharing its preserved settings.
$pulseLegacyInstall=Join-Path $env:LOCALAPPDATA 'Programs/Pulse'
& (Join-Path $PSScriptRoot 'Close-Nagi.ps1') -ExecutablePath (Join-Path $pulseLegacyInstall 'Pulse.App.exe')
New-Item -ItemType Directory -Path $pulseInstall -Force | Out-Null
Get-ChildItem -LiteralPath $pulseSource | Copy-Item -Destination $pulseInstall -Recurse -Force
$pulseShortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Nagi.lnk'
$pulseShell = New-Object -ComObject WScript.Shell
$pulseShortcut = $pulseShell.CreateShortcut($pulseShortcutPath)
$pulseShortcut.TargetPath = Join-Path $pulseInstall 'Nagi.exe'
$pulseShortcut.WorkingDirectory = $pulseInstall
$pulseShortcut.IconLocation = (Join-Path $pulseInstall 'Nagi.exe') + ',0'
$pulseShortcut.Save()

# Migrate only a shortcut/startup entry targeting the known previous installation.
$pulseLegacyShortcut=Join-Path ([Environment]::GetFolderPath('Programs')) 'Pulse.lnk'
if(Test-Path -LiteralPath $pulseLegacyShortcut) {
 $pulseOldShortcut=$pulseShell.CreateShortcut($pulseLegacyShortcut)
 if($pulseOldShortcut.TargetPath -eq (Join-Path $pulseLegacyInstall 'Pulse.App.exe')) {Remove-Item -LiteralPath $pulseLegacyShortcut}
}
$pulseRunKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$pulseRun=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
try {
 $pulseLegacyRun=if($null -ne $pulseRun){$pulseRun.GetValue('Pulse',$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)}else{$null}
} finally {if($null -ne $pulseRun){$pulseRun.Dispose()}}
if($pulseLegacyRun -eq ('"'+(Join-Path $pulseLegacyInstall 'Pulse.App.exe')+'"')) {
 Set-ItemProperty -LiteralPath $pulseRunKey -Name 'Nagi' -Value ('"'+(Join-Path $pulseInstall 'Nagi.exe')+'"')
 Remove-ItemProperty -LiteralPath $pulseRunKey -Name 'Pulse'
}

Write-Output "Installed Nagi to $pulseInstall. Open Nagi from the Start menu. Startup remains opt-in in Settings."
