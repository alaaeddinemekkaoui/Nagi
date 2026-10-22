param([Parameter(Mandatory)][ValidateRange(1,2147483647)][int]$GameProcessId,[string]$DataDirectory=(Join-Path $env:LOCALAPPDATA 'Pulse/Telemetry'),[int]$OwnerProcessId=0,[string]$SettingsPath='')
$ErrorActionPreference='Stop'
$pulseTool=Join-Path $PSScriptRoot 'Assets/Tools/PresentMon.exe'
if(!(Test-Path -LiteralPath $pulseTool)){$pulseTool=Join-Path (Split-Path $PSScriptRoot -Parent) 'src/Pulse.App/Assets/Tools/PresentMon.exe'}
if(!(Test-Path -LiteralPath $pulseTool)){throw 'PresentMon is missing.'}
$pulseProcess=Get-Process -Id $GameProcessId -ErrorAction Stop
$pulseOwner=if($OwnerProcessId -gt 0){Get-Process -Id $OwnerProcessId -ErrorAction Stop}else{$null}
$pulseSession='Pulse-Helper-'+[Guid]::NewGuid().ToString('N')
$pulseData=[IO.Path]::GetFullPath($DataDirectory); New-Item -ItemType Directory -Path $pulseData -Force | Out-Null
$pulseTemporary=Join-Path $pulseData ($pulseSession+'.csv'); $pulseOutput=Join-Path $pulseData 'foreground-presents.csv'
$pulseGuard=[Threading.Mutex]::new($false,('Local\Nagi-FpsHelper-'+$OwnerProcessId+'-'+$GameProcessId))
try{$pulseOwnsGuard=$pulseGuard.WaitOne(0)}catch [Threading.AbandonedMutexException]{$pulseOwnsGuard=$true}
if(!$pulseOwnsGuard){$pulseGuard.Dispose();return}
Write-Output 'Capturing presentation timing only. Enable FPS in Nagi and focus the selected game. Disable FPS or close Nagi to stop an app-started helper. Press Ctrl+C to stop a manual helper.'
try {
 while(!$pulseProcess.HasExited) {
  if($pulseOwner -and $pulseOwner.HasExited){break}
  if($SettingsPath){try{$pulsePreferences=Get-Content -LiteralPath $SettingsPath -Raw|ConvertFrom-Json;if(!$pulsePreferences.FpsEnabled){break}}catch{break}}
  & $pulseTool --process_id $GameProcessId --session_name $pulseSession --output_file $pulseTemporary --v1_metrics --no_console_stats --no_track_input --timed 3 --terminate_after_timed --terminate_on_proc_exit
  if($LASTEXITCODE -ne 0){throw 'Windows denied the trace or the game exited. Start only this helper in Administrator PowerShell if tracing permission is required. Keep Nagi running normally.'}
  if(Test-Path -LiteralPath $pulseTemporary){Move-Item -LiteralPath $pulseTemporary -Destination $pulseOutput -Force}
  $pulseProcess.Refresh()
 }
} finally {
 # This unique session belongs only to this helper; never stop another application's trace.
 & $pulseTool --session_name $pulseSession --terminate_existing_session 2>$null | Out-Null
 if($pulseOwnsGuard){$pulseGuard.ReleaseMutex()};$pulseGuard.Dispose()
}
