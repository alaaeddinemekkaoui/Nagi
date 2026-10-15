param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug')
# Compatibility entry point. The app is now named Nagi.
& (Join-Path $PSScriptRoot 'Run-Nagi.ps1') @PSBoundParameters
