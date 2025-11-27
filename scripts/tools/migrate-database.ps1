#Requires -Version 7.5
<#
.SYNOPSIS
    Apply EF Core migrations and seed data for WileyWidget database.

.DESCRIPTION
    Comprehensive database migration tool that:
    1. Creates database if not exists
    2. Applies all EF Core migrations
    3. Seeds initial data from SQL scripts
    4. Verifies database integrity

.PARAMETER Server
    SQL Server instance (default: auto-detect localhost\SQLEXPRESS or localhost)

.PARAMETER Database
    Database name (default: WileyWidget)

.PARAMETER ProjectPath
    Path to WileyWidget.Data project (default: src/WileyWidget.Data)

.PARAMETER SkipMigrations
    Skip EF Core migrations (only run seed scripts)

.PARAMETER SkipSeeding
    Skip data seeding (only run migrations)

.PARAMETER Force
    Drop and recreate database (WARNING: destroys all data)

.EXAMPLE
    .\scripts\tools\migrate-database.ps1

.EXAMPLE
    .\scripts\tools\migrate-database.ps1 -Force -Verbose
#>

[CmdletBinding()]
param(
    [string]$Server,
    [string]$Database = "WileyWidget",
    [string]$ProjectPath = "src\WileyWidget.Data",
    [switch]$SkipMigrations,
    [switch]$SkipSeeding,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Colors for output
function Write-Success { Write-Host "✓ $args" -ForegroundColor Green }
function Write-Info { Write-Host "ℹ $args" -ForegroundColor Cyan }
function Write-Warning { param([string]$Message) Write-Host "⚠ $Message" -ForegroundColor Yellow }
function Write-ErrorMsg { Write-Host "✗ $args" -ForegroundColor Red }

# Get workspace root
$WorkspaceRoot = $PSScriptRoot
while ($WorkspaceRoot -and -not (Test-Path (Join-Path $WorkspaceRoot "WileyWidget.sln"))) {
    $WorkspaceRoot = Split-Path -Parent $WorkspaceRoot
}

if (-not $WorkspaceRoot) {
    Write-ErrorMsg "Could not find workspace root (WileyWidget.sln not found)"
    exit 1
}

Write-Info "Workspace root: $WorkspaceRoot"

# Resolve project path
$DataProjectPath = Join-Path $WorkspaceRoot $ProjectPath
if (-not (Test-Path $DataProjectPath)) {
    Write-ErrorMsg "Data project not found: $DataProjectPath"
    exit 1
}

$DataProjectFile = Join-Path $DataProjectPath "WileyWidget.Data.csproj"
if (-not (Test-Path $DataProjectFile)) {
    Write-ErrorMsg "Project file not found: $DataProjectFile"
    exit 1
}

Write-Success "Found project: $DataProjectFile"

# Auto-detect SQL Server instance if not specified
if (-not $Server) {
    Write-Info "Auto-detecting SQL Server instance..."

    $sqlExpressService = Get-Service -Name "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
    if ($sqlExpressService -and $sqlExpressService.Status -eq 'Running') {
        $Server = "localhost\SQLEXPRESS"
        Write-Success "Detected SQL Server Express: $Server"
    } else {
        $sqlDefaultService = Get-Service -Name "MSSQLSERVER" -ErrorAction SilentlyContinue
        if ($sqlDefaultService -and $sqlDefaultService.Status -eq 'Running') {
            $Server = "localhost"
            Write-Success "Detected default SQL Server: $Server"
        } else {
            Write-ErrorMsg "No SQL Server instance found running"
            Write-Info "Start SQL Server Express: net start MSSQL`$SQLEXPRESS"
            exit 1
        }
    }
}

# Check for dotnet ef tool
$efTool = dotnet tool list --global | Select-String "dotnet-ef"
if (-not $efTool) {
    Write-Warning "dotnet-ef tool not installed globally"
    Write-Info "Installing dotnet-ef..."
    dotnet tool install --global dotnet-ef

    if ($LASTEXITCODE -ne 0) {
        Write-ErrorMsg "Failed to install dotnet-ef tool"
        exit 1
    }
}

$efVersion = dotnet ef --version
Write-Success "Using dotnet-ef: $efVersion"

# Set connection string environment variable
$ConnectionString = "Server=$Server;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$env:WILEY_WIDGET_CONNECTION_STRING = $ConnectionString

Write-Info "Connection string: Server=$Server;Database=$Database;..."

# Force recreate database if requested
if ($Force) {
    Write-Warning "Force mode enabled - dropping database $Database"
    $dropSql = "IF EXISTS (SELECT name FROM sys.databases WHERE name = N'$Database') DROP DATABASE [$Database]"

    try {
        sqlcmd -S $Server -E -Q $dropSql -b
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Database dropped successfully"
        }
    } catch {
        Write-Warning "Could not drop database (may not exist): $_"
    }
}

