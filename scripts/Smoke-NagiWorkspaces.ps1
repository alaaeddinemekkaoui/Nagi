param([Parameter(Mandatory)][string]$ExecutablePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class WorkspaceSmokeNative {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
 [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
 [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
}
'@
$qaPointer=[WorkspaceSmokeNative+Point]::new();[WorkspaceSmokeNative]::GetCursorPos([ref]$qaPointer)|Out-Null
$qaDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/workspaces-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $qaDirectory -ErrorAction Stop|Out-Null
$qaFile=Join-Path $qaDirectory 'settings.json'
@{SchemaVersion=1;Theme='System';WelcomeCompleted=$true;UserName='friend';Position=@{Orientation='Horizontal';Anchor='Bottom edge';OffsetX=0;OffsetY=0};CloseOnDeactivate=$false;HideInFullscreen=$false;IndexFiles=$false;SearchFolders=@();FpsEnabled=$false;ObsEnabled=$false;AutomaticUpdates=$false;WeatherEnabled=$false;StartWithWindows=$false;LuaUiEnabled=$false;ReducedMotion=$true;Hotkey=@{Modifiers=7;Key=131};HideHotkey=@{Modifiers=7;Key=132};SearchHotkey=@{Modifiers=7;Key=133}}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $qaFile
$start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($ExecutablePath));$start.UseShellExecute=$false;$start.ArgumentList.Add('--data-dir');$start.ArgumentList.Add($qaDirectory)
$qaProcess=[Diagnostics.Process]::Start($start)
function Find([string]$Value,[switch]$Id,[switch]$Optional){
 $property=if($Id){[Windows.Automation.AutomationElement]::AutomationIdProperty}else{[Windows.Automation.AutomationElement]::NameProperty}
 for($i=0;$i -lt 30;$i++){$item=$script:root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($property,$Value));if($item){return $item};if($Optional){return $null};Start-Sleep -Milliseconds 100};throw ('Missing '+$Value)
}
function Invoke([string]$Name){(Find $Name).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 450}
function Activate-Workspace([string]$Name){
 if(Find '← All profiles' -Optional){Invoke '← All profiles'}
 Invoke ('Customize '+$Name);Invoke ('Activate '+$Name);Invoke '← All profiles'
}
function Select-Workspace([string]$Id,[string]$Value){
 $combos=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ComboBox))
 $combo=$combos|Where-Object {$_.Current.AutomationId -eq $Id -or $_.Current.Name -eq $Id}|Select-Object -First 1
 if(!$combo){throw ('Missing combo '+$Id)}
 $combo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 150
 $choice=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$Value),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true)))
 if(!$choice){throw ('Missing selection '+$Value)};$choice.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 450
}
function Snapshot([string]$Name){
 $script:handle=[IntPtr]$root.Current.NativeWindowHandle
 $r=[WorkspaceSmokeNative+Rect]::new();[WorkspaceSmokeNative]::GetWindowRect($handle,[ref]$r)|Out-Null
 $bitmap=[Drawing.Bitmap]::new($r.Right-$r.Left,$r.Bottom-$r.Top);$g=[Drawing.Graphics]::FromImage($bitmap);$dc=$g.GetHdc()
 try{[WorkspaceSmokeNative]::PrintWindow($handle,$dc,2)|Out-Null}finally{$g.ReleaseHdc($dc)}
 $bitmap.Save((Join-Path $qaDirectory ($Name+'.png')));$g.Dispose();$bitmap.Dispose()
}
try{
 for($i=0;$i -lt 100;$i++){$script:root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))|Where-Object {$_.Current.Name -eq 'Nagi' -and $_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1;if($root){break};Start-Sleep -Milliseconds 100}
 if(!$root){throw 'No QA window'};$handle=[IntPtr]$root.Current.NativeWindowHandle
 Invoke 'Reveal Nagi';Invoke 'Open Nagi control center';$handle=[IntPtr]$root.Current.NativeWindowHandle
 Find 'Customize Default'|Out-Null;Find 'Customize Stream'|Out-Null
 Activate-Workspace 'Default';Invoke 'Overview'
 if(Find 'Workspace app VS Code' -Optional){throw 'Default profile contains apps'}
 Find 'GpuMetric' -Id|Out-Null;Snapshot 'default-profile'
 Invoke 'Settings';Invoke 'Music settings'
 Select-Workspace 'Preferred music player' 'Spotify'
 if((Get-Content $qaFile -Raw|ConvertFrom-Json).PreferredMusicPlayer -ne 'Spotify'){throw 'Music preference was not saved'}
 Invoke 'Overview'
 if(Find 'Open Spotify' -Optional){if((Find 'Open YouTube Music' -Optional) -or (Find 'Open Apple Music' -Optional)){throw 'Unselected music links are visible'}}
 Snapshot 'preferred-music'
 Invoke 'Settings';Invoke 'Music settings';Select-Workspace 'Preferred music player' 'All';Invoke 'Profiles'
 Find 'Customize Work'|Out-Null;Snapshot 'control-center-profiles'
 $r=[WorkspaceSmokeNative+Rect]::new();[WorkspaceSmokeNative]::GetWindowRect($handle,[ref]$r)|Out-Null
 if(($r.Right-$r.Left)/([WorkspaceSmokeNative]::GetDpiForWindow($handle)/96.0) -lt 700){throw 'Logo did not open a large control center'}
 Invoke 'Customize Work'
 Invoke 'Refresh installed apps';Start-Sleep -Seconds 1
 $picker=Find 'Workspace app picker'
 $picker.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 300
 $items=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem))
 $chosen=$items|Where-Object {$_.Current.Name -and $_.Current.Name -notin @('VS Code','Terminal','Browser','Automatic','Work','Gaming','Focus') -and $_.GetCurrentPropertyValue([Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty)}|Select-Object -First 1
 if(!$chosen){throw 'Installed app picker has no choices'};$chosenName=$chosen.Current.Name
 $chosen.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 200
 Invoke 'Add selected app'
 Activate-Workspace 'Work';Invoke 'Overview'
 $appButtons=@('Workspace app VS Code','Workspace app Terminal','Workspace app Browser',('Workspace app '+$chosenName))|ForEach-Object {Find $_}
 $tops=@($appButtons|ForEach-Object {$_.Current.BoundingRectangle.Top}|Select-Object -Unique)
 Snapshot 'four-app-row'
 $rowRange=$tops|Measure-Object -Minimum -Maximum
 if($rowRange.Maximum-$rowRange.Minimum -gt 2){throw ('Four app buttons are not on one line: '+($tops -join ', '))}
 Invoke 'Profiles';if(Find '← All profiles' -Optional){Invoke '← All profiles'};Invoke 'Customize Work'
 if((Get-Content $qaFile -Raw|ConvertFrom-Json).Workspaces.Where({$_.Profile -eq "Work"})[0].Apps.Count -ne 4){throw 'Selected app was not added'}
 Invoke ('Remove '+$chosenName)
 if((Get-Content $qaFile -Raw|ConvertFrom-Json).Workspaces.Where({$_.Profile -eq "Work"})[0].Apps.Count -ne 3){throw 'App removal was not saved'}
 (Find 'On manual activation').GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 200
 $auto=Find 'Open apps when I activate this profile';$pattern=$auto.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)
 if($pattern.Current.ToggleState -ne [Windows.Automation.ToggleState]::Off){throw 'Auto-open was not opt-in'}
 $pattern.Toggle();Start-Sleep -Milliseconds 500
 if(!(Get-Content $qaFile -Raw|ConvertFrom-Json).Workspaces.Where({$_.Profile -eq "Work"})[0].AutoOpenApps){throw 'Auto-open was not saved'}
 $pattern.Toggle();Start-Sleep -Milliseconds 500
 $performance=Find 'Work widget Performance';$performance.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 500
 Activate-Workspace 'Work';Invoke 'Overview'
 Find 'MicrophoneDeviceText' -Id|Out-Null;Find 'Media microphone mute'|Out-Null
 $settings=Get-Content $qaFile -Raw|ConvertFrom-Json
 if($settings.Workspaces.Where({$_.Profile -eq "Work"})[0].Widgets -contains 'Performance' -or $settings.Workspaces.Where({$_.Profile -eq "Work"})[0].AutoOpenApps){throw 'Workspace choices did not persist'}
 Snapshot 'workspace-media-microphone'
 Invoke 'Settings'
 foreach($category in @('General','Appearance & dock','Profiles & apps','Weather','Hardware & studio','Search & shortcuts','Lua UI studio','Music','Updates','Behavior','About & diagnostics')){Find ($category+' settings')|Out-Null}
 Snapshot 'settings-categories'
 foreach($category in @('General','Appearance & dock','Profiles & apps','Weather','Hardware & studio','Search & shortcuts','Lua UI studio','Music','Updates','Behavior','About & diagnostics')){
  Invoke ($category+' settings');Find 'Back to settings'|Out-Null
  if(Find 'General settings' -Optional){throw 'Category menu is still visible inside a category'}
  if($category -eq 'Profiles & apps'){
   Invoke 'Customize Work';Find 'Back to settings'|Out-Null;Find 'Work widget Performance'|Out-Null
   Invoke '← All profiles';Find 'Customize Gaming'|Out-Null
  }
  Invoke 'Back to settings';Find 'General settings'|Out-Null
 }
 Invoke 'Updates settings';Find 'Check for updates'|Out-Null
 $download=Find 'Download and update'
 if($download.Current.IsEnabled){throw 'Update action enabled without a release'}
 Snapshot 'updates-settings';Invoke 'Back to settings'
 Invoke 'General settings';Invoke 'Settings'
 Find 'Appearance & dock settings'|Out-Null
 if(Find 'Back to settings' -Optional){throw 'Settings did not reopen its category menu'}
 Invoke 'Appearance & dock settings'
 Select-Workspace 'Theme' 'Akeno';Select-Workspace 'Akeno background' 'Samurai';Snapshot 'akeno-samurai'
 Select-Workspace 'Akeno background' 'Gradient';Snapshot 'akeno-gradient'
 Select-Workspace 'Akeno background' 'None';Snapshot 'akeno-none'
 if((Get-Content $qaFile -Raw|ConvertFrom-Json).AkenoBackground -ne 'None'){throw 'Background hide did not persist'}
 Select-Workspace 'Theme' 'Glass (system)'
 Invoke 'Back to settings';Invoke 'Weather settings';Invoke 'Back to settings';Invoke 'General settings';Find 'Run welcome setup'|Out-Null
 Invoke 'Profiles';if(Find '← All profiles' -Optional){Invoke '← All profiles'}
 Invoke 'Customize Stream'
 foreach($widget in @('Performance','Media','Audio','Microphone','Weather','Battery')){(Find ('Stream widget '+$widget)).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 150}
 Activate-Workspace 'Stream';Invoke 'Overview'
 if((Find 'Toggle OBS recording').Current.IsEnabled -or (Find 'Toggle OBS streaming').Current.IsEnabled){throw 'Disconnected OBS controls are enabled'}
 foreach($slot in 1..3){Find ('Switch OBS scene '+$slot+': Not configured')|Out-Null}
 Snapshot 'stream-profile'
 Invoke 'Collapse Nagi';Invoke 'Reveal Nagi'
 $obsRail=Find 'OBS Studio controls';$obsRect=$obsRail.Current.BoundingRectangle
 [WorkspaceSmokeNative]::SetCursorPos([int]($obsRect.Left+$obsRect.Width/2),[int]($obsRect.Top+$obsRect.Height/2))|Out-Null
 Start-Sleep -Milliseconds 1200
 Find 'Preview toggle OBS recording'|Out-Null;Find 'Preview toggle OBS streaming'|Out-Null
 $peekSceneButtons=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Switch OBS scene 3: Not configured'))
 if(!($peekSceneButtons|Where-Object {!$_.Current.IsOffscreen})){throw 'Third OBS scene button is outside the hover viewport'}
 Snapshot 'obs-hover'
 [WorkspaceSmokeNative]::SetCursorPos($qaPointer.X,$qaPointer.Y)|Out-Null
 Invoke 'Open Nagi control center';if(Find '← All profiles' -Optional){Invoke '← All profiles'};Activate-Workspace 'Gaming';Invoke 'Overview'
 Find 'Workspace app Steam'|Out-Null
 Invoke 'Collapse Nagi';Invoke 'Reveal Nagi';Find 'Dock app Steam'|Out-Null
 Snapshot 'gaming-dock'
 $settings=Get-Content $qaFile -Raw|ConvertFrom-Json
 if($settings.LockedProfile -ne 'Gaming' -or $settings.Theme -ne 'System'){throw 'Profile/theme persistence failed'}
 Write-Output ('PASS: logo control center, profile app add/remove and opt-in/persistence, widget selection, microphone, eleven settings categories, empty Default, music preference, OBS profile, four-app row, Akeno image/gradient/off, system glass and Gaming app dock. Profile: '+$qaDirectory)
}catch{
 Write-Output $_.ScriptStackTrace
 throw
}finally{
 [WorkspaceSmokeNative]::SetCursorPos($qaPointer.X,$qaPointer.Y)|Out-Null
 if(!$qaProcess.HasExited){[WorkspaceSmokeNative]::PostMessage($handle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;$qaProcess.WaitForExit(10000)|Out-Null}
}
