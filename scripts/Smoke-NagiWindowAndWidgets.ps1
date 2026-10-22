param([Parameter(Mandatory)][string]$ExecutablePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,System.Drawing
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class NagiWindowWidgetNative {
 [StructLayout(LayoutKind.Sequential)]public struct Rect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)]public struct Point {public int X,Y;}
 [DllImport("user32.dll")]public static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]public static extern bool GetClientRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]public static extern bool ClientToScreen(IntPtr h,ref Point p);
 [DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]public static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")]public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]public static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")]public static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")]public static extern bool IsZoomed(IntPtr h);
 [DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
}
'@
$review=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/window-widgets-'+(Get-Date -Format yyyyMMdd-HHmmss))
[IO.Directory]::CreateDirectory($review)|Out-Null
$pointer=[NagiWindowWidgetNative+Point]::new();[NagiWindowWidgetNative]::GetCursorPos([ref]$pointer)|Out-Null
$settingsFile=Join-Path $review 'settings.json'
@{WelcomeCompleted=$true;Language='en';Theme='Light';NotchColorMode='Custom';NotchColor='#2A86D8';LockedProfile='Stream';CloseOnDeactivate=$true;HideInFullscreen=$false;AutomaticUpdates=$false;IndexFiles=$false;ObsEnabled=$false;ReducedMotion=$false;
 WeatherEnabled=$true;WeatherLocation=@{Name='Paris';Country='France';Region='';Latitude=48.85;Longitude=2.35};
 Position=@{Orientation='Horizontal';Anchor='Bottom edge'};Hotkey=@{Modifiers=15;Key=131};HideHotkey=@{Modifiers=15;Key=132};SearchHotkey=@{Modifiers=15;Key=133}}|ConvertTo-Json -Depth 8|Set-Content $settingsFile
