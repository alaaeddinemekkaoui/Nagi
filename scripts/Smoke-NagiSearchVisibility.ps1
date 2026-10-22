param([Parameter(Mandatory)][string]$ExecutablePath,[ValidateSet('Horizontal','Vertical')][string]$Orientation='Horizontal')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class SearchVisibilityNative {
 [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr h);
}
'@
$qaDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/search-visibility-'+$Orientation+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
[IO.Directory]::CreateDirectory($qaDirectory)|Out-Null
$qaFile=Join-Path $qaDirectory 'settings.json'
@{Language='en';WelcomeCompleted=$true;StartExpanded=$true;AlwaysShowMiniBar=$true;IndexFiles=$false;Theme='Dark';GlassEnabled=$false;ReducedMotion=$true;
 LockedProfile='Default';AutomaticSwitching=$false;CloseOnDeactivate=$false;HideInFullscreen=$false;AutomaticUpdates=$false;FpsEnabled=$false;ObsEnabled=$false;WeatherEnabled=$false;LiveMedia=$false;
 Position=@{Orientation=$Orientation;Anchor=$(if($Orientation -eq 'Horizontal'){'Top edge'}else{'Right edge'})};
 Hotkey=@{Modifiers=15;Key=131};HideHotkey=@{Modifiers=15;Key=132};SearchHotkey=@{Modifiers=15;Key=133}}|ConvertTo-Json -Depth 6|Set-Content $qaFile -Encoding utf8
$qaProcess=$null;$root=$null;$handle=[IntPtr]::Zero
function Start-App {
 $start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($ExecutablePath));$start.UseShellExecute=$false;$start.ArgumentList.Add('--data-dir');$start.ArgumentList.Add($qaDirectory)
 $script:qaProcess=[Diagnostics.Process]::Start($start)
 for($i=0;$i -lt 100;$i++){
  $script:root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))|Where-Object {$_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1
  if($root){$script:handle=[IntPtr]$root.Current.NativeWindowHandle;Start-Sleep -Milliseconds 800;return};Start-Sleep -Milliseconds 100
 };throw 'No test window'
}
function Find([string]$value,[switch]$Name,[switch]$Optional){
 $property=if($Name){[Windows.Automation.AutomationElement]::NameProperty}else{[Windows.Automation.AutomationElement]::AutomationIdProperty}
 $item=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($property,$value))
 if(!$item -and !$Optional){throw ('Missing '+$value)};return $item
}
function Invoke([string]$value,[switch]$Name){(Find $value -Name:$Name).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 450}
function Open-Options {Invoke 'Open Nagi control center' -Name;Invoke 'Settings' -Name;Invoke 'SettingsCategorySearchshortcuts'}
function Extent { $r=$root.Current.BoundingRectangle;return $(if($Orientation -eq 'Horizontal'){$r.Width}else{$r.Height})/([SearchVisibilityNative]::GetDpiForWindow($handle)/96.0) }
function Stop-App {if($qaProcess -and !$qaProcess.HasExited){[SearchVisibilityNative]::PostMessage($handle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;if(!$qaProcess.WaitForExit(5000)){Stop-Process -Id $qaProcess.Id -Force}}}
try{
 Start-App;Invoke 'Collapse Nagi' -Name
 if(!(Find 'RailSearchButton' -Optional)){throw 'Existing settings no longer show search by default'}
 $shown=Extent
 Open-Options;(Find 'ShowDockSearch').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 400
 $saved=Get-Content $qaFile -Raw|ConvertFrom-Json
 if($saved.ShowDockSearch -or $saved.IndexFiles){throw 'Search visibility did not save independently of disabled indexing'}
 Invoke 'Collapse Nagi' -Name
 if(Find 'RailSearchButton' -Optional){throw 'Hidden search button is still present'}
 $hidden=Extent
 if([Math]::Abs(($shown-$hidden)-36) -gt 2){throw ('Search left an empty slot: shown='+$shown+' hidden='+$hidden)}
 [SearchVisibilityNative]::PostMessage($handle,0x312,[IntPtr]0x5059,[IntPtr]::Zero)|Out-Null;Start-Sleep -Milliseconds 700
 $search=$null
 foreach($window in [Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))){
  if($window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'QuickSearchQuery'))){$search=$window;break}
 };if(!$search){throw 'Search shortcut no longer opens search when the button is hidden'}
 [SearchVisibilityNative]::PostMessage([IntPtr]$search.Current.NativeWindowHandle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;Start-Sleep -Milliseconds 500
 Stop-App;Start-App;Invoke 'Collapse Nagi' -Name
 if(Find 'RailSearchButton' -Optional){throw 'Hidden search button returned after restarting'}
 Open-Options;(Find 'ShowDockSearch').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 400
 Invoke 'Collapse Nagi' -Name
 if(!(Find 'RailSearchButton' -Optional) -or [Math]::Abs((Extent)-$shown) -gt 2){throw 'Restoring search did not restore the layout'}
 if((Get-Content $qaFile -Raw|ConvertFrom-Json).IndexFiles){throw 'Restoring the button enabled indexing'}
 @{Orientation=$Orientation;ShownExtent=$shown;HiddenExtent=$hidden;Persisted=$true;ShortcutAvailable=$true;IndexingRemainedDisabled=$true}|ConvertTo-Json|Set-Content (Join-Path $qaDirectory 'evidence.json')
 Write-Output ('PASS: search visibility, no empty slot, independent indexing, shortcut, restart and restore. '+$qaDirectory)
}finally{Stop-App}
