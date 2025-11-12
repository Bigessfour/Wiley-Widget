<#
.SYNOPSIS
    Diagnoses and fixes SQL Server single-user mode issues for WileyWidget application.

.DESCRIPTION
    This script addresses the "Server is in single user mode" error by:
    1. Detecting which SQL Server instance is in single-user mode
    2. Killing all blocking connections
    3. Setting database to MULTI_USER mode
    4. Verifying connection health
    5. Providing detailed diagnostics

.PARAMETER ServerInstance
    SQL Server instance name (default: .\SQLEXPRESS)

.PARAMETER DatabaseName
    Database name to fix (default: WileyWidgetDev)

.PARAMETER KillConnections
    Force kill all connections to the database before fixing

.PARAMETER VerifyOnly
    Only check status without making changes

.EXAMPLE
    .\Fix-DatabaseSingleUserMode.ps1
    Automatically detects and fixes single-user mode issues

.EXAMPLE
    .\Fix-DatabaseSingleUserMode.ps1 -VerifyOnly
    Check database status without making changes

.EXAMPLE
    .\Fix-DatabaseSingleUserMode.ps1 -KillConnections
    Force kill all connections before fixing
#>

[CmdletBinding()]
param(
    [string]$ServerInstance = ".\SQLEXPRESS",
    [string]$DatabaseName = "WileyWidgetDev",
    [switch]$KillConnections,
    [switch]$VerifyOnly
)

$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "SQL Server Single-User Mode Diagnostic & Fix Tool" -ForegroundColor Cyan
Write-Host "Timestamp: $timestamp" -ForegroundColor Gray
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

#region Helper Functions

function Write-Status {
    param(
        [string]$Message,
        [ValidateSet("Info", "Success", "Warning", "Error")]
        [string]$Level = "Info"
    )
    
    $color = switch ($Level) {
        "Success" { "Green" }
        "Warning" { "Yellow" }
        "Error" { "Red" }
        default { "White" }
    }
    
    $prefix = switch ($Level) {
        "Success" { "[✓]" }
        "Warning" { "[!]" }
        "Error" { "[✗]" }
        default { "[i]" }
    }
    
    Write-Host "$prefix $Message" -ForegroundColor $color
}

function Invoke-SqlQuery {
    param(
        [string]$Query,
        [string]$Server = $ServerInstance,
        [string]$Database = "master",
        [switch]$SuppressErrors
    )
    
    try {
        $result = sqlcmd -S $Server -d $Database -Q $Query -W -h -1 2>&1
        
        if ($LASTEXITCODE -ne 0 -and -not $SuppressErrors) {
            throw "SQL query failed with exit code $LASTEXITCODE"
        }
        
        return $result
    }
    catch {
        if (-not $SuppressErrors) {
            throw
        }
        return $null
    }
}

#endregion

#region Step 1: Check SQL Server Service Status

Write-Host ""
Write-Host "Step 1: Checking SQL Server Services..." -ForegroundColor Yellow
Write-Host ("─" * 66) -ForegroundColor DarkGray

try {
    $sqlServices = Get-Service -Name "MSSQL*" | Where-Object { $_.Status -eq "Running" }
    
    if ($sqlServices.Count -eq 0) {
        Write-Status "No running SQL Server instances found!" -Level Error
        exit 1
    }
    
    foreach ($service in $sqlServices) {
        Write-Status "Found: $($service.DisplayName) - $($service.Status)" -Level Success
    }
}
catch {
    Write-Status "Failed to check SQL Server services: $_" -Level Error
    exit 1
}

#endregion

#region Step 2: Check Database Existence and Access Mode

Write-Host ""
Write-Host "Step 2: Checking Database Access Mode..." -ForegroundColor Yellow
Write-Host ("─" * 66) -ForegroundColor DarkGray

$checkDbQuery = @"
SELECT 
    name,
    state_desc,
    user_access_desc,
    is_read_only,
    recovery_model_desc
FROM sys.databases 
WHERE name = '$DatabaseName'
"@

