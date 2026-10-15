param([Parameter(Mandatory)][int]$PulseProcessId,[switch]$Screenshots,[string]$FileQuery='Pulse launcher sample')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class PulseSearchSmoke {
 [DllImport("user32.dll")]public static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]public static extern bool SetProcessDpiAwarenessContext(IntPtr c);
}
'@
[PulseSearchSmoke]::SetProcessDpiAwarenessContext([IntPtr](-4))|Out-Null
function Send-SearchKey([byte]$Key,[bool]$Alt=$false) {
 if($Alt){[PulseSearchSmoke]::keybd_event(18,0,0,[UIntPtr]::Zero)}
 [PulseSearchSmoke]::keybd_event($Key,0,0,[UIntPtr]::Zero);[PulseSearchSmoke]::keybd_event($Key,0,2,[UIntPtr]::Zero)
 if($Alt){[PulseSearchSmoke]::keybd_event(18,0,2,[UIntPtr]::Zero)}
 Start-Sleep -Milliseconds 450
}
function Find-SearchWindow {
 return [Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Children,[Windows.Automation.AndCondition]::new(
 [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$PulseProcessId),
 [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Nagi · Quick search')))
}
function Search-Control([string]$Id) {
 $control=$script:pulseSearch.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$Id))
 if($null -eq $control){throw ('Missing search control: '+$Id+'; window visible='+[PulseSearchSmoke]::IsWindowVisible([IntPtr]$script:pulseSearch.Current.NativeWindowHandle))};return $control
}
function Set-SearchQuery([string]$Query) {
 (Search-Control 'QuickSearchQuery').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($Query);Start-Sleep -Milliseconds 700
}
function Search-ResultNames {
 return @((Search-Control 'QuickSearchResults').FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem)) | ForEach-Object {$_.Current.Name})
}
Send-SearchKey 32 $true
$script:pulseSearch=Find-SearchWindow
if($null -eq $script:pulseSearch){throw 'Alt+Space did not open Pulse quick search. Check shortcut conflicts in Settings.'}
$pulseHandle=[IntPtr]$pulseSearch.Current.NativeWindowHandle
for($pulseTry=0;$pulseTry -lt 30;$pulseTry++) {
 $pulseStatus=(Search-Control 'QuickSearchStatus').Current.Name
 if($pulseStatus -notlike '*Updating*' -and $pulseStatus -match '[1-9]\d* apps'){break};Start-Sleep -Milliseconds 300
}
if($pulseStatus -notmatch '[1-9]\d* apps'){throw ('Application catalog was not ready: '+$pulseStatus)}
Set-SearchQuery 'app: chrome'
if(-not ((Search-ResultNames) -match 'Chrome')){throw 'Installed Chrome app did not appear.'}
Set-SearchQuery '24 * 7'
if(-not ((Search-ResultNames) -match '^168')){throw 'Calculator did not evaluate the expression.'}
Set-SearchQuery '> volume'
if((Search-ResultNames).Count -ne 2){throw 'Command filter did not return both volume actions.'}
$pulseSelection=(Search-Control 'QuickSearchResults').GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern)
$pulseFirst=$pulseSelection.Current.GetSelection()[0].Current.Name
Send-SearchKey 40
if($pulseSelection.Current.GetSelection()[0].Current.Name -eq $pulseFirst){throw 'Down arrow did not change selection.'}
Send-SearchKey 38
if($pulseSelection.Current.GetSelection()[0].Current.Name -ne $pulseFirst){throw 'Up arrow did not restore selection.'}
Set-SearchQuery ('file: '+$FileQuery)
if((Search-ResultNames).Count -eq 0){throw 'Indexed filenames did not appear.'}
if($FileQuery -eq 'Pulse launcher sample' -and -not ((Search-ResultNames) -match 'Pulse launcher sample.txt')){throw 'Indexed fixture filename did not appear.'}
if($Screenshots) {
 Add-Type -AssemblyName System.Drawing
 $bounds=$pulseSearch.Current.BoundingRectangle;$bitmap=[Drawing.Bitmap]::new([int]$bounds.Width,[int]$bounds.Height);$graphics=[Drawing.Graphics]::FromImage($bitmap)
 try{$graphics.CopyFromScreen([int]$bounds.Left,[int]$bounds.Top,0,0,$bitmap.Size);$bitmap.Save((Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/validation/quick-search.png'))}finally{$graphics.Dispose();$bitmap.Dispose()}
}
Send-SearchKey 27
if([PulseSearchSmoke]::IsWindowVisible($pulseHandle)){throw 'Escape did not hide search.'}
Send-SearchKey 32 $true
if(-not [PulseSearchSmoke]::IsWindowVisible($pulseHandle)){throw 'Alt+Space did not reopen search.'}
Set-SearchQuery '> rebuild search index';Send-SearchKey 13
if([PulseSearchSmoke]::IsWindowVisible($pulseHandle)){throw 'Index rebuild action did not dismiss search.'}
Start-Sleep -Milliseconds 1200
Send-SearchKey 32 $true
Set-SearchQuery '> open nagi settings';Send-SearchKey 13
if([PulseSearchSmoke]::IsWindowVisible($pulseHandle)){throw 'Executing the Settings action did not hide search.'}
$pulseMain=[Windows.Automation.AutomationElement]::RootElement.FindFirst([Windows.Automation.TreeScope]::Children,[Windows.Automation.AndCondition]::new(
 [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$PulseProcessId),
 [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Nagi')))
if($null -eq $pulseMain.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Open quick search'))){throw 'Search action did not navigate to Settings.'}
Write-Output 'Alt+Space / app catalog / calculator / command filter / indexed filename / Escape / reopen / Settings action: PASS'
Write-Output $pulseStatus
