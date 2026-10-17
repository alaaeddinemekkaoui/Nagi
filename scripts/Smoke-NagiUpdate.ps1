$ErrorActionPreference = 'Stop'
$nagiWorkspace = Split-Path -Parent $PSScriptRoot
$nagiTestRoot = Join-Path $nagiWorkspace ('artifacts/validation/update-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $nagiTestRoot -Force | Out-Null
$nagiCompiler = Join-Path $nagiTestRoot 'Compile-Stub.ps1'
@'
param([string]$OutputPath)
Add-Type -TypeDefinition @"
using System; using System.IO;
public static class Stub { public static void Main(string[] args) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "restarted.txt"), string.Join("|", args)); } }
"@ -OutputAssembly $OutputPath -OutputType WindowsApplication
'@ | Set-Content -LiteralPath $nagiCompiler -Encoding UTF8
$nagiStub = Join-Path $nagiTestRoot 'stub.exe'
& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $nagiCompiler -OutputPath $nagiStub
if ($LASTEXITCODE) { throw 'Could not build updater test executable.' }
foreach ($nagiScenario in @('success', 'rollback', 'escape')) {
    $nagiCase = Join-Path $nagiTestRoot $nagiScenario
    $nagiInstall = Join-Path $nagiCase 'app'
    $nagiStage = Join-Path $nagiCase 'stage'
    $nagiPayload = Join-Path $nagiStage 'payload'
    New-Item -ItemType Directory -Path $nagiInstall,$nagiPayload -Force | Out-Null
    Copy-Item -LiteralPath $nagiStub -Destination (Join-Path $nagiInstall 'Nagi.exe')
    Copy-Item -LiteralPath $nagiStub -Destination (Join-Path $nagiPayload 'Nagi.exe')
    Set-Content -LiteralPath (Join-Path $nagiInstall 'state.txt') -Value 'old'
    Set-Content -LiteralPath (Join-Path $nagiPayload 'state.txt') -Value 'new'
    Set-Content -LiteralPath (Join-Path $nagiInstall 'preferences.txt') -Value 'preserved'
    Set-Content -LiteralPath (Join-Path $nagiPayload 'added.txt') -Value 'new'
    $nagiFiles = @('Nagi.exe', 'state.txt', 'added.txt')
    $nagiLock = $null
    if ($nagiScenario -eq 'rollback') {
        Set-Content -LiteralPath (Join-Path $nagiInstall 'locked.txt') -Value 'old'
        Set-Content -LiteralPath (Join-Path $nagiPayload 'locked.txt') -Value 'new'
        $nagiLock = [IO.File]::Open((Join-Path $nagiInstall 'locked.txt'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
        $nagiFiles += 'locked.txt'
    }
    if ($nagiScenario -eq 'escape') { $nagiFiles += '../escape.txt' }
    $nagiData = Join-Path $nagiCase 'preferences with spaces'
    $nagiPlan = Join-Path $nagiStage 'plan.json'
    @{ ProcessId=[int]::MaxValue; Executable=(Join-Path $nagiInstall 'Nagi.exe'); Destination=$nagiInstall; Payload=$nagiPayload; Files=$nagiFiles; DataDirectory=$nagiData; ResultPath=(Join-Path $nagiCase 'result.json'); Version='2.0.0' } | ConvertTo-Json | Set-Content -LiteralPath $nagiPlan -Encoding UTF8
    try {
        & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Apply-NagiUpdate.ps1') -PlanPath $nagiPlan
        $nagiExit = $LASTEXITCODE
    } finally { if ($nagiLock) { $nagiLock.Dispose() } }
    if ($nagiScenario -eq 'success' -and $nagiExit -ne 0) { throw (Get-Content -LiteralPath (Join-Path $nagiStage 'update.log') -Raw) }
    if ($nagiScenario -ne 'success' -and $nagiExit -eq 0) { throw "$nagiScenario was not rejected." }
    if((Get-Content -LiteralPath (Join-Path $nagiCase 'result.json') -Raw|ConvertFrom-Json).Success -ne ($nagiScenario -eq 'success')){throw 'Update outcome was not reported correctly.'}
    $nagiExpected = if ($nagiScenario -eq 'success') { 'new' } else { 'old' }
    if ((Get-Content -LiteralPath (Join-Path $nagiInstall 'state.txt')).Trim() -ne $nagiExpected) { throw "$nagiScenario left incorrect application files." }
    if ((Get-Content -LiteralPath (Join-Path $nagiInstall 'preferences.txt')).Trim() -ne 'preserved') { throw 'User files changed.' }
    if ($nagiScenario -eq 'rollback' -and (Test-Path -LiteralPath (Join-Path $nagiInstall 'added.txt'))) { throw 'Rollback left a newly installed file.' }
    if ($nagiScenario -ne 'escape') {
        for ($nagiAttempt = 0; $nagiAttempt -lt 30 -and !(Test-Path -LiteralPath (Join-Path $nagiInstall 'restarted.txt')); $nagiAttempt++) { Start-Sleep -Milliseconds 100 }
        if ((Get-Content -LiteralPath (Join-Path $nagiInstall 'restarted.txt') -Raw) -ne ('--data-dir|' + $nagiData)) { throw 'Restart did not preserve the data directory.' }
    }
}
Write-Output ('PASS: staged update, preference retention, restart arguments, locked-file rollback and path rejection. ' + $nagiTestRoot)