try {
    $dbInfo = Invoke-SqlQuery -Query $checkDbQuery
    
    if ([string]::IsNullOrWhiteSpace($dbInfo)) {
        Write-Status "Database '$DatabaseName' not found on $ServerInstance" -Level Error
        Write-Status "Available databases:" -Level Info
        $allDbs = Invoke-SqlQuery -Query "SELECT name FROM sys.databases"
        $allDbs | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
        exit 1
    }
    
    Write-Status "Database found: $DatabaseName" -Level Success
    Write-Host ""
    Write-Host "Database Information:" -ForegroundColor Cyan
    Write-Host $dbInfo -ForegroundColor Gray
    
    # Parse user_access_desc from output
    $isSingleUser = $dbInfo -match "SINGLE_USER"
    
    if ($isSingleUser) {
        Write-Status "Database is in SINGLE_USER mode!" -Level Error
    }
    else {
        Write-Status "Database is in MULTI_USER mode" -Level Success
    }
}
catch {
    Write-Status "Failed to query database information: $_" -Level Error
    exit 1
}

#endregion

#region Step 3: Check Active Connections

Write-Host ""
Write-Host "Step 3: Checking Active Connections..." -ForegroundColor Yellow
Write-Host ("─" * 66) -ForegroundColor DarkGray

$connectionQuery = @"
SELECT 
    session_id,
    login_name,
    host_name,
    program_name,
    status,
    login_time
FROM sys.dm_exec_sessions 
WHERE database_id = DB_ID('$DatabaseName')
    AND session_id <> @@SPID
"@

try {
    $connections = Invoke-SqlQuery -Query $connectionQuery -SuppressErrors
    
    if ([string]::IsNullOrWhiteSpace($connections)) {
        Write-Status "No active connections to $DatabaseName" -Level Success
    }
    else {
        Write-Status "Active connections found:" -Level Warning
        Write-Host $connections -ForegroundColor Gray
        
        if ($KillConnections -and -not $VerifyOnly) {
            Write-Host ""
            Write-Status "Killing active connections..." -Level Warning
            
            $killQuery = @"
DECLARE @kill varchar(8000) = '';
SELECT @kill = @kill + 'KILL ' + CONVERT(varchar(5), session_id) + ';'
FROM sys.dm_exec_sessions 
WHERE database_id = DB_ID('$DatabaseName')
    AND session_id <> @@SPID;
EXEC(@kill);
"@
            
            Invoke-SqlQuery -Query $killQuery | Out-Null
            Write-Status "Connections terminated" -Level Success
            Start-Sleep -Seconds 2
        }
    }
}
catch {
    Write-Status "Failed to check connections: $_" -Level Error
}

#endregion

#region Step 4: Fix Single-User Mode

if ($isSingleUser -and -not $VerifyOnly) {
    Write-Host ""
    Write-Host "Step 4: Fixing Single-User Mode..." -ForegroundColor Yellow
    Write-Host ("─" * 66) -ForegroundColor DarkGray
    
    try {
        # First, ensure we can access the database
        $setMultiUserQuery = @"
USE master;
ALTER DATABASE [$DatabaseName] SET MULTI_USER WITH ROLLBACK IMMEDIATE;
"@
        
        Write-Status "Setting database to MULTI_USER mode..." -Level Info
        Invoke-SqlQuery -Query $setMultiUserQuery | Out-Null
        Write-Status "Database set to MULTI_USER mode successfully!" -Level Success
        
        # Verify the change
        Start-Sleep -Seconds 2
        $verifyQuery = "SELECT user_access_desc FROM sys.databases WHERE name = '$DatabaseName'"
        $newMode = Invoke-SqlQuery -Query $verifyQuery
        
        if ($newMode -match "MULTI_USER") {
            Write-Status "Verification successful - database is now in MULTI_USER mode" -Level Success
        }
        else {
            Write-Status "Verification failed - database mode: $newMode" -Level Error
        }
    }
    catch {
        Write-Status "Failed to fix single-user mode: $_" -Level Error
        Write-Status "Manual intervention may be required" -Level Warning
        exit 1
    }
}
elseif ($VerifyOnly) {
    Write-Host ""
    Write-Status "Verify-only mode - no changes made" -Level Info
}
else {
    Write-Host ""
    Write-Status "No single-user mode issue detected" -Level Success
}

