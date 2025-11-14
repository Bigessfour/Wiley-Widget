<#
.SYNOPSIS
    Backup Wiley Widget Docker volumes and database

.DESCRIPTION
    Creates timestamped backups of Docker volumes and SQL Server database.
    Implements retention policy and compression.

.PARAMETER BackupPath
    Path to store backups (default: ./backups)

.PARAMETER RetentionDays
    Number of days to keep backups (default: 30)

.PARAMETER CompressBackups
    Enable compression for backups (default: true)

.EXAMPLE
    .\backup-docker-volumes.ps1
    .\backup-docker-volumes.ps1 -BackupPath "D:\Backups" -RetentionDays 60

.NOTES
    Author: Wiley Widget Team
    Date: November 14, 2025
    Requires: Docker, 7-Zip (for compression)
#>

[CmdletBinding()]
param(
    [string]$BackupPath = ".\backups",
    [int]$RetentionDays = 30,
    [bool]$CompressBackups = $true,
    [bool]$IncludeDatabase = $true
)

$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

# Ensure backup directory exists
if (-not (Test-Path $BackupPath)) {
    New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null
    Write-Host "✓ Created backup directory: $BackupPath" -ForegroundColor Green
}

# Function to backup Docker volume
function Backup-DockerVolume {
    param(
        [string]$VolumeName,
        [string]$OutputPath
    )
    
    Write-Host "→ Backing up volume: $VolumeName..." -ForegroundColor Cyan
    
    $volumeBackupPath = Join-Path $OutputPath "$VolumeName_$timestamp.tar"
    
    # Create temporary container to access volume
    $containerId = docker run -d --rm `
        -v "${VolumeName}:/volume" `
        -v "${OutputPath}:/backup" `
        alpine tar czf "/backup/$VolumeName_$timestamp.tar.gz" -C /volume .
    
    if ($LASTEXITCODE -eq 0) {
        docker wait $containerId | Out-Null
        Write-Host "✓ Volume backup completed: $volumeBackupPath.gz" -ForegroundColor Green
        return $true
    } else {
        Write-Host "✗ Volume backup failed: $VolumeName" -ForegroundColor Red
        return $false
    }
}

# Function to backup SQL Server database
function Backup-SqlServerDatabase {
    param(
        [string]$ContainerName = "WILEY_DB",
        [string]$OutputPath
    )
    
    Write-Host "→ Backing up SQL Server database..." -ForegroundColor Cyan
    
    $dbBackupPath = "/var/opt/mssql/backup/WileyWidget_$timestamp.bak"
    $localBackupPath = Join-Path $OutputPath "WileyWidget_$timestamp.bak"
    
    # Create backup inside container
    $backupCmd = @"
/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'WileyWidget!2025' -Q "BACKUP DATABASE [WileyWidget] TO DISK = N'$dbBackupPath' WITH NOFORMAT, NOINIT, NAME = 'WileyWidget-full', SKIP, NOREWIND, NOUNLOAD, STATS = 10" -C
"@
    
    docker exec $ContainerName bash -c $backupCmd
    
    if ($LASTEXITCODE -eq 0) {
        # Copy backup file from container
        docker cp "${ContainerName}:$dbBackupPath" $localBackupPath
        
        if (Test-Path $localBackupPath) {
            Write-Host "✓ Database backup completed: $localBackupPath" -ForegroundColor Green
            
            # Compress if enabled
            if ($CompressBackups) {
                Write-Host "→ Compressing database backup..." -ForegroundColor Cyan
                Compress-Archive -Path $localBackupPath -DestinationPath "$localBackupPath.zip" -Force
                Remove-Item $localBackupPath
                Write-Host "✓ Compression completed" -ForegroundColor Green
            }
            
            return $true
        }
    }
    
    Write-Host "✗ Database backup failed" -ForegroundColor Red
    return $false
}

# Function to cleanup old backups
function Remove-OldBackups {
    param(
        [string]$Path,
        [int]$Days
    )
    
    Write-Host "→ Cleaning up backups older than $Days days..." -ForegroundColor Cyan
    
    $cutoffDate = (Get-Date).AddDays(-$Days)
    $oldBackups = Get-ChildItem -Path $Path -File | Where-Object { $_.LastWriteTime -lt $cutoffDate }
    
    if ($oldBackups) {
        foreach ($backup in $oldBackups) {
            Remove-Item $backup.FullName -Force
            Write-Host "  Removed: $($backup.Name)" -ForegroundColor Gray
        }
        Write-Host "✓ Removed $($oldBackups.Count) old backup(s)" -ForegroundColor Green
    } else {
        Write-Host "✓ No old backups to remove" -ForegroundColor Green
    }
}

# Main execution
try {
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host "  Wiley Widget Docker Backup - $timestamp" -ForegroundColor Yellow
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host ""
    
    # Check if Docker is running
    docker ps > $null 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Docker is not running. Please start Docker and try again."
    }
    
    $successCount = 0
    $failCount = 0
    
    # Backup Docker volumes
    Write-Host "[1/3] Backing up Docker volumes..." -ForegroundColor Yellow
    Write-Host ""
    
    $volumes = @("wiley_widget_db-data", "wiley-widget-nuget-cache")
    foreach ($volume in $volumes) {
        if (Backup-DockerVolume -VolumeName $volume -OutputPath $BackupPath) {
            $successCount++
        } else {
            $failCount++
        }
    }
    
    Write-Host ""
    
    # Backup SQL Server database
    if ($IncludeDatabase) {
        Write-Host "[2/3] Backing up SQL Server database..." -ForegroundColor Yellow
        Write-Host ""
        
        if (Backup-SqlServerDatabase -OutputPath $BackupPath) {
            $successCount++
        } else {
            $failCount++
        }
        
        Write-Host ""
    }
    
    # Cleanup old backups
    Write-Host "[3/3] Cleanup old backups..." -ForegroundColor Yellow
    Write-Host ""
    
    Remove-OldBackups -Path $BackupPath -Days $RetentionDays
    
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host "  Backup Summary" -ForegroundColor Yellow
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host "  Successful: $successCount" -ForegroundColor Green
    Write-Host "  Failed: $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
    Write-Host "  Backup Path: $BackupPath" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Yellow
    Write-Host ""
    
    exit $(if ($failCount -eq 0) { 0 } else { 1 })
    
} catch {
    Write-Host ""
    Write-Host "✗ Backup failed: $_" -ForegroundColor Red
    Write-Host ""
    exit 1
}
