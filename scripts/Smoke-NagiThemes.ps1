param([Parameter(Mandatory)][string]$ExecutablePath,
 [ValidateSet('Horizontal','Vertical')][string]$Orientation='Horizontal',
 [ValidateSet('Top edge','Bottom edge','Left edge','Right edge')][string]$Anchor='Bottom edge',
 [ValidateRange(1,10)][int]$Cycles=3,[switch]$Animated,[switch]$NotchReference,[switch]$AppCaptureOnly,[switch]$ReferenceOnly,[switch]$GlassTransmission)
$ErrorActionPreference='Stop'
if($ReferenceOnly -and !$NotchReference){throw '-ReferenceOnly requires -NotchReference'}
if($GlassTransmission -and $AppCaptureOnly){throw '-GlassTransmission requires desktop capture; PrintWindow does not capture the background behind acrylic'}
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
 $combo=$null
 for($attempt=0;$attempt -lt 30 -and !$combo;$attempt++){
  $combos=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ComboBox))
  $combo=$combos|Where-Object {$_.Current.AutomationId -eq $Id -or $_.Current.Name -eq $Id}|Select-Object -First 1
  if(!$combo){Start-Sleep -Milliseconds 100}
 }
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
 if($AppCaptureOnly){Snapshot ($Name+'-window');Write-Output ('SKIP desktop composition: '+$Name+' (app-window capture mode)');return}
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
function Test-NotchReference {
 if($Orientation -ne 'Horizontal'){throw 'The notch reference check requires a horizontal dock'}
 $r=[WorkspaceSmokeNative+Rect]::new();[WorkspaceSmokeNative]::GetWindowRect($handle,[ref]$r)|Out-Null
 $w=$r.Right-$r.Left;$h=$r.Bottom-$r.Top
 $back=[Windows.Forms.Form]::new();$back.FormBorderStyle='None';$back.StartPosition='Manual';$back.ShowInTaskbar=$false;$back.TopMost=$true
 $back.BackColor=[Drawing.Color]::FromArgb(8,8,9);$back.Bounds=[Drawing.Rectangle]::new($r.Left-16,$r.Top-16,$w+32,$h+32)
 $region=[WorkspaceSmokeNative]::CreateRectRgn(0,0,0,0)
 try{
  $back.Show();[Windows.Forms.Application]::DoEvents();[WorkspaceSmokeNative]::SetWindowPos($handle,[IntPtr](-1),0,0,0,0,0x13)|Out-Null
  Start-Sleep -Milliseconds 600
  [WorkspaceSmokeNative]::GetWindowRgn($handle,$region)|Out-Null
  $bitmap=[Drawing.Bitmap]::new($w,$h);$g=[Drawing.Graphics]::FromImage($bitmap)
  try{
   if($AppCaptureOnly){$dc=$g.GetHdc();try{[WorkspaceSmokeNative]::PrintWindow($handle,$dc,2)|Out-Null}finally{$g.ReleaseHdc($dc)}}
   else{$g.CopyFromScreen($r.Left,$r.Top,0,0,[Drawing.Size]::new($w,$h))}
   $bitmap.Save((Join-Path $qaDirectory 'reference-notch-dark.png'))
   $logoLeft=$w;$logoRight=0;$logoTop=$h;$logoBottom=0;$red=0;$brightEdges=0;$samples=0
   for($x=0;$x -lt $w;$x++){for($y=0;$y -lt $h;$y++){
    $pixel=$bitmap.GetPixel($x,$y)
    if($pixel.R -gt 150 -and $pixel.R -gt 2*$pixel.G -and $pixel.R -gt 2*$pixel.B){
     $red++;$logoLeft=[Math]::Min($logoLeft,$x);$logoRight=[Math]::Max($logoRight,$x);$logoTop=[Math]::Min($logoTop,$y);$logoBottom=[Math]::Max($logoBottom,$y)
    }
   }}
   for($x=6;$x -lt $w-6;$x+=3){
    $edgeY=if($Anchor -eq 'Top edge'){$h-1}else{0};$step=if($Anchor -eq 'Top edge'){-1}else{1}
    while($edgeY -ge 0 -and $edgeY -lt $h -and ![WorkspaceSmokeNative]::PtInRegion($region,$x,$edgeY)){$edgeY+=$step}
    $innerY=$edgeY+4*$step
    if($innerY -lt 0 -or $innerY -ge $h){continue}
    $edge=$bitmap.GetPixel($x,$edgeY);$inner=$bitmap.GetPixel($x,$innerY)
    $samples++;if(($edge.R+$edge.G+$edge.B-$inner.R-$inner.G-$inner.B)/3 -gt 14){$brightEdges++}
   }
   if($red -lt 8){throw 'The centered red notch logo is missing'}
   if([Math]::Abs(($logoLeft+$logoRight)/2-$w/2) -gt 3){throw 'The notch logo is not horizontally centered'}
   $expectedY=if($Anchor -eq 'Top edge'){.65*$h}else{.35*$h}
   if([Math]::Abs(($logoTop+$logoBottom)/2-$expectedY) -gt 4){throw 'The notch logo is misplaced inside the dip'}
   Write-Output ('Notch reference: '+$red+' red logo pixels; '+$brightEdges+' bright border samples / '+$samples)
   if($samples -lt 20 -or $brightEdges -gt 0){throw 'A white or gray rim remains on the notch'}
  }finally{$g.Dispose();$bitmap.Dispose()}
 }finally{[WorkspaceSmokeNative]::DeleteObject($region)|Out-Null;$back.Close();$back.Dispose()}
}
function Test-GlassTransmission([string]$Name) {
 $r=[WorkspaceSmokeNative+Rect]::new();[WorkspaceSmokeNative]::GetWindowRect($handle,[ref]$r)|Out-Null
 $w=$r.Right-$r.Left;$h=$r.Bottom-$r.Top
 $back=[Windows.Forms.Form]::new();$back.FormBorderStyle='None';$back.StartPosition='Manual';$back.ShowInTaskbar=$false;$back.TopMost=$true
 $back.Bounds=[Drawing.Rectangle]::new($r.Left,$r.Top,$w,$h)
 $signals=@()
 try{
  foreach($color in @([Drawing.Color]::FromArgb(240,40,60),[Drawing.Color]::FromArgb(30,70,235))){
   $back.BackColor=$color;$back.Show();[Windows.Forms.Application]::DoEvents()
   [WorkspaceSmokeNative]::SetWindowPos($handle,[IntPtr](-1),0,0,0,0,0x13)|Out-Null
   Start-Sleep -Milliseconds 700
   $bitmap=[Drawing.Bitmap]::new($w,$h);$g=[Drawing.Graphics]::FromImage($bitmap)
   try{
    $g.CopyFromScreen($r.Left,$r.Top,0,0,[Drawing.Size]::new($w,$h))
    $bitmap.Save((Join-Path $qaDirectory ('glass-'+$Name+'-'+$signals.Count+'.png')))
    $signal=0
    for($x=[int]($w/2)-10;$x -lt [int]($w/2)+10;$x++){for($y=8;$y -lt 16;$y++){$pixel=$bitmap.GetPixel($x,$y);$signal+=$pixel.R-$pixel.B}}
    $signals+=($signal/160)
   }finally{$g.Dispose();$bitmap.Dispose()}
  }
  $difference=$signals[0]-$signals[1]
  Write-Output ('Glass background response '+$Name+': '+[Math]::Round($difference,1))
  if($difference -lt 8){throw ('Glass does not transmit the background, or composition capture is unavailable: '+$Name)}
 }finally{$back.Close();$back.Dispose()}
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
   if($AppCaptureOnly){$dc=$g.GetHdc();try{[WorkspaceSmokeNative]::PrintWindow($handle,$dc,2)|Out-Null}finally{$g.ReleaseHdc($dc)}}
   else{$g.CopyFromScreen($r.Left,$r.Top,0,0,[Drawing.Size]::new($w,$h))}
   $bitmap.Save((Join-Path $qaDirectory $(if($AppCaptureOnly){'light-window.png'}else{'light-desktop.png'})))
   $brightness=0
   for($x=[int]($w/2)-10;$x -lt [int]($w/2)+10;$x++){for($y=8;$y -lt 16;$y++){$pixel=$bitmap.GetPixel($x,$y);$brightness+=($pixel.R+$pixel.G+$pixel.B)/3}}
   $brightness/=160
   Write-Output ('Light surface '+$(if($AppCaptureOnly){'in window capture'}else{'over a black background'})+': '+[Math]::Round($brightness,1)+'/255')
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
 if($NotchReference){
  Select-Workspace 'Theme' 'Dark';Invoke 'Collapse Nagi';[WorkspaceSmokeNative]::SetCursorPos(10,10)|Out-Null;Start-Sleep -Milliseconds 1200
  Test-NotchReference
  if($ReferenceOnly){Write-Output ('PASS: notch reference. Profile: '+$qaDirectory);return}
  Invoke 'Reveal Nagi';Invoke 'Open Nagi control center';Invoke 'Settings';Invoke 'Appearance & dock settings'
 }
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
  if($GlassTransmission){Test-GlassTransmission $theme}
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
 Select-Workspace 'Theme' 'Dark'
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