#endregion

#region Step 5: Test Connection

Write-Host ""
Write-Host "Step 5: Testing Connection..." -ForegroundColor Yellow
Write-Host ("─" * 66) -ForegroundColor DarkGray

$connectionString = "Server=$ServerInstance;Database=$DatabaseName;Trusted_Connection=True;TrustServerCertificate=True;"

try {
    $testQuery = "SELECT @@VERSION AS Version, DB_NAME() AS CurrentDatabase, USER_NAME() AS CurrentUser"
    $testResult = Invoke-SqlQuery -Query $testQuery -Database $DatabaseName
    
    Write-Status "Connection test successful!" -Level Success
    Write-Host ""
    Write-Host "Connection Details:" -ForegroundColor Cyan
    Write-Host $testResult -ForegroundColor Gray
}
catch {
    Write-Status "Connection test failed: $_" -Level Error
    Write-Status "The application may still have connection issues" -Level Warning
}

#endregion

#region Step 6: Additional Diagnostics

Write-Host ""
Write-Host "Step 6: Additional Diagnostics..." -ForegroundColor Yellow
Write-Host ("─" * 66) -ForegroundColor DarkGray

# Check for locks
$lockQuery = @"
SELECT 
    request_session_id,
    resource_type,
    resource_database_id,
    request_mode,
    request_status
FROM sys.dm_tran_locks
WHERE resource_database_id = DB_ID('$DatabaseName')
"@

try {
    $locks = Invoke-SqlQuery -Query $lockQuery -SuppressErrors
    
    if ([string]::IsNullOrWhiteSpace($locks)) {
        Write-Status "No locks detected on database" -Level Success
    }
    else {
        Write-Status "Active locks found:" -Level Warning
        Write-Host $locks -ForegroundColor Gray
    }
}
catch {
    Write-Status "Could not check for locks: $_" -Level Warning
}

# Check database file status
$fileQuery = @"
SELECT 
    name,
    physical_name,
    state_desc,
    size * 8 / 1024 AS size_mb
FROM sys.master_files
WHERE database_id = DB_ID('$DatabaseName')
"@

try {
    $files = Invoke-SqlQuery -Query $fileQuery
    Write-Host ""
    Write-Status "Database Files:" -Level Info
    Write-Host $files -ForegroundColor Gray
}
catch {
    Write-Status "Could not retrieve file information: $_" -Level Warning
}

#endregion

#region Summary

Write-Host ""
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

if ($VerifyOnly) {
    Write-Host "✓ Diagnostic check completed (no changes made)" -ForegroundColor Yellow
}
elseif ($isSingleUser) {
    Write-Host "✓ Single-user mode issue fixed!" -ForegroundColor Green
    Write-Host "✓ Database is now accessible" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "  1. Restart your application" -ForegroundColor Gray
    Write-Host "  2. Monitor for connection errors" -ForegroundColor Gray
    Write-Host "  3. Check application logs in logs/" -ForegroundColor Gray
}
else {
    Write-Host "✓ No single-user mode issues detected" -ForegroundColor Green
    Write-Host ""
    Write-Host "If you're still experiencing connection errors:" -ForegroundColor Yellow
    Write-Host "  1. Run with -KillConnections to force close connections" -ForegroundColor Gray
    Write-Host "  2. Check application connection string configuration" -ForegroundColor Gray
    Write-Host "  3. Review DryIoc container registration errors" -ForegroundColor Gray
    Write-Host "  4. Check logs/startup-*.txt for detailed diagnostics" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Connection String:" -ForegroundColor Cyan
Write-Host "  $connectionString" -ForegroundColor Gray
Write-Host ""

#endregion
