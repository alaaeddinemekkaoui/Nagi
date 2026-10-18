param([Parameter(Mandatory)][string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
$testData = Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/language-restart-' + (Get-Date -Format yyyyMMdd-HHmmss))
[IO.Directory]::CreateDirectory($testData) | Out-Null
@{ Language='en'; AutomaticUpdates=$false; IndexFiles=$false; FpsEnabled=$false; CloseOnDeactivate=$false;
   PreferredMusicPlayer='Spotify'; Hotkey=@{Modifiers=15;Key=131}; HideHotkey=@{Modifiers=15;Key=132}; SearchHotkey=@{Modifiers=15;Key=133} } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $testData 'settings.json') -Encoding utf8
$owned = [Collections.Generic.List[Diagnostics.Process]]::new()
try {
 . (Join-Path $PSScriptRoot 'Smoke-NagiWelcome.ps1') -ExecutablePath $ExecutablePath -DataDirectory $testData -SkipWeather -IntroductionOnly
 $owned.Add($nagiProcess); $before = $nagiProcess.Id
 Invoke-Nagi 'Reveal Nagi' -Name
 Invoke-Nagi 'Open Nagi control center' -Name
 Invoke-Nagi 'Settings' -Name
 Invoke-Nagi 'SettingsCategoryLanguage'
 Select-Nagi 'AppLanguage' 'Français · French'
 $saved = Get-Content -LiteralPath $nagiSettingsPath -Raw | ConvertFrom-Json
 if ($saved.Language -ne 'fr') { throw 'French preference was not saved' }
 Invoke-Nagi 'RestartForLanguage'
 if (!$nagiProcess.WaitForExit(15000)) { throw 'Previous process did not exit after language restart' }
 $replacement = $null
 for ($attempt=0; $attempt -lt 100 -and !$replacement; $attempt++) {
  Start-Sleep -Milliseconds 100
  $replacement = Get-CimInstance Win32_Process -Filter "Name='Nagi.exe'" | Where-Object {
   $_.ProcessId -ne $before -and $_.CommandLine -and $_.CommandLine.Contains($testData) -and $_.ExecutablePath -eq [IO.Path]::GetFullPath($ExecutablePath)
  } | Select-Object -First 1
 }
 if (!$replacement) { throw 'Restarted language process did not appear' }
 $nagiProcess = Get-Process -Id $replacement.ProcessId; $owned.Add($nagiProcess)
 $script:nagiRoot = Get-NagiRoot
 $nagiActivate = [Threading.EventWaitHandle]::OpenExisting($nagiActivationName); $nagiActivate.Set()|Out-Null; $nagiActivate.Dispose()
 Start-Sleep -Milliseconds 500
 $pack = Get-Content -LiteralPath (Join-Path (Split-Path $ExecutablePath -Parent) 'languages/fr.json') -Raw | ConvertFrom-Json -AsHashtable
 Invoke-Nagi $pack.strings.Settings -Name
 Invoke-Nagi 'SettingsCategoryLanguage'
 Find-Nagi 'AppLanguage' | Out-Null
 $after = Get-Content -LiteralPath $nagiSettingsPath -Raw | ConvertFrom-Json
 if ($after.Language -ne 'fr' -or $after.UserName -ne 'Nagi QA' -or $after.PreferredMusicPlayer -ne 'Spotify' -or $after.Theme -ne 'Akeno') {
  throw 'Language restart did not preserve other preferences'
 }
 @{ BeforePid=$before; AfterPid=$nagiProcess.Id; Language=$after.Language; PreferencesPreserved=$true; FrenchSettingsVisible=$true } |
  ConvertTo-Json | Set-Content -LiteralPath (Join-Path $testData 'evidence.json') -Encoding utf8
 Save-NagiImage 'language-settings-french'
 Write-Output "PASS: full welcome, persisted French choice, actual restart, translated Settings and preserved preferences. $testData"
} finally {
 if ($nagiProcess -and !$owned.Contains($nagiProcess)) { $owned.Add($nagiProcess) }
 foreach ($process in $owned) {
  if (!$process.HasExited) { $process.CloseMainWindow()|Out-Null; if (!$process.WaitForExit(5000)) { Stop-Process -Id $process.Id -Force } }
 }
}
