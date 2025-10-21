<#
migration_precheck.ps1

Checks the target SQL Server database for the presence of tables, columns and
constraints referenced by the SyncDateOnlyToDateTime migration. Exits with
non-zero code if any critical object is missing.

Usage examples:
  # Integrated security
  .\migration_precheck.ps1 -Server localhost -Database WileyWidget -Integrated

  # SQL auth
  .\migration_precheck.ps1 -Server mydbserver -Database WileyWidget -User dbuser -Password 'p@ss'

#>
param(
    [string]$Server = 'localhost',
    [string]$Database = 'WileyWidgetDev',
    [string]$User,
    [SecureString]$Password,
    [switch]$Integrated,
    [System.Management.Automation.PSCredential]$Credential,
    [switch]$DryRun
)

Add-Type -AssemblyName System.Data

if ($Integrated -or (-not $User)) {
    $connString = "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
}
else {
    if (-not $Password) { Write-Error "Password is required when using -User"; exit 2 }
    $connString = "Server=$Server;Database=$Database;User Id=$User;Password=$Password;TrustServerCertificate=True;"
}

function Invoke-Scalar([string]$sql) {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connString)
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $sql
        $conn.Open()
        $result = $cmd.ExecuteScalar()
        return $result
    }
    catch {
        Write-Error "SQL execution error: $_"
        return $null
    }
    finally {
        if ($conn.State -eq 'Open') { $conn.Close() }
    }
}

$errors = @()
$warnings = @()

Write-Verbose "Running migration pre-check against $Server / $Database`n"

# Check/establish a working connection string (try multiple protocols)
function Test-OpenConnection([string]$cs) {
    $conn = New-Object System.Data.SqlClient.SqlConnection($cs)
    try {
        $conn.Open()
        if ($conn.State -eq 'Open') { $conn.Close(); return $true }
        return $false
    }
    catch {
        return $false
    }
    finally {
        if ($conn.State -eq 'Open') { $conn.Close() }
    }
}

# Build candidate connection strings depending on credentials
$candidates = @()
if ($Integrated -or (-not $User)) {
    $candidates += "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
}
else {
    if (-not $Password) { Write-Error "Password is required when using -User"; exit 2 }
    $candidates += "Server=$Server;Database=$Database;User Id=$User;Password=$Password;TrustServerCertificate=True;"
}

if ($Credential) {
    $plainPwd = $Credential.GetNetworkCredential().Password
    $candidates += "Server=$Server;Database=$Database;User Id=$($Credential.UserName);Password=$plainPwd;TrustServerCertificate=True;"
}

# If the server is a named instance like '.\\SQLEXPRESS', add Named Pipes candidate
if ($Server -match "^(.+?)\\\\(?<inst>.+)$") {
    $inst = $Matches['inst']
}
else {
    # handle formats like '.\SQLEXPRESS' or 'localhost\SQLEXPRESS'
    if ($Server -match '\\') { $parts = $Server.Split('\\'); $inst = $parts[-1] } else { $inst = $null }
}
if ($inst) {
    $np = "Server=np:\\\\.\\\\pipe\\MSSQL$inst\\sql\\query;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
    $candidates += $np
}

# Add common TCP ports to try (if SQL Browser/DNS fails)
$commonPorts = @(1433, 14333)
foreach ($p in $commonPorts) {
    $candidates += "Server=tcp:localhost,$p;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
}

$connString = $null
foreach ($cs in $candidates) {
    Write-Verbose "Trying connection string (masked): $($cs -replace 'Password=[^;]*','Password=*****' -replace 'User Id=[^;]*','User Id=*****')"
    if (Test-OpenConnection $cs) { $connString = $cs; break }
}

if (-not $connString) {
    Write-Error "Either use -Integrated or supply -Credential (PSCredential), and ensure the instance is reachable. No connection candidate succeeded."
    exit 2
}

# Print connection info (mask password) for diagnostics when verbose
$maskedConn = $connString -replace 'Password=[^;]*', 'Password=*****' -replace 'User Id=[^;]*', 'User Id=*****'
Write-Verbose "Using connection string: $maskedConn"

$hasHistory = Invoke-Scalar("SELECT OBJECT_ID(N'[dbo].[__EFMigrationsHistory]','U')")
if (-not $hasHistory) {
    $warnings += "__EFMigrationsHistory table not found. If migrations have been applied before, this could indicate the DB was created outside of EF migrations."
}
else {
    $lastMigration = Invoke-Scalar("SELECT TOP 1 [MigrationId] FROM [dbo].[__EFMigrationsHistory] ORDER BY [MigrationId] DESC")
    Write-Verbose "Last applied migration: $lastMigration`n"
}

