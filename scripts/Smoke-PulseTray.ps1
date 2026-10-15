param([Parameter(Mandatory)][int]$PulseProcessId,[switch]$Quit,[switch]$Search)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class PulseTraySmokeNative {
 [DllImport("user32.dll")] public static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
 [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern bool SetPhysicalCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] public static extern int GetMenuItemCount(IntPtr menu);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetMenuString(IntPtr menu,uint index,System.Text.StringBuilder text,int capacity,uint flags);
 [DllImport("user32.dll")] public static extern bool GetMenuItemRect(IntPtr window,IntPtr menu,uint index,out Rect rect);
 [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
}
'@
[PulseTraySmokeNative]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$pulseOriginalCursor=[PulseTraySmokeNative+Point]::new()
[PulseTraySmokeNative]::GetCursorPos([ref]$pulseOriginalCursor) | Out-Null
function Get-PulseTrayIcon {
    for($pulseTry=0;$pulseTry -lt 4;$pulseTry++) {
        $pulseOverflow=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ClassNameProperty,'TopLevelWindowForOverflowXamlIsland'))
        if($null -ne $pulseOverflow) {
            $pulseIcon=@($pulseOverflow.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition) | Where-Object { $_.Current.Name.StartsWith('Nagi ·') -and !$_.Current.IsOffscreen -and $_.Current.BoundingRectangle.Width -gt 0 }) | Select-Object -First 1
            if($null -ne $pulseIcon) { return $pulseIcon }
        }
        $pulseTaskbar=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ClassNameProperty,'Shell_TrayWnd'))
        $pulseTaskbar.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Show Hidden Icons')).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 300
    }
    throw 'Visible Pulse tray icon not found. This smoke script expects the English Windows 11 tray overflow.'
}
function Invoke-PulseTrayCommand([string]$Name) {
    Write-Output ('Checking tray command: ' + $Name)
    $pulseIcon=Get-PulseTrayIcon
    $pulseBounds=$pulseIcon.Current.BoundingRectangle
    [PulseTraySmokeNative]::SetPhysicalCursorPos([int]($pulseBounds.X+$pulseBounds.Width/2),[int]($pulseBounds.Y+$pulseBounds.Height/2)) | Out-Null
    [PulseTraySmokeNative]::mouse_event(0x8,0,0,0,[UIntPtr]::Zero)
    [PulseTraySmokeNative]::mouse_event(0x10,0,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
    $pulseMenu=$null
    for($pulseMenuTry=0;$pulseMenuTry -lt 15 -and $null -eq $pulseMenu;$pulseMenuTry++) {
        $pulseMenu=@([System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.Condition]::TrueCondition) | Where-Object {$_.Current.ClassName -eq '#32768' -and $_.Current.ProcessId -eq $PulseProcessId}) | Select-Object -First 1
        if($null -eq $pulseMenu){Start-Sleep -Milliseconds 100}
    }
    if($null -eq $pulseMenu) {throw "Pulse tray context menu did not open for $Name."}
    $pulseItem=$pulseMenu.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$Name))
    if($null -ne $pulseItem) {$pulseItemBounds=$pulseItem.Current.BoundingRectangle}
    else {
        # Some Windows builds expose the popup window through UIA without its MSAA children.
        # Read the actual native popup labels and rectangles, then click the real menu item.
        $pulseNativeMenu=[PulseTraySmokeNative]::SendMessage([IntPtr]$pulseMenu.Current.NativeWindowHandle,0x1e1,[IntPtr]::Zero,[IntPtr]::Zero)
        $pulseItemBounds=$null
        for($pulseIndex=0;$pulseIndex -lt [PulseTraySmokeNative]::GetMenuItemCount($pulseNativeMenu);$pulseIndex++) {
            $pulseText=[Text.StringBuilder]::new(256)
            [PulseTraySmokeNative]::GetMenuString($pulseNativeMenu,$pulseIndex,$pulseText,256,0x400)|Out-Null
            if(($pulseText.ToString() -split "`t")[0] -eq $Name) {
                $pulseRect=[PulseTraySmokeNative+Rect]::new()
                if([PulseTraySmokeNative]::GetMenuItemRect([IntPtr]::Zero,$pulseNativeMenu,$pulseIndex,[ref]$pulseRect)) {
                    $pulseItemBounds=[pscustomobject]@{X=$pulseRect.Left;Y=$pulseRect.Top;Width=$pulseRect.Right-$pulseRect.Left;Height=$pulseRect.Bottom-$pulseRect.Top}
                }
                break
            }
        }
        if($null -eq $pulseItemBounds){throw "Missing tray command: $Name"}
    }
    [PulseTraySmokeNative]::SetPhysicalCursorPos([int]($pulseItemBounds.X+$pulseItemBounds.Width/2),[int]($pulseItemBounds.Y+$pulseItemBounds.Height/2)) | Out-Null
    [PulseTraySmokeNative]::mouse_event(0x2,0,0,0,[UIntPtr]::Zero)
    [PulseTraySmokeNative]::mouse_event(0x4,0,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 1100
}
try {
    if($Search) {
        Invoke-PulseTrayCommand 'Quick search'
        $pulseSearch=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.AndCondition]::new(
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$PulseProcessId),
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Nagi · Quick search')))
        if($null -eq $pulseSearch){throw 'Tray Quick search did not open the launcher.'}
        [PulseTraySmokeNative]::keybd_event(27,0,0,[UIntPtr]::Zero);[PulseTraySmokeNative]::keybd_event(27,0,2,[UIntPtr]::Zero)
        Start-Sleep -Milliseconds 400
    }
    Invoke-PulseTrayCommand 'Open Nagi'
    $pulseWindow=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.AndCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$PulseProcessId),
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Nagi')))
    $pulseHandle=[IntPtr]$pulseWindow.Current.NativeWindowHandle
    if(![PulseTraySmokeNative]::IsWindowVisible($pulseHandle)) {throw 'Tray Open Nagi did not reveal the window.'}
    Invoke-PulseTrayCommand 'Hide Nagi'
    if([PulseTraySmokeNative]::IsWindowVisible($pulseHandle)) {throw 'Tray Hide Nagi did not hide the window.'}
    Invoke-PulseTrayCommand 'Settings…'
    if(![PulseTraySmokeNative]::IsWindowVisible($pulseHandle)) {throw 'Tray Settings did not recover the hidden window.'}
    $pulseScroll=$pulseWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'ContentScroll'))
    $pulseScroll.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern).SetScrollPercent(-1,100)
    Start-Sleep -Milliseconds 250
    $pulseMaterial=@($pulseWindow.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition) | Where-Object {$_.Current.Name -like '*Glass material*'} | ForEach-Object {$_.Current.Name})
    if($Quit) {
        $pulseProcess=Get-Process -Id $PulseProcessId
        Invoke-PulseTrayCommand 'Quit Nagi'
        if(!$pulseProcess.WaitForExit(10000)) {throw 'Tray Quit did not stop Pulse.'}
    }
    [pscustomobject]@{TrayContextMenu='Passed';TraySearch=$(if($Search){'Passed'}else{'Not requested'});TrayShow='Passed';TrayHide='Passed';TraySettingsRecovery='Passed';TrayQuit=$(if($Quit){'Passed'}else{'Not requested'});MaterialDiagnostics=$pulseMaterial} | ConvertTo-Json -Depth 3
} finally {
    [PulseTraySmokeNative]::SetCursorPos($pulseOriginalCursor.X,$pulseOriginalCursor.Y) | Out-Null
}
