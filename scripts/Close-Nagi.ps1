param([Parameter(Mandatory)][string]$ExecutablePath)
$ErrorActionPreference='Stop'
$pulseExecutable=[IO.Path]::GetFullPath($ExecutablePath)
$pulseProcesses=@(Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($pulseExecutable)) -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $pulseExecutable})
if($pulseProcesses.Count -eq 0){return}
Add-Type -AssemblyName UIAutomationClient
if(-not ('PulseInstallerClose' -as [type])){Add-Type 'using System;using System.Runtime.InteropServices;public static class PulseInstallerClose { [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l); }'}
foreach($pulseOwned in $pulseProcesses){
 try{$pulseActivation=[Threading.EventWaitHandle]::OpenExisting('Local\Pulse.Desktop.Activate.v1');$pulseActivation.Set()|Out-Null;$pulseActivation.Dispose()}catch [Threading.WaitHandleCannotBeOpenedException]{}
 Start-Sleep -Milliseconds 700
 $pulseWindows=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$pulseOwned.Id))
 $pulseWindow=$pulseWindows | Where-Object {$_.Current.Name -in @('Nagi',('Nagi '+[char]0xB7+' Welcome'),('Pulse '+[char]0xB7+' Halo'))} | Select-Object -First 1
 if($null -eq $pulseWindow){throw 'Nagi could not be closed gracefully. Quit it from the tray and retry.'}
 [PulseInstallerClose]::PostMessage([IntPtr]$pulseWindow.Current.NativeWindowHandle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
 if(!$pulseOwned.WaitForExit(10000)){throw 'Nagi is still closing. Quit it from the tray and retry.'}
}
