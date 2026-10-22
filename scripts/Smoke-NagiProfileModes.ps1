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
$meetingStart=[DateTime]::UtcNow.AddHours(1).ToString("yyyyMMdd'T'HHmmss'Z'");$meetingEnd=[DateTime]::UtcNow.AddHours(2).ToString("yyyyMMdd'T'HHmmss'Z'")
$calendarFile=Join-Path $qaDirectory 'meetings.ics'
[IO.File]::WriteAllText($calendarFile,"BEGIN:VCALENDAR`r`nVERSION:2.0`r`nBEGIN:VEVENT`r`nUID:mode-test`r`nDTSTART:$meetingStart`r`nDTEND:$meetingEnd`r`nSUMMARY:Mode test meeting`r`nURL:https://example.com/meeting`r`nEND:VEVENT`r`nEND:VCALENDAR`r`n")
$protected=[Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes($calendarFile),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
[IO.File]::WriteAllBytes((Join-Path $qaDirectory 'calendar.credential'),$protected)
@{Language='en';WelcomeCompleted=$true;StartExpanded=$true;Theme='Dark';LockedProfile='Gaming';AutomaticSwitching=$true;
 CloseOnDeactivate=$false;HideInFullscreen=$false;AutomaticUpdates=$false;IndexFiles=$false;FpsEnabled=$false;ObsEnabled=$false;WeatherEnabled=$false;ReducedMotion=$true;AlwaysShowMiniBar=$true;
 Position=@{Orientation='Horizontal';Anchor='Top edge'};CalendarEnabled=$true;GitHubEnabled=$true;PinnedRepositories=@("alaaeddinemekkaoui/Nagi");
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
 };if(!$root){throw 'No test window'};$handle=[IntPtr]$root.Current.NativeWindowHandle;Start-Sleep -Milliseconds 800
 Invoke 'OverviewTab';Find 'GameSessionTime'|Out-Null;Find 'GamingFps'|Out-Null;Find 'GamingDiscord'|Out-Null;Snapshot 'gaming-mode'
 Invoke 'RulesTab';Invoke 'ActivateProfile0';Invoke 'OverviewTab';Find 'DeveloperNotifications'|Out-Null;Snapshot 'developer-mode'
 Invoke 'RulesTab';Invoke 'ActivateProfile4';Invoke 'OverviewTab';Find 'CreatorObs'|Out-Null
 $meter=Find 'Live microphone level' -Name
 Start-Sleep -Milliseconds 400
 $sample=Get-Content (Join-Path $qaDirectory 'logs/pulse.jsonl')|ForEach-Object {$_|ConvertFrom-Json}|Where-Object {$_.category -eq 'sampling' -and $_.message -like 'audio=*'}|Select-Object -Last 1
 if($sample.message -notlike '*micPeak=True*'){throw 'Creator microphone meter is not active'};Snapshot 'creator-mode'
 Open-Category 'SettingsCategoryAboutdiagnostics'
 $sample=Get-Content (Join-Path $qaDirectory 'logs/pulse.jsonl')|ForEach-Object {$_|ConvertFrom-Json}|Where-Object {$_.category -eq 'sampling' -and $_.message -like 'audio=*'}|Select-Object -Last 1
 if($sample.message -notlike '*micPeak=False*'){throw 'Hidden Creator meter kept sampling'}
 Invoke 'RulesTab';Invoke 'ActivateProfile2'
 $open=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Open Nagi control center'))
 if($open -and !$open.Current.IsOffscreen){$open.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 300}
 Invoke 'OverviewTab';Find 'FocusTaskValue'|Out-Null
 for($attempt=0;$attempt -lt 60;$attempt++){if((Find 'FocusMeeting').Current.Name -like '*Mode test meeting*'){break};Start-Sleep -Milliseconds 200}
 if((Find 'FocusMeeting').Current.Name -notlike '*Mode test meeting*'){throw 'Focus did not load the protected local .ics calendar'};Snapshot 'focus-mode'
 Invoke 'RulesTab';Invoke 'ProfileCardFocus';Find 'ProfileModeSelector'|Out-Null
 (Find 'ProfileFocusTask').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue('Ship a silly little notch')
 Invoke 'Save focus task' -Name;Invoke 'OverviewTab'
 if((Find 'FocusTaskValue').Current.Name -ne 'Ship a silly little notch'){throw 'Focus task was not saved'}
 @{Passed=$true;Gaming=$true;Developer=$true;Creator=$true;Focus=$true;CalendarFile=$true;CreatorVisibilitySampling=$true;FocusTaskSaved=$true;Data=$qaDirectory}|ConvertTo-Json|Set-Content (Join-Path $qaDirectory 'result.json')
 Get-Content (Join-Path $qaDirectory 'result.json')
}finally{
 if($handle -ne [IntPtr]::Zero){[SettingsGamingNative]::PostMessage($handle,0x10,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null}
 if($qaProcess -and !$qaProcess.HasExited -and !$qaProcess.WaitForExit(15000)){$qaProcess.Kill()}
}
