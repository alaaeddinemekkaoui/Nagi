param([Parameter(Mandatory)][string]$ExecutablePath,[string]$ProductionExecutablePath='',[switch]$MediaAppearance,[switch]$SkipUiChecks)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class NagiDevelopmentSmokeNative {
 [StructLayout(LayoutKind.Sequential)]public struct Rect{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
}
'@
$qaRoot=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/development-'+(Get-Date -Format yyyyMMdd-HHmmss))
[IO.Directory]::CreateDirectory($qaRoot)|Out-Null
$qaProcess=$null;$productionProcess=$null;$root=$null
function Find([string]$id){$item=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$id));if(!$item){throw ('Missing '+$id)};return $item}
function Invoke([string]$id){(Find $id).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 400}
function Select-DevelopmentColor([string]$value){
 $box=Find 'VisualizerColorMode';$box.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 150
 $choice=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$value),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true)))
 if(!$choice){throw ('Missing visualizer color mode '+$value)};$choice.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 500
}
function Wait-Window([Diagnostics.Process]$process){
 for($attempt=0;$attempt -lt 150;$attempt++){
  if($process.HasExited){throw ('App exited with '+$process.ExitCode)}
  $window=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id))|Where-Object {$_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1
  if($window){return $window};Start-Sleep -Milliseconds 100
 };throw 'App window did not open'
}
function Start-App([string]$path,[string]$data,[string[]]$extra){
 $start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($path));$start.UseShellExecute=$false
 $start.ArgumentList.Add('--data-dir');$start.ArgumentList.Add($data)
 foreach($argument in $extra){$start.ArgumentList.Add($argument)}
 return [Diagnostics.Process]::Start($start)
}
function Snapshot([string]$name){
 $r=[NagiDevelopmentSmokeNative+Rect]::new();[NagiDevelopmentSmokeNative]::GetWindowRect([IntPtr]$root.Current.NativeWindowHandle,[ref]$r)|Out-Null
 $bitmap=[Drawing.Bitmap]::new($r.Right-$r.Left,$r.Bottom-$r.Top);$graphics=[Drawing.Graphics]::FromImage($bitmap);$dc=$graphics.GetHdc()
 try{[NagiDevelopmentSmokeNative]::PrintWindow([IntPtr]$root.Current.NativeWindowHandle,$dc,2)|Out-Null}finally{$graphics.ReleaseHdc($dc)}
 try{$bitmap.Save((Join-Path $qaRoot ($name+'.png')))}finally{$graphics.Dispose();$bitmap.Dispose()}
}
try{
 $launchArguments=@('--dev-page','Developer tools');if(!$SkipUiChecks){$launchArguments+='--dev-checks'}
 $qaProcess=Start-App $ExecutablePath $qaRoot $launchArguments
 $root=Wait-Window $qaProcess
 for($attempt=0;!$SkipUiChecks -and $attempt -lt 1200 -and !(Test-Path -LiteralPath (Join-Path $qaRoot 'diagnostics/ui-checks.json'));$attempt++){
  if($qaProcess.HasExited){throw ('Development app exited during automatic checks: '+$qaProcess.ExitCode)};Start-Sleep -Milliseconds 100
 }
 $report=if($SkipUiChecks){@{Passed=$true;Checks=@()}}else{Get-Content -LiteralPath (Join-Path $qaRoot 'diagnostics/ui-checks.json') -Raw|ConvertFrom-Json}
 if(!$report.Passed){$report.Checks|Where-Object {!$_.Passed}|Format-Table|Out-String|Write-Output;throw 'Built-in UI checks failed'}
 Start-Sleep -Milliseconds 800
 Find 'DevelopmentRunChecks'|Out-Null;Snapshot 'developer-menu'
 Invoke 'DevelopmentExport'
 $state=Get-Content -LiteralPath (Join-Path $qaRoot 'diagnostics/state.json') -Raw|ConvertFrom-Json
 if($state.Channel -ne 'Development' -or !$state.ApplicationLog){throw 'Missing development diagnostics'}
 Invoke 'DevelopmentTestError'
 if((Get-Content -LiteralPath (Join-Path $qaRoot 'logs/errors.jsonl') -Raw) -notlike '*Developer test error*'){throw 'Separate error log did not record intentional test error'}
 if($MediaAppearance){
  Invoke 'DevelopmentPage12';Select-DevelopmentColor 'Custom';Invoke 'ChooseVisualizerColor'
  $edits=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Edit))
  $hex=$edits|Where-Object {$_.Current.AutomationId -match 'Hex' -or $_.Current.Name -match 'Hex'}|Select-Object -First 1
  if(!$hex){$edits|ForEach-Object {@{Id=$_.Current.AutomationId;Name=$_.Current.Name}}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $qaRoot 'color-picker-tree.json');throw 'Missing color picker hex input'}
  $hex.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue('#39CCAB');Start-Sleep -Milliseconds 350
  Invoke 'PrimaryButton'
  for($attempt=0;$attempt -lt 40;$attempt++){
   $picker=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'VisualizerColorPicker'))
   if(!$picker){break};Start-Sleep -Milliseconds 100
  }
  Start-Sleep -Milliseconds 200
  $preferences=Get-Content -LiteralPath (Join-Path $qaRoot 'settings.json') -Raw|ConvertFrom-Json
  if($preferences.VisualizerColorMode -ne 'Custom' -or $preferences.VisualizerColor -ne '#39CCAB'){throw 'Color picker did not save the chosen visualizer color'}
  for($attempt=0;$attempt -lt 30;$attempt++){
   $preview=Find 'VisualizerColorPreview';$previewRectangle=$preview.Current.BoundingRectangle
   if($preview.Current.HelpText -like '*#39CCAB*' -and !$preview.Current.IsOffscreen -and [double]::IsFinite($previewRectangle.Left) -and $previewRectangle.Width -gt 0){break};Start-Sleep -Milliseconds 100
  }
  if($preview.Current.HelpText -notlike '*#39CCAB*'){throw ('Visualizer preview did not use the saved custom color: '+$preview.Current.HelpText)}
  if($preview.Current.IsOffscreen -or ![double]::IsFinite($preview.Current.BoundingRectangle.Left)){throw ('Visualizer bounds are unavailable: '+$preview.Current.BoundingRectangle)}
  Snapshot 'custom-visualizer-color'
  $capture=[Drawing.Bitmap]::FromFile((Join-Path $qaRoot 'custom-visualizer-color.png'))
  try{
   $windowBounds=[NagiDevelopmentSmokeNative+Rect]::new();[NagiDevelopmentSmokeNative]::GetWindowRect([IntPtr]$root.Current.NativeWindowHandle,[ref]$windowBounds)|Out-Null
   $previewBounds=$preview.Current.BoundingRectangle;$scale=$previewBounds.Width/92
   $left=[int][Math]::Round($previewBounds.Left-$windowBounds.Left);$top=[int][Math]::Round($previewBounds.Top-$windowBounds.Top)
   $barWidth=($previewBounds.Width-8*$scale)/5
   $middle=$capture.GetPixel([int]($left+$barWidth/2),[int]($top+$previewBounds.Height/2))
   $corner=$capture.GetPixel($left+1,[int][Math]::Ceiling($top+$previewBounds.Height*(1-[Math]::Sqrt(.3))/2)+1)
   if($middle.G-$middle.R -lt 25){throw 'Rendered visualizer does not show the chosen green color'}
   if(($middle.G-$middle.R)-($corner.G-$corner.R) -lt 20){throw 'Visualizer bar corners are square or have a visible backing'}
  }finally{$capture.Dispose()}
  Select-DevelopmentColor 'System';Snapshot 'system-visualizer-color'
  Write-Output 'PASS: color picker saves custom color; rendered bars have rounded transparent corners; System color can be restored.'
  $back=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Back to settings'))
  $back.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 400
  Invoke 'SettingsCategoryDevelopertools'
 }
 Invoke 'DevelopmentEdge2';Start-Sleep -Milliseconds 1000
 $notice=Find 'DevelopmentNotchNotification'
 if($notice.Current.IsOffscreen){throw 'Test notch notification is hidden'};Snapshot 'notch-notification-left'
 if($ProductionExecutablePath){
  $productionData=Join-Path $qaRoot 'production';[IO.Directory]::CreateDirectory($productionData)|Out-Null
  @{WelcomeCompleted=$true;StartExpanded=$true;Language='en';AutomaticUpdates=$false;IndexFiles=$false;ObsEnabled=$false;FpsEnabled=$false;WeatherEnabled=$false;CloseOnDeactivate=$false;HideInFullscreen=$false;Hotkey=@{Modifiers=7;Key=131};HideHotkey=@{Modifiers=7;Key=132};SearchHotkey=@{Modifiers=7;Key=133}}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $productionData 'settings.json') -Encoding utf8
  $productionProcess=Start-App $ProductionExecutablePath $productionData @();$productionRoot=Wait-Window $productionProcess
  Start-Sleep -Milliseconds 1000
  $settings=$productionRoot.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Settings'))
  $settings.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 500
  $debug=$productionRoot.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'SettingsCategoryDevelopertools'))
  if($debug){throw 'Development tools leaked into the production menu'}
  $binary=[Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes((Join-Path (Split-Path ([IO.Path]::GetFullPath($ProductionExecutablePath)) -Parent) 'Nagi.dll')))
  if($binary.Contains('BuildDevelopmentSettings') -or $binary.Contains('RunDevelopmentChecksAsync')){throw 'Development implementation was included in the production assembly'}
 }
 Write-Output ('PASS: '+$report.Checks.Count+' built-in checks, menu, state export, error log, notch notification and production exclusion when supplied. '+$qaRoot)
}finally{
 foreach($process in @($qaProcess,$productionProcess)){
  if($process -and !$process.HasExited){
   $windows=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id))
   foreach($window in $windows){[NagiDevelopmentSmokeNative]::PostMessage([IntPtr]$window.Current.NativeWindowHandle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null}
   if(!$process.WaitForExit(5000)){Stop-Process -Id $process.Id -Force}
  }
 }
}
