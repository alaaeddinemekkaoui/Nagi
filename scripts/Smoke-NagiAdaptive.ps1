param([Parameter(Mandatory)][string]$ExecutablePath,
 [ValidateSet('Horizontal','Vertical')][string]$Orientation='Horizontal',[switch]$Quick,[switch]$Animated,[switch]$ProfileTimers,[switch]$TimerHover,[ValidateSet('Top edge','Bottom edge','Left edge','Right edge')][string]$Anchor='Bottom edge')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class AdaptiveSmokeNative {
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
 [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point p);
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
 [DllImport("user32.dll")] public static extern int GetWindowRgn(IntPtr h,IntPtr r);
 [DllImport("gdi32.dll")] public static extern IntPtr CreateRectRgn(int l,int t,int r,int b);
 [DllImport("gdi32.dll")] public static extern bool PtInRegion(IntPtr r,int x,int y);
 [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr r);
}
'@
$qaPointer=[AdaptiveSmokeNative+Point]::new();[AdaptiveSmokeNative]::GetCursorPos([ref]$qaPointer)|Out-Null
$qaDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/adaptive-'+$Orientation+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $qaDirectory|Out-Null
$qaFile=Join-Path $qaDirectory 'settings.json'
@{SchemaVersion=1;Language='en';Theme='Dark';WelcomeCompleted=$true;LockedProfile='Default';AutomaticSwitching=$false;
 Position=@{Orientation=$Orientation;Anchor=$(if($PSBoundParameters.ContainsKey('Anchor')){$Anchor}elseif($Orientation -eq 'Horizontal'){'Bottom edge'}else{'Right edge'})};CloseOnDeactivate=$false;HideInFullscreen=$false;IndexFiles=$false;
 FpsEnabled=$false;ObsEnabled=$false;AutomaticUpdates=$false;WeatherEnabled=$false;ReducedMotion=(!$Animated);LiveMedia=$false;TimerMinutes=1;
 Hotkey=@{Modifiers=7;Key=131};HideHotkey=@{Modifiers=7;Key=132};SearchHotkey=@{Modifiers=7;Key=133}}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $qaFile
