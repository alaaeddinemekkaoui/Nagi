param([Parameter(Mandatory)][int]$PulseProcessId)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PulseSmokeNative {
 [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
'@
$pulseProcess = Get-Process -Id $PulseProcessId
$pulseActivation = [System.Threading.EventWaitHandle]::OpenExisting('Local\Pulse.Desktop.Activate.v1')
$pulseActivation.Set() | Out-Null
$pulseActivation.Dispose()
Start-Sleep -Milliseconds 500
$pulseCondition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$PulseProcessId)
$pulseWindow = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$pulseCondition)
if ($null -eq $pulseWindow) { throw 'Pulse window not found.' }
function Get-PulseControl([string]$Name) {
    $pulseNameCondition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$Name)
    $pulseControl = $pulseWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$pulseNameCondition)
    if ($null -eq $pulseControl) {
        for ($pulseRetry=0; $pulseRetry -lt 10 -and $null -eq $pulseControl; $pulseRetry++) {
            Start-Sleep -Milliseconds 100
            $pulseControl = $pulseWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$pulseNameCondition)
        }
    }
    if ($null -eq $pulseControl) {
        $pulseScroll = $pulseWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'ContentScroll'))
        if ($null -ne $pulseScroll) {
            $pulsePattern=$pulseScroll.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
            for ($pulsePage=0; $pulsePage -lt 8 -and $null -eq $pulseControl; $pulsePage++) {
                $pulsePattern.Scroll([System.Windows.Automation.ScrollAmount]::NoAmount,[System.Windows.Automation.ScrollAmount]::LargeIncrement)
                Start-Sleep -Milliseconds 150
                $pulseControl = $pulseWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$pulseNameCondition)
            }
        }
    }
    if ($null -eq $pulseControl) { throw "Missing Pulse control: $Name" }
    return $pulseControl
}
function Invoke-PulseControl([string]$Name) {
    if($Name -eq 'Open Nagi control center') {
        $pulseDrop=$pulseWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Reveal Nagi'))
        if($null -ne $pulseDrop -and !$pulseDrop.Current.IsOffscreen){$pulseDrop.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 1100}
    }
    $pulseControl = Get-PulseControl $Name
    $pulseControl.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 1100
    if($Name -eq 'Open Nagi control center') {
        $pulseOverview=$pulseWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'OverviewTab'))
        if($null -eq $pulseOverview -or $pulseOverview.Current.IsOffscreen){(Get-PulseControl $Name).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 1100}
    }
}
function Get-PulseWidth { return $pulseWindow.Current.BoundingRectangle.Width }
Invoke-PulseControl 'Collapse Nagi'
$pulseCompactWidth = Get-PulseWidth
Invoke-PulseControl 'Open Nagi control center'
if ((Get-PulseWidth) -le $pulseCompactWidth) { throw 'Halo did not expand.' }
Invoke-PulseControl 'Profiles'
Get-PulseControl 'Application rules' | Out-Null
Invoke-PulseControl '+ Add application rule'
Get-PulseControl 'Process name or full executable path' | Out-Null
Invoke-PulseControl 'Cancel'
Invoke-PulseControl '+ Add quick action'
Get-PulseControl 'Target' | Out-Null
Invoke-PulseControl 'Cancel'
Invoke-PulseControl 'Settings'
Get-PulseControl 'Integrations & diagnostics' | Out-Null
Invoke-PulseControl 'Change global shortcut · Ctrl + Alt + Space'
Get-PulseControl 'Shortcut key' | Out-Null
Invoke-PulseControl 'Cancel'
Invoke-PulseControl 'Overview'
Invoke-PulseControl 'Focus'
if ((Get-PulseWidth) -ne $pulseCompactWidth) { throw 'Focus did not collapse Halo.' }
Invoke-PulseControl 'Open Nagi control center'
Invoke-PulseControl 'Work'
$pulseProfilePicker = Get-PulseControl 'Activity profile'
$pulseProfilePicker.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
Start-Sleep -Milliseconds 200
$pulseAutomatic = Get-PulseControl 'Automatic'
$pulseAutomatic.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Start-Sleep -Milliseconds 750
# Test the real default registered global shortcut, without sending media or volume commands.
[PulseSmokeNative]::keybd_event(0x11,0,0,[UIntPtr]::Zero)
[PulseSmokeNative]::keybd_event(0x12,0,0,[UIntPtr]::Zero)
[PulseSmokeNative]::keybd_event(0x20,0,0,[UIntPtr]::Zero)
[PulseSmokeNative]::keybd_event(0x20,0,2,[UIntPtr]::Zero)
[PulseSmokeNative]::keybd_event(0x12,0,2,[UIntPtr]::Zero)
[PulseSmokeNative]::keybd_event(0x11,0,2,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 750
if ((Get-PulseWidth) -ne $pulseCompactWidth) { throw 'Global shortcut did not collapse Halo.' }
Invoke-PulseControl 'Open Nagi control center'
# A second launch must exit promptly and preserve the original process.
$pulseSecond = Start-Process -FilePath $pulseProcess.Path -WindowStyle Hidden -PassThru
if (!$pulseSecond.WaitForExit(10000)) { throw 'Second instance did not exit.' }
if ((Get-Process -Id $PulseProcessId).HasExited) { throw 'Original Pulse instance exited.' }
[pscustomobject]@{Expansion='Passed';Profiles='Passed';RuleAndActionDialogs='Passed';Settings='Passed';HotkeyDialog='Passed';GlobalHotkey='Passed';SingleInstance='Passed';ProcessId=$PulseProcessId} | ConvertTo-Json
