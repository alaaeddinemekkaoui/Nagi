param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$pulseRoot = Split-Path $PSScriptRoot -Parent
dotnet build (Join-Path $pulseRoot 'src/Pulse.App/Pulse.App.csproj') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Nagi build failed.' }
$pulseExe = Join-Path $pulseRoot "src/Pulse.App/bin/$Configuration/net10.0-windows10.0.22621.0/win-x64/Nagi.exe"
if (!(Test-Path -LiteralPath $pulseExe)) { throw "Executable not found: $pulseExe" }
Start-Process -FilePath $pulseExe -WindowStyle Hidden
