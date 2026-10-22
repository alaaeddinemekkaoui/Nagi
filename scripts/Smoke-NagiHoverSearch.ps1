param([Parameter(Mandatory)][string]$ExecutablePath,[string]$AudioPath='',[string]$VideoPath='', [ValidateSet('Horizontal','Vertical')][string]$Orientation='Horizontal',[ValidateSet('Top edge','Bottom edge','Left edge','Right edge')][string]$Anchor='Bottom edge')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class NagiHoverNative {
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
 [DllImport("user32.dll")]public static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
 [DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]public static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")]public static extern IntPtr GetWindow(IntPtr h,uint command);
 [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
}
'@
$savedPointer=[NagiHoverNative+Point]::new();[NagiHoverNative]::GetCursorPos([ref]$savedPointer)|Out-Null
$qaDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/hover-search-'+(Get-Date -Format yyyyMMdd-HHmmss))
[IO.Directory]::CreateDirectory($qaDirectory)|Out-Null
$qaFile=Join-Path $qaDirectory 'settings.json'
@{Language='en';WelcomeCompleted=$true;Theme='Akeno';NotchColor='#3366AA';NotchColorMode='Theme';LockedProfile='Work';CloseOnDeactivate=$false;HideInFullscreen=$false;AutomaticUpdates=$false;IndexFiles=$false;FpsEnabled=$false;ObsEnabled=$false;WeatherEnabled=$true;WeatherLocation=@{Name='Casablanca';Country='Morocco';Region='';Latitude=33.59;Longitude=-7.61};ReducedMotion=$false;LiveMedia=$false;
 Position=@{Orientation=$Orientation;Anchor=$(if($PSBoundParameters.ContainsKey('Anchor')){$Anchor}elseif($Orientation -eq 'Horizontal'){'Bottom edge'}else{'Right edge'})};Hotkey=@{Modifiers=15;Key=131};HideHotkey=@{Modifiers=15;Key=132};SearchHotkey=@{Modifiers=15;Key=133}} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $qaFile -Encoding utf8
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
 $box=Find $id -Id -Optional
 if(!$box){$box=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$id),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ComboBox)))}
 if(!$box){throw "Missing combo $id"};$box.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 150
 $item=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$value),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true)))
 if(!$item){throw "Missing choice $value"};$item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 400
}
function Snapshot([string]$name,[Windows.Automation.AutomationElement]$window=$root){
 $r=[NagiHoverNative+Rect]::new();[NagiHoverNative]::GetWindowRect([IntPtr]$window.Current.NativeWindowHandle,[ref]$r)|Out-Null
 $image=[Drawing.Bitmap]::new($r.Right-$r.Left,$r.Bottom-$r.Top);$g=[Drawing.Graphics]::FromImage($image);$dc=$g.GetHdc()
 try{[NagiHoverNative]::PrintWindow([IntPtr]$window.Current.NativeWindowHandle,$dc,2)|Out-Null}finally{$g.ReleaseHdc($dc)}
 try{$image.Save((Join-Path $qaDirectory ($name+'.png')))}finally{$g.Dispose();$image.Dispose()}
}
function Open-LocalFile([string]$path){
 [NagiHoverNative]::SetForegroundWindow($handle)|Out-Null;Start-Sleep -Milliseconds 200
 Invoke 'Settings';Invoke 'Music settings';Invoke 'OpenLocalMediaSettings' -Id
 $dialog=$null
 for($retry=0;$retry -lt 600 -and !$dialog;$retry++){
  $dialogHandle=[NagiHoverNative]::FindDialog($qaProcess.Id);if($dialogHandle -ne [IntPtr]::Zero){$dialog=[Windows.Automation.AutomationElement]::FromHandle($dialogHandle);break}
  $windows=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.Condition]::TrueCondition)
  foreach($window in $windows){
   $owner=[NagiHoverNative]::GetWindow([IntPtr]$window.Current.NativeWindowHandle,4)
   $belongs=$window.Current.ProcessId -eq $qaProcess.Id -and $window.Current.NativeWindowHandle -ne $handle
   for($depth=0;$depth -lt 12 -and $owner -ne [IntPtr]::Zero;$depth++){
    if($owner -eq $handle){$belongs=$true;break};$owner=[NagiHoverNative]::GetWindow($owner,4)
   }
   if($belongs -and $window.Current.NativeWindowHandle -ne $handle){$dialog=$window;break}
  };if(!$dialog){Start-Sleep -Milliseconds 100}
 }
 if(!$dialog){
  $debug=$windows|ForEach-Object {[pscustomobject]@{Class=$_.Current.ClassName;Pid=$_.Current.ProcessId;Handle=$_.Current.NativeWindowHandle;Owner=[NagiHoverNative]::GetWindow([IntPtr]$_.Current.NativeWindowHandle,4).ToInt64()}}
  $debug|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $qaDirectory 'picker-debug.json') -Encoding utf8
  Snapshot 'picker-state'; $root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)|ForEach-Object {[pscustomobject]@{Id=$_.Current.AutomationId;Name=$_.Current.Name;Class=$_.Current.ClassName;Pid=$_.Current.ProcessId}}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $qaDirectory 'picker-tree.json') -Encoding utf8
  throw 'Owned media file picker did not appear'
 }
 $dialogHandle=[IntPtr]$dialog.Current.NativeWindowHandle
 [NagiHoverNative]::SetForegroundWindow($dialogHandle)|Out-Null
 Start-Sleep -Milliseconds 300
 if([NagiHoverNative]::GetForegroundWindow() -ne $dialogHandle){throw 'Owned picker did not receive focus'}
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
function Reveal-Dock {
 $drop=Find 'Reveal Nagi';$box=$drop.Current.BoundingRectangle
 [NagiHoverNative]::SetCursorPos([int]($box.Left+$box.Width/2),[int]($box.Top+$box.Height/2))|Out-Null
 Invoke 'Reveal Nagi'
}
function Point-To([string]$id){
 $box=(Find $id -Id).Current.BoundingRectangle
 [NagiHoverNative]::SetCursorPos([int]($box.Left+$box.Width/2),[int]($box.Top+$box.Height/2))|Out-Null
}
function Trace([string]$message){$p=[NagiHoverNative+Point]::new();[NagiHoverNative]::GetCursorPos([ref]$p)|Out-Null;Add-Content (Join-Path $qaDirectory 'trace.txt') ($message+' pointer='+$p.X+','+$p.Y+' window='+$root.Current.BoundingRectangle)}
function Assert-PreviewStable {
 Point-To 'CompactMediaButton';Trace 'media-enter';$expected=[NagiHoverNative+Point]::new();[NagiHoverNative]::GetCursorPos([ref]$expected)|Out-Null;$interruptions=0;Start-Sleep -Milliseconds 1200
 for($i=0;$i -lt 20;$i++){
  $actual=[NagiHoverNative+Point]::new();[NagiHoverNative]::GetCursorPos([ref]$actual)|Out-Null
  if($actual.X -ne $expected.X -or $actual.Y -ne $expected.Y){if(++$interruptions -gt 4){throw 'Desktop pointer repeatedly moved during hover verification'};Point-To 'RailSearchButton';Start-Sleep -Milliseconds 300;Point-To 'CompactMediaButton';[NagiHoverNative]::GetCursorPos([ref]$expected)|Out-Null;Start-Sleep -Milliseconds 1200;$i=-1;continue}
  Trace ('media sample '+$i);$preview=Find 'PeekHost' -Id
  if($preview.Current.IsOffscreen -or $preview.Current.BoundingRectangle.Height -lt 40){throw ('Media hover disappeared at sample '+$i)}
  if($AudioPath -and [string]::IsNullOrWhiteSpace((Find 'CompactTrackTitle' -Id).Current.Name)){throw 'Playing media title went blank'}
  Start-Sleep -Milliseconds 100
 }
}
try{
 [NagiHoverNative]::SetCursorPos(30,30)|Out-Null
 for($i=0;$i -lt 100;$i++){
  $root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))|Where-Object {$_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1
  if($root){break};Start-Sleep -Milliseconds 100
 };if(!$root){throw 'No QA window'};$handle=[IntPtr]$root.Current.NativeWindowHandle
 Start-Sleep -Seconds 2
 if(!(Find 'Reveal Nagi' -Optional)){Invoke 'Collapse Nagi'}
 Reveal-Dock;Invoke 'Open Nagi control center'
 if($AudioPath){$player=Open-LocalFile $AudioPath}
 Invoke 'Collapse Nagi'
 for($round=0;$round -lt 4;$round++){
  Trace ('round '+$round)
  Reveal-Dock;Start-Sleep -Milliseconds 300
  [NagiHoverNative]::SetWindowPos($handle,[IntPtr](-1),0,0,0,0,0x13)|Out-Null
  Point-To 'RailSearchButton';Start-Sleep -Milliseconds 300
  Assert-PreviewStable
  Point-To 'RailSearchButton';Start-Sleep -Milliseconds 400
  Assert-PreviewStable
  Point-To 'RailSearchButton'
  Invoke 'Quick search'
  $search=$null
  for($i=0;$i -lt 30;$i++){
   $windows=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))
   foreach($window in $windows){if($window.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'QuickSearchQuery'))){$search=$window;break}}
   if($search){break};Start-Sleep -Milliseconds 100
  };if(!$search){throw 'Search did not open'}
  [Windows.Forms.SendKeys]::SendWait('{ESC}')
  Start-Sleep -Milliseconds 1300
  $bounds=$root.Current.BoundingRectangle
  $scale=[Math]::Max(96,[NagiHoverNative]::GetDpiForWindow($handle))/96.0
  if($bounds.Height/$scale -gt 40 -or $bounds.Width/$scale -gt 150){throw ('Search Escape left stale dock bounds: '+$bounds)}
  $preview=Find 'PeekHost' -Id -Optional
  if($preview -and !$preview.Current.IsOffscreen){throw 'Search Escape left media preview visible'}
  [NagiHoverNative]::SetCursorPos(30,30)|Out-Null;Start-Sleep -Milliseconds 300
 }
 Reveal-Dock;Invoke 'Open Nagi control center';Invoke 'Settings';Invoke 'SettingsCategoryBehavior' -Id
 (Find 'AlwaysShowMiniBar' -Id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 600
 if(!(Get-Content $qaFile -Raw|ConvertFrom-Json).AlwaysShowMiniBar){throw 'Always show mini bar was not persisted'}
 Invoke 'Collapse Nagi';[NagiHoverNative]::SetCursorPos(30,30)|Out-Null;Start-Sleep -Milliseconds 1300
 if((Find 'RailSearchButton' -Id).Current.IsOffscreen){throw 'Persistent mini bar collapsed after pointer departure'}
 Assert-PreviewStable
 [NagiHoverNative]::SetCursorPos(30,30)|Out-Null;Start-Sleep -Milliseconds 1300
 if((Find 'RailSearchButton' -Id).Current.IsOffscreen -or (Find 'PeekHost' -Id -Optional)){throw 'Persistent mode did not close only the hover preview'}
 Point-To 'CompactBrand';$before=$root.Current.BoundingRectangle
 [NagiHoverNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
 Start-Sleep -Milliseconds 150
 [NagiHoverNative]::SetCursorPos([int]($before.Left+150),[int]($before.Top-100))|Out-Null;Start-Sleep -Milliseconds 500
 [NagiHoverNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 700
 if(Find 'Collapse Nagi' -Optional){Invoke 'Collapse Nagi';Start-Sleep -Milliseconds 800}
 $after=$root.Current.BoundingRectangle
 if([Math]::Abs($before.Left-$after.Left) -gt 2 -or [Math]::Abs($before.Top-$after.Top) -gt 2){throw 'Dragging the logo moved the dock'}
 $dockWeather=(Find 'RailWeatherValue' -Id).Current.Name
 Invoke 'Open Nagi control center';Invoke 'Utilities'
 if((Find 'UtilityWeatherCity' -Id).Current.Name -notlike '*Casablanca*' -or (Find 'UtilityWeatherValue' -Id).Current.Name -notlike ('*'+$dockWeather+'*')){throw 'Utilities and dock weather values differ'}
 (Find 'UtilityWeatherWidget' -Id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 500
 $saved=Get-Content $qaFile -Raw|ConvertFrom-Json
 if(($saved.Workspaces|Where-Object Profile -eq 'Work').Widgets -contains 'Weather'){throw 'Utilities weather switch did not update profile visibility'}
 Invoke 'Collapse Nagi';[NagiHoverNative]::SetCursorPos(30,30)|Out-Null;Start-Sleep -Milliseconds 800
 if(Find 'RailWeatherButton' -Id -Optional){throw 'Weather hidden in Utilities remains in the dock'}
 Snapshot 'persistent-mini-bar'
 @{HoverReentries=8;StationarySearchEscapes=4;PlayingLocalMedia=[bool]$AudioPath;PersistentMiniBar=$true;ClickPins=$false;LogoDragging=$false;WeatherProfileLinked=$true}|ConvertTo-Json|Set-Content (Join-Path $qaDirectory 'evidence.json')
 Write-Output ('PASS: repeated media hover, stationary-pointer empty search + Escape, persistent mini bar, removed logo dragging, and shared Utilities/dock weather visibility. '+$qaDirectory)
}catch{
 Add-Content (Join-Path $qaDirectory 'trace.txt') $_.ScriptStackTrace
 if($root){try{Snapshot 'failure'}catch{};$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)|ForEach-Object {[pscustomobject]@{Id=$_.Current.AutomationId;Name=$_.Current.Name;Offscreen=$_.Current.IsOffscreen}}|ConvertTo-Json|Set-Content (Join-Path $qaDirectory 'failure-tree.json')}
 throw
}finally{
 if($qaProcess -and !$qaProcess.HasExited){
  $windows=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))
  foreach($window in $windows){[NagiHoverNative]::PostMessage([IntPtr]$window.Current.NativeWindowHandle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null}
  if(!$qaProcess.WaitForExit(5000)){Stop-Process -Id $qaProcess.Id -Force}
 }
 [NagiHoverNative]::SetCursorPos($savedPointer.X,$savedPointer.Y)|Out-Null
}
