param([Parameter(Mandatory)][int]$PulseProcessId,[switch]$SkipEdge)
$ErrorActionPreference='Stop'
# Also validates attachment, battery, logo dragging and the tighter contour animation.
$pulseMediaSides=if($SkipEdge){@()}else{@('Right edge','Left edge')}
. (Join-Path $PSScriptRoot 'Smoke-PulseEdge.ps1') -PulseProcessId $PulseProcessId -Screenshots -Sides $pulseMediaSides
function Read-AudioFrames {
    $pulseNotes=$pulseRoot.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Text))
    foreach($pulseNote in $pulseNotes){if($pulseNote.Current.Name -match '^Audio refresh:.*Frames: (\d+)'){return [long]$Matches[1]}}
    throw 'Live audio diagnostics were not found.'
}
$pulseActivation=[Threading.EventWaitHandle]::OpenExisting('Local\Pulse.Desktop.Activate.v1')
$pulseActivation.Set()|Out-Null;$pulseActivation.Dispose();Start-Sleep -Milliseconds 700
Invoke-EdgeControl 'Open settings'
$pulseThemes=$pulseRoot.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Theme'))
$pulseTheme=$pulseThemes | Where-Object {$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ComboBox} | Select-Object -First 1
$pulseSelected=$pulseTheme.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()[0].Current.Name
if($pulseSelected -ne 'Akeno'){throw ('Expected Akeno theme; found '+$pulseSelected)}
$pulseBefore=Read-AudioFrames
Start-Sleep -Milliseconds 450
Invoke-EdgeControl 'Refresh diagnostics'
$pulseAfter=Read-AudioFrames
if($pulseAfter -lt $pulseBefore+3){throw 'Audio did not update independently while Settings was open.'}
Write-Output ('Akeno theme / live audio frames: PASS ('+($pulseAfter-$pulseBefore)+' frames)')
Invoke-EdgeControl 'Overview'
$pulseFullVolume=(Find-EdgeControl 'VolumeSlider' -Id).GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value
Save-EdgeImage 'akeno-overview'
Invoke-EdgeControl 'Collapse Nagi';Move-EdgeAway;Wait-EdgeWidth 18;Start-Sleep -Milliseconds 350
$pulsePointerBefore=[PulseEdgeNative+Point]::new();[PulseEdgeNative]::GetPhysicalCursorPos([ref]$pulsePointerBefore)|Out-Null
try {
 Move-EdgePointer (Find-EdgeControl 'EdgeDropButton' -Id);Wait-EdgeWidth 80;Start-Sleep -Milliseconds 500
 Move-EdgePointer (Find-EdgeControl 'CompactMediaButton' -Id);Wait-EdgeWidth 430;Start-Sleep -Milliseconds 350
 $pulsePeekVolume=Find-EdgeControl 'PeekVolumeSlider' -Id
 if($pulsePeekVolume.Current.IsOffscreen){throw 'Hover audio volume is not visible.'}
 $pulsePeekValue=$pulsePeekVolume.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value
 if([Math]::Abs($pulseFullVolume-$pulsePeekValue) -gt 1){throw 'Full and hover audio controls disagree.'}
 $pulseRect=Get-EdgeRect
 $pulseMonitor=[PulseEdgeNative+Monitor]::new();$pulseMonitor.Size=[Runtime.InteropServices.Marshal]::SizeOf($pulseMonitor)
 [PulseEdgeNative]::GetMonitorInfo([PulseEdgeNative]::MonitorFromWindow($pulseHandle,2),[ref]$pulseMonitor)|Out-Null
 if($pulseRect.Top -lt $pulseMonitor.Work.Top -or $pulseRect.Bottom -gt $pulseMonitor.Work.Bottom){throw 'Media preview extends beyond the screen work area.'}
 Save-EdgeImage 'akeno-media-audio'
 Write-Output ('Media / audio hover / synchronized volume / screen fit: PASS ('+$pulsePeekValue+'%)')
} catch { Save-EdgeImage 'vertical-media-failure'; Write-Output ('Preview width at failure: '+((Get-EdgeRect).Right-(Get-EdgeRect).Left)); throw } finally {[PulseEdgeNative]::SetPhysicalCursorPos($pulsePointerBefore.X,$pulsePointerBefore.Y)|Out-Null}
