param([Parameter(Mandatory)][string]$CertificateThumbprint,[string]$PackageDirectory='')
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($PackageDirectory)) {
 $PackageDirectory=if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Nagi.exe')){$PSScriptRoot}else{Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/Nagi-win-x64'}
}
$pulseCertificate=Get-Item -LiteralPath ('Cert:/CurrentUser/My/'+($CertificateThumbprint -replace '\s',''))
if(!$pulseCertificate.HasPrivateKey -or $pulseCertificate.NotAfter -lt (Get-Date)){throw 'Choose a current code-signing certificate with its private key.'}
$pulseEku=@($pulseCertificate.Extensions | Where-Object {$_.Oid.Value -eq '2.5.29.37'} | ForEach-Object {$_.EnhancedKeyUsages} | Where-Object {$_.Value -eq '1.3.6.1.5.5.7.3.3'})
if($pulseEku.Count -eq 0){throw 'The certificate must permit code signing.'}
$pulseDirectory=(Resolve-Path -LiteralPath $PackageDirectory).Path
foreach($pulseFile in @('Nagi.exe','Install-Nagi.ps1','Uninstall-Nagi.ps1','Close-Nagi.ps1','Start-NagiFpsCapture.ps1')) {
 $pulseTarget=Join-Path $pulseDirectory $pulseFile
 $pulseSignature=Set-AuthenticodeSignature -LiteralPath $pulseTarget -Certificate $pulseCertificate -HashAlgorithm SHA256 -TimestampServer 'http://timestamp.digicert.com'
 if($pulseSignature.Status -ne 'Valid'){throw ('Signing failed: '+$pulseFile+' · '+$pulseSignature.StatusMessage)}
}
$pulseArchive=Join-Path (Split-Path $pulseDirectory -Parent) 'Nagi-win-x64.zip'
Compress-Archive -Path (Join-Path $pulseDirectory '*') -DestinationPath $pulseArchive -Force
$pulseHash=Get-FileHash -LiteralPath $pulseArchive -Algorithm SHA256
Set-Content -LiteralPath ($pulseArchive+'.sha256') -Value ($pulseHash.Hash.ToLowerInvariant()+'  Nagi-win-x64.zip') -Encoding ascii
Write-Output 'App and installer scripts signed and timestamped; ZIP and checksum refreshed.'
