param([Parameter(Mandatory)][string]$PlanPath)
$ErrorActionPreference = 'Stop'
$nagiStage = [IO.Path]::GetFullPath((Split-Path -Parent $PlanPath))
$nagiLog = Join-Path $nagiStage 'update.log'
$nagiChanged = [Collections.Generic.List[object]]::new()
$nagiPlan = $null
$nagiCanRestart = $false
$nagiOldVersion = $null
$nagiRollbackFailed = $false
function Get-ContainedPath([string]$Root, [string]$Relative) {
    $nagiBase = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $nagiPath = [IO.Path]::GetFullPath((Join-Path $nagiBase $Relative))
    if (!$nagiPath.StartsWith($nagiBase, [StringComparison]::OrdinalIgnoreCase)) { throw 'Update path leaves its target directory.' }
    $nagiParent = Split-Path -Parent $nagiPath
    while ($nagiParent -and $nagiParent.Length -ge $nagiBase.TrimEnd('\').Length) {
        if ((Test-Path -LiteralPath $nagiParent) -and ((Get-Item -LiteralPath $nagiParent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked update directories are not supported.' }
        $nagiParent = Split-Path -Parent $nagiParent
    }
    if ((Test-Path -LiteralPath $nagiPath) -and ((Get-Item -LiteralPath $nagiPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked update files are not supported.' }
    return $nagiPath
}
function Write-UpdateResult([bool]$Success, [string]$Message) {
    if ($nagiPlan -and $nagiPlan.ResultPath) {
        [IO.Directory]::CreateDirectory((Split-Path -Parent $nagiPlan.ResultPath)) | Out-Null
        [IO.File]::WriteAllText($nagiPlan.ResultPath, (@{Success=$Success;Message=$Message;Log=$nagiLog}|ConvertTo-Json))
    }
}
function Start-UpdatedNagi {
    $nagiArgs = @()
    if ($nagiPlan.DataDirectory) { $nagiArgs = @('--data-dir', ('"' + $nagiPlan.DataDirectory + '"')) }
    if ($nagiArgs.Count) { Start-Process -FilePath $nagiPlan.Executable -WorkingDirectory $nagiPlan.Destination -ArgumentList $nagiArgs | Out-Null }
    else { Start-Process -FilePath $nagiPlan.Executable -WorkingDirectory $nagiPlan.Destination | Out-Null }
}
try {
    $nagiPlan = Get-Content -LiteralPath $PlanPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $nagiDestination = [IO.Path]::GetFullPath($nagiPlan.Destination)
    if ([IO.Path]::GetFullPath($nagiPlan.Executable) -ne (Join-Path $nagiDestination 'Nagi.exe')) { throw 'Update executable is invalid.' }
    if ([IO.Path]::GetFullPath($nagiPlan.Payload) -ne (Join-Path $nagiStage 'payload')) { throw 'Update staging path is invalid.' }
    if ($nagiDestination.StartsWith($nagiStage + '\', [StringComparison]::OrdinalIgnoreCase) -or $nagiStage.StartsWith($nagiDestination + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Update staging overlaps the application.' }
    $nagiProcess = Get-Process -Id $nagiPlan.ProcessId -ErrorAction SilentlyContinue
    if ($nagiProcess) {
        if ($nagiProcess.Path -ne $nagiPlan.Executable) { throw 'Update process does not match this installation.' }
        if (!$nagiProcess.WaitForExit(30000)) { throw 'Nagi is still running. No update files were installed.' }
    }
    $nagiCanRestart = $true
    # Build and validate every path before replacing any file.
    $nagiBackupRoot = Join-Path $nagiStage 'backup'
    $nagiFiles = @($nagiPlan.Files | ForEach-Object {
        [pscustomobject]@{Source=(Get-ContainedPath $nagiPlan.Payload $_);Target=(Get-ContainedPath $nagiDestination $_);Backup=(Get-ContainedPath $nagiBackupRoot $_)}
    })
    foreach ($nagiFile in $nagiFiles) {
        if (!(Test-Path -LiteralPath $nagiFile.Source -PathType Leaf)) { throw 'Staged update file is missing.' }
    }
    foreach ($nagiFile in $nagiFiles) {
        $nagiExisted = Test-Path -LiteralPath $nagiFile.Target
        if ($nagiExisted) {
            New-Item -ItemType Directory -Path (Split-Path -Parent $nagiFile.Backup) -Force | Out-Null
            Copy-Item -LiteralPath $nagiFile.Target -Destination $nagiFile.Backup
        }
        $nagiChanged.Add([pscustomobject]@{Target=$nagiFile.Target;Backup=$nagiFile.Backup;Existed=$nagiExisted})
        New-Item -ItemType Directory -Path (Split-Path -Parent $nagiFile.Target) -Force | Out-Null
        Copy-Item -LiteralPath $nagiFile.Source -Destination $nagiFile.Target -Force
    }
    $nagiRegistry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Nagi'
    if ((Test-Path $nagiRegistry) -and (Get-ItemProperty $nagiRegistry).InstallLocation -eq $nagiDestination) {
        $nagiOldVersion = (Get-ItemProperty $nagiRegistry).DisplayVersion
        Set-ItemProperty $nagiRegistry -Name DisplayVersion -Value $nagiPlan.Version
    }
    'Update installed successfully.' | Set-Content -LiteralPath $nagiLog
    Write-UpdateResult $true ('Nagi '+$nagiPlan.Version+' was installed successfully.')
    Start-UpdatedNagi
} catch {
    $nagiFailure = $_.Exception.Message
    $_ | Out-String | Set-Content -LiteralPath $nagiLog
    for ($nagiIndex = $nagiChanged.Count - 1; $nagiIndex -ge 0; $nagiIndex--) {
        $nagiFile = $nagiChanged[$nagiIndex]
        try {
            if ($nagiFile.Existed) { Copy-Item -LiteralPath $nagiFile.Backup -Destination $nagiFile.Target -Force }
            elseif (Test-Path -LiteralPath $nagiFile.Target) { Remove-Item -LiteralPath $nagiFile.Target -Force }
        } catch { $nagiRollbackFailed = $true; $_ | Out-String | Add-Content -LiteralPath $nagiLog }
    }
    if ($nagiOldVersion) { try { Set-ItemProperty $nagiRegistry -Name DisplayVersion -Value $nagiOldVersion } catch { $_ | Out-String | Add-Content -LiteralPath $nagiLog } }
    $nagiRecovery = if($nagiRollbackFailed){'Some files could not be restored. See '+$nagiLog}else{'Your previous app files were restored.'}
    try { Write-UpdateResult $false ('The update failed. '+$nagiRecovery+' '+$nagiFailure) } catch { $_ | Out-String | Add-Content -LiteralPath $nagiLog }
    if ($nagiPlan -and $nagiCanRestart) { try { Start-UpdatedNagi } catch { $_ | Out-String | Add-Content -LiteralPath $nagiLog } }
    exit 1
}
