param([Parameter(Mandatory)][string]$InstallDirectory)
$ErrorActionPreference='Stop'
$nagiInstall=[IO.Path]::GetFullPath($InstallDirectory)
$nagiExpected=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs/Nagi'))
if($nagiInstall -ne $nagiExpected -or !(Test-Path -LiteralPath (Join-Path $nagiInstall 'Nagi.exe'))){throw 'Invalid Nagi installation directory.'}
$nagiShell=New-Object -ComObject WScript.Shell
$nagiShortcut=$nagiShell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'Nagi.lnk'))
$nagiShortcut.TargetPath=Join-Path $nagiInstall 'Nagi.exe'
$nagiShortcut.WorkingDirectory=$nagiInstall
$nagiShortcut.IconLocation=(Join-Path $nagiInstall 'Nagi.exe')+',0'
$nagiShortcut.Save()
$nagiLegacy=Join-Path $env:LOCALAPPDATA 'Programs/Pulse'
$nagiLegacyShortcut=Join-Path ([Environment]::GetFolderPath('Programs')) 'Pulse.lnk'
if(Test-Path -LiteralPath $nagiLegacyShortcut){
 $nagiOld=$nagiShell.CreateShortcut($nagiLegacyShortcut)
 if($nagiOld.TargetPath -eq (Join-Path $nagiLegacy 'Pulse.App.exe')){Remove-Item -LiteralPath $nagiLegacyShortcut}
}
$nagiRun=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run',$true)
try{
 if($null -ne $nagiRun -and $nagiRun.GetValue('Pulse',$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) -eq ('"'+(Join-Path $nagiLegacy 'Pulse.App.exe')+'"')){
  $nagiRun.SetValue('Nagi',('"'+(Join-Path $nagiInstall 'Nagi.exe')+'"'))
  $nagiRun.DeleteValue('Pulse',$false)
 }
}finally{if($null -ne $nagiRun){$nagiRun.Dispose()}}
