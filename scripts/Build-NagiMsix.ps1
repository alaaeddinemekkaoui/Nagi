param([string]$PackageDirectory='', [string]$CertificateThumbprint='', [ValidateSet('CurrentUser','LocalMachine')][string]$CertificateStore='CurrentUser', [string]$Publisher='CN=Nagi')
$ErrorActionPreference='Stop'
$nagiRoot=Split-Path $PSScriptRoot -Parent
$nagiSource=if($PackageDirectory){[IO.Path]::GetFullPath($PackageDirectory)}else{Join-Path $nagiRoot 'artifacts/Nagi-win-x64'}
if(!(Test-Path -LiteralPath (Join-Path $nagiSource 'Nagi.exe'))){throw 'Build the production package first.'}
$nagiSdk=Get-ChildItem -LiteralPath 'C:/Program Files (x86)/Windows Kits/10/bin' -Directory | Sort-Object Name -Descending | Where-Object {Test-Path -LiteralPath (Join-Path $_.FullName 'x64/makeappx.exe')} | Select-Object -First 1
if(!$nagiSdk){throw 'Install the Windows SDK with MakeAppx.'}
if($CertificateThumbprint){
 if($CertificateThumbprint -notmatch '^[a-fA-F0-9]{40}$'){throw 'Use a certificate-store SHA1 thumbprint.'}
 $nagiCertificate=Get-Item -LiteralPath ('Cert:/'+$CertificateStore+'/My/'+$CertificateThumbprint)
 if(!$nagiCertificate.HasPrivateKey -or $nagiCertificate.NotAfter -lt (Get-Date)){throw 'Choose a valid code-signing certificate with a private key.'}
 $Publisher=$nagiCertificate.Subject
}
$nagiStage=Join-Path $nagiRoot ('artifacts/msix-stage-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $nagiStage|Out-Null
Get-ChildItem -LiteralPath $nagiSource | Copy-Item -Destination $nagiStage -Recurse
Add-Type -AssemblyName System.Drawing
$nagiIcon=[Drawing.Icon]::new((Join-Path $nagiSource 'Assets/Nagi.ico'),256,256)
$nagiBitmap=$nagiIcon.ToBitmap()
try {
 foreach($nagiSize in @(44,150)){
  $nagiImage=[Drawing.Bitmap]::new($nagiSize,$nagiSize);$nagiGraphics=[Drawing.Graphics]::FromImage($nagiImage)
  try{$nagiGraphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$nagiGraphics.DrawImage($nagiBitmap,0,0,$nagiSize,$nagiSize);$nagiImage.Save((Join-Path $nagiStage ('Assets/PackageLogo'+$nagiSize+'.png')),[Drawing.Imaging.ImageFormat]::Png)}finally{$nagiGraphics.Dispose();$nagiImage.Dispose()}
 }
}finally{$nagiBitmap.Dispose();$nagiIcon.Dispose()}
[xml]$nagiProject=Get-Content -LiteralPath (Join-Path $nagiRoot 'src/Pulse.App/Pulse.App.csproj')
$nagiVersion=[string]$nagiProject.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
$nagiPublisherXml=[Security.SecurityElement]::Escape($Publisher)
$nagiManifest=@"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap uap3 rescap">
 <Identity Name="Nagi.Desktop" Publisher="$nagiPublisherXml" Version="$nagiVersion.0" ProcessorArchitecture="x64"/>
 <Properties><DisplayName>Nagi</DisplayName><PublisherDisplayName>Nagi</PublisherDisplayName><Logo>Assets\PackageLogo150.png</Logo></Properties>
 <Resources><Resource Language="en-us"/></Resources>
 <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.22621.0" MaxVersionTested="10.0.26100.0"/></Dependencies>
 <Applications><Application Id="Nagi" Executable="Nagi.exe" EntryPoint="Windows.FullTrustApplication"><uap:VisualElements DisplayName="Nagi" Description="Your calm desktop companion" BackgroundColor="transparent" Square150x150Logo="Assets\PackageLogo150.png" Square44x44Logo="Assets\PackageLogo44.png"/></Application></Applications>
 <Capabilities><rescap:Capability Name="runFullTrust"/><uap3:Capability Name="userNotificationListener"/></Capabilities>
</Package>
"@
[IO.File]::WriteAllText((Join-Path $nagiStage 'AppxManifest.xml'),$nagiManifest,[Text.UTF8Encoding]::new($false))
$nagiOutput=Join-Path $nagiRoot $(if($CertificateThumbprint){'artifacts/Nagi.msix'}else{'artifacts/Nagi-Unsigned.msix'})
& (Join-Path $nagiSdk.FullName 'x64/makeappx.exe') pack /d $nagiStage /p $nagiOutput /o | Out-Null
if($LASTEXITCODE -ne 0){throw 'MSIX packaging failed.'}
if($CertificateThumbprint){
 $nagiStoreArguments=if($CertificateStore -eq 'LocalMachine'){@('/sm')}else{@()}
 & (Join-Path $nagiSdk.FullName 'x64/signtool.exe') sign /fd SHA256 /sha1 $CertificateThumbprint @nagiStoreArguments /tr http://timestamp.digicert.com /td SHA256 $nagiOutput
 if($LASTEXITCODE -ne 0){throw 'MSIX signing failed.'}
 & (Join-Path $nagiSdk.FullName 'x64/signtool.exe') verify /pa $nagiOutput
 if($LASTEXITCODE -ne 0){throw 'MSIX signature verification failed.'}
}
$nagiHash=(Get-FileHash -LiteralPath $nagiOutput -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($nagiOutput+'.sha256'),$nagiHash+'  '+(Split-Path $nagiOutput -Leaf),[Text.Encoding]::ASCII)
Write-Output ('Built '+$nagiOutput)
if(!$CertificateThumbprint){Write-Output 'Unsigned review package. Windows requires a trusted signature before installation. No certificate or trust setting was changed.'}
