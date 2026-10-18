param([Parameter(Mandatory)][string]$ExecutablePath,[string]$AudioPath='',[string]$VideoPath='', [ValidateSet('Horizontal','Vertical')][string]$Orientation='Horizontal')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class NagiPolishNative {
 public delegate bool EnumCallback(IntPtr h,IntPtr p);
 [DllImport("user32.dll")]public static extern bool EnumWindows(EnumCallback callback,IntPtr p);
 [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern int GetClassName(IntPtr h,System.Text.StringBuilder s,int count);
 [DllImport("user32.dll")]public static extern IntPtr GetDlgItem(IntPtr h,int id);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern bool SetDlgItemText(IntPtr h,int id,string text);
 public static IntPtr FindDialog(uint pid){IntPtr result=IntPtr.Zero;EnumWindows((h,p)=>{GetWindowThreadProcessId(h,out var owner);if(owner==pid && IsWindowVisible(h)){var text=new System.Text.StringBuilder(100);GetClassName(h,text,100);if(text.ToString()=="#32770"){result=h;return false;}}return true;},IntPtr.Zero);return result;}
 [StructLayout(LayoutKind.Sequential)]public struct Rect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)]public struct Point {public int X,Y;}
 [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]public static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")]public static extern IntPtr GetWindow(IntPtr h,uint command);
 [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
}
'@
$savedPointer=[NagiPolishNative+Point]::new();[NagiPolishNative]::GetCursorPos([ref]$savedPointer)|Out-Null
$qaDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/polish-'+(Get-Date -Format yyyyMMdd-HHmmss))
[IO.Directory]::CreateDirectory($qaDirectory)|Out-Null
$qaFile=Join-Path $qaDirectory 'settings.json'
@{Language='en';WelcomeCompleted=$true;Theme='Akeno';NotchColor='#3366AA';NotchColorMode='Theme';LockedProfile='Work';CloseOnDeactivate=$false;HideInFullscreen=$false;AutomaticUpdates=$false;IndexFiles=$false;FpsEnabled=$false;ObsEnabled=$false;WeatherEnabled=$false;ReducedMotion=$false;
 Position=@{Orientation=$Orientation;Anchor=$(if($Orientation -eq 'Horizontal'){'Bottom edge'}else{'Right edge'})};Hotkey=@{Modifiers=15;Key=131};HideHotkey=@{Modifiers=15;Key=132};SearchHotkey=@{Modifiers=15;Key=133}} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $qaFile -Encoding utf8
