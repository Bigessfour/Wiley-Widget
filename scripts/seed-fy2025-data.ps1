# Seed FY 2025 Budget Data for WileyWidgetDev Database
# This script populates the WileyWidgetDev database with budget data for FY 2025

param(
    [string]$ServerInstance = ".\SQLEXPRESS",
    [string]$Database = "WileyWidgetDev"
)

Write-Host "Seeding FY 2025 budget data into $Database on $ServerInstance..." -ForegroundColor Green

# Get the directory where this script is located
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sqlDir = Join-Path -Path (Split-Path -Parent $scriptDir) -ChildPath "sql"

# List of SQL files to execute in order
$sqlFiles = @(
    "insert_budget_period.sql",
    "insert_departments.sql",
    "insert_missing_municipal_accounts.sql",
    "insert_sewer_expenditures.sql",
    "insert_sewer_municipal_accounts.sql",
    "insert_sewer_revenues.sql",
    "insert_town_wiley_municipal_accounts.sql",
    "link_budget_entries.sql"
)

foreach ($sqlFile in $sqlFiles) {
    $sqlPath = Join-Path -Path $sqlDir -ChildPath $sqlFile

    if (Test-Path $sqlPath) {
        Write-Host "Executing $sqlFile..." -ForegroundColor Yellow

        try {
            Invoke-Sqlcmd -ConnectionString "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;" -InputFile $sqlPath -ErrorAction Stop
            Write-Host "✓ Successfully executed $sqlFile" -ForegroundColor Green
        }
        catch {
            Write-Host "✗ Failed to execute $sqlFile : $($_.Exception.Message)" -ForegroundColor Red
            # Continue with other files
        }
    }
    else {
        Write-Host "⚠ SQL file not found: $sqlPath" -ForegroundColor Yellow
    }
}

Write-Host "Budget data seeding completed!" -ForegroundColor Green
Write-Host "You can now run the Wiley Widget application to see FY 2025 budget data in the dashboard." -ForegroundColor Cyan
