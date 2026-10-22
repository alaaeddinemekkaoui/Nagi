param([string]$CompilerPath='', [switch]$SkipPublish, [string]$PackageDirectory='')
$ErrorActionPreference='Stop'
$nagiRoot=Split-Path $PSScriptRoot -Parent
if(!$SkipPublish){& (Join-Path $PSScriptRoot 'Publish-Nagi.ps1') -PackageDirectory $PackageDirectory}
$nagiPublish=if($PackageDirectory){[IO.Path]::GetFullPath($PackageDirectory)}else{Join-Path $nagiRoot 'artifacts/Nagi-win-x64'}
foreach($nagiRequired in @('Nagi.exe','Nagi.pri','Close-Nagi.ps1','Complete-NagiInstall.ps1','docs/NSIS-LICENSE.txt')){
 if(!(Test-Path -LiteralPath (Join-Path $nagiPublish $nagiRequired))){throw ('Publish Nagi first; missing '+$nagiRequired)}
}
if(!$CompilerPath){
 $nagiCandidates=@((Join-Path $nagiRoot 'artifacts/tooling/nsis-3.12/makensis.exe'),(Join-Path ${env:ProgramFiles(x86)} 'NSIS/makensis.exe'))
 $CompilerPath=$nagiCandidates | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
 if(!$CompilerPath){$CompilerPath=(Get-Command makensis.exe -ErrorAction SilentlyContinue).Source}
}
if(!$CompilerPath){throw 'Install NSIS from https://nsis.sourceforge.io/Download or pass -CompilerPath to its makensis.exe.'}
[xml]$nagiProject=Get-Content -LiteralPath (Join-Path $nagiRoot 'src/Pulse.App/Pulse.App.csproj')
$nagiVersion=[string]$nagiProject.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
$nagiSetup=Join-Path $nagiRoot 'artifacts/Nagi-Setup.exe'
$nagiList=Join-Path $nagiRoot 'artifacts/validation/nagi-owned-files.nsh'
New-Item -ItemType Directory -Path (Split-Path $nagiList -Parent) -Force | Out-Null
function Escape-NagiNsi([string]$Value){return $Value.Replace('$','$$').Replace('"','$\"')}
$nagiLines=[Collections.Generic.List[string]]::new()
foreach($nagiFile in (Get-ChildItem -LiteralPath $nagiPublish -File -Recurse)){
 $nagiRelative=[IO.Path]::GetRelativePath($nagiPublish,$nagiFile.FullName)
 $nagiLines.Add('Delete "$INSTDIR\'+(Escape-NagiNsi $nagiRelative)+'"')
}
foreach($nagiDirectory in (Get-ChildItem -LiteralPath $nagiPublish -Directory -Recurse | Sort-Object {$_.FullName.Length} -Descending)){
 $nagiRelative=[IO.Path]::GetRelativePath($nagiPublish,$nagiDirectory.FullName)
 $nagiLines.Add('RMDir "$INSTDIR\'+(Escape-NagiNsi $nagiRelative)+'"')
}
[IO.File]::WriteAllLines($nagiList,$nagiLines,[Text.UTF8Encoding]::new($false))
& $CompilerPath /V2 "/DSourceDir=$nagiPublish" "/DSetupOutput=$nagiSetup" "/DAppVersion=$nagiVersion" "/DOwnedFiles=$nagiList" (Join-Path $nagiRoot 'installer/Nagi.nsi')
if($LASTEXITCODE -ne 0){throw 'Nagi installer compilation failed.'}
$nagiHash=Get-FileHash -LiteralPath $nagiSetup -Algorithm SHA256
Set-Content -LiteralPath ($nagiSetup+'.sha256') -Value ($nagiHash.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($nagiSetup)) -Encoding ascii
Get-Item -LiteralPath $nagiSetup | Select-Object FullName,Length