$start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($ExecutablePath));$start.UseShellExecute=$false
$start.ArgumentList.Add('--data-dir');$start.ArgumentList.Add($qaDirectory)
$qaProcess=[Diagnostics.Process]::Start($start);$root=$null;$handle=[IntPtr]::Zero
function Find([string]$value,[switch]$Id,[switch]$Optional){
 $property=if($Id){[Windows.Automation.AutomationElement]::AutomationIdProperty}else{[Windows.Automation.AutomationElement]::NameProperty}
 $item=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($property,$value))
 if(!$item -and !$Optional){throw "Missing $value"};return $item
}
function Invoke([string]$value,[switch]$Id){(Find $value -Id:$Id).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 400}
function Select-Choice([string]$id,[string]$value){
 $box=Find $id -Id;$box.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 150
 $item=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$value),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true)))
 if(!$item){throw "Missing choice $value"};$item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 400
}
function Snapshot([string]$name,[Windows.Automation.AutomationElement]$window=$root){
 $r=[NagiPolishNative+Rect]::new();[NagiPolishNative]::GetWindowRect([IntPtr]$window.Current.NativeWindowHandle,[ref]$r)|Out-Null
 $image=[Drawing.Bitmap]::new($r.Right-$r.Left,$r.Bottom-$r.Top);$g=[Drawing.Graphics]::FromImage($image);$dc=$g.GetHdc()
 try{[NagiPolishNative]::PrintWindow([IntPtr]$window.Current.NativeWindowHandle,$dc,2)|Out-Null}finally{$g.ReleaseHdc($dc)}
 try{$image.Save((Join-Path $qaDirectory ($name+'.png')))}finally{$g.Dispose();$image.Dispose()}
}
function Open-LocalFile([string]$path){
 [NagiPolishNative]::SetForegroundWindow($handle)|Out-Null;Start-Sleep -Milliseconds 200
 Invoke 'Settings';Invoke 'Music settings';Invoke 'OpenLocalMediaSettings' -Id
 $dialog=$null
 for($retry=0;$retry -lt 600 -and !$dialog;$retry++){
  $dialogHandle=[NagiPolishNative]::FindDialog($qaProcess.Id);if($dialogHandle -ne [IntPtr]::Zero){$dialog=[Windows.Automation.AutomationElement]::FromHandle($dialogHandle);break}
  $windows=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.Condition]::TrueCondition)
  foreach($window in $windows){
   $owner=[NagiPolishNative]::GetWindow([IntPtr]$window.Current.NativeWindowHandle,4)
   $belongs=$window.Current.ProcessId -eq $qaProcess.Id -and $window.Current.NativeWindowHandle -ne $handle
   for($depth=0;$depth -lt 12 -and $owner -ne [IntPtr]::Zero;$depth++){
    if($owner -eq $handle){$belongs=$true;break};$owner=[NagiPolishNative]::GetWindow($owner,4)
   }
   if($belongs -and $window.Current.NativeWindowHandle -ne $handle){$dialog=$window;break}
  };if(!$dialog){Start-Sleep -Milliseconds 100}
 }
 if(!$dialog){
  $debug=$windows|ForEach-Object {[pscustomobject]@{Class=$_.Current.ClassName;Pid=$_.Current.ProcessId;Handle=$_.Current.NativeWindowHandle;Owner=[NagiPolishNative]::GetWindow([IntPtr]$_.Current.NativeWindowHandle,4).ToInt64()}}
  $debug|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $qaDirectory 'picker-debug.json') -Encoding utf8
  Snapshot 'picker-state'; $root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)|ForEach-Object {[pscustomobject]@{Id=$_.Current.AutomationId;Name=$_.Current.Name;Class=$_.Current.ClassName;Pid=$_.Current.ProcessId}}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $qaDirectory 'picker-tree.json') -Encoding utf8
  throw 'Owned media file picker did not appear'
 }
 $dialogHandle=[IntPtr]$dialog.Current.NativeWindowHandle
 [NagiPolishNative]::SetForegroundWindow($dialogHandle)|Out-Null
 Start-Sleep -Milliseconds 300
 if([NagiPolishNative]::GetForegroundWindow() -ne $dialogHandle){throw 'Owned picker did not receive focus'}
 [Windows.Forms.SendKeys]::SendWait('%n')
 [Windows.Forms.SendKeys]::SendWait('^a')
 $typed=[IO.Path]::GetFullPath($path)
 if($typed -match '[+^%~(){}\[\]]'){throw 'Fixture path contains SendKeys metacharacters'}
 [Windows.Forms.SendKeys]::SendWait($typed)
 [Windows.Forms.SendKeys]::SendWait('{ENTER}')
 $player=$null;$status=$null
 for($retry=0;$retry -lt 200;$retry++){
  $windows=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))
  foreach($window in $windows){$value=$window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'LocalPlaybackStatus'));if($value){$player=$window;$status=$value;break}}
  if($status -and $status.Current.Name -like 'Playing*' -and $status.Current.Name.Contains([IO.Path]::GetFileNameWithoutExtension($path))){break}
  if($status -and $status.Current.Name -like 'Windows could not*'){throw $status.Current.Name}
  Start-Sleep -Milliseconds 100
 }
 if(!$status -or $status.Current.Name -notlike 'Playing*' -or !$status.Current.Name.Contains([IO.Path]::GetFileNameWithoutExtension($path))){throw 'Selected local media did not start playback'}
 Start-Sleep -Milliseconds 1300
 if($status.Current.Name -notlike 'Playing*'){throw 'Selected media did not remain playing'}
 return $player
}
try{
 for($retry=0;$retry -lt 100 -and !$root;$retry++){
  $root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))|Where-Object {$_.Current.Name -eq 'Nagi' -and $_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1
  if(!$root){Start-Sleep -Milliseconds 100}
 }
 if(!$root){throw 'No test window'};$handle=[IntPtr]$root.Current.NativeWindowHandle
 [NagiPolishNative]::SetCursorPos(20,20)|Out-Null
 Invoke 'Reveal Nagi';Start-Sleep -Milliseconds 800
 $r=[NagiPolishNative+Rect]::new();[NagiPolishNative]::GetWindowRect($handle,[ref]$r)|Out-Null;$scale=[NagiPolishNative]::GetDpiForWindow($handle)/96d
 if($Orientation -eq 'Horizontal'){if(($r.Right-$r.Left)/$scale -gt 700 -or [Math]::Abs(($r.Bottom-$r.Top)/$scale-56) -gt 2){throw 'Compact horizontal dimensions are wrong'}}else{if([Math]::Abs(($r.Right-$r.Left)/$scale-64) -gt 2){throw 'Compact vertical dimensions are wrong'}}
 $scroll=Find 'RailViewport' -Id
 $scrollPattern=$null
 if($scroll.TryGetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern,[ref]$scrollPattern)){
  if($scrollPattern.Current.HorizontallyScrollable -or $scrollPattern.Current.VerticallyScrollable){throw 'The dock still scrolls'}
 }
 foreach($id in @('CompactBrand','RailSearchButton','AppFolderButton','CompactCpuButton','RailGpuButton','CompactMediaButton')){
  $control=Find $id -Id;$bounds=$control.Current.BoundingRectangle
  if($control.Current.IsOffscreen -or $bounds.Left -lt $r.Left-1 -or $bounds.Right -gt $r.Right+1 -or $bounds.Bottom -gt $r.Bottom+1){throw "Clipped widget: $id"}
 }
 foreach($app in @('VS Code','Terminal','Browser')){if(Find ('Dock app '+$app) -Optional){throw 'More than two apps were not grouped'}}
 foreach($id in @('RailMicrophoneButton','CompactBatteryButton')){
  $control=Find $id -Id -Optional
  if($control){$bounds=$control.Current.BoundingRectangle;if($control.Current.IsOffscreen -or $bounds.Bottom -gt $r.Bottom+1 -or $bounds.Right -gt $r.Right+1){throw "Clipped optional widget: $id"}}
 }
 Snapshot 'compact-no-scroll'
 Invoke 'Open Nagi control center';Invoke 'Settings';Invoke 'Appearance & dock settings'
 Select-Choice 'NotchColorMode' 'System';Select-Choice 'NotchColorMode' 'Custom'
 if((Get-Content -LiteralPath $qaFile -Raw|ConvertFrom-Json).NotchColorMode -ne 'Custom'){throw 'Notch color mode was not saved'}
 $before=[NagiPolishNative+Rect]::new();[NagiPolishNative]::GetWindowRect($handle,[ref]$before)|Out-Null
 [NagiPolishNative]::SetWindowPos($handle,[IntPtr]::Zero,$before.Left,$before.Top,($before.Right-$before.Left-120),($before.Bottom-$before.Top-80),0x14)|Out-Null
 Start-Sleep -Milliseconds 500
 Invoke 'Settings';Invoke 'Music settings'
 $after=[NagiPolishNative+Rect]::new();[NagiPolishNative]::GetWindowRect($handle,[ref]$after)|Out-Null
 if($after.Right-$after.Left -ne $before.Right-$before.Left-120){throw 'Settings resize was reset by navigation'}
 Snapshot 'resized-settings'
 if($AudioPath){
  $player=Open-LocalFile $AudioPath;Snapshot 'local-audio' $player
  Invoke 'Overview';Start-Sleep -Milliseconds 1700
  Invoke 'PlayButton' -Id;Start-Sleep -Milliseconds 500
  $status=$player.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'LocalPlaybackStatus'))
  if($status.Current.Name -notlike 'Paused*'){throw 'Dock did not pause local audio'}
  Invoke 'PlayButton' -Id
  Invoke 'Collapse Nagi';Invoke 'Reveal Nagi';Start-Sleep -Milliseconds 500
  $title=(Find 'CompactTrackTitle' -Id).Current.Name
  if($title.Length -gt 25 -or $title -notlike '*…'){throw "Long title was not shortened: $title"}
  Snapshot 'compact-local-audio';Invoke 'Open Nagi control center'
 }
 if($VideoPath){$player=Open-LocalFile $VideoPath;Snapshot 'local-video' $player}
 if((Get-Content -LiteralPath (Join-Path $qaDirectory 'logs/pulse.jsonl') -Raw) -match '"category":"fatal"'){throw 'A fatal UI exception was logged'}
 @{DockFits=$true;DockScrolls=$false;AppsGrouped=$true;NotchColorSaved=$true;SettingsResizeRetained=$true;LocalAudio=[bool]$AudioPath;LocalVideo=[bool]$VideoPath}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $qaDirectory 'evidence.json') -Encoding utf8
 Write-Output "PASS: compact fit, no scrolling, folder grouping, notch color and resizing; local audio/video when provided. $qaDirectory"
}catch{Write-Output $_.ScriptStackTrace;throw}finally{
 [NagiPolishNative]::SetCursorPos($savedPointer.X,$savedPointer.Y)|Out-Null
 if(!$qaProcess.HasExited){if($handle -ne [IntPtr]::Zero){[NagiPolishNative]::PostMessage($handle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null};if(!$qaProcess.WaitForExit(5000)){Stop-Process -Id $qaProcess.Id -Force}}
}

if((Get-Content -LiteralPath (Join-Path $qaDirectory 'logs/pulse.jsonl') -Raw) -match '"category":"fatal"'){throw 'Fatal exception during shutdown'}
