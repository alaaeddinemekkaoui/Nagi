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
& (Join-Path $PSScriptRoot 'Complete-NagiInstall.ps1') -InstallDirectory $pulseInstall
Write-Output "Installed Nagi to $pulseInstall. Open Nagi from the Start menu. Startup remains opt-in in Settings."
