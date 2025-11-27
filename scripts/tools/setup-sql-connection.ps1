#Requires -Version 7.5
<#
.SYNOPSIS
    Setup and test SQL Server connection for WileyWidget database.

.DESCRIPTION
    Configures SQL Server connection, tests connectivity, and registers with MCP SQL tools.
    Supports both local SQL Server and Docker-based SQL Server instances.

.PARAMETER Server
    SQL Server instance (default: localhost)

.PARAMETER Database
    Database name (default: WileyWidget)

.PARAMETER Port
    SQL Server port (default: 1433)

.PARAMETER UseDocker
    Use Docker container connection (db:1433)

.PARAMETER CreateDatabase
    Create database if it doesn't exist

.PARAMETER RunMigrations
    Run SQL migration scripts from sql/ folder

.EXAMPLE
    .\setup-sql-connection.ps1 -CreateDatabase -RunMigrations
#>

[CmdletBinding()]
param(
    [string]$Server = "localhost",
    [string]$Database = "WileyWidget",
    [int]$Port = 1433,
    [switch]$UseDocker,
    [switch]$CreateDatabase,
    [switch]$RunMigrations,
    [switch]$Verbose
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Colors for output
function Write-Success { Write-Host "✓ $args" -ForegroundColor Green }
function Write-Info { Write-Host "ℹ $args" -ForegroundColor Cyan }
function Write-Warning { Write-Host "⚠ $args" -ForegroundColor Yellow }
function Write-Error { Write-Host "✗ $args" -ForegroundColor Red }

# Get workspace root
$WorkspaceRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$SqlScriptsPath = Join-Path $WorkspaceRoot "sql"

# For Windows Authentication, no password needed
$UseWindowsAuth = $true
if ($UseDocker -or $env:AZURE_SQL_PASSWORD) {
    $UseWindowsAuth = $false
    if (-not $env:AZURE_SQL_PASSWORD) {
        Write-Error "AZURE_SQL_PASSWORD environment variable required for Docker/SQL Auth"
        Write-Info "Set it using: `$env:AZURE_SQL_PASSWORD = 'YourPassword'"
        exit 1
    }
}

# Adjust server for Docker or Express
if ($UseDocker) {
    $Server = "db"
    Write-Info "Using Docker SQL Server at $Server`:$Port"
} elseif ($Server -eq "localhost" -and -not $Server.Contains("\")) {
    # Check if SQLEXPRESS instance exists
    $sqlServices = Get-Service -Name "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
    if ($sqlServices) {
        $Server = "localhost\SQLEXPRESS"
        Write-Info "Using SQL Server Express at $Server"
    } else {
        Write-Info "Using default SQL Server instance at $Server"
    }
} else {
    Write-Info "Using SQL Server at $Server"
}

# Build connection string
if ($UseWindowsAuth) {
    $ConnectionString = "Server=$Server;Database=master;Integrated Security=True;TrustServerCertificate=True;"
    $SqlCmdAuth = "-E"  # Windows Authentication
} else {
    $ConnectionString = "Server=$Server,$Port;Database=master;User Id=sa;Password=$env:AZURE_SQL_PASSWORD;TrustServerCertificate=True;Encrypt=False;"
    $SqlCmdAuth = "-U sa -P $env:AZURE_SQL_PASSWORD"
}

Write-Info "Testing SQL Server connection..."

try {
    # Test connection using sqlcmd if available
    $sqlcmdPath = Get-Command sqlcmd -ErrorAction SilentlyContinue

    if ($sqlcmdPath) {
        Write-Info "Using sqlcmd to test connection"

        if ($UseWindowsAuth) {
            $result = & sqlcmd -S $Server -E -Q "SELECT @@VERSION" -b
        } else {
            $result = & sqlcmd -S "$Server,$Port" -U sa -P $env:AZURE_SQL_PASSWORD -Q "SELECT @@VERSION" -b
        }

        if ($LASTEXITCODE -eq 0) {
            Write-Success "SQL Server connection successful"
            Write-Info "Version: $($result[0])"
        } else {
            Write-Error "Connection failed"
            exit 1
        }
    } else {
        Write-Warning "sqlcmd not found. Install SQL Server command-line tools."
        Write-Info "Download from: https://learn.microsoft.com/en-us/sql/tools/sqlcmd-utility"
    }
} catch {
    Write-Error "Connection failed: $_"
    exit 1
}# Create database if requested
if ($CreateDatabase) {
    Write-Info "Creating database '$Database'..."

    try {
        $createDbSql = @"
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'$Database')
BEGIN
    CREATE DATABASE [$Database];
    PRINT 'Database created successfully';
END
ELSE
BEGIN
    PRINT 'Database already exists';
END
"@

        if ($sqlcmdPath) {
            if ($UseWindowsAuth) {
                $result = $createDbSql | & sqlcmd -S $Server -E -b
            } else {
                $result = $createDbSql | & sqlcmd -S "$Server,$Port" -U sa -P $env:AZURE_SQL_PASSWORD -b
            }
            Write-Success "Database '$Database' ready"
        }
    } catch {
        Write-Error "Failed to create database: $_"
        exit 1
    }
}

# Run migrations if requested
if ($RunMigrations) {
    Write-Info "Running SQL migration scripts..."

    if (-not (Test-Path $SqlScriptsPath)) {
        Write-Warning "SQL scripts folder not found: $SqlScriptsPath"
    } else {
        $sqlFiles = Get-ChildItem -Path $SqlScriptsPath -Filter "*.sql" | Sort-Object Name

        if ($sqlFiles.Count -eq 0) {
            Write-Warning "No SQL scripts found in $SqlScriptsPath"
        } else {
            Write-Info "Found $($sqlFiles.Count) SQL scripts"

            foreach ($sqlFile in $sqlFiles) {
                Write-Info "Executing: $($sqlFile.Name)"

                try {
                    if ($sqlcmdPath) {
                        if ($UseWindowsAuth) {
                            & sqlcmd -S $Server -E -d $Database -i $sqlFile.FullName -b
                        } else {
                            & sqlcmd -S "$Server,$Port" -U sa -P $env:AZURE_SQL_PASSWORD -d $Database -i $sqlFile.FullName -b
                        }

                        if ($LASTEXITCODE -eq 0) {
                            Write-Success "  ✓ $($sqlFile.Name) executed successfully"
                        } else {
                            Write-Error "  ✗ $($sqlFile.Name) failed"
                        }
                    }
                } catch {
                    Write-Error "  ✗ Failed to execute $($sqlFile.Name): $_"
                }
            }

            Write-Success "Migration scripts completed"
        }
    }
}

# Output connection details for VS Code
Write-Info "`nConnection details for VS Code:"
$authType = if ($UseWindowsAuth) { "Integrated" } else { "SqlLogin" }
$authInfo = if ($UseWindowsAuth) { "" } else { @"
      "user": "sa",
      "password": "",
      "savePassword": false,
"@ }

Write-Host @"

Add this to your .vscode/settings.json (if not already present):

{
  "mssql.connections": [
    {
      "server": "$Server",
      "database": "$Database",
      "authenticationType": "$authType",$authInfo
      "profileName": "WileyWidget-Local",
      "encrypt": "optional",
      "trustServerCertificate": true
    }
  ]
}

"@ -ForegroundColor Cyan

Write-Success "`nSetup complete! You can now:"
Write-Host "  1. Use MCP SQL tools to query the database" -ForegroundColor White
Write-Host "  2. Connect via Azure Data Studio or SQL Server Management Studio" -ForegroundColor White
Write-Host "  3. Use sqlcmd for command-line queries" -ForegroundColor White

# Create a quick reference card
$authCmd = if ($UseWindowsAuth) { "-E" } else { "-U sa -P %AZURE_SQL_PASSWORD%" }
$quickRef = @"
# SQL Server Quick Reference

## Connection Details
- Server: $Server
- Database: $Database
- Authentication: $(if ($UseWindowsAuth) { "Windows Authentication" } else { "SQL Authentication (sa)" })

## Quick Commands

### List all tables
sqlcmd -S $Server $authCmd -d $Database -Q "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"

### Run a SQL file
sqlcmd -S $Server $authCmd -d $Database -i path\to\script.sql

### Query data
sqlcmd -S $Server $authCmd -d $Database -Q "SELECT * FROM YourTable"

### Backup database
sqlcmd -S $Server $authCmd -Q "BACKUP DATABASE [$Database] TO DISK = 'C:\Backups\$Database.bak'"

### Create database (if needed)
sqlcmd -S $Server $authCmd -Q "CREATE DATABASE [$Database]"

"@

$quickRefPath = Join-Path $WorkspaceRoot "docs" "sql-quick-reference.md"
$quickRef | Out-File -FilePath $quickRefPath -Encoding UTF8 -Force
Write-Success "Quick reference saved to: $quickRefPath"
