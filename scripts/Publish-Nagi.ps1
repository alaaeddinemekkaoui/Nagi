param([switch]$NoRestore, [switch]$SkipPortable, [string]$ReleaseTag='v0.1.2', [string]$PackageDirectory='')
$ErrorActionPreference = 'Stop'
$pulseRoot = Split-Path $PSScriptRoot -Parent
$pulseOutput = if($PackageDirectory){[IO.Path]::GetFullPath($PackageDirectory)}else{Join-Path $pulseRoot 'artifacts/Nagi-win-x64'}
[string[]]$nagiRestoreArguments = @()
if ($NoRestore) { $nagiRestoreArguments += '--no-restore' }
dotnet publish @nagiRestoreArguments (Join-Path $pulseRoot 'src/Pulse.App/Pulse.App.csproj') -c Release -r win-x64 --self-contained true -o $pulseOutput
if ($LASTEXITCODE -ne 0) { throw 'Nagi publish failed.' }
foreach ($pulseRequired in @('Apply-NagiUpdate.ps1','Nagi.exe','Nagi.pri','App.xbf','MainWindow.xbf','Themes/Tokens.xbf','Controls/PulseMark.xbf','Assets/Nagi.Tray.ico','Assets/Nagi.Logo.png','Assets/Nagi.ico','Assets/Akeno.Samurai.png','Microsoft.Graphics.Canvas.dll','Microsoft.Graphics.Canvas.Interop.dll','Assets/Tools/PresentMon.exe','Assets/Tools/PresentMon-LICENSE.txt')) {
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
if($ReleaseTag -notmatch '^[a-zA-Z0-9][a-zA-Z0-9._/-]*$'){throw 'Choose a valid release tag.'}
$nagiReadme=Get-Content -LiteralPath (Join-Path $pulseRoot 'README.md') -Raw
$nagiWebRoot='https://github.com/alaaeddinemekkaoui/Nagi'
$nagiReleaseReadme=$nagiReadme.Replace('src="docs/',('src="'+$nagiWebRoot+'/raw/refs/tags/'+$ReleaseTag+'/docs/')).Replace('href="docs/',('href="'+$nagiWebRoot+'/blob/'+$ReleaseTag+'/docs/')).Replace('(docs/',('('+$nagiWebRoot+'/blob/'+$ReleaseTag+'/docs/'))
foreach($nagiDocument in @('CHANGELOG.md','THIRD-PARTY-NOTICES.md')){$nagiReleaseReadme=$nagiReleaseReadme.Replace(('('+$nagiDocument+')'),('('+$nagiWebRoot+'/blob/'+$ReleaseTag+'/'+$nagiDocument+')'))}
Set-Content -LiteralPath (Join-Path $pulseRoot 'RELEASE_README.md') -Value $nagiReleaseReadme -Encoding utf8 -NoNewline
Copy-Item -LiteralPath (Join-Path $pulseRoot 'RELEASE_README.md') -Destination $pulseOutput -Force
Copy-Item -LiteralPath (Join-Path $pulseRoot 'CHANGELOG.md') -Destination $pulseOutput -Force
Copy-Item -LiteralPath (Join-Path $pulseRoot 'docs') -Destination $pulseOutput -Recurse -Force
foreach($nagiObsolete in @('AGENTS.md','AI_CONTEXT.md','TASKS.md','docs/ASSETS.md')){
 $nagiObsoletePath=Join-Path $pulseOutput $nagiObsolete
 if(Test-Path -LiteralPath $nagiObsoletePath){Remove-Item -LiteralPath $nagiObsoletePath -Force}
}
$pulseArchive = Join-Path $pulseRoot 'artifacts/Nagi-win-x64.zip'
Compress-Archive -Path "$pulseOutput/*" -DestinationPath $pulseArchive -Force
Get-Item -LiteralPath $pulseArchive | Select-Object FullName,Length
$pulseHash = Get-FileHash -LiteralPath $pulseArchive -Algorithm SHA256
Set-Content -LiteralPath ($pulseArchive + '.sha256') -Value ($pulseHash.Hash.ToLowerInvariant() + '  Nagi-win-x64.zip') -Encoding ascii
Write-Output ('SHA256: ' + $pulseHash.Hash)
if(!$SkipPortable){& (Join-Path $PSScriptRoot 'Build-NagiPortable.ps1') -SkipPublish -PackageDirectory $pulseOutput}