# Define checks (table, column, constraint names discovered from the migration)
$checks = @(
    @{ type = 'table'; sql = "SELECT OBJECT_ID(N'dbo.BudgetPeriods','U')"; name = 'dbo.BudgetPeriods' ; level = 'critical' },
    @{ type = 'table'; sql = "SELECT OBJECT_ID(N'dbo.Invoices','U')"; name = 'dbo.Invoices' ; level = 'warning' },
    @{ type = 'table'; sql = "SELECT OBJECT_ID(N'dbo.MunicipalAccounts','U')"; name = 'dbo.MunicipalAccounts' ; level = 'critical' },
    @{ type = 'table'; sql = "SELECT OBJECT_ID(N'dbo.BudgetEntries','U')"; name = 'dbo.BudgetEntries' ; level = 'critical' },
    @{ type = 'column'; sql = "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.MunicipalAccounts','U') AND name = 'AccountNumber'"; name = 'MunicipalAccounts.AccountNumber' ; level = 'critical' },
    @{ type = 'fk'; sql = "SELECT COUNT(*) FROM sys.foreign_keys WHERE name = 'FK_Invoices_MunicipalAccounts_MunicipalAccountId'"; name = 'FK_Invoices_MunicipalAccounts_MunicipalAccountId' ; level = 'warning' },
    @{ type = 'fk'; sql = "SELECT COUNT(*) FROM sys.foreign_keys WHERE name = 'FK_Invoices_Vendor_VendorId'"; name = 'FK_Invoices_Vendor_VendorId' ; level = 'warning' },
    @{ type = 'fk'; sql = "SELECT COUNT(*) FROM sys.foreign_keys WHERE name = 'FK_MunicipalAccounts_BudgetPeriods_BudgetPeriodId'"; name = 'FK_MunicipalAccounts_BudgetPeriods_BudgetPeriodId' ; level = 'critical' },
    @{ type = 'pk'; sql = "SELECT COUNT(*) FROM sys.key_constraints WHERE name = 'PK_BudgetPeriods'"; name = 'PK_BudgetPeriods' ; level = 'warning' },
    @{ type = 'pk'; sql = "SELECT COUNT(*) FROM sys.key_constraints WHERE name = 'PK_Invoices'"; name = 'PK_Invoices' ; level = 'warning' }
)

foreach ($c in $checks) {
    $r = Invoke-Scalar($c.sql)
    if ($null -eq $r) {
        $errors += "Failed to check $($c.name): SQL error (query: $($c.sql))"
        continue
    }
    # normalize/inspect result safely
    $present = $false
    switch ($c.type) {
        'table' {
            if ($r -is [int]) { $present = ($r -gt 0) }
            elseif ($r -is [long]) { $present = ([int]$r -gt 0) }
            elseif ($r -is [string] -and $r -match '^\d+$') { $present = ([int]$r -gt 0) }
            else { $present = $false }
        }
        'column' {
            $present = ($null -ne $r) -and ($r -ne '')
        }
        default {
            if ($r -is [int] -or $r -is [long]) { $present = ([int]$r -gt 0) }
            elseif ($r -is [string] -and $r -match '^\d+$') { $present = ([int]$r -gt 0) }
            else { $present = $false }
        }
    }

    if (-not $present) {
        $msg = "MISSING: $($c.name) (type=$($c.type))"
        if ($c.level -eq 'critical') { $errors += $msg } else { $warnings += $msg }
    }
    else {
        Write-Verbose "OK: $($c.name)"
    }
}

Write-Output "`nPre-check results:`n"
if ($warnings.Count -gt 0) {
    Write-Warning "Warnings:`n$($warnings -join "`n")"
}
if ($errors.Count -gt 0) {
    Write-Error "Errors:`n$($errors -join "`n")"
    if ($DryRun) {
        Write-Warning "(DryRun) Pre-check would have failed; no exit code returned because -DryRun was specified."
        exit 0
    }
    Write-Error "Pre-check failed. Fix the missing objects or adjust/harden the migration script before applying."
    exit 3
}
else {
    Write-Output "No critical errors detected. You may proceed to review and apply the idempotent script on a staging DB."
    exit 0
}
