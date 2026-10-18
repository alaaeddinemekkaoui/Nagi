param([Parameter(Mandatory)][string]$ExecutablePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class NagiUiReviewNative {
 [StructLayout(LayoutKind.Sequential)]public struct Rect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)]public struct Point {public int X,Y;}
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]public static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
}
'@
$review=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/ui-review-'+(Get-Date -Format yyyyMMdd-HHmmss))
[IO.Directory]::CreateDirectory($review)|Out-Null
$pointer=[NagiUiReviewNative+Point]::new();[NagiUiReviewNative]::GetCursorPos([ref]$pointer)|Out-Null
$results=[Collections.Generic.List[object]]::new()
function Find([string]$id){
 $item=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id))
 if(!$item){throw "Missing $id"};return $item
}
function Invoke([string]$id,[int]$wait=210){(Find $id).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds $wait}
function Capture([string]$name){
 $rect=[NagiUiReviewNative+Rect]::new();[NagiUiReviewNative]::GetWindowRect($handle,[ref]$rect)|Out-Null
 $bitmap=[Drawing.Bitmap]::new($rect.Right-$rect.Left,$rect.Bottom-$rect.Top);$g=[Drawing.Graphics]::FromImage($bitmap);$dc=$g.GetHdc()
 try{[NagiUiReviewNative]::PrintWindow($handle,$dc,2)|Out-Null}finally{$g.ReleaseHdc($dc)}
 try{$bitmap.Save((Join-Path $review ($name+'.png')))}finally{$g.Dispose();$bitmap.Dispose()}
}
function Check-Layout([string]$page){
 $bounds=$root.Current.BoundingRectangle
 $elements=(Find 'ContentScroll').FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.OrCondition]::new(
  [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Button),
  [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ComboBox)))
 foreach($element in $elements){
  if($element.Current.IsOffscreen){continue};$box=$element.Current.BoundingRectangle
  if($box.Left -lt $bounds.Left-1 -or $box.Right -gt $bounds.Right+1){throw "$code $page has horizontal overflow: $($element.Current.Name)"}
 }
 $header=(Find 'BrandSubtitle').Current.BoundingRectangle
 if($header.Right -gt $bounds.Right+1 -or $header.Left -lt $bounds.Left-1){throw "$code header escapes the window"}
 if($page -notin @('Settings','Overview','Profiles')){
  $title=(Find 'SettingsCategoryTitle').Current.BoundingRectangle
  if($title.Width -le 0 -or $title.Right -gt $bounds.Right+1){throw "$code category heading is clipped"}
 }
}
try{
 foreach($code in @('en','fr','ru','ar')){
  $process=$null;$root=$null;$handle=[IntPtr]::Zero
  try{
   $data=Join-Path $review $code;[IO.Directory]::CreateDirectory($data)|Out-Null
   $theme=@{en='Akeno';fr='Light';ar='Dark';ru='Akeno'}[$code]
   @{Language=$code;WelcomeCompleted=$true;Theme=$theme;LockedProfile=$(if($code -eq 'fr'){'Stream'}else{'Work'});CloseOnDeactivate=$false;HideInFullscreen=$false;AutomaticUpdates=$false;IndexFiles=$false;FpsEnabled=$false;ObsEnabled=$false;WeatherEnabled=$false;ReducedMotion=$false;
     Position=@{Orientation='Horizontal';Anchor='Bottom edge'};Hotkey=@{Modifiers=15;Key=131};HideHotkey=@{Modifiers=15;Key=132};SearchHotkey=@{Modifiers=15;Key=133}}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $data 'settings.json') -Encoding utf8
   $start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($ExecutablePath));$start.UseShellExecute=$false;$start.ArgumentList.Add('--data-dir');$start.ArgumentList.Add($data)
   $process=[Diagnostics.Process]::Start($start)
   for($attempt=0;$attempt -lt 100 -and !$root;$attempt++){
    $root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id))|Where-Object {$_.Current.Name -eq 'Nagi' -and $_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1
    if(!$root){Start-Sleep -Milliseconds 100}
   }
   if(!$root){throw "No $code review window"};$handle=[IntPtr]$root.Current.NativeWindowHandle
   [NagiUiReviewNative]::SetCursorPos(10,10)|Out-Null
   Invoke 'EdgeDropButton';Invoke 'CompactBrand';Invoke 'PreferencesTab'
   foreach($size in @(@(640,460),@(960,720))){
    $scale=[NagiUiReviewNative]::GetDpiForWindow($handle)/96d
    [NagiUiReviewNative]::SetWindowPos($handle,[IntPtr]::Zero,0,0,[int]($size[0]*$scale),[int]($size[1]*$scale),0x16)|Out-Null
    Start-Sleep -Milliseconds 240;Invoke 'PreferencesTab';Check-Layout 'Settings'
    $tile=Find 'SettingsCategoryLanguage';if($tile.Current.IsOffscreen){throw 'Language is not immediately visible in Settings'}
    $initial=(Find 'BrandSubtitle').Current.BoundingRectangle
    $tileRect=$tile.Current.BoundingRectangle;[NagiUiReviewNative]::SetCursorPos([int]($tileRect.Left+20),[int]($tileRect.Top+20))|Out-Null;Start-Sleep -Milliseconds 180
    $hover=$tile.Current.BoundingRectangle
    if([Math]::Abs($hover.Left-$tileRect.Left) -gt 1 -or [Math]::Abs($hover.Width-$tileRect.Width) -gt 1){throw 'Hover shifts the language tile'}
    Capture "$code-$($size[0])-settings"
    foreach($category in @('Language','General','Appearancedock','Profilesapps','Weather','Hardwarestudio','Searchshortcuts','LuaUIstudio','Music','Updates','Behavior','Aboutdiagnostics')){
     Invoke ('SettingsCategory'+$category) 20
     foreach($delay in @(0,70,140)){
      if($delay){Start-Sleep -Milliseconds $delay};$current=(Find 'BrandSubtitle').Current.BoundingRectangle
      if([Math]::Abs($initial.Left-$current.Left) -gt 1 -or [Math]::Abs($initial.Top-$current.Top) -gt 1 -or [Math]::Abs($initial.Width-$current.Width) -gt 1){throw "$category navigation moves the header"}
     }
     Check-Layout $category
     if($category -eq 'Aboutdiagnostics'){
      if((Find 'AboutRedditCredit').Current.Name -notlike '*u/alkidnn*'){throw 'Reddit credit missing in About'}
      $pack=Get-Content -LiteralPath (Join-Path (Split-Path ([IO.Path]::GetFullPath($ExecutablePath))) "languages/$code.json") -Raw|ConvertFrom-Json -AsHashtable
      if((Find 'AboutAcknowledgment').Current.Name -ne $pack.strings['Thank you for the suggestions to improve Nagi.']){throw 'About acknowledgment is not translated'}
      if((Find 'AboutRedditCredit').Current.IsOffscreen){throw 'About credit is not immediately visible'}
      Capture "$code-$($size[0])-about"
     }
     if($category -eq 'Language'){
      Find 'AppLanguage'|Out-Null;Find 'RestartForLanguage'|Out-Null
      $pack=Get-Content -LiteralPath (Join-Path (Split-Path ([IO.Path]::GetFullPath($ExecutablePath))) "languages/$code.json") -Raw|ConvertFrom-Json -AsHashtable
      if(!(Find 'SettingsCurrentLanguage').Current.Name.Contains($pack.nativeName)){throw 'Current language is not shown'}
      Capture "$code-$($size[0])-language"
     }
     $results.Add(@{Language=$code;Width=$size[0];Category=$category;NoHorizontalOverflow=$true;StableHeader=$true})
     Invoke 'PreferencesTab'
    }
    foreach($view in @('Overview','Profiles')){
     Invoke $(if($view -eq 'Overview'){'OverviewTab'}else{'RulesTab'})
     Check-Layout $view;Capture "$code-$($size[0])-$view"
     $results.Add(@{Language=$code;Width=$size[0];Category=$view;NoHorizontalOverflow=$true;StableHeader=$true})
    }
    Invoke 'PreferencesTab'
   }
   Write-Output "PASS: $code settings, visible language entry, hover, 12 categories at 640/960 width and stable navigation"
  }finally{
   if($process -and !$process.HasExited){$process.CloseMainWindow()|Out-Null;if(!$process.WaitForExit(5000)){Stop-Process -Id $process.Id -Force}}
  }
  if((Get-Item (Join-Path $data 'logs/errors.jsonl')).Length -ne 0){throw "$code logged a UI error"}
 }
 $results|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $review 'evidence.json') -Encoding utf8
 Write-Output "PASS: $($results.Count) settings-page/size/language checks. $review"
}finally{[NagiUiReviewNative]::SetCursorPos($pointer.X,$pointer.Y)|Out-Null}
