param([switch]$NoRestore)
$ErrorActionPreference = 'Stop'
$pulseRoot = Split-Path $PSScriptRoot -Parent
$pulseOutput = Join-Path $pulseRoot 'artifacts/Nagi-win-x64'
[string[]]$nagiRestoreArguments = @()
if ($NoRestore) { $nagiRestoreArguments += '--no-restore' }
dotnet publish @nagiRestoreArguments (Join-Path $pulseRoot 'src/Pulse.App/Pulse.App.csproj') -c Release -r win-x64 --self-contained true -o $pulseOutput
if ($LASTEXITCODE -ne 0) { throw 'Nagi publish failed.' }
foreach ($pulseRequired in @('Nagi.exe','Nagi.pri','App.xbf','MainWindow.xbf','Themes/Tokens.xbf','Controls/PulseMark.xbf','Assets/Nagi.Tray.ico','Assets/Nagi.Logo.png','Assets/Nagi.ico','Assets/Akeno.Samurai.png','Microsoft.Graphics.Canvas.dll','Microsoft.Graphics.Canvas.Interop.dll','Assets/Tools/PresentMon.exe','Assets/Tools/PresentMon-LICENSE.txt')) {
    if (!(Test-Path -LiteralPath (Join-Path $pulseOutput $pulseRequired))) { throw "Publish is missing required WinUI resource: $pulseRequired" }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-Nagi.ps1') -Destination $pulseOutput -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Complete-NagiInstall.ps1') -Destination $pulseOutput -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall-Nagi.ps1') -Destination $pulseOutput -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Close-Nagi.ps1') -Destination $pulseOutput -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Sign-Nagi.ps1') -Destination $pulseOutput -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-NagiFpsCapture.ps1') -Destination $pulseOutput -Force
Copy-Item -LiteralPath (Join-Path $pulseRoot 'THIRD-PARTY-NOTICES.md') -Destination $pulseOutput -Force
Copy-Item -LiteralPath (Join-Path $pulseRoot 'README.md') -Destination $pulseOutput -Force
Copy-Item -LiteralPath (Join-Path $pulseRoot 'RELEASE_README.md') -Destination $pulseOutput -Force
Copy-Item -LiteralPath (Join-Path $pulseRoot 'CHANGELOG.md') -Destination $pulseOutput -Force
Copy-Item -LiteralPath (Join-Path $pulseRoot 'TASKS.md') -Destination $pulseOutput -Force
Copy-Item -LiteralPath (Join-Path $pulseRoot 'docs') -Destination $pulseOutput -Recurse -Force
$pulseArchive = Join-Path $pulseRoot 'artifacts/Nagi-win-x64.zip'
Compress-Archive -Path "$pulseOutput/*" -DestinationPath $pulseArchive -Force
Get-Item -LiteralPath $pulseArchive | Select-Object FullName,Length
$pulseHash = Get-FileHash -LiteralPath $pulseArchive -Algorithm SHA256
Set-Content -LiteralPath ($pulseArchive + '.sha256') -Value ($pulseHash.Hash.ToLowerInvariant() + '  Nagi-win-x64.zip') -Encoding ascii
Write-Output ('SHA256: ' + $pulseHash.Hash)
