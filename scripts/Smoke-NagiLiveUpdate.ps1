param([Parameter(Mandatory)][string]$BaselinePackageDirectory,[string]$TargetVersion='0.1.1',[switch]$LegacyClient)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
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
 [DllImport("user32.dll")] public static extern int GetWindowRgn(IntPtr h,IntPtr r);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("gdi32.dll")] public static extern IntPtr CreateRectRgn(int l,int t,int r,int b);
 [DllImport("gdi32.dll")] public static extern bool PtInRegion(IntPtr r,int x,int y);
 [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr r);
}
'@

$qaPointer=[WorkspaceSmokeNative+Point]::new();[WorkspaceSmokeNative]::GetCursorPos([ref]$qaPointer)|Out-Null
$qaDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/live-update-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$qaApp=Join-Path $qaDirectory 'app';$qaData=Join-Path $qaDirectory 'preferences with spaces'
New-Item -ItemType Directory -Path $qaApp,$qaData|Out-Null
$sourceDirectory=[IO.Path]::GetFullPath($BaselinePackageDirectory)
Get-ChildItem -LiteralPath $sourceDirectory|Copy-Item -Destination $qaApp -Recurse
$qaExe=Join-Path $qaApp 'Nagi.exe';$qaFile=Join-Path $qaData 'settings.json'
$beforeVersion=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $qaApp 'Nagi.dll')).Version.ToString(3)
@{SchemaVersion=1;Theme='Light';WelcomeCompleted=$true;UserName='Update verifier';PreferredMusicPlayer='Spotify';LockedProfile='Work';Position=@{Orientation='Horizontal';Anchor='Bottom edge';OffsetX=0;OffsetY=0};CloseOnDeactivate=$false;HideInFullscreen=$false;IndexFiles=$false;SearchFolders=@();FpsEnabled=$false;ObsEnabled=$false;AutomaticUpdates=$true;WeatherEnabled=$false;StartWithWindows=$false;LuaUiEnabled=$false;ReducedMotion=$true;Hotkey=@{Modifiers=7;Key=131};HideHotkey=@{Modifiers=7;Key=132};SearchHotkey=@{Modifiers=7;Key=133}}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $qaFile
$start=[Diagnostics.ProcessStartInfo]::new($qaExe);$start.UseShellExecute=$false;$start.ArgumentList.Add('--data-dir');$start.ArgumentList.Add($qaData)
$qaProcess=[Diagnostics.Process]::Start($start);$initialPid=$qaProcess.Id
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

