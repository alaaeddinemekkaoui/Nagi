param([Parameter(Mandatory)][ValidateRange(1,2147483647)][int]$GameProcessId,[string]$DataDirectory=(Join-Path $env:LOCALAPPDATA 'Pulse/Telemetry'))
$ErrorActionPreference='Stop'
$pulseTool=Join-Path $PSScriptRoot 'Assets/Tools/PresentMon.exe'
if(!(Test-Path -LiteralPath $pulseTool)){$pulseTool=Join-Path (Split-Path $PSScriptRoot -Parent) 'src/Pulse.App/Assets/Tools/PresentMon.exe'}
if(!(Test-Path -LiteralPath $pulseTool)){throw 'PresentMon is missing.'}
$pulseProcess=Get-Process -Id $GameProcessId -ErrorAction Stop
$pulseSession='Pulse-Helper-'+[Guid]::NewGuid().ToString('N')
$pulseData=[IO.Path]::GetFullPath($DataDirectory); New-Item -ItemType Directory -Path $pulseData -Force | Out-Null
$pulseTemporary=Join-Path $pulseData ($pulseSession+'.csv'); $pulseOutput=Join-Path $pulseData 'foreground-presents.csv'
Write-Output 'Capturing presentation timing only. Enable FPS in Pulse and focus the selected game. Press Ctrl+C to stop.'
try {
 while(!$pulseProcess.HasExited) {
  & $pulseTool --process_id $GameProcessId --session_name $pulseSession --output_file $pulseTemporary --v1_metrics --no_console_stats --no_track_input --timed 3 --terminate_after_timed --terminate_on_proc_exit
  if($LASTEXITCODE -ne 0){throw 'Windows denied the trace or the game exited. Start only this helper in Administrator PowerShell if tracing permission is required. Keep Pulse running normally.'}
  if(Test-Path -LiteralPath $pulseTemporary){Move-Item -LiteralPath $pulseTemporary -Destination $pulseOutput -Force}
  $pulseProcess.Refresh()
 }
} finally {
 # This unique session belongs only to this helper; never stop another application's trace.
 & $pulseTool --session_name $pulseSession --terminate_existing_session 2>$null | Out-Null
}
