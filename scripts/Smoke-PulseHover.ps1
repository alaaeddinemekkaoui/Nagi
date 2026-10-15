# Compatibility entry point; Halo 1.3 uses the edge interaction smoke checks.
param([Parameter(Mandatory)][int]$PulseProcessId)
& (Join-Path $PSScriptRoot 'Smoke-PulseEdge.ps1') -PulseProcessId $PulseProcessId