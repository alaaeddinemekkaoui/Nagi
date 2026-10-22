param([string]$ExecutablePath=(Join-Path (Split-Path $PSScriptRoot -Parent) 'Nagi-Portable.exe'))
$ErrorActionPreference='Stop'
$developmentPortable=[Diagnostics.FileVersionInfo]::GetVersionInfo([IO.Path]::GetFullPath($ExecutablePath)).FileDescription -eq 'Nagi Development Portable'
$expectedWindowTitle=if($developmentPortable){'Nagi · Development *'}else{'Nagi'}
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class NagiPortableSmokeNative {
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
}
'@
$qaRoot=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/portable-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$qaCopyDirectory=Join-Path $qaRoot 'standalone executable'
$qaData=Join-Path $qaRoot 'isolated preferences'
New-Item -ItemType Directory -Path $qaCopyDirectory,$qaData -Force|Out-Null
$qaCopy=Join-Path $qaCopyDirectory 'Nagi-Portable.exe'
Copy-Item -LiteralPath $ExecutablePath -Destination $qaCopy
@{SchemaVersion=1;WelcomeCompleted=$true;UserName='friend';StartExpanded=$true;CloseOnDeactivate=$false;HideInFullscreen=$false;IndexFiles=$false;AutomaticUpdates=$false;ObsEnabled=$false;FpsEnabled=$false;WeatherEnabled=$false;StartWithWindows=$false;ReducedMotion=$true;Position=@{Orientation='Horizontal';Anchor='Bottom edge'};Hotkey=@{Modifiers=7;Key=131};HideHotkey=@{Modifiers=7;Key=132};SearchHotkey=@{Modifiers=7;Key=133}}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $qaData 'settings.json') -Encoding UTF8
$qaChild=$null
$qaWindow=$null
function Open-TestPortable {
 $qaStart=[Diagnostics.ProcessStartInfo]::new($qaCopy)
 $qaStart.UseShellExecute=$false;$qaStart.CreateNoWindow=$true;$qaStart.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
 $qaStart.ArgumentList.Add('--data-dir');$qaStart.ArgumentList.Add($qaData)
 $qaLauncher=[Diagnostics.Process]::Start($qaStart)
 try {
  if(!$qaLauncher.WaitForExit(30000)){throw 'Portable preparation did not finish in 30 seconds.'}
  if($qaLauncher.ExitCode -ne 0){throw ('Portable launcher failed: '+$qaLauncher.ExitCode)}
 } finally {$qaLauncher.Dispose()}
}
function Get-TestNagi {
 @(Get-CimInstance Win32_Process -Filter "Name='Nagi.exe'"|Where-Object {$_.CommandLine -and $_.CommandLine.IndexOf($qaData,[StringComparison]::OrdinalIgnoreCase) -ge 0})
}
try {
 Open-TestPortable
 for($qaAttempt=0;$qaAttempt -lt 100;$qaAttempt++){
  $qaProcesses=@(Get-TestNagi)
  if($qaProcesses.Count -eq 1){$qaChild=$qaProcesses[0];break}
  Start-Sleep -Milliseconds 100
 }
 if(!$qaChild){throw 'Portable app did not start with the isolated data directory.'}
 $qaCache=Split-Path -Parent $qaChild.ExecutablePath
 $qaExpectedCacheRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Nagi/Portable')).TrimEnd('\')+'\'
 if(!$qaChild.ExecutablePath.StartsWith($qaExpectedCacheRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Portable app was not launched from its own payload cache.'}
 foreach($qaRequired in @('Nagi.exe','Nagi.dll','Nagi.pri','App.xbf','MainWindow.xbf','Apply-NagiUpdate.ps1','.nagi-ready')){
  if(!(Test-Path -LiteralPath (Join-Path $qaCache $qaRequired))){throw ('Portable payload is missing '+$qaRequired)}
 }
 for($qaAttempt=0;$qaAttempt -lt 100;$qaAttempt++){
  $qaWindow=[Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,[int]$qaChild.ProcessId))
  if($qaWindow -and $qaWindow.Current.Name -like $expectedWindowTitle -and $qaWindow.Current.NativeWindowHandle -ne 0){break}
  Start-Sleep -Milliseconds 100
 }
 if(!$qaWindow -or $qaWindow.Current.Name -notlike $expectedWindowTitle){throw 'The extracted Nagi window did not open with its expected channel title.'}
 $qaReadyTime=(Get-Item -LiteralPath (Join-Path $qaCache '.nagi-ready')).LastWriteTimeUtc
 Open-TestPortable
 Start-Sleep -Milliseconds 700
 $qaAgain=@(Get-TestNagi)
 if($qaAgain.Count -ne 1 -or $qaAgain[0].ProcessId -ne $qaChild.ProcessId){throw 'A second portable launch did not reuse the isolated app instance.'}
 if((Get-Item -LiteralPath (Join-Path $qaCache '.nagi-ready')).LastWriteTimeUtc -ne $qaReadyTime){throw 'Cached portable files were needlessly extracted again.'}
 if(@(Get-ChildItem -LiteralPath $qaCopyDirectory -File).Count -ne 1){throw 'Portable launch placed extra files beside the executable.'}
 Write-Output ('PASS: standalone EXE, extraction, real Nagi window, isolated arguments with spaces, cached relaunch and single instance. '+$qaRoot)
}finally{
 if($qaWindow){[NagiPortableSmokeNative]::PostMessage([IntPtr]$qaWindow.Current.NativeWindowHandle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null}
 if($qaChild){$qaClosing=Get-Process -Id $qaChild.ProcessId -ErrorAction SilentlyContinue;if($qaClosing){$qaClosing.WaitForExit(10000)|Out-Null}}
}
