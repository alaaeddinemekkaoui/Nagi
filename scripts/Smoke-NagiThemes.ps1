param([Parameter(Mandatory)][string]$ExecutablePath,
 [ValidateSet('Horizontal','Vertical')][string]$Orientation='Horizontal',
 [ValidateSet('Top edge','Bottom edge','Left edge','Right edge')][string]$Anchor='Bottom edge',
 [ValidateRange(1,10)][int]$Cycles=3,[switch]$Animated)
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
$qaDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/themes-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $qaDirectory -ErrorAction Stop|Out-Null
$qaFile=Join-Path $qaDirectory 'settings.json'
@{SchemaVersion=1;Theme='Akeno';WelcomeCompleted=$true;UserName='friend';Position=@{Orientation=$Orientation;Anchor=$Anchor;OffsetX=0;OffsetY=0};CloseOnDeactivate=$false;HideInFullscreen=$false;IndexFiles=$false;SearchFolders=@();FpsEnabled=$false;ObsEnabled=$false;AutomaticUpdates=$false;WeatherEnabled=$false;StartWithWindows=$false;LuaUiEnabled=$false;ReducedMotion=(!$Animated);Hotkey=@{Modifiers=7;Key=131};HideHotkey=@{Modifiers=7;Key=132};SearchHotkey=@{Modifiers=7;Key=133}}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $qaFile
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
function Test-DropBackground([string]$Name){
 $r=[WorkspaceSmokeNative+Rect]::new();[WorkspaceSmokeNative]::GetWindowRect($handle,[ref]$r)|Out-Null
 $w=$r.Right-$r.Left;$h=$r.Bottom-$r.Top
 $back=[Windows.Forms.Form]::new();$back.FormBorderStyle='None';$back.StartPosition='Manual';$back.ShowInTaskbar=$false;$back.TopMost=$true
 $back.BackColor=[Drawing.Color]::FromArgb(27,170,210);$back.Bounds=[Drawing.Rectangle]::new($r.Left-32,$r.Top-32,$w+64,$h+64)
 $region=[WorkspaceSmokeNative]::CreateRectRgn(0,0,0,0)
 try{
  $back.Show();[Windows.Forms.Application]::DoEvents()
  [WorkspaceSmokeNative]::SetWindowPos($handle,[IntPtr](-1),0,0,0,0,0x13)|Out-Null
  Start-Sleep -Milliseconds 600
  if([WorkspaceSmokeNative]::GetWindowRgn($handle,$region) -le 0){throw 'Teardrop has no native clipping region'}
  $bitmap=[Drawing.Bitmap]::new($w,$h);$g=[Drawing.Graphics]::FromImage($bitmap)
  try{
   $g.CopyFromScreen($r.Left,$r.Top,0,0,[Drawing.Size]::new($w,$h))
   $bitmap.Save((Join-Path $qaDirectory ($Name+'-desktop.png')))
   $outside=0;$painted=0
   for($x=3;$x -lt $w-3;$x+=2){for($y=3;$y -lt $h-3;$y+=2){
    if([WorkspaceSmokeNative]::PtInRegion($region,$x,$y) -or [WorkspaceSmokeNative]::PtInRegion($region,$x-2,$y) -or [WorkspaceSmokeNative]::PtInRegion($region,$x+2,$y) -or [WorkspaceSmokeNative]::PtInRegion($region,$x,$y-2) -or [WorkspaceSmokeNative]::PtInRegion($region,$x,$y+2)){continue}
    $outside++;$pixel=$bitmap.GetPixel($x,$y)
    if([Math]::Abs($pixel.R-27)+[Math]::Abs($pixel.G-170)+[Math]::Abs($pixel.B-210) -gt 12){$painted++;if($painted -le 10){Write-Output ('Outside pixel '+$x+','+$y+': '+$pixel)}}
   }}
   Write-Output ('Desktop silhouette '+$Name+': '+$painted+' painted pixels outside contour / '+$outside+' sampled')
   if($outside -lt 20 -or $painted -gt 0){throw 'Background material extends outside the teardrop'}
  }finally{$g.Dispose();$bitmap.Dispose()}
 }finally{[WorkspaceSmokeNative]::DeleteObject($region)|Out-Null;$back.Close();$back.Dispose()}
}
function Test-LightSurface {
 $r=[WorkspaceSmokeNative+Rect]::new();[WorkspaceSmokeNative]::GetWindowRect($handle,[ref]$r)|Out-Null
 $w=$r.Right-$r.Left;$h=$r.Bottom-$r.Top
 $back=[Windows.Forms.Form]::new();$back.FormBorderStyle='None';$back.StartPosition='Manual';$back.ShowInTaskbar=$false;$back.TopMost=$true
 $back.BackColor=[Drawing.Color]::Black;$back.Bounds=[Drawing.Rectangle]::new($r.Left,$r.Top,$w,$h)
 try{
  $back.Show();[Windows.Forms.Application]::DoEvents();[WorkspaceSmokeNative]::SetWindowPos($handle,[IntPtr](-1),0,0,0,0,0x13)|Out-Null
  Start-Sleep -Milliseconds 500
  $bitmap=[Drawing.Bitmap]::new($w,$h);$g=[Drawing.Graphics]::FromImage($bitmap)
  try{
   $g.CopyFromScreen($r.Left,$r.Top,0,0,[Drawing.Size]::new($w,$h));$bitmap.Save((Join-Path $qaDirectory 'light-desktop.png'))
   $brightness=0
   for($x=[int]($w/2)-10;$x -lt [int]($w/2)+10;$x++){for($y=8;$y -lt 16;$y++){$pixel=$bitmap.GetPixel($x,$y);$brightness+=($pixel.R+$pixel.G+$pixel.B)/3}}
   $brightness/=160
   Write-Output ('Light surface over a black background: '+[Math]::Round($brightness,1)+'/255')
   if($brightness -lt 200){throw 'Light surface is too dark to support dark text'}
  }finally{$g.Dispose();$bitmap.Dispose()}
 }finally{$back.Close();$back.Dispose()}
}

