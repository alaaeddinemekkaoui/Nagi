param([Parameter(Mandatory)][string]$ExecutablePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
$qaDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/windows-copy-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$qaSource=Join-Path $qaDirectory 'source';$qaDestination=Join-Path $qaDirectory 'destination'
New-Item -ItemType Directory -Path $qaDirectory,$qaSource,$qaDestination|Out-Null
$qaFiles=@();$qaApp=$null;$qaCopy=$null;$qaPointer=[System.Windows.Forms.Cursor]::Position
try {
 for($i=0;$i -lt 512;$i++){$path=Join-Path $qaSource ('owned-copy-test-'+$i+'.bin');$stream=[IO.File]::Create($path);try{$stream.SetLength(2MB)}finally{$stream.Dispose()};$qaFiles+=$path}
 @{Language='en';WelcomeCompleted=$true;StartExpanded=$false;Theme='Dark';LockedProfile='Default';AutomaticSwitching=$false;
  CloseOnDeactivate=$true;HideInFullscreen=$false;AutomaticUpdates=$false;IndexFiles=$false;FpsEnabled=$false;ObsEnabled=$false;WeatherEnabled=$false;ReducedMotion=$true;LiveMedia=$false;
  WindowsTransfersEnabled=$true;WindowsNotificationBanners=$false;Hotkey=@{Modifiers=15;Key=134};HideHotkey=@{Modifiers=15;Key=135};SearchHotkey=@{Modifiers=15;Key=136}}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $qaDirectory 'settings.json') -Encoding utf8
 $start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($ExecutablePath));$start.UseShellExecute=$false;$start.ArgumentList.Add('--data-dir');$start.ArgumentList.Add($qaDirectory)
 $qaApp=[Diagnostics.Process]::Start($start);Start-Sleep -Seconds 3
 [System.Windows.Forms.Cursor]::Position=[Drawing.Point]::new(5,500)
 $helper=Join-Path $qaDirectory 'copy-with-windows.ps1'
 'param([string]$Source,[string]$Destination);$ErrorActionPreference="Stop";$shell=New-Object -ComObject Shell.Application;$shell.NameSpace($Destination).CopyHere($shell.NameSpace($Source).Items(),16);Start-Sleep -Seconds 10'|Set-Content -LiteralPath $helper -Encoding utf8
 $copyStart=[Diagnostics.ProcessStartInfo]::new('powershell.exe');$copyStart.UseShellExecute=$false;$copyStart.CreateNoWindow=$true;$copyStart.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
 foreach($argument in @('-STA','-NoProfile','-File',$helper,'-Source',$qaSource,'-Destination',$qaDestination)){$copyStart.ArgumentList.Add($argument)}
 $qaCopy=[Diagnostics.Process]::Start($copyStart)
 $observed=@();$labels=[Collections.Generic.HashSet[string]]::new();$dialogs=[Collections.Generic.HashSet[string]]::new();$savedTree=$false;$watch=[Diagnostics.Stopwatch]::StartNew()
 while($watch.Elapsed.TotalSeconds -lt 90){
  if($qaApp.HasExited){throw 'Nagi exited during Windows copy'}
  $all=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.Condition]::TrueCondition)
  foreach($entry in $all){if($entry.Current.ClassName -like '*OperationStatus*'){$p=Get-Process -Id $entry.Current.ProcessId -ErrorAction SilentlyContinue;$dialogs.Add($entry.Current.ClassName+':'+$p.ProcessName)|Out-Null
   if(!$savedTree -and $entry.Current.ProcessId -eq $qaCopy.Id){$savedTree=$true;$nodes=$entry.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition);@(@{Name=$entry.Current.Name;Id=$entry.Current.AutomationId;Class=$entry.Current.ClassName;Type=$entry.Current.ControlType.Id})+@($nodes|ForEach-Object {@{Name=$_.Current.Name;Id=$_.Current.AutomationId;Class=$_.Current.ClassName;Type=$_.Current.ControlType.Id;Offscreen=$_.Current.IsOffscreen}})|ConvertTo-Json -Depth 5|Set-Content (Join-Path $qaDirectory 'owned-shell-tree.json') -Encoding utf8}
  }}
  $roots=$all|Where-Object {$_.Current.ProcessId -eq $qaApp.Id}
  foreach($root in $roots){
   $label=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,'NotchNotificationText'))
   if($label){$labels.Add($label.Current.Name+':offscreen='+$label.Current.IsOffscreen)|Out-Null;if(!$label.Current.IsOffscreen -and $label.Current.Name -like 'Windows · *%'){$observed+=$label.Current.Name}}
  }
  if((Get-ChildItem -LiteralPath $qaDestination -File).Count -eq 512 -and $watch.Elapsed.TotalSeconds -gt 8){break}
  Start-Sleep -Milliseconds 100
 }
 if($observed.Count -eq 0){throw ('No real Windows transfer progress was observed in the resting notch. Dialogs: '+($dialogs -join ', ')+' Labels: '+($labels -join ', ')+' Evidence: '+$qaDirectory)}
 foreach($path in $qaFiles){$copy=Join-Path $qaDestination (Split-Path $path -Leaf);if((Get-Item -LiteralPath $path).Length -ne 2MB -or (Get-Item -LiteralPath $copy).Length -ne 2MB){throw 'Windows copy did not finish'}}
 @{Passed=$true;Source='Windows Shell CopyHere';NotchSamples=$observed;Files=512;Bytes=1GB;Dialogs=@($dialogs);Data=$qaDirectory}|ConvertTo-Json|Set-Content (Join-Path $qaDirectory 'result.json')
 Get-Content (Join-Path $qaDirectory 'result.json')
}finally{
 [System.Windows.Forms.Cursor]::Position=$qaPointer
 if($qaApp -and !$qaApp.HasExited){$qaApp.CloseMainWindow()|Out-Null;if(!$qaApp.WaitForExit(5000)){$qaApp.Kill()}}
 if($qaCopy -and !$qaCopy.HasExited){if(!$qaCopy.WaitForExit(15000)){$qaCopy.Kill()}}
 # Delete only the owned fixture files; preserve logs, preferences and result evidence.
 foreach($path in $qaFiles){foreach($candidate in @($path,(Join-Path $qaDestination (Split-Path $path -Leaf)))){
  if(Test-Path -LiteralPath $candidate){try{Remove-Item -LiteralPath $candidate -Force}catch{Write-Warning 'A test copy is still in use; its fixture was retained.'}}
 }}
}
