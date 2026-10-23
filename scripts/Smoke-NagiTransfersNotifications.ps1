param([Parameter(Mandatory)][string]$ExecutablePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$qaDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/transfers-notifications-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
[IO.Directory]::CreateDirectory($qaDirectory)|Out-Null
$qaSource=Join-Path $qaDirectory 'a little file.txt';[IO.File]::WriteAllText($qaSource,'keep the original')
ConvertTo-Json -InputObject @($qaSource)|Set-Content (Join-Path $qaDirectory 'transfer-shelf.json') -Encoding utf8
@{Language='en';WelcomeCompleted=$true;StartExpanded=$true;Theme='Dark';LockedProfile='Default';AutomaticSwitching=$false;
 CloseOnDeactivate=$false;HideInFullscreen=$false;AutomaticUpdates=$false;IndexFiles=$false;FpsEnabled=$false;ObsEnabled=$false;WeatherEnabled=$false;ReducedMotion=$true;AlwaysShowMiniBar=$true;
 Hotkey=@{Modifiers=15;Key=131};HideHotkey=@{Modifiers=15;Key=132};SearchHotkey=@{Modifiers=15;Key=133}}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $qaDirectory 'settings.json') -Encoding utf8
$start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($ExecutablePath));$start.UseShellExecute=$false;$start.ArgumentList.Add('--data-dir');$start.ArgumentList.Add($qaDirectory)
$qaProcess=[Diagnostics.Process]::Start($start);$root=$null
function Find([string]$value,[switch]$Name){
 $property=if($Name){[Windows.Automation.AutomationElement]::NameProperty}else{[Windows.Automation.AutomationElement]::AutomationIdProperty}
 $item=$script:root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($property,$value))
 if(!$item){throw ('Missing '+$value)};return $item
}
function Invoke([string]$value,[switch]$Name){
 $item=Find $value -Name:$Name
 if($item.Current.IsOffscreen){try{$item.GetCurrentPattern([Windows.Automation.ScrollItemPattern]::Pattern).ScrollIntoView();Start-Sleep -Milliseconds 200}catch{}}
 $item.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 450
}
try {
 for($attempt=0;$attempt -lt 120;$attempt++){
  if($qaProcess.HasExited){throw 'Nagi exited during startup'}
  $root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))|Where-Object {$_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1
  if($root){break};Start-Sleep -Milliseconds 100
 };if(!$root){throw 'No test window'};Start-Sleep -Milliseconds 600
 Invoke 'UtilitiesTab';Find 'ShelfAddFiles'|Out-Null;Find 'ShelfCopyFiles'|Out-Null;Find 'a little file.txt' -Name|Out-Null
 Invoke 'Clear shelf' -Name
 if(!(Test-Path -LiteralPath $qaSource) -or [IO.File]::ReadAllText($qaSource) -ne 'keep the original'){throw 'Clearing the shelf changed the original'}
 $savedShelf=Get-Content (Join-Path $qaDirectory 'transfer-shelf.json') -Raw|ConvertFrom-Json -NoEnumerate
 if(@($savedShelf).Count -gt 0){throw 'Cleared shelf was not persisted'}
 Invoke 'Notification settings' -Name;Find 'ConnectWindowsNotifications'|Out-Null;Invoke 'ConnectWindowsNotifications'
 $saved=Get-Content (Join-Path $qaDirectory 'settings.json') -Raw|ConvertFrom-Json
 if($saved.WindowsNotificationsEnabled){throw 'Portable notification connection reported false success'}
 if(!$saved.WindowsNotificationBanners){throw 'Portable banner mirroring was not enabled'}
 $texts=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Text))
 if(!($texts|Where-Object {$_.Current.Name -like '*packaged*'})){throw 'Portable package requirement is not explained'}
 Invoke 'Disconnect Windows notifications' -Name
 $saved=Get-Content (Join-Path $qaDirectory 'settings.json') -Raw|ConvertFrom-Json
 if($saved.WindowsNotificationBanners -or $saved.WindowsNotificationsEnabled){throw 'Disconnect did not disable Windows observation'}
 Invoke 'UtilitiesTab';Find 'Clear Nagi alerts' -Name|Out-Null;Invoke 'Clear Nagi alerts' -Name
 @{Passed=$true;ShelfRestored=$true;ClearPreservesOriginal=$true;ShelfSaved=$true;NotificationCards=$true;PortablePermissionExplained=$true;Data=$qaDirectory}|ConvertTo-Json|Set-Content (Join-Path $qaDirectory 'result.json')
 Get-Content (Join-Path $qaDirectory 'result.json')
}finally{
 if($qaProcess -and !$qaProcess.HasExited){$qaProcess.CloseMainWindow()|Out-Null;if(!$qaProcess.WaitForExit(15000)){$qaProcess.Kill()}}
}
