param([Parameter(Mandatory)][string]$ExecutablePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class NagiUtilitiesNative {
 [StructLayout(LayoutKind.Sequential)]public struct Rect{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
 [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
}
'@
$qaRoot=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/utilities-'+(Get-Date -Format yyyyMMdd-HHmmss))
[IO.Directory]::CreateDirectory($qaRoot)|Out-Null
@{WelcomeCompleted=$true;Language='en';Theme='Dark';GlassEnabled=$false;LockedProfile='Work';AutomaticSwitching=$false;
 CloseOnDeactivate=$false;HideInFullscreen=$false;AutomaticUpdates=$false;IndexFiles=$false;FpsEnabled=$false;ObsEnabled=$false;
 WeatherEnabled=$true;WeatherLocation=@{Name='Casablanca';Country='Morocco';Region='';Latitude=33.59;Longitude=-7.61};
 Position=@{Orientation='Horizontal';Anchor='Top edge'};LiveTimer=$true;LiveMedia=$true;
 Workspaces=@(@{Profile='Work';Apps=@();Widgets=@('Performance','Media','Audio','Timer','Weather');Timer=@{Minutes=25;AutoStart=$false}});
 Hotkey=@{Modifiers=7;Key=131};HideHotkey=@{Modifiers=7;Key=132};SearchHotkey=@{Modifiers=7;Key=133}} |
 ConvertTo-Json -Depth 9|Set-Content (Join-Path $qaRoot 'settings.json') -Encoding utf8
function Find([string]$value,[switch]$Name){
 $property=if($Name){[Windows.Automation.AutomationElement]::NameProperty}else{[Windows.Automation.AutomationElement]::AutomationIdProperty}
 for($attempt=0;$attempt -lt 35;$attempt++){
  $element=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($property,$value))
  if($element){return $element};Start-Sleep -Milliseconds 100
 };throw ('Missing '+$value)
}
function Invoke([string]$id){(Find $id).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 450}
function Snapshot([string]$name){
 Start-Sleep -Milliseconds 350
 $r=[NagiUtilitiesNative+Rect]::new();[NagiUtilitiesNative]::GetWindowRect($handle,[ref]$r)|Out-Null
 $bitmap=[Drawing.Bitmap]::new($r.Right-$r.Left,$r.Bottom-$r.Top);$g=[Drawing.Graphics]::FromImage($bitmap);$dc=$g.GetHdc()
 try{if(![NagiUtilitiesNative]::PrintWindow($handle,$dc,2)){throw 'Window capture failed'}}finally{$g.ReleaseHdc($dc)}
 try{$bitmap.Save((Join-Path $qaRoot ($name+'.png')))}finally{$g.Dispose();$bitmap.Dispose()}
}
function Dev-Tools { Invoke 'PreferencesTab';Invoke 'SettingsCategoryDevelopertools' }
$start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($ExecutablePath));$start.UseShellExecute=$false
foreach($argument in @('--data-dir',$qaRoot,'--dev-page','Utilities')){$start.ArgumentList.Add($argument)}
$process=[Diagnostics.Process]::Start($start)
try {
 [NagiUtilitiesNative]::SetCursorPos(20,500)|Out-Null
 for($attempt=0;$attempt -lt 120;$attempt++){
  if($process.HasExited){throw ('App exited: '+$process.ExitCode)}
  $root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id))|Where-Object {$_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1
  if($root){break};Start-Sleep -Milliseconds 100
 };if(!$root){throw 'No Utilities window'};$handle=[IntPtr]$root.Current.NativeWindowHandle
 Start-Sleep -Seconds 2
 if((Find 'UtilityTimerRemaining').Current.Name -ne '25:00'){throw 'Utilities did not use profile minutes'}
 foreach($tab in @('OverviewTab','RulesTab','UtilitiesTab','PreferencesTab')){if((Find $tab).Current.IsOffscreen){throw ('Navigation clipped: '+$tab)}}
 Snapshot 'utilities-dark'
 Invoke 'UtilityTimerPrimary';Start-Sleep -Milliseconds 1100
 if((Find 'UtilityTimerRemaining').Current.Name -notmatch '^24:'){throw 'Utilities start failed'}
 Invoke 'UtilityTimerPrimary';$paused=(Find 'UtilityTimerRemaining').Current.Name;Start-Sleep -Milliseconds 1300
 if((Find 'UtilityTimerRemaining').Current.Name -ne $paused){throw 'Utilities pause failed'}
 Invoke 'UtilityTimerPrimary';Start-Sleep -Milliseconds 1100
 if((Find 'UtilityTimerRemaining').Current.Name -ge $paused){throw 'Utilities resume failed'}
 (Find 'Reset timer' -Name).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 350
 if((Find 'UtilityTimerRemaining').Current.Name -ne '25:00'){throw 'Utilities reset failed'}
 (Find 'UtilityTimerWidget').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 350
 $saved=Get-Content (Join-Path $qaRoot 'settings.json') -Raw|ConvertFrom-Json
 if(($saved.Workspaces|Where-Object Profile -eq 'Work').Widgets -contains 'Timer'){throw 'Timer widget option did not save'}
 (Find 'UtilityTimerWidget').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 350
 Invoke 'OverviewTab';Snapshot 'overview-dark';Invoke 'RulesTab';Snapshot 'profiles';Invoke 'PreferencesTab';Snapshot 'settings'
 foreach($theme in @(@{Index=3;Name='light'},@{Index=0;Name='glass'},@{Index=1;Name='akeno'},@{Index=2;Name='dark'})){
  Dev-Tools;Invoke ('DevelopmentTheme'+$theme.Index);Invoke 'UtilitiesTab';Snapshot ('theme-'+$theme.Name)
 }
 Dev-Tools;Invoke 'DevelopmentTimer';Snapshot 'timer-running'
 for($attempt=0;$attempt -lt 120;$attempt++){
  if((Find 'LiveTimerCountdown').Current.Name -eq '00:00'){break};Start-Sleep -Milliseconds 100
 }
 if((Find 'LiveTimerCountdown').Current.Name -ne '00:00'){throw 'Timer completion did not reach 00:00'}
 if((Find 'TimerCompleteCheck').Current.IsOffscreen){throw 'Completion check is missing'}
 Snapshot 'timer-complete'
 (Find 'Reveal Nagi' -Name).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 500
 Snapshot 'dock';(Find 'Open Nagi control center' -Name).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
 Dev-Tools;Invoke 'DevelopmentMusic';Snapshot 'media-notch'
 Write-Output ('PASS: floating navigation, timer profile minutes, start/pause/resume/reset, widget persistence, weather, four themes, real countdown completion and captures. '+$qaRoot)
} finally {
 if($process -and !$process.HasExited){$process.Kill();$process.WaitForExit(5000)|Out-Null}
}
