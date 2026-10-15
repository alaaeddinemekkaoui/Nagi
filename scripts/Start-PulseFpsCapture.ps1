param([Parameter(Mandatory)][ValidateRange(1,2147483647)][int]$GameProcessId,[string]$DataDirectory=(Join-Path $env:LOCALAPPDATA 'Pulse/Telemetry'))
# Compatibility entry point. The app is now named Nagi.
& (Join-Path $PSScriptRoot 'Start-NagiFpsCapture.ps1') @PSBoundParameters
