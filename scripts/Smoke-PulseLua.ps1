param([Parameter(Mandatory)][int]$PulseProcessId,[switch]$Screenshots)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type 'using System;using System.Runtime.InteropServices;public static class PulseLuaDpi { [DllImport("user32.dll")]public static extern bool SetProcessDpiAwarenessContext(IntPtr context); }'
[PulseLuaDpi]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$pulseActivation = [Threading.EventWaitHandle]::OpenExisting('Local\Pulse.Desktop.Activate.v1')
$pulseActivation.Set() | Out-Null
$pulseActivation.Dispose()
Start-Sleep -Milliseconds 800
$pulseWindow = [Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Children,
    [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$PulseProcessId))
if ($null -eq $pulseWindow) { throw 'Pulse window not found.' }
function Find-LuaControl([string]$Name,[switch]$Id) {
    $pulseProperty = if ($Id) { [Windows.Automation.AutomationElement]::AutomationIdProperty } else { [Windows.Automation.AutomationElement]::NameProperty }
    $pulseFound = $pulseWindow.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($pulseProperty,$Name))
    return $pulseFound
}
function Get-LuaControl([string]$Name) {
    $pulseScroll = Find-LuaControl 'ContentScroll' -Id
    for ($pulseAttempt=0;$pulseAttempt -lt 15;$pulseAttempt++) {
        $pulseFound = Find-LuaControl $Name
        if ($null -ne $pulseFound -and !$pulseFound.Current.IsOffscreen) { return $pulseFound }
        if ($null -ne $pulseScroll) {
            $pulsePattern = $pulseScroll.GetCurrentPattern([Windows.Automation.ScrollPattern]::Pattern)
            $pulseNext = [Math]::Min(100, $pulseAttempt * 8)
            $pulsePattern.SetScrollPercent(-1, $pulseNext)
        }
        Start-Sleep -Milliseconds 120
    }
    throw ('Missing visible Lua control: '+$Name)
}
function Invoke-LuaControl([string]$Name) {
    (Get-LuaControl $Name).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500
}
function Set-LuaSource([string]$Source) {
    (Get-LuaControl 'Lua UI source').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($Source)
}
function Read-LuaText([string]$Name) {
    $pulseControl = Get-LuaControl $Name
    return $pulseControl.GetCurrentPattern([Windows.Automation.TextPattern]::Pattern).DocumentRange.GetText(-1)
}
function Wait-LuaStatus([string]$Expected) {
    for ($pulseAttempt=0;$pulseAttempt -lt 20;$pulseAttempt++) {
        $pulseText = Read-LuaText 'Lua UI status'
        if ($pulseText -like $Expected) { return $pulseText }
        Start-Sleep -Milliseconds 150
    }
    throw ('Unexpected Lua status: '+$pulseText)
}
function Save-LuaScreenshot([string]$Name) {
    if (!$Screenshots) { return }
    Add-Type -AssemblyName System.Drawing
    $pulseBounds = $pulseWindow.Current.BoundingRectangle
    $pulseBitmap = [Drawing.Bitmap]::new([int]$pulseBounds.Width,[int]$pulseBounds.Height)
    $pulseGraphics = [Drawing.Graphics]::FromImage($pulseBitmap)
    try {
        $pulseGraphics.CopyFromScreen([int]$pulseBounds.Left,[int]$pulseBounds.Top,0,0,$pulseBitmap.Size)
        $pulseDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\validation'
        [IO.Directory]::CreateDirectory($pulseDirectory) | Out-Null
        $pulseBitmap.Save((Join-Path $pulseDirectory ($Name+'.png')))
    } finally { $pulseGraphics.Dispose(); $pulseBitmap.Dispose() }
}
Invoke-LuaControl 'Open settings'
Invoke-LuaControl 'Edit Lua UI'
Set-LuaSource "return { name = 'Lua smoke', accent = '#78DFFF', card_radius = 24, spacing = 16, labels = { cpu = 'Lua processor' }, sections = { network = false } }"
Invoke-LuaControl 'Apply script'
Wait-LuaStatus 'Lua active*Lua smoke' | Out-Null
Invoke-LuaControl 'Overview'
if ($null -eq (Find-LuaControl 'Lua processor')) { throw 'Custom Lua processor label was not applied.' }
if ($null -ne (Find-LuaControl 'Network')) { throw 'Lua did not hide the overview network section.' }
Save-LuaScreenshot 'lua-custom-overview'
Write-Output 'Lua editor / apply / labels / section visibility: PASS'

Invoke-LuaControl 'Open settings'
Invoke-LuaControl 'Edit Lua UI'
Set-LuaSource 'while true do end'
Invoke-LuaControl 'Apply script'
if ((Read-LuaText 'Lua UI error') -notlike '*budget*') { throw 'The Lua instruction limit was not reported in the editor.' }
Invoke-LuaControl 'Cancel'
Wait-LuaStatus 'Lua active*Lua smoke' | Out-Null
Write-Output 'Lua invalid script / instruction limit / previous appearance retained: PASS'

$pulseScriptPath = Join-Path $env:LOCALAPPDATA 'Pulse\scripts\ui.lua'
[IO.File]::WriteAllText($pulseScriptPath,"return { name = 'Reload smoke', labels = { cpu = 'Reload processor' } }",[Text.UTF8Encoding]::new($false))
Invoke-LuaControl 'Reload Lua UI'
Wait-LuaStatus 'Lua active*Reload smoke' | Out-Null
Invoke-LuaControl 'Overview'
if ($null -eq (Find-LuaControl 'Reload processor')) { throw 'External script reload did not update the label.' }
Write-Output 'Lua external file / reload: PASS'

Invoke-LuaControl 'Open settings'
[IO.File]::WriteAllText($pulseScriptPath,'return { spacing = -1 }',[Text.UTF8Encoding]::new($false))
Invoke-LuaControl 'Reload Lua UI'
Wait-LuaStatus 'Lua error*spacing*' | Out-Null
Invoke-LuaControl 'Overview'
if ($null -eq (Find-LuaControl 'Reload processor')) { throw 'Bad reload discarded the previous working appearance.' }
Write-Output 'Lua invalid external reload / previous appearance retained: PASS'

Invoke-LuaControl 'Open settings'
$pulseToggle = Get-LuaControl 'Enable Lua UI On'
$pulseToggle.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
Start-Sleep -Milliseconds 500
Wait-LuaStatus 'Built-in appearance' | Out-Null
Invoke-LuaControl 'Overview'
if ($null -eq (Find-LuaControl 'Processor')) { throw 'Disabling Lua did not restore the default label.' }
if ($null -eq (Find-LuaControl 'Network')) { throw 'Disabling Lua did not restore the network section.' }
Write-Output 'Lua opt-out / built-in appearance restored: PASS'
