param([Parameter(Mandatory)][int]$PulseProcessId,[string[]]$Sides=@('Right edge','Left edge'),[switch]$Screenshots)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class PulseEdgeNative {
 [StructLayout(LayoutKind.Sequential)]public struct Point { public int X,Y; }
 [StructLayout(LayoutKind.Sequential)]public struct Rect { public int Left,Top,Right,Bottom; }
 [StructLayout(LayoutKind.Sequential)]public struct Power { public byte AC,Flags,Percent,Saver;public uint Remaining,Full; }
 [DllImport("kernel32.dll")]public static extern bool GetSystemPowerStatus(out Power power);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]public struct Monitor { public uint Size;public Rect Screen,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device; }
 [DllImport("user32.dll")]public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")]public static extern bool GetPhysicalCursorPos(out Point p);
 [DllImport("user32.dll")]public static extern bool SetPhysicalCursorPos(int x,int y);
 [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]public static extern IntPtr MonitorFromWindow(IntPtr h,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern bool GetMonitorInfo(IntPtr h,ref Monitor monitor);
 [DllImport("user32.dll")]public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
}
'@
[PulseEdgeNative]::SetProcessDpiAwarenessContext([IntPtr](-4))|Out-Null
$pulseActivation=[System.Threading.EventWaitHandle]::OpenExisting('Local\Pulse.Desktop.Activate.v1')
$pulseActivation.Set()|Out-Null;$pulseActivation.Dispose();Start-Sleep -Milliseconds 1000
$pulseRoot=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$PulseProcessId))
if($null -eq $pulseRoot){throw 'Pulse window was not found.'}
$pulseHandle=[IntPtr]$pulseRoot.Current.NativeWindowHandle
function Get-EdgeRect { $pulseRect=[PulseEdgeNative+Rect]::new();[PulseEdgeNative]::GetWindowRect($pulseHandle,[ref]$pulseRect)|Out-Null;return $pulseRect }
function Find-EdgeControl([string]$Value,[switch]$Id) {
    $pulseProperty=if($Id){[System.Windows.Automation.AutomationElement]::AutomationIdProperty}else{[System.Windows.Automation.AutomationElement]::NameProperty}
    $pulseControl=$pulseRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new($pulseProperty,$Value))
    if($null -eq $pulseControl){throw ('Missing control '+$Value)}
    return $pulseControl
}
function Invoke-EdgeControl([string]$Name) { (Find-EdgeControl $Name).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 600 }
function Wait-EdgeWidth([double]$Dips) {
    for($pulseTry=0;$pulseTry -lt 45;$pulseTry++){
        $pulseRect=Get-EdgeRect;$pulseScale=[PulseEdgeNative]::GetDpiForWindow($pulseHandle)/96.0
        if([Math]::Abs(($pulseRect.Right-$pulseRect.Left)-$Dips*$pulseScale) -le 2){Start-Sleep -Milliseconds 250;return}
        Start-Sleep -Milliseconds 100
    }
    throw ('Expected width '+$Dips+' DIP; got '+($pulseRect.Right-$pulseRect.Left)+' pixels.')
}
function Move-EdgePointer($Control) {
    $pulseRect=$Control.Current.BoundingRectangle
    if($Control.Current.IsOffscreen -or $pulseRect.IsEmpty){throw 'Pointer target is offscreen.'}
    [PulseEdgeNative]::SetPhysicalCursorPos([int]($pulseRect.X+$pulseRect.Width/2),[int]($pulseRect.Y+$pulseRect.Height/2))|Out-Null
}
function Click-EdgeControl($Control) { Move-EdgePointer $Control;[PulseEdgeNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero);[PulseEdgeNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero) }
function Check-EdgeBattery {
    $pulsePower=[PulseEdgeNative+Power]::new()
    if(![PulseEdgeNative]::GetSystemPowerStatus([ref]$pulsePower)){return 'Unavailable'}
    $pulseBattery=Find-EdgeControl 'CompactBatteryButton' -Id
    if($pulsePower.Flags -ne 255 -and ($pulsePower.Flags -band 128) -ne 0){
        if($pulseBattery.Current.Name -notlike '*No system battery*'){throw 'No-battery status was not exposed.'}
        return 'AC'
    }
    if($pulsePower.Percent -le 100){
        if($pulseBattery.Current.Name -notmatch '^Battery (\d+)%'){throw 'Battery percentage was not exposed.'}
        if([Math]::Abs([int]$Matches[1]-$pulsePower.Percent) -gt 1){throw 'Displayed battery percentage does not match Windows.'}
        return ($Matches[1]+'%')
    }
    if($pulseBattery.Current.Name -notlike '*unavailable*'){throw 'Unknown battery percentage was not reported.'}
    return 'Unavailable'
}
function Drag-EdgeLogo {
    $pulseBefore=Get-EdgeRect
    $pulseLogo=Find-EdgeControl 'Open Nagi control center'
    $pulseBounds=$pulseLogo.Current.BoundingRectangle
    $pulseX=[int]($pulseBounds.Left+$pulseBounds.Width/2);$pulseY=[int]($pulseBounds.Top+$pulseBounds.Height/2)
    [PulseEdgeNative]::SetPhysicalCursorPos($pulseX,$pulseY)|Out-Null
    [PulseEdgeNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 100
    [PulseEdgeNative]::SetPhysicalCursorPos(($pulseX+12),$pulseY)|Out-Null
    Start-Sleep -Milliseconds 200
    [PulseEdgeNative]::SetPhysicalCursorPos(($pulseX+12),($pulseY+40))|Out-Null
    Start-Sleep -Milliseconds 250
    [PulseEdgeNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 900
    Wait-EdgeWidth 80
    if([Math]::Abs((Get-EdgeRect).Top-$pulseBefore.Top) -lt 10){throw 'Dragging the Pulse logo did not reposition Halo.'}
}
function Move-EdgeAway {
    $pulseMonitor=[PulseEdgeNative+Monitor]::new();$pulseMonitor.Size=[System.Runtime.InteropServices.Marshal]::SizeOf($pulseMonitor)
    [PulseEdgeNative]::GetMonitorInfo([PulseEdgeNative]::MonitorFromWindow($pulseHandle,2),[ref]$pulseMonitor)|Out-Null
    [PulseEdgeNative]::SetPhysicalCursorPos(($pulseMonitor.Work.Left+$pulseMonitor.Work.Right)/2,$pulseMonitor.Work.Top+20)|Out-Null
}
function Save-EdgeImage([string]$Name) {
    if(!$Screenshots){return}
    Add-Type -AssemblyName System.Drawing
    $pulseRect=Get-EdgeRect;$pulseBitmap=[System.Drawing.Bitmap]::new($pulseRect.Right-$pulseRect.Left,$pulseRect.Bottom-$pulseRect.Top)
    $pulseGraphics=[System.Drawing.Graphics]::FromImage($pulseBitmap)
    try{$pulseGraphics.CopyFromScreen($pulseRect.Left,$pulseRect.Top,0,0,$pulseBitmap.Size);$pulseDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\validation';[System.IO.Directory]::CreateDirectory($pulseDirectory)|Out-Null;$pulseBitmap.Save((Join-Path $pulseDirectory ($Name+'.png')))}finally{$pulseGraphics.Dispose();$pulseBitmap.Dispose()}
}
$pulseOriginalPointer=[PulseEdgeNative+Point]::new();[PulseEdgeNative]::GetPhysicalCursorPos([ref]$pulseOriginalPointer)|Out-Null
try {
    foreach($pulseSide in $Sides){
        $pulseActivation=[System.Threading.EventWaitHandle]::OpenExisting('Local\Pulse.Desktop.Activate.v1');$pulseActivation.Set()|Out-Null;$pulseActivation.Dispose();Start-Sleep -Milliseconds 900
        Invoke-EdgeControl 'Open settings'
        $pulsePosition=Find-EdgeControl 'Nagi position'
        $pulsePosition.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 200
        $pulseChoice=$pulseRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$pulseSide),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true)))
        if($null -eq $pulseChoice){throw ('Missing edge choice '+$pulseSide)}
        $pulseChoice.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 700
        Invoke-EdgeControl 'Collapse Nagi';Move-EdgeAway;Wait-EdgeWidth 18;Start-Sleep -Milliseconds 400
        $pulseRect=Get-EdgeRect;$pulseMonitor=[PulseEdgeNative+Monitor]::new();$pulseMonitor.Size=[System.Runtime.InteropServices.Marshal]::SizeOf($pulseMonitor);[PulseEdgeNative]::GetMonitorInfo([PulseEdgeNative]::MonitorFromWindow($pulseHandle,2),[ref]$pulseMonitor)|Out-Null
        if(($pulseSide -eq 'Left edge' -and $pulseRect.Left -ne $pulseMonitor.Screen.Left) -or ($pulseSide -eq 'Right edge' -and $pulseRect.Right -ne $pulseMonitor.Screen.Right)){throw 'Droplet is not flush against the screen edge.'}
        Save-EdgeImage ('edge-'+$pulseSide.Split(' ')[0].ToLower()+'-drop')
        $pulseOriginalDropX=($pulseRect.Left+$pulseRect.Right)/2;$pulseOriginalDropY=($pulseRect.Top+$pulseRect.Bottom)/2
        $pulseForeground=[PulseEdgeNative]::GetForegroundWindow()
        Move-EdgePointer (Find-EdgeControl 'EdgeDropButton' -Id);Wait-EdgeWidth 80;Start-Sleep -Milliseconds 600
        $pulseHoverForeground=[PulseEdgeNative]::GetForegroundWindow()
        if($pulseHoverForeground -ne $pulseForeground){throw ('Hover changed foreground from '+$pulseForeground+' to '+$pulseHoverForeground+'; Halo is '+$pulseHandle+'.')}
        $pulseRect=Get-EdgeRect;$pulseScale=[PulseEdgeNative]::GetDpiForWindow($pulseHandle)/96.0
        if([Math]::Abs(($pulseRect.Bottom-$pulseRect.Top)-420*$pulseScale) -gt 2){throw 'Hover skipped the minimal controls.'}
        $pulseBatteryText=Check-EdgeBattery
        $pulseOldGrip=$pulseRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Drag Halo'))
        if($null -ne $pulseOldGrip){throw 'The old two-line grip is still present.'}
        $pulseLogoWord=(Find-EdgeControl 'Open Nagi control center').FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'pulse'))
        if($null -ne $pulseLogoWord){throw 'The compact logo still includes the wordmark.'}
        Save-EdgeImage ('edge-'+$pulseSide.Split(' ')[0].ToLower()+'-compact')
        [PulseEdgeNative]::SetPhysicalCursorPos($pulseOriginalDropX,$pulseOriginalDropY)|Out-Null
        [PulseEdgeNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero);[PulseEdgeNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
        Start-Sleep -Milliseconds 100 # Let WinUI dispatch release before teleporting the test pointer away.
        Move-EdgeAway;Start-Sleep -Milliseconds 800;Wait-EdgeWidth 80
        [PulseEdgeNative]::SetPhysicalCursorPos($pulseOriginalDropX,$pulseOriginalDropY)|Out-Null
        [PulseEdgeNative]::mouse_event(2,0,0,0,[UIntPtr]::Zero);[PulseEdgeNative]::mouse_event(4,0,0,0,[UIntPtr]::Zero);Wait-EdgeWidth 580
        Invoke-EdgeControl 'Collapse Nagi'
        Move-EdgeAway;Wait-EdgeWidth 18
        Click-EdgeControl (Find-EdgeControl 'EdgeDropButton' -Id);Wait-EdgeWidth 80;Move-EdgeAway;Start-Sleep -Milliseconds 800;Wait-EdgeWidth 80
        Drag-EdgeLogo
        Click-EdgeControl (Find-EdgeControl 'Open Nagi control center');Wait-EdgeWidth 580
        Save-EdgeImage ('edge-'+$pulseSide.Split(' ')[0].ToLower()+'-panel')
        Invoke-EdgeControl 'Collapse Nagi';Move-EdgeAway;Wait-EdgeWidth 18;Start-Sleep -Milliseconds 300
        Move-EdgePointer (Find-EdgeControl 'EdgeDropButton' -Id);Wait-EdgeWidth 80;Start-Sleep -Milliseconds 500
        Move-EdgePointer (Find-EdgeControl 'CompactCpuButton' -Id);Start-Sleep -Milliseconds 1800
        Wait-EdgeWidth 430
        Save-EdgeImage ('edge-'+$pulseSide.Split(' ')[0].ToLower()+'-cpu')
        Invoke-EdgeControl 'Show more details';Wait-EdgeWidth 580
        Invoke-EdgeControl 'Collapse Nagi';Move-EdgeAway;Wait-EdgeWidth 18
        Move-EdgePointer (Find-EdgeControl 'EdgeDropButton' -Id)
        [PulseEdgeNative]::mouse_event(8,0,0,0,[UIntPtr]::Zero);[PulseEdgeNative]::mouse_event(16,0,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 800
        $pulseQuickOpen=$null
        foreach($pulsePeer in [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$PulseProcessId))){
            $pulseCandidate=$pulsePeer.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Open Nagi'))
            if($null -ne $pulseCandidate -and !$pulseCandidate.Current.IsOffscreen){$pulseQuickOpen=$pulseCandidate;break}
        }
        if($null -eq $pulseQuickOpen){throw 'Edge quick menu did not stay open during hover.'}
        $pulseQuickOpen.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Wait-EdgeWidth 580
        Invoke-EdgeControl 'Collapse Nagi';Move-EdgeAway;Wait-EdgeWidth 18
        [pscustomobject]@{Side=$pulseSide;Battery=$pulseBatteryText;LogoOnly="Passed";LogoDrag="Passed";FlushToScreen='Passed';HoverMinimal='Passed';HoverFocus='Passed';RetractOnLeave='Passed';ClickAtOriginalDropPosition='Passed';FirstClickPins='Passed';SecondClickExpands='Passed';CpuPreview='Passed';ShowMore='Passed';EdgeQuickMenu='Passed'}|ConvertTo-Json
    }
}
finally{[PulseEdgeNative]::SetPhysicalCursorPos($pulseOriginalPointer.X,$pulseOriginalPointer.Y)|Out-Null}
