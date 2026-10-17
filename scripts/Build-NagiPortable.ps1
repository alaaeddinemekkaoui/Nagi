param([string]$CompilerPath='', [switch]$SkipPublish, [string]$PackageDirectory='')
$ErrorActionPreference='Stop'
$nagiRoot=Split-Path $PSScriptRoot -Parent
if(!$SkipPublish){& (Join-Path $PSScriptRoot 'Publish-Nagi.ps1') -SkipPortable -PackageDirectory $PackageDirectory}
$nagiPublish=if($PackageDirectory){[IO.Path]::GetFullPath($PackageDirectory)}else{Join-Path $nagiRoot 'artifacts/Nagi-win-x64'}
$nagiArchive=Join-Path $nagiRoot 'artifacts/Nagi-win-x64.zip'
foreach($nagiRequired in @('Nagi.exe','Nagi.dll','Nagi.pri','Apply-NagiUpdate.ps1','Assets/Nagi.ico')){
 if(!(Test-Path -LiteralPath (Join-Path $nagiPublish $nagiRequired))){throw ('Publish Nagi first; missing '+$nagiRequired)}
}
if(!(Test-Path -LiteralPath $nagiArchive)){throw 'Publish Nagi first; missing the portable ZIP.'}
if(!$CompilerPath){
 $nagiCandidates=@((Join-Path $nagiRoot 'artifacts/tooling/nsis-3.12/makensis.exe'),(Join-Path ${env:ProgramFiles(x86)} 'NSIS/makensis.exe'))
 $CompilerPath=$nagiCandidates|Where-Object {Test-Path -LiteralPath $_}|Select-Object -First 1
 if(!$CompilerPath){$CompilerPath=(Get-Command makensis.exe -ErrorAction SilentlyContinue).Source}
}
if(!$CompilerPath){throw 'Install NSIS or pass -CompilerPath to makensis.exe to build the portable EXE.'}
[xml]$nagiProject=Get-Content -LiteralPath (Join-Path $nagiRoot 'src/Pulse.App/Pulse.App.csproj')
$nagiVersion=[string]$nagiProject.Project.PropertyGroup.Version
$nagiPayloadId=(Get-FileHash -LiteralPath $nagiArchive -Algorithm SHA256).Hash.ToLowerInvariant().Substring(0,24)
$nagiPortable=Join-Path $nagiRoot 'Nagi-Portable.exe'
& $CompilerPath /V2 "/DSourceDir=$nagiPublish" "/DPortableOutput=$nagiPortable" "/DAppVersion=$nagiVersion" "/DPayloadId=$nagiPayloadId" (Join-Path $nagiRoot 'installer/NagiPortable.nsi')
if($LASTEXITCODE -ne 0){throw 'Nagi portable EXE compilation failed.'}
$nagiHash=Get-FileHash -LiteralPath $nagiPortable -Algorithm SHA256
Set-Content -LiteralPath ($nagiPortable+'.sha256') -Value ($nagiHash.Hash.ToLowerInvariant()+'  Nagi-Portable.exe') -Encoding ascii
Copy-Item -LiteralPath $nagiPortable,($nagiPortable+'.sha256') -Destination (Join-Path $nagiRoot 'artifacts') -Force
Get-Item -LiteralPath $nagiPortable | Select-Object FullName,Length
