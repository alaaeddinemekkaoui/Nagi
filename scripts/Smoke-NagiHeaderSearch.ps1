param([Parameter(Mandatory)][string]$ExecutablePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class HeaderSearchNative {
 [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
}
'@
$qaDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/header-search-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
[IO.Directory]::CreateDirectory($qaDirectory)|Out-Null
@{Language='en';WelcomeCompleted=$true;StartExpanded=$true;Theme='Akeno';LockedProfile='Work';AutomaticSwitching=$false;
 CloseOnDeactivate=$false;HideInFullscreen=$false;AutomaticUpdates=$false;IndexFiles=$false;FpsEnabled=$false;ObsEnabled=$false;WeatherEnabled=$false;ReducedMotion=$true;
 Hotkey=@{Modifiers=15;Key=131};HideHotkey=@{Modifiers=15;Key=132};SearchHotkey=@{Modifiers=15;Key=133}}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $qaDirectory 'settings.json') -Encoding utf8
$start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($ExecutablePath));$start.UseShellExecute=$false;$start.ArgumentList.Add('--data-dir');$start.ArgumentList.Add($qaDirectory)
$qaProcess=[Diagnostics.Process]::Start($start);$root=$null;$handle=[IntPtr]::Zero
function Find([string]$value,[switch]$Name){
 $property=if($Name){[Windows.Automation.AutomationElement]::NameProperty}else{[Windows.Automation.AutomationElement]::AutomationIdProperty}
 $item=$script:root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($property,$value))
 if(!$item){throw ('Missing '+$value)};return $item
}
function Invoke([string]$value,[switch]$Name){(Find $value -Name:$Name).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 350}
function Snapshot([string]$name){
 $box=$root.Current.BoundingRectangle;$image=[Drawing.Bitmap]::new([int]$box.Width,[int]$box.Height);$g=[Drawing.Graphics]::FromImage($image);$dc=$g.GetHdc()
 try{[HeaderSearchNative]::PrintWindow([IntPtr]$root.Current.NativeWindowHandle,$dc,2)|Out-Null}finally{$g.ReleaseHdc($dc)}
 try{$image.Save((Join-Path $qaDirectory ($name+'.png')))}finally{$g.Dispose();$image.Dispose()}
}
function Assert-Provider([string]$provider,[string]$expectedHost=''){
 if((Find ('QuickSearchProvider'+$provider)).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [Windows.Automation.ToggleState]::On){throw ('Provider was not selected: '+$provider)}
 if($expectedHost -and (Find 'QuickSearchDetail').Current.Name -notlike ('https://'+$expectedHost+'/*')){throw ('Unexpected browser target: '+(Find 'QuickSearchDetail').Current.Name)}
}
try{
 for($i=0;$i -lt 100;$i++){
  $root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))|Where-Object {$_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1
  if($root){break};Start-Sleep -Milliseconds 100
 };if(!$root){throw 'No test window'};$handle=[IntPtr]$root.Current.NativeWindowHandle;Start-Sleep -Milliseconds 800
 Invoke 'PreferencesTab'
 $scale=[HeaderSearchNative]::GetDpiForWindow($handle)/96.0
 foreach($width in @(960,760,640)){
  [HeaderSearchNative]::SetWindowPos($handle,[IntPtr]::Zero,0,0,[int]($width*$scale),[int](720*$scale),0x16)|Out-Null;Start-Sleep -Milliseconds 500
  $bounds=$root.Current.BoundingRectangle;$action=(Find 'HideHeaderButton').Current.BoundingRectangle;$prior=0
  foreach($id in @('OverviewTab','RulesTab','UtilitiesTab','PreferencesTab')){
   $tab=Find $id;$r=$tab.Current.BoundingRectangle
   if($tab.Current.IsOffscreen -or $r.Width -lt 25 -or [Math]::Abs(($r.Top+$r.Height/2)-($action.Top+$action.Height/2)) -gt 3*$scale){throw ('Navigation is not on the header row: '+$id+' width='+$width)}
   if($r.Left -lt $bounds.Left -or $r.Right -gt $action.Left -or $r.Left -lt $prior-1){throw ('Header control overlap: '+$id+' width='+$width)};$prior=$r.Right
  };Snapshot ('header-'+$width)
 }
 [HeaderSearchNative]::SetWindowPos($handle,[IntPtr]::Zero,0,0,[int](960*$scale),[int](720*$scale),0x16)|Out-Null;Start-Sleep -Milliseconds 400
 Invoke 'SettingsCategoryAboutdiagnostics';Find 'AboutRedditSearchCredit'|Out-Null
 Invoke 'Quick search' -Name
 $search=$null
 foreach($window in [Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))){
  if($window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'QuickSearchQuery'))){$search=$window;break}
 };if(!$search){throw 'Search did not open'};$owner=$root;$root=$search
 $query=Find 'QuickSearchQuery';$value=$query.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern);$value.SetValue('quiet cats');$query.SetFocus();Start-Sleep -Milliseconds 300
 Assert-Provider 'Local'
 foreach($provider in @('Google','YouTube','Local')){
  [Windows.Forms.SendKeys]::SendWait('{TAB}');Start-Sleep -Milliseconds 350
  Assert-Provider $provider $(if($provider -eq 'Google'){'www.google.com'}elseif($provider -eq 'YouTube'){'www.youtube.com'}else{''})
 }
 $value.SetValue('youtube: quiet cats');Start-Sleep -Milliseconds 350;Assert-Provider 'YouTube' 'www.youtube.com'
 $query.SetFocus();[Windows.Forms.SendKeys]::SendWait('{TAB}');Start-Sleep -Milliseconds 350;Assert-Provider 'Local'
 if($value.Current.Value -ne 'quiet cats'){throw 'Switching lost the query or retained its old prefix'}
 $value.SetValue('browser : cats & dogs');Start-Sleep -Milliseconds 350;Assert-Provider 'Google' 'www.google.com'
 (Find 'QuickSearchProviderYouTube').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 350;Assert-Provider 'YouTube' 'www.youtube.com'
 if($value.Current.Value -ne 'cats & dogs'){throw 'Clicking a provider lost the search terms'}
 Snapshot 'youtube-search'
 (Find 'QuickSearchProviderLocal').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();$value.SetValue('> default');Start-Sleep -Milliseconds 350
 $query.SetFocus();$value.SetValue('> gaming');[Windows.Forms.SendKeys]::SendWait('{ENTER}');Start-Sleep -Milliseconds 650;$root=$owner
 if((Get-Content (Join-Path $qaDirectory 'settings.json') -Raw|ConvertFrom-Json).LockedProfile -ne 'Gaming'){throw 'Fast Enter opened an older result'}
 @{InlineHeaderWidths=@(960,760,640);TabCycle=$true;PrefixSelection=$true;ProviderButtons=$true;SearchTermsPreserved=$true;RedditCredit=$true;FreshResultOnEnter=$true}|ConvertTo-Json|Set-Content (Join-Path $qaDirectory 'evidence.json')
 Write-Output ('PASS: inline header at three widths, Reddit credit, Google/YouTube prefixes, Tab cycle, provider buttons and query preservation. '+$qaDirectory)
}finally{
 if($qaProcess -and !$qaProcess.HasExited){
  foreach($window in [Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))){[HeaderSearchNative]::PostMessage([IntPtr]$window.Current.NativeWindowHandle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null}
  if(!$qaProcess.WaitForExit(5000)){Stop-Process -Id $qaProcess.Id -Force}
 }
}
