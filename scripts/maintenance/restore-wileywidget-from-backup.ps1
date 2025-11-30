#Requires -Version 7.0

<#
.SYNOPSIS
    Restore WileyWidget database from a full backup (optionally differential + logs) into a staging/target DB for verification/testing.

.DESCRIPTION
    This helper automates the common restore workflow:
      1) RESTORE FILELISTONLY from the full backup to determine logical file names
      2) RESTORE DATABASE <target> FROM DISK = <full> WITH MOVE ... , NORECOVERY (or RECOVERY if no subsequent restores)
      3) (optional) RESTORE DATABASE <target> FROM DISK = <differential> WITH NORECOVERY
      4) (optional) RESTORE LOG <target> FROM DISK = <log1>, ... , WITH (RECOVERY on final restore)

    Designed for manual verification in staging/test environments. Do NOT run against production without understanding physical file moves and permission requirements.

.EXAMPLE
    .\restore-wileywidget-from-backup.ps1 -FullBackupFile C:\backups\WileyWidget_FULL_20251130_010000.bak -TargetDatabase WileyWidget_Staging -SqlInstance ".\\SQLEXPRESS" -RestoreRoot C:\temp\restores -WhatIf

#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FullBackupFile,

    [string]$DiffBackupFile,

    [string[]]$LogBackupFiles,

    [string]$TargetDatabase = "WileyWidget_Restore_$((Get-Date).ToString('yyyyMMdd_HHmmss'))",

    [string]$SqlInstance = '.\\SQLEXPRESS',

    [string]$RestoreRoot = "${PSScriptRoot}\\restores",

    [switch]$VerboseLogging,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Log { param($m) if ($VerboseLogging) { Write-Host "[Restore] $m" } }

if (-not (Test-Path -Path $RestoreRoot)) {
    if (-not $WhatIf) { New-Item -ItemType Directory -Path $RestoreRoot -Force | Out-Null }
    Write-Log "Created restore root: $RestoreRoot"
}

function Invoke-TSQL {
    param([string]$Sql)
    if (Get-Command -Name Invoke-Sqlcmd -ErrorAction SilentlyContinue) {
        if ($WhatIf) { Write-Log "[WhatIf] SQL: $Sql"; return }
        return Invoke-Sqlcmd -ServerInstance $SqlInstance -Query $Sql -ErrorAction Stop
    }
    $sqlcmdPath = 'sqlcmd'
    $args = @('-S', $SqlInstance, '-Q', $Sql, '-b')
    if ($WhatIf) { Write-Log "[WhatIf] sqlcmd: $sqlcmdPath $($args -join ' ')"; return }
    $proc = Start-Process -FilePath $sqlcmdPath -ArgumentList $args -NoNewWindow -Wait -PassThru -ErrorAction Stop
    if ($proc.ExitCode -ne 0) { throw "sqlcmd failed with exit code $($proc.ExitCode)" }
}

try {
    Write-Log "Gathering file list from full backup: $FullBackupFile"
    $filelistSql = "RESTORE FILELISTONLY FROM DISK = N'$FullBackupFile'"
    $fileList = Invoke-TSQL -Sql $filelistSql

    # Invoke-Sqlcmd returns objects; sqlcmd path will not return structured objects. Try to normalize.
    if (-not $fileList -or ($fileList -is [String])) {
        # If we got back plain text, attempt to parse via regex lines (best-effort)
        $text = $fileList -join "`n"
        $rows = $text -split "`n" | Where-Object { $_ -match '\S' }
        Write-Log "Note: sqlcmd path returned textual output; parsed file list may be incomplete."
        # fallback names
        $logicalDataName = "WileyWidget"
        $logicalLogName = "WileyWidget_log"
    }
    else {
        # Usually returns columns: LogicalName, PhysicalName, Type
        $logicalDataName = ($fileList | Where-Object { $_.Type -eq 'D' } | Select-Object -First 1).LogicalName
        $logicalLogName = ($fileList | Where-Object { $_.Type -eq 'L' } | Select-Object -First 1).LogicalName
    }

    Write-Log "Logical data name: $logicalDataName, log name: $logicalLogName"

    $dataFile = Join-Path $RestoreRoot "$($TargetDatabase)_Data.mdf"
    $logFile = Join-Path $RestoreRoot "$($TargetDatabase)_Log.ldf"

    $restoreFullSql = "RESTORE DATABASE [$TargetDatabase] FROM DISK = N'$FullBackupFile' WITH MOVE N'$logicalDataName' TO N'$dataFile', MOVE N'$logicalLogName' TO N'$logFile', FILE = 1, NORECOVERY, STATS = 10"

    if ($WhatIf) { Write-Log "[WhatIf] $restoreFullSql" }
    else { Invoke-TSQL -Sql $restoreFullSql; Write-Log "Full backup restored (NORECOVERY) to $TargetDatabase" }

    if ($DiffBackupFile) {
        $restoreDiffSql = "RESTORE DATABASE [$TargetDatabase] FROM DISK = N'$DiffBackupFile' WITH NORECOVERY, STATS = 10"
        if ($WhatIf) { Write-Log "[WhatIf] $restoreDiffSql" }
        else { Invoke-TSQL -Sql $restoreDiffSql; Write-Log "Differential backup applied (NORECOVERY)" }
    }

    if ($LogBackupFiles -and $LogBackupFiles.Count -gt 0) {
        for ($i = 0; $i -lt $LogBackupFiles.Count; $i++) {
            $logFilePath = $LogBackupFiles[$i]
            $isLast = ($i -eq ($LogBackupFiles.Count - 1))
            $recoveryClause = $isLast ? 'RECOVERY' : 'NORECOVERY'
            $restoreLogSql = "RESTORE LOG [$TargetDatabase] FROM DISK = N'$logFilePath' WITH $recoveryClause, STATS=10"
            if ($WhatIf) { Write-Log "[WhatIf] $restoreLogSql" }
            else { Invoke-TSQL -Sql $restoreLogSql; Write-Log "Restored log: $logFilePath ($recoveryClause)" }
        }
    }
    else {
        # If no logs provided, finish with RECOVERY
        $finishSql = "RESTORE DATABASE [$TargetDatabase] WITH RECOVERY"
        if ($WhatIf) { Write-Log "[WhatIf] $finishSql" }
        else { Invoke-TSQL -Sql $finishSql; Write-Log "Database brought online (RECOVERY)" }
    }

    Write-Log "Restore completed for $TargetDatabase"
}
catch {
    Write-Log "Restore failed: $_"
    throw
}