# Apply EF Core migrations
if (-not $SkipMigrations) {
    Write-Info "Applying EF Core migrations..."
    Write-Info "Project: $DataProjectFile"

    Push-Location $DataProjectPath

    try {
        # List available migrations
        Write-Info "Available migrations:"
        dotnet ef migrations list --no-build 2>&1 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }

        # Apply migrations
        Write-Info "Updating database..."
        dotnet ef database update --verbose --no-build --connection $ConnectionString --context AppDbContext -- --DatabaseProvider SqlServer

        if ($LASTEXITCODE -eq 0) {
            Write-Success "EF Core migrations applied successfully"
        } else {
            Write-ErrorMsg "Migration failed with exit code $LASTEXITCODE"
            Pop-Location
            exit 1
        }
    } catch {
        Write-ErrorMsg "Migration error: $_"
        Pop-Location
        exit 1
    } finally {
        Pop-Location
    }
} else {
    Write-Info "Skipping EF Core migrations (--SkipMigrations)"
}

# Seed data from SQL scripts
if (-not $SkipSeeding) {
    Write-Info "Seeding data from SQL scripts..."

    $SqlScriptsPath = Join-Path $WorkspaceRoot "sql"

    if (-not (Test-Path $SqlScriptsPath)) {
        Write-Warning "SQL scripts folder not found: $SqlScriptsPath"
    } else {
        # Define script execution order
        $scriptOrder = @(
            "00_create_schema.sql",
            "insert_departments.sql",
            "insert_budget_period.sql",
            "insert_town_wiley_municipal_accounts.sql",
            "insert_sewer_municipal_accounts.sql",
            "insert_missing_municipal_accounts.sql",
            "insert_sewer_revenues.sql",
            "insert_sewer_expenditures.sql",
            "link_budget_entries.sql"
        )

        $executedScripts = 0

        foreach ($scriptName in $scriptOrder) {
            $scriptPath = Join-Path $SqlScriptsPath $scriptName

            if (Test-Path $scriptPath) {
                Write-Info "Executing: $scriptName"

                try {
                    sqlcmd -S $Server -E -d $Database -i $scriptPath -b

                    if ($LASTEXITCODE -eq 0) {
                        Write-Success "  ✓ $scriptName"
                        $executedScripts++
                    } else {
                        Write-Warning "  ⚠ $scriptName returned exit code $LASTEXITCODE (may be expected)"
                    }
                } catch {
                    Write-Warning "  ⚠ Error executing $scriptName : $_"
                }
            } else {
                Write-Info "  ⊘ $scriptName not found (skipping)"
            }
        }

        Write-Success "Executed $executedScripts SQL seed scripts"
    }
} else {
    Write-Info "Skipping data seeding (--SkipSeeding)"
}

# Verify database
Write-Info "Verifying database integrity..."

$verifyQuery = @"
SELECT
    'Tables' AS ObjectType,
    COUNT(*) AS Count
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
UNION ALL
SELECT
    'Stored Procedures',
    COUNT(*)
FROM INFORMATION_SCHEMA.ROUTINES
WHERE ROUTINE_TYPE = 'PROCEDURE'
UNION ALL
SELECT
    'Views',
    COUNT(*)
FROM INFORMATION_SCHEMA.VIEWS
"@

try {
    Write-Info "Database objects:"
    sqlcmd -S $Server -E -d $Database -Q $verifyQuery -h-1 | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Gray
    }
} catch {
    Write-Warning "Could not verify database: $_"
}

# Summary
Write-Host "`n" -NoNewline
Write-Success "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
Write-Success "  Database migration completed successfully!"
Write-Success "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
Write-Host ""
Write-Info "Database: $Database"
Write-Info "Server: $Server"
Write-Info "Connection: Windows Authentication"
Write-Host ""
Write-Info "Next steps:"
Write-Host "  1. Connect via MS SQL extension in VS Code" -ForegroundColor White
Write-Host "  2. Run queries: Ctrl+Shift+P → 'MS SQL: Connect'" -ForegroundColor White
Write-Host "  3. Verify data: SELECT * FROM MunicipalAccounts" -ForegroundColor White
