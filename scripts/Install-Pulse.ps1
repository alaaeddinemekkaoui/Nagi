param([string]$SourceDirectory = $PSScriptRoot)
# Compatibility entry point. The app is now named Nagi.
& (Join-Path $PSScriptRoot 'Install-Nagi.ps1') @PSBoundParameters