if($TimerHover){
 $configuration=Get-Content $qaFile -Raw|ConvertFrom-Json -AsHashtable
 $configuration.LockedProfile='Work'
 $configuration.Workspaces=@(@{Profile='Work';Apps=@();Widgets=@('Timer');Timer=@{AutoStart=$false;Minutes=3}})
 $configuration|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $qaFile -Encoding utf8
}
$start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($ExecutablePath));$start.UseShellExecute=$false;$start.ArgumentList.Add('--data-dir');$start.ArgumentList.Add($qaDirectory)
$qaProcess=[Diagnostics.Process]::Start($start)
function Find([string]$Value,[switch]$Id,[switch]$Optional){
 $property=if($Id){[Windows.Automation.AutomationElement]::AutomationIdProperty}else{[Windows.Automation.AutomationElement]::NameProperty}
 for($i=0;$i -lt 40;$i++){$item=$script:root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($property,$Value));if($item){return $item};if($Optional){return $null};Start-Sleep -Milliseconds 100};throw ('Missing '+$Value)
}
function Invoke([string]$Name,[switch]$Id){(Find $Name -Id:$Id).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 500}
function Timer-Input {
 (Find 'ProfileTimerMinutes' -Id).FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Edit))
}
function Set-ProfileMinutes([string]$value){
 $input=Timer-Input;$input.SetFocus();$input.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($value)
 (Find 'ProfileTimerSave' -Id).SetFocus();Invoke 'ProfileTimerSave' -Id
}
function Snapshot([string]$Name){
 $r=[AdaptiveSmokeNative+Rect]::new();[AdaptiveSmokeNative]::GetWindowRect($handle,[ref]$r)|Out-Null
 $bitmap=[Drawing.Bitmap]::new($r.Right-$r.Left,$r.Bottom-$r.Top);$g=[Drawing.Graphics]::FromImage($bitmap);$dc=$g.GetHdc()
 try{[AdaptiveSmokeNative]::PrintWindow($handle,$dc,2)|Out-Null}finally{$g.ReleaseHdc($dc)}
 try{$bitmap.Save((Join-Path $qaDirectory ($Name+'.png')))}finally{$g.Dispose();$bitmap.Dispose()}
}
function Open-Timer {
 Invoke 'Reveal Nagi';Invoke 'Open Nagi control center';Invoke 'Settings';Invoke 'SettingsCategoryTimerliveactivity' -Id
}
function Assert-CountdownFits {
 $box=(Find 'LiveTimerCountdown' -Id).Current.BoundingRectangle
 $window=[AdaptiveSmokeNative+Rect]::new();[AdaptiveSmokeNative]::GetWindowRect($handle,[ref]$window)|Out-Null
 $scale=[Math]::Max(96,[AdaptiveSmokeNative]::GetDpiForWindow($handle))/96.0
 if($Orientation -eq 'Horizontal'){
  if($window.Right-$window.Left -lt 3*($window.Bottom-$window.Top)){throw 'Top/bottom activity notch is not horizontal'}
  if(($window.Right-$window.Left)/$scale -gt 160 -or ($window.Bottom-$window.Top)/$scale -gt 36){throw 'One-minute activity notch has excess empty space'}
 }elseif(($window.Bottom-$window.Top) -le ($window.Right-$window.Left) -or ($window.Right-$window.Left)/$scale -gt 80 -or ($window.Bottom-$window.Top)/$scale -gt 140){throw 'Side activity notch is not compact and vertical'}
 $region=[AdaptiveSmokeNative]::CreateRectRgn(0,0,0,0)
 try {
  if([AdaptiveSmokeNative]::GetWindowRgn($handle,$region) -eq 0){throw 'Missing notch region'}
  foreach($x in @(($box.Left+1),($box.Right-1))){foreach($y in @(($box.Top+1),($box.Bottom-1))){
   if(![AdaptiveSmokeNative]::PtInRegion($region,[int]($x-$window.Left),[int]($y-$window.Top))){throw 'The notch curve clips countdown text'}
  }}
 } finally {[AdaptiveSmokeNative]::DeleteObject($region)|Out-Null}
}
try{
 [AdaptiveSmokeNative]::SetCursorPos(30,30)|Out-Null
 for($i=0;$i -lt 100;$i++){$script:root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))|Where-Object {$_.Current.Name -eq 'Nagi' -and $_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1;if($root){break};Start-Sleep -Milliseconds 100}
 if(!$root){throw 'No QA window'};$handle=[IntPtr]$root.Current.NativeWindowHandle
 Start-Sleep -Seconds 4
 $idleLog=Get-Content (Join-Path $qaDirectory 'logs/pulse.jsonl') -Raw
 if($idleLog -notmatch 'audio=2000ms; peak=False; gpu=False; mic=False'){throw 'Resting providers did not become idle'}
 if($TimerHover){
  Invoke 'Reveal Nagi'
  Start-Sleep -Milliseconds 1200
  [AdaptiveSmokeNative]::SetWindowPos($handle,[IntPtr](-1),0,0,0,0,0x13)|Out-Null
  $timer=Find 'CompactTimerButton' -Id;$before=$timer.Current.BoundingRectangle
  if($timer.Current.IsOffscreen){throw 'Enabled timer widget is missing before starting'}
  if(![AdaptiveSmokeNative]::SetCursorPos([int]($before.Left+$before.Width/2),[int]($before.Top+$before.Height/2))){throw 'Desktop pointer unavailable'}
  Start-Sleep -Seconds 2
  $startButton=Find 'PeekTimerStart' -Id
  if($startButton.Current.IsOffscreen -or (Find 'PeekTimerRemaining' -Id).Current.Name -ne '03:00'){throw 'Timer hover did not show the saved idle duration'}
  if((Find 'PeekTimerPause' -Id).Current.IsEnabled -or (Find 'PeekTimerCancel' -Id).Current.IsEnabled){throw 'Idle timer controls are enabled incorrectly'}
  $after=(Find 'CompactTimerButton' -Id).Current.BoundingRectangle
  if([Math]::Abs($before.Left-$after.Left) -gt 2 -or [Math]::Abs($before.Top-$after.Top) -gt 2){throw 'Opening the timer preview moved its dock button'}
  Snapshot 'idle-timer-hover'
  foreach($id in @('PeekTimerStart','PeekTimerPause','PeekTimerCancel')){
   $box=(Find $id -Id).Current.BoundingRectangle;$window=$root.Current.BoundingRectangle
   if($box.Width -le 0 -or $box.Left -lt $window.Left-1 -or $box.Right -gt $window.Right+1 -or $box.Top -lt $window.Top-1 -or $box.Bottom -gt $window.Bottom+1){throw ('Timer hover control is clipped: '+$id)}
  }
  $startBounds=$startButton.Current.BoundingRectangle
  [AdaptiveSmokeNative]::SetCursorPos([int]($startBounds.Left+$startBounds.Width/2),[int]($startBounds.Top+$startBounds.Height/2))|Out-Null
  Start-Sleep -Milliseconds 500
  Invoke 'PeekTimerStart' -Id;Start-Sleep -Milliseconds 1300
  if((Find 'PeekTimerRemaining' -Id).Current.Name -notmatch '^02:'){throw 'Hover start did not activate the three-minute timer'}
  Invoke 'PeekTimerPause' -Id
  $paused=(Find 'PeekTimerRemaining' -Id).Current.Name;Start-Sleep -Seconds 2
  if((Find 'PeekTimerRemaining' -Id).Current.Name -ne $paused -or (Find 'PeekTimerPause' -Id).Current.Name -ne 'Resume'){throw 'Hover timer did not pause'}
  Invoke 'PeekTimerPause' -Id;Start-Sleep -Milliseconds 1300
  if((Find 'PeekTimerRemaining' -Id).Current.Name -ge $paused){throw 'Hover timer did not resume'}
  Snapshot 'running-timer-hover'
  Invoke 'PeekTimerCancel' -Id
  if((Find 'PeekTimerRemaining' -Id).Current.Name -ne '03:00' -or (Find 'PeekTimerPause' -Id).Current.IsEnabled){throw 'Cancel did not restore the idle widget'}
  [AdaptiveSmokeNative]::SetCursorPos(30,30)|Out-Null;Start-Sleep -Milliseconds 1200
  if(Find 'Reveal Nagi' -Optional){Invoke 'Reveal Nagi'}
  Invoke 'CompactTimerButton' -Id;Find 'UtilityTimerRemaining' -Id|Out-Null
  Invoke 'Profiles';Invoke 'Customize Work'
  (Find 'Work widget Timer').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 500
  Invoke 'Collapse Nagi';Invoke 'Reveal Nagi'
  $hidden=Find 'CompactTimerButton' -Id -Optional
  if($hidden -and !$hidden.Current.IsOffscreen){throw 'Disabled idle timer widget remains visible'}
  $sampling=Get-Content (Join-Path $qaDirectory 'logs/pulse.jsonl') -Raw
  if($sampling -match 'peak=True'){throw 'The timer hover unnecessarily enabled audio peak sampling'}
  Write-Output ('PASS: idle timer widget, actual hover, saved minutes, compact controls, start/pause/resume/cancel, dock stability, hide option and no audio peaks. '+$qaDirectory);return
 }
 Invoke 'Reveal Nagi';Invoke 'Open Nagi control center'
 Invoke 'Activate Work';Invoke 'Overview';Start-Sleep -Milliseconds 600;Invoke 'Profiles'
 if($ProfileTimers){
  Invoke 'Customize Work'
  Set-ProfileMinutes '25'
  (Find 'ProfileTimerAutoStart' -Id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 600
  Snapshot 'work-profile-timer'
  Invoke '← All profiles';Invoke 'Customize Gaming'
  Set-ProfileMinutes '7'
  (Find 'ProfileTimerAutoStart' -Id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 600
  $saved=Get-Content $qaFile -Raw|ConvertFrom-Json
  $work=$saved.Workspaces|Where-Object Profile -eq 'Work';$gaming=$saved.Workspaces|Where-Object Profile -eq 'Gaming'
  if(!$work.Timer.AutoStart -or $work.Timer.Minutes -ne 25 -or !$gaming.Timer.AutoStart -or $gaming.Timer.Minutes -ne 7){throw 'Independent profile timers were not saved'}
  Invoke 'Activate Gaming';Invoke 'Collapse Nagi';Start-Sleep -Seconds 1
  if((Find 'LiveTimerCountdown' -Id).Current.Name -notmatch '^0[67]:'){throw 'Gaming did not start its seven-minute timer'}
  Invoke 'Reveal Nagi';Invoke 'Open Nagi control center';Invoke 'Profiles'
  if(Find '← All profiles' -Optional){Invoke '← All profiles'}
  Invoke 'Activate Work';Invoke 'Collapse Nagi'
  if((Find 'LiveTimerCountdown' -Id).Current.Name -notmatch '^0[67]:'){throw 'Switching profiles interrupted an active timer'}
  Open-Timer
  if((Find 'ProfileTimerMinutes' -Id -Optional) -or (Find 'TimerMinutes' -Id -Optional) -or (Find 'ProfileTimerAutoStart' -Id -Optional)){throw 'Profile duration/activation controls remain in global timer settings'}
  Invoke 'Cancel timer';Invoke 'Profiles'
  if(Find '← All profiles' -Optional){Invoke '← All profiles'}
  Invoke 'Activate Default';Invoke 'Activate Work';Invoke 'Collapse Nagi';Start-Sleep -Seconds 1
  if((Find 'LiveTimerCountdown' -Id).Current.Name -notmatch '^2[45]:'){throw 'Work did not start its twenty-five-minute timer'}
  Open-Timer;Invoke 'Cancel timer';Invoke 'Profiles'
  if(Find '← All profiles' -Optional){Invoke '← All profiles'}
  Invoke 'Customize Work'
  (Find 'ProfileTimerAutoStart' -Id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 600
  Invoke '← All profiles';Invoke 'Activate Default';Invoke 'Activate Work';Invoke 'Collapse Nagi'
  $disabled=Find 'LiveTimerCountdown' -Id -Optional
  if($disabled -and !$disabled.Current.IsOffscreen){throw 'Disabled profile timer still auto-started'}
  [AdaptiveSmokeNative]::PostMessage($handle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
  if(!$qaProcess.WaitForExit(10000)){throw 'Owned profile test did not close'}
  $qaProcess=[Diagnostics.Process]::Start($start);$root=$null
  for($i=0;$i -lt 100;$i++){$script:root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))|Where-Object {$_.Current.Name -eq 'Nagi' -and $_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1;if($root){break};Start-Sleep -Milliseconds 100}
  if(!$root){throw 'Profile timer restart did not open'};$handle=[IntPtr]$root.Current.NativeWindowHandle
  Start-Sleep -Seconds 2
  Invoke 'Reveal Nagi';Invoke 'Open Nagi control center';Invoke 'Profiles';Invoke 'Customize Work'
  if((Timer-Input).GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value -ne '25'){throw 'Profile minutes did not survive restart'}
  if((Find 'ProfileTimerAutoStart' -Id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [Windows.Automation.ToggleState]::Off){throw 'Profile auto-start did not survive restart'}
  Write-Output ('PASS: independent Work/Gaming minutes, activation, active-timer preservation, disabled auto-start, global controls removed and restart persistence. '+$qaDirectory);return
 }
 Invoke '+ Create profile'
 (Find 'NewProfileName' -Id).GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue('Design QA')
 Invoke 'Save';Find 'Activate Design QA'|Out-Null;Invoke 'Activate Design QA'
 $saved=Get-Content $qaFile -Raw|ConvertFrom-Json
 if($saved.CustomProfiles[0].Name -ne 'Design QA' -or $saved.LockedProfile -ne 1000){throw 'Custom profile did not save and activate'}
 Invoke '← All profiles';Invoke 'Activate Focus';Invoke 'Collapse Nagi'
 Start-Sleep -Seconds 2
 $live=Find 'LiveTimerCountdown' -Id
 if($live.Current.IsOffscreen -or $live.Current.Name -notmatch '^00:'){throw 'Focus did not start the live timer'}
 $beforeTick=$live.Current.Name;Start-Sleep -Milliseconds 1600
 $afterTick=(Find 'LiveTimerCountdown' -Id).Current.Name
 if($afterTick -ge $beforeTick){throw 'Resting timer did not count down without hovering'}
 Assert-CountdownFits;Snapshot 'live-timer'
 Invoke 'Reveal Nagi'
 $timerButton=Find 'CompactTimerButton' -Id
 $mediaButton=Find 'CompactMediaButton' -Id
 $volume=Find 'CompactAudioValue' -Id
 if($timerButton.Current.IsOffscreen -or $timerButton.Current.BoundingRectangle.Width -le 0){throw 'Separate dock timer is not visible'}
 if($mediaButton.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'CompactTimerButton'))){throw 'Timer remains inside the media/volume widget'}
 $timerBounds=$timerButton.Current.BoundingRectangle;$mediaBounds=$mediaButton.Current.BoundingRectangle;$volumeBounds=$volume.Current.BoundingRectangle
 $scale=[Math]::Max(96,[AdaptiveSmokeNative]::GetDpiForWindow($handle))/96.0
 if($Orientation -eq 'Horizontal'){
  if($timerBounds.Left -lt $mediaBounds.Right-1 -or [Math]::Abs(($timerBounds.Top+$timerBounds.Height/2)-($volumeBounds.Top+$volumeBounds.Height/2)) -gt 2*$scale){throw 'Timer and volume are overlapping or not on the same level'}
 }elseif($timerBounds.Top -lt $mediaBounds.Bottom-1){throw 'Side-dock timer overlaps the media widget'}
 Snapshot 'separate-timer-rail'
 Invoke 'CompactTimerButton' -Id;Find 'UtilityTimerRemaining' -Id|Out-Null
 Invoke 'Profiles';Invoke 'Customize Focus'
 foreach($widget in @('Media','Audio')){(Find ('Focus widget '+$widget)).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 400}
 Invoke 'Collapse Nagi';Invoke 'Reveal Nagi'
 if((Find 'CompactTimerButton' -Id).Current.IsOffscreen){throw 'Hiding media also hid the timer'}
 $hiddenMedia=Find 'CompactMediaButton' -Id -Optional
 if($hiddenMedia -and !$hiddenMedia.Current.IsOffscreen){throw 'A timer-only profile forced the media/volume widget to appear'}
 Snapshot 'timer-only-rail';Invoke 'Open Nagi control center';Invoke 'Collapse Nagi'
 Open-Timer
 Invoke 'Pause';$paused=(Find 'TimerRemaining' -Id).Current.Name;Start-Sleep -Seconds 2
 if((Find 'TimerRemaining' -Id).Current.Name -ne $paused){throw 'Paused timer continued counting'}
 Invoke 'Resume'
 (Find 'Show timer before hovering').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 600
 Invoke 'Collapse Nagi'
 $hiddenTimer=Find 'LiveTimerCountdown' -Id -Optional
 if($hiddenTimer -and !$hiddenTimer.Current.IsOffscreen){throw 'Disabled live timer remains visible'}
 if($Quick){
  Open-Timer;Invoke 'Cancel timer'
  Write-Output ('PASS: edge-oriented live content, native text fit, animated reveal, custom activation and timer controls. '+$qaDirectory);return
 }
 $completed=$false
 for($i=0;$i -lt 270;$i++){
  Start-Sleep -Milliseconds 250
  $check=Find 'TimerCompleteCheck' -Id -Optional
  if($check -and !$check.Current.IsOffscreen){$completed=$true;break}
 }
 if(!$completed){throw 'Hidden timer did not show its completion check'}
 $zero=Find 'LiveTimerCountdown' -Id
 if($zero.Current.IsOffscreen -or $zero.Current.Name -ne '00:00'){throw 'Completion did not show 00:00 beside the check'}
 Assert-CountdownFits;Snapshot 'timer-complete';Start-Sleep -Seconds 6
 $dismissedCheck=Find 'TimerCompleteCheck' -Id -Optional
 if($dismissedCheck -and !$dismissedCheck.Current.IsOffscreen){throw 'Completion check did not dismiss'}
 $runtime=Get-Content (Join-Path $qaDirectory 'logs/pulse.jsonl') -Raw
 if($runtime -notmatch 'audio=50ms; peak=True'){throw 'Opening Overview did not wake audio peak sampling'}
 if($runtime -notmatch 'Countdown complete'){throw 'Timer completion was not recorded'}
 Write-Output ('PASS: resting demand, visible peak wake, custom creation/activation, Focus auto-start, live timer, pause/resume, disabled live display, green completion and dismissal. '+$qaDirectory)
}catch{
 try {
  $failurePointer=[AdaptiveSmokeNative+Point]::new();[AdaptiveSmokeNative]::GetCursorPos([ref]$failurePointer)|Out-Null
  @{X=$failurePointer.X;Y=$failurePointer.Y;Window=$root.Current.BoundingRectangle}|ConvertTo-Json -Depth 3|Set-Content -LiteralPath (Join-Path $qaDirectory 'failure-pointer.json') -Encoding utf8
  Snapshot 'failure-state'
  $root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)|ForEach-Object {[pscustomobject]@{Id=$_.Current.AutomationId;Name=$_.Current.Name;Offscreen=$_.Current.IsOffscreen}}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $qaDirectory 'failure-tree.json') -Encoding utf8
 }catch{}
 Write-Output $_.ScriptStackTrace;throw
}finally{
 [AdaptiveSmokeNative]::SetCursorPos($qaPointer.X,$qaPointer.Y)|Out-Null
 if(!$qaProcess.HasExited){[AdaptiveSmokeNative]::PostMessage($handle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;$qaProcess.WaitForExit(10000)|Out-Null}
}
