param([Parameter(Mandatory)][string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
if (-not ('NagiLanguageNative' -as [type])) { Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class NagiLanguageNative {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect rect);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
}
'@ }
$testRoot = Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/languages-' + (Get-Date -Format yyyyMMdd-HHmmss))
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
$package = Split-Path ([IO.Path]::GetFullPath($ExecutablePath)) -Parent
$results = [Collections.Generic.List[object]]::new()
foreach ($code in @('en','fr','es','de','ru','ar')) {
 $ownedProcess = $null
 try {
  $data = Join-Path $testRoot $code; [IO.Directory]::CreateDirectory($data) | Out-Null
  @{ Language=$code; WelcomeCompleted=$false; AutomaticUpdates=$false; IndexFiles=$false; FpsEnabled=$false; ReducedMotion=$false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $data 'settings.json') -Encoding utf8
  $pack = Get-Content -LiteralPath (Join-Path $package "languages/$code.json") -Raw | ConvertFrom-Json -AsHashtable
  $start = [Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($ExecutablePath)); $start.UseShellExecute=$false
  $start.ArgumentList.Add('--data-dir'); $start.ArgumentList.Add($data)
  $ownedProcess = [Diagnostics.Process]::Start($start)
  $window = $null
  for ($attempt=0; $attempt -lt 100 -and !$window; $attempt++) {
   Start-Sleep -Milliseconds 100
   $windows = [Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,
    [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$ownedProcess.Id))
   $window = $windows | Where-Object { $_.Current.ControlType -eq [Windows.Automation.ControlType]::Window -and $_.Current.NativeWindowHandle -ne 0 -and $_.Current.BoundingRectangle.Width -gt 200 } | Select-Object -First 1
  }
  if (!$window) { throw "$code did not open" }
  function Find-Control([string]$id) {
   for ($retry=0; $retry -lt 50; $retry++) {
    $control = $window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id))
    if ($control) { return $control }; Start-Sleep -Milliseconds 100
   }; throw "Missing $id in $code"
  }
  function Invoke-Control([string]$id) { (Find-Control $id).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 350 }
  function Capture([string]$name) {
   [NagiLanguageNative]::SetWindowPos([IntPtr]$window.Current.NativeWindowHandle,[IntPtr](-1),0,0,0,0,0x43) | Out-Null
   [NagiLanguageNative]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle) | Out-Null
   Start-Sleep -Milliseconds 250
   $rect=[NagiLanguageNative+Rect]::new(); [NagiLanguageNative]::GetWindowRect([IntPtr]$window.Current.NativeWindowHandle,[ref]$rect)|Out-Null
   $bitmap=[Drawing.Bitmap]::new($rect.Right-$rect.Left,$rect.Bottom-$rect.Top); $graphics=[Drawing.Graphics]::FromImage($bitmap)
   try { $graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size); $bitmap.Save((Join-Path $data "$name.png")) }
   finally { $graphics.Dispose(); $bitmap.Dispose() }
  }
  if ((Find-Control 'WelcomeNext').Current.Name -ne $pack.strings.Continue) { throw "$code greeting was not translated" }
  Start-Sleep -Milliseconds 1500; Capture 'greeting-english'; Start-Sleep -Milliseconds 2800; Capture 'greeting-japanese'
  Invoke-Control 'WelcomeNext'
  $status = (Find-Control 'WelcomeCurrentLanguage').Current.Name
  if (!$status.Contains($pack.nativeName)) { throw "$code current language missing: $status" }
  Capture 'language'
  $combo = Find-Control 'WelcomeLanguage'
  $combo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 250
  $english = $window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new(
    [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'English · English'),
    [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true)))
  if (!$english) { throw "English language choice missing for $code" }
  $english.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 500
  if ((Find-Control 'WelcomeNext').Current.Name -ne 'Continue') { throw "$code did not switch to English immediately" }
  if (!(Find-Control 'WelcomeCurrentLanguage').Current.Name.Contains('English')) { throw "$code did not update language status" }
  Invoke-Control 'WelcomeNext'; Find-Control 'WelcomeName' | Out-Null
  Capture 'name-in-english'
  if (Test-Path (Join-Path $data 'logs/errors.jsonl')) {
   if ((Get-Item (Join-Path $data 'logs/errors.jsonl')).Length -gt 0) { throw "$code wrote errors" }
  }
  $results.Add([pscustomobject]@{ Language=$code; Loaded=$true; CurrentLanguage=$status; SwitchedToEnglish=$true })
  Write-Output "PASS: $code welcome, language status and immediate switch to English"
 } finally {
  if ($ownedProcess -and !$ownedProcess.HasExited) {
   if ($window) { [NagiLanguageNative]::PostMessage([IntPtr]$window.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null }
   if (!$ownedProcess.WaitForExit(5000)) { Stop-Process -Id $ownedProcess.Id -Force }
  }
 }
}
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $testRoot 'evidence.json') -Encoding utf8
Write-Output "PASS: six languages. Evidence: $testRoot"
