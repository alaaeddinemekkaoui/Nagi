param([Parameter(Mandatory)][string]$ExecutablePath)
# Compatibility entry point. The app is now named Nagi.
& (Join-Path $PSScriptRoot 'Close-Nagi.ps1') @PSBoundParameters
