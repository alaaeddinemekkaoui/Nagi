param([Parameter(Mandatory)][string]$ExecutablePath, [switch]$DocumentationImages, [ValidateSet("Horizontal","Vertical")][string]$Orientation="Horizontal")
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
$qaDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) (('artifacts/validation/app-folder-'+$Orientation+'-')+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $qaDirectory -ErrorAction Stop|Out-Null
$qaFile=Join-Path $qaDirectory 'settings.json'
@{SchemaVersion=1;Theme='System';WelcomeCompleted=$true;UserName='friend';Position=@{Orientation='Horizontal';Anchor='Bottom edge';OffsetX=0;OffsetY=0};CloseOnDeactivate=$false;HideInFullscreen=$false;IndexFiles=$false;SearchFolders=@();FpsEnabled=$false;ObsEnabled=$false;AutomaticUpdates=$false;WeatherEnabled=$false;StartWithWindows=$false;LuaUiEnabled=$false;ReducedMotion=$true;Hotkey=@{Modifiers=7;Key=131};HideHotkey=@{Modifiers=7;Key=132};SearchHotkey=@{Modifiers=7;Key=133}}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $qaFile
$qaConfiguration=Get-Content -LiteralPath $qaFile -Raw|ConvertFrom-Json -AsHashtable
$qaConfiguration.Position.Orientation=$Orientation
$qaConfiguration.Position.Anchor=if($Orientation -eq 'Horizontal'){'Bottom edge'}else{'Right edge'}
$qaConfiguration.LockedProfile='Work'
$qaConfiguration.Workspaces=@(
 @{Profile='Work';Apps=@(@{Name='VS Code';Target='code';Kind='Suggested'},@{Name='Terminal';Target='terminal';Kind='Suggested'},@{Name='Browser';Target='browser';Kind='Suggested'},@{Name='Steam';Target='steam';Kind='Suggested'},@{Name='OBS Studio';Target='obs';Kind='Suggested'});Widgets=@('Performance','Media','Audio','Microphone','Battery')},
 @{Profile='Gaming';Apps=@(1..16|ForEach-Object {@{Name=('Test app '+$_);Target=('fixture-app-'+$_);Kind='Catalog'}});Widgets=@('Media')}
)
$qaConfiguration|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $qaFile -Encoding UTF8
$start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($ExecutablePath));$start.UseShellExecute=$false;$start.ArgumentList.Add('--data-dir');$start.ArgumentList.Add($qaDirectory)
$qaProcess=[Diagnostics.Process]::Start($start)
function Find([string]$Value,[switch]$Id,[switch]$Optional){
 $property=if($Id){[Windows.Automation.AutomationElement]::AutomationIdProperty}else{[Windows.Automation.AutomationElement]::NameProperty}
 for($i=0;$i -lt 30;$i++){$item=$script:root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($property,$Value));if($item){return $item};if($Optional){return $null};Start-Sleep -Milliseconds 100};throw ('Missing '+$Value)
}
function Invoke([string]$Name){(Find $Name).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 450}
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
 [WorkspaceSmokeNative]::SetCursorPos(30,30)|Out-Null
 Start-Sleep -Milliseconds 500
 $drop=Find 'Reveal Nagi';Snapshot 'teardrop'
 $dropRect=$drop.Current.BoundingRectangle
 [WorkspaceSmokeNative]::SetCursorPos([int]($dropRect.Left+$dropRect.Width/2),[int]($dropRect.Top+$dropRect.Height/2))|Out-Null
 Start-Sleep -Milliseconds 1000
 $folder=Find 'More workspace apps'
 foreach($app in @('VS Code','Terminal','Browser')){if(Find ('Dock app '+$app) -Optional){throw 'Grouped apps still appear directly on the dock'}}
 if(Find 'Dock app Steam' -Optional){throw 'Fourth app was not grouped'}
 Snapshot 'dock'
 $folderRect=$folder.Current.BoundingRectangle
 [WorkspaceSmokeNative]::SetCursorPos([int]($folderRect.Left+$folderRect.Width/2),[int]($folderRect.Top+$folderRect.Height/2))|Out-Null
 Start-Sleep -Milliseconds 900
 $steam=Find 'Folder app Steam';$obs=Find 'Folder app OBS Studio'
 if($steam.Current.IsOffscreen -or $obs.Current.IsOffscreen){throw 'Hover did not expand the grouped apps'}
 $expandedFolderRect=(Find 'More workspace apps').Current.BoundingRectangle
 if([Math]::Abs($expandedFolderRect.Top-$folderRect.Top) -gt 2 -or [Math]::Abs($expandedFolderRect.Left-$folderRect.Left) -gt 2){throw 'Opening the folder moved the dock'}
 $steamRect=$steam.Current.BoundingRectangle
 [WorkspaceSmokeNative]::SetCursorPos([int]($steamRect.Left+$steamRect.Width/2),[int]($steamRect.Top+$steamRect.Height/2))|Out-Null
 Start-Sleep -Milliseconds 600
 if((Find 'Folder app Steam').Current.IsOffscreen){throw 'Folder closed while moving to its apps'}
 Snapshot 'app-folder'
 [WorkspaceSmokeNative]::SetCursorPos(30,30)|Out-Null;Start-Sleep -Milliseconds 1100
 Find 'Reveal Nagi'|Out-Null
 Invoke 'Reveal Nagi';$clickFolderRect=(Find 'More workspace apps').Current.BoundingRectangle;[WorkspaceSmokeNative]::SetCursorPos([int]($clickFolderRect.Left+$clickFolderRect.Width/2),[int]($clickFolderRect.Top+$clickFolderRect.Height/2))|Out-Null;Invoke 'More workspace apps'
 if((Find 'Folder app Steam').Current.IsOffscreen){throw 'Folder click did not open apps'}
 Invoke 'Open Nagi control center';Find 'Customize Work'|Out-Null
 Invoke 'Activate Gaming';Invoke 'Collapse Nagi';Invoke 'Reveal Nagi';$clickFolderRect=(Find 'More workspace apps').Current.BoundingRectangle;[WorkspaceSmokeNative]::SetCursorPos([int]($clickFolderRect.Left+$clickFolderRect.Width/2),[int]($clickFolderRect.Top+$clickFolderRect.Height/2))|Out-Null;Invoke 'More workspace apps'
 Find 'Folder app Test app 4'|Out-Null;Snapshot 'sixteen-apps'
 Start-Sleep -Milliseconds 1800
 foreach($appNumber in 1..16){Find ('Folder app Test app '+$appNumber)|Out-Null}
 Find 'Folder app Test app 16'|Out-Null;Snapshot 'sixteen-apps'
 Invoke 'Open Nagi control center';Invoke 'Activate Default';Invoke 'Collapse Nagi';Invoke 'Reveal Nagi'
 if(Find 'More workspace apps' -Optional){throw 'Empty Default profile still shows the folder'}
 Invoke 'Open Nagi control center';Invoke 'Activate Work';Invoke 'Customize Work'
 Invoke 'Remove OBS Studio';Invoke 'Remove Steam';Invoke 'Remove Browser';Invoke '← All profiles'
 Invoke 'Overview';Invoke 'Collapse Nagi';Invoke 'Reveal Nagi'
 if(Find 'More workspace apps' -Optional){throw 'Folder remains after returning to two apps'}
 foreach($app in @('VS Code','Terminal')){Find ('Dock app '+$app)|Out-Null}
 if($DocumentationImages){
  $docImages=Join-Path (Split-Path $PSScriptRoot -Parent) 'docs/images'
  foreach($entry in @(@('teardrop','nagi-teardrop'),@('dock','nagi-dock'),@('app-folder','nagi-app-folder'))){Copy-Item -LiteralPath (Join-Path $qaDirectory ($entry[0]+'.png')) -Destination (Join-Path $docImages ($entry[1]+'.png')) -Force}
 }
 Write-Output ('PASS: teardrop hover, two direct apps, grouped apps hover/click, pointer movement, dock position, 16-app profile, Default profile, and removal back to two apps. '+$qaDirectory)
}catch{
 Write-Output $_.ScriptStackTrace
 throw
}finally{
 [WorkspaceSmokeNative]::SetCursorPos($qaPointer.X,$qaPointer.Y)|Out-Null
 if(!$qaProcess.HasExited){[WorkspaceSmokeNative]::PostMessage($handle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;$qaProcess.WaitForExit(10000)|Out-Null}
}