try{
 for($i=0;$i -lt 100;$i++){$script:root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))|Where-Object {$_.Current.Name -eq 'Nagi' -and $_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1;if($root){break};Start-Sleep -Milliseconds 100}
 if(!$root){throw 'No QA window'};$handle=[IntPtr]$root.Current.NativeWindowHandle
 [WorkspaceSmokeNative]::SetCursorPos(10,10)|Out-Null
 Snapshot 'resting-before'
 Test-DropBackground 'resting-before'
 Invoke 'Reveal Nagi';Invoke 'Open Nagi control center';Invoke 'Settings';Invoke 'Appearance & dock settings'
 $transitions=0
 foreach($cycle in 1..$Cycles){
  foreach($theme in @('Light','Dark','Light','Akeno','Light','Glass (system)','Dark','Akeno','Dark','Glass (system)')){
   Write-Output ('Switch '+$cycle+': '+$theme)
   Select-Workspace 'Theme' $theme
   Start-Sleep -Milliseconds 350
   if($qaProcess.HasExited){throw ('App exited during theme switch to '+$theme)}
   $expected=if($theme -eq 'Glass (system)'){'System'}else{$theme}
   if((Get-Content $qaFile -Raw|ConvertFrom-Json).Theme -ne $expected){throw ('Theme not saved: '+$expected)}
   $transitions++
  }
 }
 foreach($theme in @('Light','Dark','Akeno','Glass (system)')){
  Select-Workspace 'Theme' $theme
  $transitions++
  Snapshot ('expanded-'+$theme)
  if($theme -eq 'Light'){Test-LightSurface}
  Invoke 'Quick search'
  $search=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))|Where-Object {$_.Current.Name -eq 'Nagi · Quick search'}|Select-Object -First 1
  if(!$search){throw ('Quick search did not reopen in '+$theme)}
  $close=$search.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Close quick search'))
  if(!$close){throw 'Quick search close control missing'}
  $close.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 250
  Invoke 'Collapse Nagi';Start-Sleep -Milliseconds 1200
  Test-DropBackground ('resting-'+$theme)
  Invoke 'Reveal Nagi';Invoke 'Open Nagi control center';Invoke 'Settings';Invoke 'Appearance & dock settings'
 }
 (Find 'Translucent glass').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 400
 foreach($theme in @('Light','Dark','Akeno','Light')){Select-Workspace 'Theme' $theme;$transitions++}
 Invoke 'Collapse Nagi';Start-Sleep -Milliseconds 1200;Test-DropBackground 'resting-glass-off'
 if((Get-Content (Join-Path $qaDirectory 'logs/pulse.jsonl') -Raw) -match '"category":"fatal"'){throw 'Fatal UI exception was logged'}
 Write-Output ('PASS: '+$transitions+' theme transitions and persisted choices. Profile: '+$qaDirectory)
}catch{
 Write-Output $_.ScriptStackTrace
 if(Test-Path (Join-Path $qaDirectory 'logs/pulse.jsonl')){Get-Content (Join-Path $qaDirectory 'logs/pulse.jsonl') -Tail 6|Write-Output}
 throw
}finally{
 [WorkspaceSmokeNative]::SetCursorPos($qaPointer.X,$qaPointer.Y)|Out-Null
 if(!$qaProcess.HasExited){if($handle){[WorkspaceSmokeNative]::PostMessage($handle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null};if(!$qaProcess.WaitForExit(5000)){Stop-Process -Id $qaProcess.Id -Force}}
}
