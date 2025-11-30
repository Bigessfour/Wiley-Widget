#Requires -Version 7.0

<#
.SYNOPSIS
    Automated backup script for WileyWidget SQL Server databases (full/differential/transaction log)

.DESCRIPTION
    Performs SQL Server backups using either Invoke-Sqlcmd (preferred) or sqlcmd.
    Supports compressed backups (uses SQL Server native compression), optional TDE/certificate-based encryption,
    retention cleanup, and optional offsite upload hooks.

.NOTES
    - Run as a service account with appropriate SQL Server backup privileges.
    - For encryption, create a server certificate and pass its name via -BackupCertificateName.
    - This script *does not* embed credentials; use integrated auth or run under a user with sufficient rights.

.EXAMPLE
    .\backup-wileywidget.ps1 -BackupType All -BackupRoot \\"C:\Backups\\WileyWidget\\" -SqlInstance ".\\SQLEXPRESS" -Database WileyWidgetDb -RetentionDays 14 -Compress -WhatIf

#>

[CmdletBinding()]
param(
    [ValidateSet('Full','Differential','Log','All')]
    [string]$BackupType = 'All',

    [string]$SqlInstance = '.\\SQLEXPRESS',
    [string]$Database = 'WileyWidgetDb',

    [string]$BackupRoot = "${PSScriptRoot}\\backups",
    [int]$RetentionDays = 14,

    [switch]$Compress,
    [string]$BackupCertificateName,

    [switch]$VerboseLogging,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $ts = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    $line = "[$ts] [$Level] $Message"
    if ($VerboseLogging) { Write-Host $line }
    Add-Content -Path (Join-Path $BackupRoot 'backup.log') -Value $line -ErrorAction SilentlyContinue
}

if ($WhatIf) {
    Write-Host "🧭 Running in WhatIf mode — no changes will be applied." -ForegroundColor Cyan
}

if (-not (Test-Path -Path $BackupRoot)) {
    if (-not $WhatIf) { New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null }
    Write-Log "Created backup root: $BackupRoot"
}

$timestamp = (Get-Date).ToString('yyyyMMdd_HHmmss')

function Invoke-TSQL {
    param([string]$Sql)

    # Try Invoke-Sqlcmd first
    if (Get-Command -Name Invoke-Sqlcmd -ErrorAction SilentlyContinue) {
        if ($WhatIf) { Write-Log "[WhatIf] Would run T-SQL (Invoke-Sqlcmd): $Sql"; return }
        return Invoke-Sqlcmd -ServerInstance $SqlInstance -Query $Sql -ErrorAction Stop
    }

    # Fall back to sqlcmd
    $sqlcmdPath = 'sqlcmd'
    $args = @('-S', $SqlInstance, '-Q', $Sql, '-b')
    if ($WhatIf) { Write-Log "[WhatIf] Would run sqlcmd: $sqlcmdPath $($args -join ' ')"; return }

    $proc = Start-Process -FilePath $sqlcmdPath -ArgumentList $args -NoNewWindow -Wait -PassThru -ErrorAction Stop
    if ($proc.ExitCode -ne 0) { throw "sqlcmd failed with exit code $($proc.ExitCode)" }
}

function Get-BackupFilePath([string]$type) {
    $fileName = "WileyWidget_${type}_${timestamp}.bak"
    return Join-Path $BackupRoot $fileName
}

try {
    Write-Log "Starting backup: Type=$BackupType, Instance=$SqlInstance, DB=$Database"

    if ($BackupType -eq 'Full' -or $BackupType -eq 'All') {
        $fullPath = Get-BackupFilePath -type 'FULL'
        $compressionClause = $Compress ? 'COMPRESSION' : ''
        $encryptClause = $BackupCertificateName ? "ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = [$BackupCertificateName])" : ''

        $sql = "BACKUP DATABASE [$Database] TO DISK = N'$fullPath' WITH INIT, FORMAT, STATS = 10" + ($compressionClause ? ", $compressionClause" : "") + ($encryptClause ? ", $encryptClause" : "")
        Write-Log "Running full backup -> $fullPath"
        Invoke-TSQL -Sql $sql
        Write-Log "Full backup completed: $fullPath"
    }

    if ($BackupType -eq 'Differential' -or $BackupType -eq 'All') {
        $diffPath = Get-BackupFilePath -type 'DIFF'
        $compressionClause = $Compress ? 'COMPRESSION' : ''
        $encryptClause = $BackupCertificateName ? "ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = [$BackupCertificateName])" : ''

        $sql = "BACKUP DATABASE [$Database] TO DISK = N'$diffPath' WITH DIFFERENTIAL, INIT, STATS = 10" + ($compressionClause ? ", $compressionClause" : "") + ($encryptClause ? ", $encryptClause" : "")
        Write-Log "Running differential backup -> $diffPath"
        Invoke-TSQL -Sql $sql
        Write-Log "Differential backup completed: $diffPath"
    }

    if ($BackupType -eq 'Log' -or $BackupType -eq 'All') {
        $logPath = Get-BackupFilePath -type 'LOG'
        $compressionClause = $Compress ? 'COMPRESSION' : ''
        $encryptClause = $BackupCertificateName ? "ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = [$BackupCertificateName])" : ''

        $sql = "BACKUP LOG [$Database] TO DISK = N'$logPath' WITH INIT, STATS = 10" + ($compressionClause ? ", $compressionClause" : "") + ($encryptClause ? ", $encryptClause" : "")
        Write-Log "Running transaction log backup -> $logPath"
        Invoke-TSQL -Sql $sql
        Write-Log "Transaction log backup completed: $logPath"
    }

    # Retention cleanup
    Write-Log "Running retention cleanup for files older than $RetentionDays days"
    if (-not $WhatIf) {
        $cutoff = (Get-Date).AddDays(-$RetentionDays)
        $files = Get-ChildItem -Path $BackupRoot -Filter '*.bak' -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -lt $cutoff }
        foreach ($f in $files) {
            try {
                Remove-Item -Path $f.FullName -Force -ErrorAction Stop
                Write-Log "Deleted old backup: $($f.Name)" -Level 'DEBUG'
            }
            catch {
                Write-Log "Failed to delete $($f.FullName): $_" -Level 'WARN'
            }
        }
    }

    Write-Log "Backup operation completed successfully." -Level 'SUCCESS'
    exit 0
}
catch {
    Write-Log "Backup operation failed: $_" -Level 'ERROR'
    throw
}
