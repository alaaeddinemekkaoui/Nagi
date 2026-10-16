param([Parameter(Mandatory)][int]$PulseProcessId)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Smoke-PulseEdge.ps1') -PulseProcessId $PulseProcessId -Screenshots -Sides @()
function Select-NagiLayout([string]$Name,[string]$Value){
 $nagiControl=$pulseRoot.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$Name),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ComboBox)))
 $nagiScroll=(Find-EdgeControl 'ContentScroll' -Id).GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern)
 $nagiScroll.SetScrollPercent(-1,0)
 for($nagiAttempt=0;$nagiControl.Current.IsOffscreen -and $nagiAttempt -lt 30;$nagiAttempt++){$nagiScroll.Scroll([Windows.Automation.ScrollAmount]::NoAmount,[Windows.Automation.ScrollAmount]::SmallIncrement);Start-Sleep -Milliseconds 50}
 $nagiControl.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 200
 $nagiChoice=$pulseRoot.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$Value),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true)))
 if(!$nagiChoice){throw ('Missing layout choice '+$Value)};$nagiChoice.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 900
}
function Wait-NagiHeight([double]$Dips){
 for($nagiAttempt=0;$nagiAttempt -lt 40;$nagiAttempt++){$nagiRect=Get-EdgeRect;$nagiScale=[PulseEdgeNative]::GetDpiForWindow($pulseHandle)/96.0;if([Math]::Abs(($nagiRect.Bottom-$nagiRect.Top)/$nagiScale-$Dips) -lt 2){Start-Sleep -Milliseconds 200;return};Start-Sleep -Milliseconds 100};throw ('Expected height '+$Dips+' DIP; got '+(($nagiRect.Bottom-$nagiRect.Top)/$nagiScale))
}
foreach($nagiSide in @('Top edge','Bottom edge')){
 $nagiActivation=[Threading.EventWaitHandle]::OpenExisting('Local\Pulse.Desktop.Activate.v1');$nagiActivation.Set()|Out-Null;$nagiActivation.Dispose();Start-Sleep -Milliseconds 700
 Invoke-EdgeControl 'Open settings'
 Select-NagiLayout 'Dock layout' 'Horizontal';Select-NagiLayout 'Nagi position' $nagiSide
 Invoke-EdgeControl 'Collapse Nagi';[PulseEdgeNative]::SetPhysicalCursorPos(400,250)|Out-Null;Wait-NagiHeight 18
 $nagiRect=Get-EdgeRect;$nagiMonitor=[PulseEdgeNative+Monitor]::new();$nagiMonitor.Size=[Runtime.InteropServices.Marshal]::SizeOf($nagiMonitor);[PulseEdgeNative]::GetMonitorInfo([PulseEdgeNative]::MonitorFromWindow($pulseHandle,2),[ref]$nagiMonitor)|Out-Null
 if(($nagiSide -eq 'Top edge' -and $nagiRect.Top -ne $nagiMonitor.Work.Top) -or ($nagiSide -eq 'Bottom edge' -and $nagiRect.Bottom -ne $nagiMonitor.Work.Bottom)){throw 'Horizontal drop is not attached to its work-area edge.'}
 Save-EdgeImage ('drop-'+$nagiSide.Split(' ')[0].ToLower())
 Move-EdgePointer (Find-EdgeControl 'EdgeDropButton' -Id);Wait-NagiHeight 76
 Invoke-EdgeControl 'Open Nagi control center';[PulseEdgeNative]::SetPhysicalCursorPos(400,250)|Out-Null;Start-Sleep -Milliseconds 700;Wait-NagiHeight 76
 Invoke-EdgeControl 'Open Nagi control center';Wait-NagiHeight 780
 Save-EdgeImage ('horizontal-'+$nagiSide.Split(' ')[0].ToLower()+'-panel')
 Invoke-EdgeControl 'Collapse Nagi';[PulseEdgeNative]::SetPhysicalCursorPos(400,250)|Out-Null;Wait-NagiHeight 18
 Move-EdgePointer (Find-EdgeControl 'EdgeDropButton' -Id);Wait-NagiHeight 76
 Move-EdgePointer (Find-EdgeControl 'CompactMediaButton' -Id);Wait-NagiHeight 400
 if((Find-EdgeControl 'Choose preview speaker').Current.IsOffscreen){throw 'Speaker selector is hidden in the media preview.'}
 Save-EdgeImage ('horizontal-'+$nagiSide.Split(' ')[0].ToLower()+'-music')
 Write-Output ($nagiSide+': attachment / drop / hover / pin / expand / media preview / speakers PASS')
}
$nagiActivation=[Threading.EventWaitHandle]::OpenExisting('Local\Pulse.Desktop.Activate.v1');$nagiActivation.Set()|Out-Null;$nagiActivation.Dispose();Start-Sleep -Milliseconds 700
Invoke-EdgeControl 'Open settings';Select-NagiLayout 'Dock layout' 'Vertical'
& (Join-Path $PSScriptRoot 'Smoke-PulseEdge.ps1') -PulseProcessId $PulseProcessId -Screenshots
