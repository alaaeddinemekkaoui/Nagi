param([Parameter(Mandatory)][string]$ExecutablePath,[switch]$SkipWeather,[int]$ObsPort=0,[string]$ObsPassword='')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
if(-not ('NagiWelcomeNative' -as [type])){Add-Type @'
using System;using System.Runtime.InteropServices;
public static class NagiWelcomeNative {
 [StructLayout(LayoutKind.Sequential)]public struct Rect {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")]public static extern bool SetPhysicalCursorPos(int x,int y);
 [DllImport("user32.dll")]public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
}
'@}
[NagiWelcomeNative]::SetProcessDpiAwarenessContext([IntPtr](-4))|Out-Null
$nagiProcess=Start-Process -FilePath $ExecutablePath -PassThru
function Get-NagiRoot {
 for($nagiAttempt=0;$nagiAttempt -lt 100;$nagiAttempt++){
  $nagiWindow=[Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$nagiProcess.Id))
  if($nagiWindow -and ($nagiWindow.Current.Name -eq 'Nagi' -or $nagiWindow.Current.Name -like '*Welcome*')){return $nagiWindow};Start-Sleep -Milliseconds 100
 };throw 'Nagi window did not appear.'
}
$script:nagiRoot=Get-NagiRoot
function Find-Nagi([string]$Value,[switch]$Name){
 $nagiProperty=if($Name){[Windows.Automation.AutomationElement]::NameProperty}else{[Windows.Automation.AutomationElement]::AutomationIdProperty}
 $nagiItem=$script:nagiRoot.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($nagiProperty,$Value))
 for($nagiWait=0;!$nagiItem -and $nagiWait -lt 50;$nagiWait++){Start-Sleep -Milliseconds 100;$nagiItem=$script:nagiRoot.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($nagiProperty,$Value))};if(!$nagiItem){throw ('Missing control '+$Value)};return $nagiItem
}
function Invoke-Nagi([string]$Value,[switch]$Name){(Find-Nagi $Value -Name:$Name).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 400}
function Set-NagiText([string]$Value,[string]$Text){(Find-Nagi $Value).GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($Text)}
function Select-Nagi([string]$Id,[string]$Value){
 $nagiCombo=Find-Nagi $Id
 $nagiCombo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 150
 $nagiChoice=$script:nagiRoot.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$Value),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true)))
 if(!$nagiChoice){throw ('Missing choice '+$Value)};$nagiChoice.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 250
}
function Save-NagiImage([string]$Name){
 if($script:nagiRoot.Current.Name -like '*Welcome*'){$nagiActivate=[Threading.EventWaitHandle]::OpenExisting('Local\Pulse.Desktop.Activate.v1');$nagiActivate.Set()|Out-Null;$nagiActivate.Dispose();Start-Sleep -Milliseconds 400}
 $nagiRect=[NagiWelcomeNative+Rect]::new();[NagiWelcomeNative]::GetWindowRect([IntPtr]$script:nagiRoot.Current.NativeWindowHandle,[ref]$nagiRect)|Out-Null
 $nagiBitmap=[Drawing.Bitmap]::new($nagiRect.Right-$nagiRect.Left,$nagiRect.Bottom-$nagiRect.Top);$nagiGraphics=[Drawing.Graphics]::FromImage($nagiBitmap)
 try{$nagiGraphics.CopyFromScreen($nagiRect.Left,$nagiRect.Top,0,0,$nagiBitmap.Size);$nagiFolder=Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/validation';[IO.Directory]::CreateDirectory($nagiFolder)|Out-Null;$nagiBitmap.Save((Join-Path $nagiFolder ($Name+'.png')))}finally{$nagiGraphics.Dispose();$nagiBitmap.Dispose()}
}
if($nagiRoot.Current.Name -notlike '*Welcome*'){throw 'Back up preferences and set WelcomeCompleted=false before running this development check.'}
Set-NagiText 'WelcomeName' ''
Invoke-Nagi 'WelcomeNext'
Find-Nagi 'WelcomeName'|Out-Null
Set-NagiText 'WelcomeName' 'Nagi QA'
Invoke-Nagi 'WelcomeNext'
Select-Nagi 'WelcomeTheme' 'Light'
Select-Nagi 'WelcomeTheme' 'Akeno'
Select-Nagi 'WelcomeOrientation' 'Horizontal'
Select-Nagi 'WelcomeEdge' 'Top edge'
Select-Nagi 'WelcomeEdge' 'Bottom edge'
Save-NagiImage 'welcome-theme-layout'
Invoke-Nagi 'WelcomeNext'
if($ObsPort -gt 0){
 # NumberBox exposes its editable TextBox as InputBox.
 $nagiPort=Find-Nagi 'WelcomeObsPort'
 $nagiInput=$nagiPort.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Edit))
 $nagiInput.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue([string]$ObsPort)
 (Find-Nagi 'WelcomeObsPassword').SetFocus();Set-NagiText 'WelcomeObsPassword' $ObsPassword
 Invoke-Nagi 'WelcomeObsConnect'
 for($nagiTry=0;$nagiTry -lt 100 -and !(Find-Nagi 'WelcomeNext').Current.IsEnabled;$nagiTry++){Start-Sleep -Milliseconds 100}
 if(!(Find-Nagi 'WelcomeNext').Current.IsEnabled){throw ('OBS did not connect: '+(Find-Nagi 'WelcomeObsStatus').Current.Name)}
 Write-Output 'Welcome OBS status connection: PASS';Save-NagiImage 'welcome-obs-connected';Invoke-Nagi 'WelcomeNext'
}else{
 Invoke-Nagi 'WelcomeObsConnect';Start-Sleep -Seconds 4
 if((Find-Nagi 'WelcomeNext').Current.IsEnabled){throw 'Expected disabled Continue with no local OBS fixture.'}
 Invoke-Nagi 'WelcomeSkip';Write-Output 'Welcome OBS unavailable / skip: PASS'
}
if($SkipWeather){Invoke-Nagi 'WelcomeSkip'}else{
 Set-NagiText 'WeatherCityQuery' 'Tokyo';Invoke-Nagi 'SearchWeatherCities'
 $nagiCities=Find-Nagi 'WeatherCities';$nagiCity=$null
 for($nagiTry=0;$nagiTry -lt 100;$nagiTry++){
  $nagiCity=$nagiCities.FindFirst([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true))
  if($nagiCity){break};Start-Sleep -Milliseconds 100
 }
 if(!$nagiCity){Save-NagiImage 'welcome-weather-failure';throw 'Live city lookup returned no selectable results.'}
 $nagiCity.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 200
 Save-NagiImage 'welcome-weather-city';Invoke-Nagi 'WelcomeNext';Write-Output 'Welcome live weather city selection: PASS'
}
Start-Sleep -Seconds 2;$script:nagiRoot=Get-NagiRoot
if($nagiRoot.Current.Name -ne 'Nagi'){throw 'Welcome did not open the dock.'}
$nagiHandle=[IntPtr]$nagiRoot.Current.NativeWindowHandle;$nagiScale=[NagiWelcomeNative]::GetDpiForWindow($nagiHandle)/96.0
[NagiWelcomeNative]::SetPhysicalCursorPos(400,250)|Out-Null;Start-Sleep -Seconds 1
$nagiRect=[NagiWelcomeNative+Rect]::new();[NagiWelcomeNative]::GetWindowRect($nagiHandle,[ref]$nagiRect)|Out-Null
if([Math]::Abs(($nagiRect.Bottom-$nagiRect.Top)/$nagiScale-18) -gt 2){throw 'Horizontal drop is not 18 DIP deep.'}
Save-NagiImage 'horizontal-water-drop'
[NagiWelcomeNative]::SetPhysicalCursorPos(($nagiRect.Left+$nagiRect.Right)/2,($nagiRect.Top+$nagiRect.Bottom)/2)|Out-Null;Start-Sleep -Seconds 2
[NagiWelcomeNative]::GetWindowRect($nagiHandle,[ref]$nagiRect)|Out-Null
if([Math]::Abs(($nagiRect.Bottom-$nagiRect.Top)/$nagiScale-76) -gt 2){throw 'Horizontal hover did not reveal the rail.'}
Save-NagiImage 'horizontal-rail'
$nagiEvent=[Threading.EventWaitHandle]::OpenExisting('Local\Pulse.Desktop.Activate.v1');$nagiEvent.Set()|Out-Null;$nagiEvent.Dispose();Start-Sleep -Seconds 1
Invoke-Nagi 'Choose speaker' -Name
Find-Nagi 'SpeakerDevices'|Out-Null
Save-NagiImage 'speaker-selector'
Invoke-Nagi 'Cancel' -Name
Save-NagiImage 'horizontal-overview'
$nagiSettings=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'Pulse/settings.json') -Raw | ConvertFrom-Json
if(!$nagiSettings.WelcomeCompleted -or $nagiSettings.UserName -ne 'Nagi QA' -or $nagiSettings.Position.Orientation -ne 'Horizontal'){throw 'Welcome preferences were not persisted.'}
Write-Output ('Welcome validation / theme / horizontal hover / speaker UI / persistence: PASS; process '+$nagiProcess.Id)
