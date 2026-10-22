param([Parameter(Mandatory)][string]$ExecutablePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class SettingsGamingNative {
 [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
}
'@
$qaDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/settings-gaming-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
[IO.Directory]::CreateDirectory($qaDirectory)|Out-Null
@{Language='en';WelcomeCompleted=$true;StartExpanded=$true;Theme='Dark';LockedProfile='Gaming';AutomaticSwitching=$true;
 CloseOnDeactivate=$false;HideInFullscreen=$false;AutomaticUpdates=$false;IndexFiles=$false;FpsEnabled=$false;ObsEnabled=$false;WeatherEnabled=$false;ReducedMotion=$true;AlwaysShowMiniBar=$true;
 Position=@{Orientation='Horizontal';Anchor='Top edge'};
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
 try{[SettingsGamingNative]::PrintWindow([IntPtr]$root.Current.NativeWindowHandle,$dc,2)|Out-Null}finally{$g.ReleaseHdc($dc)}
 try{$image.Save((Join-Path $qaDirectory ($name+'.png')))}finally{$g.Dispose();$image.Dispose()}
}
function Open-Category([string]$id){
 Invoke 'PreferencesTab'
 $back=$script:root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Back to settings'))
 if($back){$back.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 250}
 Invoke $id
}
try{
 for($i=0;$i -lt 100;$i++){
  $root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))|Where-Object {$_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1
  if($root){break};Start-Sleep -Milliseconds 100
 };if(!$root){throw 'No test window'};$handle=[IntPtr]$root.Current.NativeWindowHandle;Start-Sleep -Milliseconds 700
 $scale=[SettingsGamingNative]::GetDpiForWindow($handle)/96.0
 [SettingsGamingNative]::SetWindowPos($handle,[IntPtr]::Zero,0,0,[int](960*$scale),[int](800*$scale),0x16)|Out-Null
 Open-Category 'SettingsCategoryAboutdiagnostics'
 Find 'AboutVersion'|Out-Null;Find 'DiagnosticsSession'|Out-Null;Find 'AboutRedditCredit'|Out-Null;Find 'AboutRedditSearchCredit'|Out-Null;Snapshot 'about-desktop'
 Open-Category 'SettingsCategoryHardwareaudio'
 Find 'HardwareCpuStatus'|Out-Null;Find 'HardwareGpuStatus'|Out-Null;Find 'SettingsOutputVolume'|Out-Null;Snapshot 'hardware-audio-desktop'
 [SettingsGamingNative]::SetWindowPos($handle,[IntPtr]::Zero,0,0,[int](640*$scale),[int](800*$scale),0x16)|Out-Null;Start-Sleep -Milliseconds 400
 $cpu=(Find 'HardwareCpuStatus').Current.BoundingRectangle;$gpu=(Find 'HardwareGpuStatus').Current.BoundingRectangle
 if($gpu.Top -le $cpu.Top){throw 'Hardware cards did not stack in narrow window'};Snapshot 'hardware-audio-narrow'
 Open-Category 'SettingsCategoryIntegrations'
 Find 'PresentMonFpsValue'|Out-Null;Find 'AddGamingRule'|Out-Null
 $game=Find 'AutomaticGameDetection';$game.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 500
 $fps=Find 'PresentMonCapture';$fps.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 500
 $saved=Get-Content (Join-Path $qaDirectory 'settings.json') -Raw|ConvertFrom-Json
 if(!$saved.AutomaticGameDetection -or !$saved.FpsEnabled -or $saved.LockedProfile -ne 'Gaming'){throw 'Game/FPS settings did not save independently of profile lock'}
 Find 'Save OBS connection' -Name|Out-Null;Snapshot 'integrations-narrow'
 Open-Category 'SettingsCategoryAboutdiagnostics';Start-Sleep -Milliseconds 300
 $sampling=Get-Content (Join-Path $qaDirectory 'logs/pulse.jsonl')|ForEach-Object {$_|ConvertFrom-Json}|Where-Object {$_.category -eq 'sampling' -and $_.message -like 'audio=*'}|Select-Object -Last 1
 if($sampling.message -notlike '*fps=False*'){throw 'FPS sampling continued on an unrelated settings page'}
 Open-Category 'SettingsCategoryIntegrations'
 Invoke 'Collapse Nagi' -Name;Start-Sleep -Milliseconds 800
 $slot=Find 'RailFpsButton'
 if($slot.Current.IsOffscreen -or $slot.Current.BoundingRectangle.Width -lt 20){throw 'FPS slot is not visible on the horizontal mini bar'};Snapshot 'fps-dock'
 Invoke 'Open Nagi control center' -Name;Open-Category 'SettingsCategoryIntegrations'
 (Find 'PresentMonCapture').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 350
 @{Passed=$true;AboutCards=$true;HardwareAudioCards=$true;ResponsiveCards=$true;IntegrationsCategory=$true;GamingFpsPreferences=$true;FpsDock=$true;HiddenFpsPaused=$true;Credits=$true;Data=$qaDirectory}|ConvertTo-Json|Set-Content (Join-Path $qaDirectory 'result.json')
 Get-Content (Join-Path $qaDirectory 'result.json')
}finally{
 if($handle -ne [IntPtr]::Zero){[SettingsGamingNative]::PostMessage($handle,0x10,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null}
 if($qaProcess -and !$qaProcess.HasExited -and !$qaProcess.WaitForExit(15000)){$qaProcess.Kill()}
}