function Wait-For([scriptblock]$Condition,[string]$Description,[int]$Seconds=180){
 $deadline=(Get-Date).AddSeconds($Seconds)
 while((Get-Date) -lt $deadline){if(&$Condition){return};Start-Sleep -Milliseconds 400}
 throw ('Timed out: '+$Description)
}
function Set-Root([int]$ProcessId){
 $script:root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId))|Where-Object {$_.Current.Name -eq 'Nagi' -and $_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1
 if($root){$script:handle=[IntPtr]$root.Current.NativeWindowHandle;return $true};return $false
}
function Open-Updates {
 # Keep the hover-only bar open while navigating. A click intentionally does not pin it.
 $dockBounds=$root.Current.BoundingRectangle
 [WorkspaceSmokeNative]::SetCursorPos([int]($dockBounds.Left+$dockBounds.Width/2),[int]($dockBounds.Top+$dockBounds.Height/2))|Out-Null
 if(Find 'Reveal Nagi' -Optional){Invoke 'Reveal Nagi'}
 if(Find 'Open Nagi control center' -Optional){Invoke 'Open Nagi control center'}
 Invoke 'Settings';Invoke 'Updates settings'
}
try{
 [WorkspaceSmokeNative]::SetCursorPos(10,10)|Out-Null
 Wait-For {Set-Root $qaProcess.Id} 'Nagi window' 30
 $notes='';$laterWorked=$false
 if(!$LegacyClient){
  Wait-For {Find 'Later' -Optional} 'automatic update prompt with changelog' 240
  $notesElement=Find 'Available update changelog'
  $notes=$notesElement.GetCurrentPattern([Windows.Automation.TextPattern]::Pattern).DocumentRange.GetText(-1)
  if($notes -notlike ('*'+$TargetVersion+'*') -or $notes.Length -lt 60){throw 'Release changelog is missing'}
  Snapshot 'update-prompt';Invoke 'Later';Start-Sleep -Milliseconds 800
  if($qaProcess.HasExited){throw 'Later unexpectedly stopped Nagi'}
  $laterWorked=$true
  if(!(Test-Path -LiteralPath (Join-Path $qaData 'logs/errors.jsonl'))){throw 'Separate error log was not created'}
 }
 Open-Updates
 Wait-For { $button=Find 'Install and restart' -Optional; $button -and $button.Current.IsEnabled } 'verified automatic download' 900
 Snapshot 'download-verified'
 if(!$LegacyClient){
  Invoke 'Check for updates'
  Wait-For {Find 'Update now' -Optional} 'manual update choice' 120
  Snapshot 'update-now';Invoke 'Update now'
 }else{Invoke 'Install and restart'}
 Wait-For {$qaProcess.HasExited} 'old process exit' 60
 Wait-For {
  $candidate=Get-CimInstance Win32_Process -Filter "Name = 'Nagi.exe'"|Where-Object {$_.ExecutablePath -eq $qaExe -and $_.CommandLine.Contains($qaData) -and $_.ProcessId -ne $initialPid}|Select-Object -First 1
  if(!$candidate){return $false}
  $script:restarted=[Diagnostics.Process]::GetProcessById($candidate.ProcessId)
  Set-Root $restarted.Id
 } 'updated Nagi restart' 120
 $afterVersion=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $qaApp 'Nagi.dll')).Version.ToString(3)
 if($afterVersion -ne $TargetVersion){throw ('Wrong installed version: '+$afterVersion)}
 $saved=Get-Content -LiteralPath $qaFile -Raw|ConvertFrom-Json
 if($saved.UserName -ne 'Update verifier' -or $saved.Theme -ne 'Light' -or $saved.PreferredMusicPlayer -ne 'Spotify' -or $saved.LockedProfile -ne 'Work'){throw 'Update changed preferences'}
 Open-Updates
 $versionLabel=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Text))|Where-Object {$_.Current.Name.StartsWith('Installed version '+$TargetVersion)}|Select-Object -First 1
 if(!$versionLabel){throw 'Restarted UI does not report the new version'}
 Snapshot 'updated-app'
 $activity=Get-Content -LiteralPath (Join-Path $qaData 'logs/pulse.jsonl') -Raw
 if($activity -match '"category":"fatal"'){throw 'Fatal exception in update test'}
 [PSCustomObject]@{Before=$beforeVersion;After=$afterVersion;BeforePid=$initialPid;AfterPid=$restarted.Id;LaterKeptRunning=$laterWorked;ChangelogCharacters=$notes.Length;PreferencesPreserved=$true;AppDirectory=$qaApp;DataDirectory=$qaData;LegacyClient=[bool]$LegacyClient}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $qaDirectory 'evidence.json')
 Write-Output ('PASS: real GitHub update '+$beforeVersion+' -> '+$afterVersion+', verified download, explicit installation, new process, correct UI version and preserved preferences. '+$qaDirectory)
}catch{
 Write-Output $_.ScriptStackTrace
 if(Test-Path (Join-Path $qaData 'logs/pulse.jsonl')){Get-Content (Join-Path $qaData 'logs/pulse.jsonl') -Tail 12|Write-Output}
 throw
}finally{
 [WorkspaceSmokeNative]::SetCursorPos($qaPointer.X,$qaPointer.Y)|Out-Null
 $owned=Get-CimInstance Win32_Process -Filter "Name = 'Nagi.exe'"|Where-Object {$_.ExecutablePath -eq $qaExe -and $_.CommandLine.Contains($qaData)}
 foreach($item in $owned){
  $process=Get-Process -Id $item.ProcessId -ErrorAction SilentlyContinue
  if(!$process){continue}
  if(Set-Root $item.ProcessId){[WorkspaceSmokeNative]::PostMessage($handle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null}
  if(!$process.WaitForExit(10000)){Stop-Process -Id $item.ProcessId}
 }
}
