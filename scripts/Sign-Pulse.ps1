param([Parameter(Mandatory)][string]$CertificateThumbprint,[string]$PackageDirectory='')
# Compatibility entry point. The app is now named Nagi.
& (Join-Path $PSScriptRoot 'Sign-Nagi.ps1') @PSBoundParameters
