param([string]$CompilerPath='', [switch]$SkipPublish, [string]$PackageDirectory='', [switch]$Development)
$ErrorActionPreference='Stop'
$nagiRoot=Split-Path $PSScriptRoot -Parent
if(!$SkipPublish){& (Join-Path $PSScriptRoot 'Publish-Nagi.ps1') -SkipPortable -PackageDirectory $PackageDirectory -Development:$Development}
$nagiArtifactName=if($Development){'Nagi-Dev-win-x64'}else{'Nagi-win-x64'}
$nagiPublish=if($PackageDirectory){[IO.Path]::GetFullPath($PackageDirectory)}else{Join-Path $nagiRoot ('artifacts/'+$nagiArtifactName)}
$nagiArchive=Join-Path $nagiRoot ('artifacts/'+$nagiArtifactName+'.zip')
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
$nagiVersion=[string]($nagiProject.SelectSingleNode('/Project/PropertyGroup/Version').InnerText)
$nagiPayloadId=(Get-FileHash -LiteralPath $nagiArchive -Algorithm SHA256).Hash.ToLowerInvariant().Substring(0,24)
$nagiPortableName=if($Development){'Nagi-Dev-Portable.exe'}else{'Nagi-Portable.exe'}
$nagiPortable=Join-Path $nagiRoot $nagiPortableName
$nagiChannel=if($Development){'Development'}else{'Production'}
& $CompilerPath /V2 "/DSourceDir=$nagiPublish" "/DPortableOutput=$nagiPortable" "/DAppVersion=$nagiVersion" "/DPayloadId=$nagiPayloadId" "/DBuildChannel=$nagiChannel" (Join-Path $nagiRoot 'installer/NagiPortable.nsi')
if($LASTEXITCODE -ne 0){throw 'Nagi portable EXE compilation failed.'}
$nagiHash=Get-FileHash -LiteralPath $nagiPortable -Algorithm SHA256
Set-Content -LiteralPath ($nagiPortable+'.sha256') -Value ($nagiHash.Hash.ToLowerInvariant()+'  '+$nagiPortableName) -Encoding ascii
Copy-Item -LiteralPath $nagiPortable,($nagiPortable+'.sha256') -Destination (Join-Path $nagiRoot 'artifacts') -Force
Get-Item -LiteralPath $nagiPortable | Select-Object FullName,Length