$start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($ExecutablePath));$start.UseShellExecute=$false;$start.ArgumentList.Add('--data-dir');$start.ArgumentList.Add($review)
$process=[Diagnostics.Process]::Start($start)
function Find([string]$value,[switch]$Name,[switch]$Optional){
 $property=if($Name){[Windows.Automation.AutomationElement]::NameProperty}else{[Windows.Automation.AutomationElement]::AutomationIdProperty}
 $item=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($property,$value));if(!$item -and !$Optional){throw "Missing $value"};return $item
}
function Invoke([string]$value,[switch]$Name){(Find $value -Name:$Name).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 650}
function Capture([string]$name){
 $r=[NagiWindowWidgetNative+Rect]::new();[NagiWindowWidgetNative]::GetWindowRect($handle,[ref]$r)|Out-Null
 $b=[Drawing.Bitmap]::new($r.Right-$r.Left,$r.Bottom-$r.Top);$g=[Drawing.Graphics]::FromImage($b);$dc=$g.GetHdc()
 try{[NagiWindowWidgetNative]::PrintWindow($handle,$dc,2)|Out-Null}finally{$g.ReleaseHdc($dc)}
 try{
  $b.Save((Join-Path $review ($name+'.png')))
  if($name -eq 'settings-window'){
   $g.CopyFromScreen($r.Left,$r.Top,0,0,$b.Size)
   $b.Save((Join-Path $review 'settings-window-desktop.png'))
  }
 }finally{$g.Dispose();$b.Dispose()}
}
function Hover([string]$id){$b=(Find $id).Current.BoundingRectangle;[NagiWindowWidgetNative]::SetCursorPos([int]($b.Left+$b.Width/2),[int]($b.Top+$b.Height/2))|Out-Null;Start-Sleep -Milliseconds 1200}
function Assert-MediaFits {
 $bounds=$root.Current.BoundingRectangle
 foreach($id in @('PeekVolumeSlider','PeekMuteButton','PeekMicrophoneButton','PreviewMoreButton')){
  $element=Find $id;$box=$element.Current.BoundingRectangle
  if($element.Current.IsOffscreen -or $box.Width -le 0 -or $box.Height -le 0 -or $box.Left -lt $bounds.Left -or $box.Right -gt $bounds.Right -or $box.Top -lt $bounds.Top -or $box.Bottom -gt $bounds.Bottom){throw "Clipped media control: $id"}
 }
 $scrolls=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsScrollPatternAvailableProperty,$true))
 foreach($scroll in $scrolls){if(!$scroll.Current.IsOffscreen){$state=$scroll.GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern).Current;if($state.VerticallyScrollable -or $state.HorizontallyScrollable){throw 'Media hover requires scrolling'}}}
 $playback=Find 'PeekPlaybackState' -Optional;$waveform=Find 'PeekMusicVisualizer' -Optional
 if($playback -and $playback.Current.Name -ne 'Playing' -and $waveform -and !$waveform.Current.IsOffscreen){throw 'Inactive media still shows a waveform'}
}
function Choose-Obs([string]$label){
 $box=Find 'ObsDockPresentation';$box.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 150
 $choice=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$label),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true)))
 if(!$choice){throw "Missing OBS mode $label"};$choice.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 650
}
try{
 for($i=0;$i -lt 100 -and !$root;$i++){
  $root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id))|Where-Object {$_.Current.Name -eq 'Nagi' -and $_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1
  if(!$root){Start-Sleep -Milliseconds 100}
 }
 if(!$root){throw 'No review window'};$handle=[IntPtr]$root.Current.NativeWindowHandle
 [NagiWindowWidgetNative]::SetCursorPos(10,10)|Out-Null
 Invoke 'EdgeDropButton'
 foreach($id in @('CompactCpuButton','RailGpuButton')){
  $texts=(Find $id).FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Text))
  $label=$texts|Where-Object {$_.Current.Name -in @('CPU','GPU')}|Select-Object -First 1
  $value=$texts|Where-Object {$_.Current.Name -notin @('CPU','GPU')}|Select-Object -First 1
  if(!$value -or !$label -or $value.Current.BoundingRectangle.Bottom -gt $label.Current.BoundingRectangle.Top+1){throw "$id value is not above its label"}
 }
 $duplicate=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Dock app OBS Studio'))
 if($duplicate -and !$duplicate.Current.IsOffscreen){throw 'Duplicate OBS app in dock'}
 Capture 'stacked-widgets'
 Hover 'CompactMediaButton';Assert-MediaFits;Capture 'media-no-scroll'
 Hover 'RailObsButton'
 if((Find 'Preview toggle OBS recording' -Name).Current.IsOffscreen){throw 'OBS hover controls did not open'}
 Capture 'obs-hover'
 Invoke 'Use OBS app shortcut' -Name
 Hover 'RailObsButton'
 $controls=Find 'Preview toggle OBS recording' -Name -Optional
 if($controls -and !$controls.Current.IsOffscreen){throw 'App shortcut still opens OBS hover controls'}
 Capture 'obs-app-shortcut'
 [NagiWindowWidgetNative]::SetCursorPos(10,10)|Out-Null
 Invoke 'CompactBrand';Invoke 'PreferencesTab'
 $style=[NagiWindowWidgetNative]::GetWindowLongPtr($handle,-16).ToInt64();$extended=[NagiWindowWidgetNative]::GetWindowLongPtr($handle,-20).ToInt64()
 if(($style -band 0x00CF0000) -ne 0x00CF0000 -or ($extended -band 0x80) -ne 0 -or ($extended -band 0x8) -ne 0){throw 'Settings is not a regular resizable taskbar window'}
 $caption=Find 'WindowCaption';$subtitle=Find 'BrandSubtitle'
 if($caption.Current.IsOffscreen -or $caption.Current.BoundingRectangle.Bottom+20 -gt $subtitle.Current.BoundingRectangle.Top){throw 'Title bar overlaps Settings client content'}
 [NagiWindowWidgetNative]::SendMessage($handle,6,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;Start-Sleep -Milliseconds 400
 if(([NagiWindowWidgetNative]::GetWindowLongPtr($handle,-16).ToInt64() -band 0xC00000) -eq 0){throw 'Settings collapsed when deactivated'}
 Capture 'settings-window'
 [NagiWindowWidgetNative]::ShowWindow($handle,3)|Out-Null;Start-Sleep -Milliseconds 400
 if(![NagiWindowWidgetNative]::IsZoomed($handle)){throw 'Maximize failed'}
 [NagiWindowWidgetNative]::ShowWindow($handle,6)|Out-Null;Start-Sleep -Milliseconds 400
 if(![NagiWindowWidgetNative]::IsIconic($handle)){throw 'Minimize failed'}
 [NagiWindowWidgetNative]::ShowWindow($handle,9)|Out-Null;Start-Sleep -Milliseconds 400
 Invoke 'SettingsCategoryHardwarestudio';Choose-Obs 'Hidden'
 [NagiWindowWidgetNative]::SendMessage($handle,0x112,[IntPtr]0xF060,[IntPtr]::Zero)|Out-Null;Start-Sleep -Milliseconds 1200
 if($process.HasExited){throw 'Caption Close terminated Nagi'}
 if(([NagiWindowWidgetNative]::GetWindowLongPtr($handle,-16).ToInt64() -band 0x00CF0000) -ne 0){throw 'Dock retained native window chrome'}
 Invoke 'EdgeDropButton'
 $obsButton=Find 'RailObsButton' -Optional
 if($obsButton -and !$obsButton.Current.IsOffscreen){throw 'Hidden OBS still occupies the dock'}
 Capture 'obs-hidden'
 Invoke 'CompactBrand';Invoke 'PreferencesTab';Invoke 'SettingsCategoryHardwarestudio';Choose-Obs 'Hover controls'
 $saved=Get-Content $settingsFile -Raw|ConvertFrom-Json
 if(($saved.Workspaces|Where-Object Profile -eq 'Stream').ObsPresentation -ne 'Widget'){throw 'OBS mode was not saved'}
 Invoke 'PreferencesTab';Invoke 'SettingsCategoryAppearancedock'
 foreach($theme in @('Akeno','Light','Dark','Glass (system)','Light')){
  $box=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Theme'),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ComboBox)))
  if(!$box){throw 'Missing theme selector'};$box.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 120
  $choice=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$theme),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true)))
  if(!$choice){throw "Missing theme $theme"};$choice.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 450
  if($process.HasExited){throw "Theme $theme closed the app"}
 }
 Capture 'window-themes'
 foreach($mode in @('System','Custom','Theme')){
  $box=Find 'NotchColorMode';$box.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 120
  $choice=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$mode),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true)))
  if(!$choice){throw "Missing notch color mode $mode"};$choice.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 400
  if((Get-Content $settingsFile -Raw|ConvertFrom-Json).NotchColorMode -ne $mode){throw "Notch color $mode was not saved"}
  [NagiWindowWidgetNative]::SendMessage($handle,0x112,[IntPtr]0xF060,[IntPtr]::Zero)|Out-Null;Start-Sleep -Milliseconds 1200
  Capture ('drop-'+$mode)
  $events=Get-Content (Join-Path $review 'logs/pulse.jsonl') -Raw
  if($events -notlike '*Glass=True; resting=True*'){throw 'Resting drop disconnected its glass material'}
  Invoke 'EdgeDropButton';Hover 'CompactMediaButton';Assert-MediaFits;Capture ('media-'+$mode)
  [NagiWindowWidgetNative]::SetCursorPos(10,10)|Out-Null;Invoke 'CompactBrand';Invoke 'PreferencesTab';Invoke 'SettingsCategoryAppearancedock'
 }
 $errors=Join-Path $review 'logs/errors.jsonl';if((Get-Item $errors).Length -ne 0){throw 'Window/widget check logged errors'}
 @{ValuesAboveLabels=$true;NoDuplicateObs=$true;ObsAppWidgetHidden=$true;NativeWindowControls=$true;FocusKeepsSettingsOpen=$true;CaptionCloseReturnsToDock=$true}|ConvertTo-Json|Set-Content (Join-Path $review 'evidence.json')
 Write-Output "PASS: stacked values, OBS modes, native Settings window, focus, minimize/maximize and Close-to-dock. $review"
}finally{
 [NagiWindowWidgetNative]::SetCursorPos($pointer.X,$pointer.Y)|Out-Null
 if($process -and !$process.HasExited){$process.CloseMainWindow()|Out-Null;if(!$process.WaitForExit(5000)){Stop-Process -Id $process.Id}}
}
