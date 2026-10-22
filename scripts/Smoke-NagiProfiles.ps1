param([Parameter(Mandatory)][string]$ExecutablePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class WorkspaceSmokeNative { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
 [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
 [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
}
'@
$qaPointer=[WorkspaceSmokeNative+Point]::new();[WorkspaceSmokeNative]::GetCursorPos([ref]$qaPointer)|Out-Null
$qaDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/validation/profiles-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $qaDirectory -ErrorAction Stop|Out-Null
$qaFile=Join-Path $qaDirectory 'settings.json'
@{SchemaVersion=1;Language='en';Theme='System';WelcomeCompleted=$true;UserName='friend';Position=@{Orientation='Horizontal';Anchor='Bottom edge';OffsetX=0;OffsetY=0};CloseOnDeactivate=$false;HideInFullscreen=$false;IndexFiles=$false;SearchFolders=@();FpsEnabled=$false;ObsEnabled=$false;AutomaticUpdates=$false;WeatherEnabled=$false;StartWithWindows=$false;LuaUiEnabled=$false;ReducedMotion=$true;Hotkey=@{Modifiers=7;Key=131};HideHotkey=@{Modifiers=7;Key=132};SearchHotkey=@{Modifiers=7;Key=133}}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $qaFile
$start=[Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($ExecutablePath));$start.UseShellExecute=$false;$start.ArgumentList.Add('--data-dir');$start.ArgumentList.Add($qaDirectory)
$qaProcess=[Diagnostics.Process]::Start($start)
function Find([string]$Value,[switch]$Id,[switch]$Optional){
 $property=if($Id){[Windows.Automation.AutomationElement]::AutomationIdProperty}else{[Windows.Automation.AutomationElement]::NameProperty}
 for($i=0;$i -lt 30;$i++){$item=$script:root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new($property,$Value));if($item){return $item};if($Optional){return $null};Start-Sleep -Milliseconds 100};throw ('Missing '+$Value)
}
function Invoke([string]$Name){(Find $Name).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 450}
function Activate-Workspace([string]$Name){
 if(Find '← All profiles' -Optional){Invoke '← All profiles'}
 Invoke ('Customize '+$Name);Invoke ('Activate '+$Name);Invoke '← All profiles'
}
function Select-Workspace([string]$Id,[string]$Value){
 $combos=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ComboBox))
 $combo=$combos|Where-Object {$_.Current.AutomationId -eq $Id -or $_.Current.Name -eq $Id}|Select-Object -First 1
 if(!$combo){throw ('Missing combo '+$Id)}
 $combo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand();Start-Sleep -Milliseconds 150
 $choice=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$Value),[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty,$true)))
 if(!$choice){throw ('Missing selection '+$Value)};$choice.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select();Start-Sleep -Milliseconds 450
}
function Snapshot([string]$Name){
 $script:handle=[IntPtr]$root.Current.NativeWindowHandle
 $r=[WorkspaceSmokeNative+Rect]::new();[WorkspaceSmokeNative]::GetWindowRect($handle,[ref]$r)|Out-Null
 $bitmap=[Drawing.Bitmap]::new($r.Right-$r.Left,$r.Bottom-$r.Top);$g=[Drawing.Graphics]::FromImage($bitmap);$dc=$g.GetHdc()
 try{[WorkspaceSmokeNative]::PrintWindow($handle,$dc,2)|Out-Null}finally{$g.ReleaseHdc($dc)}
 $bitmap.Save((Join-Path $qaDirectory ($Name+'.png')));$g.Dispose();$bitmap.Dispose()
}
Add-Type -AssemblyName System.Windows.Forms
$foregroundForm=$null
try{
 for($i=0;$i -lt 100;$i++){$script:root=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$qaProcess.Id))|Where-Object {$_.Current.Name -eq 'Nagi' -and $_.Current.NativeWindowHandle -ne 0}|Select-Object -First 1;if($root){break};Start-Sleep -Milliseconds 100}
 if(!$root){throw 'No QA window'};$handle=[IntPtr]$root.Current.NativeWindowHandle
 Invoke 'Reveal Nagi';Invoke 'Open Nagi control center'
 foreach($name in @('Default','Work','Gaming','Stream','Focus')){Find ('ProfileCard'+$name) -Id|Out-Null}
 $lastCard=(Find 'ProfileCardFocus' -Id).Current.BoundingRectangle
 if($lastCard.Bottom -gt $root.Current.BoundingRectangle.Bottom-30){throw 'The profile list clips its last card at the initial window size'}
 Find 'Activate Work'|Out-Null
 Snapshot 'profile-cards'
 Invoke 'Customize Work';Find 'Work widget Performance'|Out-Null;Find 'When to activate'|Out-Null
 Invoke '+ Add application rule'
 $rulePath=[Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
 (Find 'ProfileRuleExecutable' -Id).GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($rulePath)
 Invoke 'Save'
 $saved=Get-Content $qaFile -Raw|ConvertFrom-Json
 if(!($saved.Rules|Where-Object {$_.Identity -eq $rulePath -and $_.Profile -eq 'Work'})){throw 'The rule did not save under Work'}
 Snapshot 'profile-editor'
 Invoke '← All profiles'
 $foregroundForm=[Windows.Forms.Form]::new();$foregroundForm.Text='Nagi owned profile trigger';$foregroundForm.StartPosition='Manual';$foregroundForm.Bounds=[Drawing.Rectangle]::new(40,60,280,100);$foregroundForm.TopMost=$true
 $foregroundForm.Show();[Windows.Forms.Application]::DoEvents();$foregroundForm.Activate();[WorkspaceSmokeNative]::SetForegroundWindow($foregroundForm.Handle)|Out-Null
 $matched=$false
 for($attempt=0;$attempt -lt 75;$attempt++){
  [Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 200
  $card=Find 'ProfileCardWork' -Id
  $badge=$card.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Active'))
  if($badge -and !$badge.Current.IsOffscreen){$matched=$true;break}
 }
 if(!$matched){Snapshot 'foreground-failure';throw 'The owned foreground executable did not activate Work'}
 $foregroundForm.Hide();[Windows.Forms.Application]::DoEvents()
 Activate-Workspace 'Default'
 $foregroundForm.Show();[Windows.Forms.Application]::DoEvents();$foregroundForm.Activate();[WorkspaceSmokeNative]::SetForegroundWindow($foregroundForm.Handle)|Out-Null
 for($attempt=0;$attempt -lt 25;$attempt++){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 200}
 $defaultCard=Find 'ProfileCardDefault' -Id
 if(!$defaultCard.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Active'))){throw 'Executable rule overrode a manually locked profile'}
 $foregroundForm.Close();$foregroundForm.Dispose();$foregroundForm=$null
 Invoke 'Customize Work'
 $removeButtons=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,'Remove'))
 $removeButtons[$removeButtons.Count-1].GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 500
 if((Get-Content $qaFile -Raw|ConvertFrom-Json).Rules|Where-Object {$_.Identity -eq $rulePath}){throw 'Rule removal did not persist'}
 Invoke 'Settings';Invoke 'Appearance & dock settings'
 if(Find 'Tint background' -Optional){throw 'Glass exposes background tint controls'}
 if(Find 'BackgroundColorMode' -Id -Optional){throw 'Glass exposes a separate color source'}
 Select-Workspace 'Theme' 'Dark'
 (Find 'Tint background').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle();Start-Sleep -Milliseconds 500
 Select-Workspace 'BackgroundColorMode' 'Custom'
 Invoke 'Choose background color';Invoke 'Save'
 if((Get-Content $qaFile -Raw|ConvertFrom-Json).NotchColorMode -ne 'Custom'){throw 'Shared custom color did not persist'}
 Snapshot 'dark-shared-background'
 Select-Workspace 'Theme' 'Light';Find 'Choose background color'|Out-Null;Snapshot 'light-shared-background'
 Select-Workspace 'BackgroundColorMode' 'System';Select-Workspace 'Theme' 'Glass (system)'
 if(Find 'Tint background' -Optional){throw 'Glass retained tint controls after a theme change'}
 if(Find 'Choose background color' -Optional){throw 'Glass retained a custom color button'}
 if(Find 'Notch color' -Optional){throw 'A separate notch theme is still exposed'}
 Snapshot 'glass-shared-background'
 if((Get-Content (Join-Path $qaDirectory 'logs/pulse.jsonl') -Raw) -match '"category":"fatal"'){throw 'A fatal UI error was logged'}
 Write-Output ('PASS: profile card navigation, per-profile rule add/remove, real foreground executable activation, manual lock, shared Dark/Light colors, hidden Glass tint controls and persisted preferences. '+$qaDirectory)
}catch{Write-Output $_.ScriptStackTrace;throw}
finally{
 if($foregroundForm){$foregroundForm.Close();$foregroundForm.Dispose()}
 [WorkspaceSmokeNative]::SetCursorPos($qaPointer.X,$qaPointer.Y)|Out-Null
 if(!$qaProcess.HasExited){[WorkspaceSmokeNative]::PostMessage($handle,16,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null;if(!$qaProcess.WaitForExit(2000)){Stop-Process -Id $qaProcess.Id -Force}}
}
