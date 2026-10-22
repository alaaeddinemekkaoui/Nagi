param([switch]$NoRestore, [string]$ReleaseTag='v0.2.2')
$ErrorActionPreference='Stop'
$nagiRoot=Split-Path $PSScriptRoot -Parent
function Get-NagiSourceFingerprint {
 $hash=[Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
 try {
  $files=@(foreach($directory in @('src','languages','scripts','installer')){
   Get-ChildItem -LiteralPath (Join-Path $nagiRoot $directory) -Recurse -File | Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'}
  })+@(Get-Item -LiteralPath (Join-Path $nagiRoot 'Directory.Build.props'))
  foreach($file in ($files|Sort-Object FullName)){
   $hash.AppendData([Text.Encoding]::UTF8.GetBytes([IO.Path]::GetRelativePath($nagiRoot,$file.FullName)))
   $hash.AppendData([IO.File]::ReadAllBytes($file.FullName))
  }
  return [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
 } finally {$hash.Dispose()}
}
$fingerprint=Get-NagiSourceFingerprint
& (Join-Path $PSScriptRoot 'Publish-Nagi.ps1') -Development -NoRestore:$NoRestore -ReleaseTag $ReleaseTag -SourceFingerprint $fingerprint
if((Get-NagiSourceFingerprint) -ne $fingerprint){throw 'Source changed during the development build. Re-run to generate a matching pair.'}
& (Join-Path $PSScriptRoot 'Publish-Nagi.ps1') -NoRestore:$NoRestore -ReleaseTag $ReleaseTag -SourceFingerprint $fingerprint
if((Get-NagiSourceFingerprint) -ne $fingerprint){throw 'Source changed during the production build. Re-run to generate a matching pair.'}
$development=Get-Content -LiteralPath (Join-Path $nagiRoot 'artifacts/Nagi-Dev-win-x64/build-info.json') -Raw|ConvertFrom-Json
$production=Get-Content -LiteralPath (Join-Path $nagiRoot 'artifacts/Nagi-win-x64/build-info.json') -Raw|ConvertFrom-Json
if($development.Version -ne $production.Version -or $development.SourceFingerprint -ne $production.SourceFingerprint){throw 'Build pair does not match.'}
Write-Output ('PASS: matching development and production '+$production.Version+'; source '+$fingerprint)
