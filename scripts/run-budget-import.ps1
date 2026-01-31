<#
.SYNOPSIS
Imports Town of Wiley Colorado 2026 budget data into SQL Server database.

.DESCRIPTION
Executes the SQL import script to create and populate the TownOfWileyBudget2026 table
with budget data from multiple sources.

.EXAMPLE
PS> .\run-budget-import.ps1 -ServerName "(LocalDb)\MSSQLLocalDB" -DatabaseName "WileyWidget"

.NOTES
Requires SQL Server and sqlcmd or SQL Server Management Studio.
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$ServerName = "(LocalDb)\MSSQLLocalDB",

    [Parameter(Mandatory = $false)]
    [string]$DatabaseName = "WileyWidget",

    [Parameter(Mandatory = $false)]
    [string]$SqlScriptPath = "$PSScriptRoot\..\sql\TownOfWileyBudget2026_Import.sql"
)

# Resolve absolute path
$SqlScriptPath = Resolve-Path $SqlScriptPath -ErrorAction Stop

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "Town of Wiley Budget Data Import" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Server:       $ServerName"
Write-Host "Database:     $DatabaseName"
Write-Host "Script:       $SqlScriptPath"
Write-Host ""

# Verify script exists
if (-not (Test-Path $SqlScriptPath)) {
    Write-Error "SQL script not found: $SqlScriptPath"
    exit 1
}

# Try using sqlcmd (SQL Server command line utility)
$sqlcmdPath = Get-Command sqlcmd -ErrorAction SilentlyContinue

if ($sqlcmdPath) {
    Write-Host "Using sqlcmd to execute script..." -ForegroundColor Green
    Write-Host ""

    try {
        # Execute using sqlcmd
        sqlcmd -S "$ServerName" -d "$DatabaseName" -i "$SqlScriptPath" -b

        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "[SUCCESS] Import completed successfully!" -ForegroundColor Green
            Write-Host ""

            # Query to verify
            Write-Host "Verifying data..." -ForegroundColor Cyan
            sqlcmd -S "$ServerName" -d "$DatabaseName" -Q "SELECT FundOrDepartment, COUNT(*) AS Rows, SUM(BudgetYear) AS TotalBudget FROM dbo.TownOfWileyBudget2026 GROUP BY FundOrDepartment ORDER BY FundOrDepartment;"
        } else {
            Write-Error "Script execution failed with exit code: $LASTEXITCODE"
            exit 1
        }
    } catch {
        Write-Error "Error executing sqlcmd: $_"
        exit 1
    }
} else {
    Write-Host "sqlcmd not found. Trying .NET SqlClient approach..." -ForegroundColor Yellow
    Write-Host ""

    try {
        # Use .NET SqlClient
        Add-Type -AssemblyName System.Data

        # Build connection string
        $connectionString = "Server=$ServerName;Database=$DatabaseName;Integrated Security=true;Encrypt=false;"

        Write-Host "Connection string: $connectionString"
        Write-Host ""

        # Read SQL script
        $sqlScript = Get-Content -Path $SqlScriptPath -Raw

        # Split by GO statements (SQL Server batch separator)
        $batches = $sqlScript -split "(?m)^\s*GO\s*$"

        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()

        Write-Host "Connected to server: $($connection.ServerVersion)" -ForegroundColor Green
        Write-Host ""

        $batchCount = 0
        foreach ($batch in $batches) {
            $batch = $batch.Trim()
            if ($batch) {
                $batchCount++
                Write-Host "Executing batch $batchCount..." -ForegroundColor Cyan

                $command = $connection.CreateCommand()
                $command.CommandText = $batch
                $command.CommandTimeout = 300
                $command.ExecuteNonQuery() | Out-Null

                Write-Host "  [OK] Batch $batchCount completed"
            }
        }

        $connection.Close()

        Write-Host ""
        Write-Host "[SUCCESS] Import completed successfully! ($batchCount batches executed)" -ForegroundColor Green
        Write-Host ""

        # Verify
        Write-Host "Verifying data..." -ForegroundColor Cyan
        $verifyQuery = @"
        SELECT FundOrDepartment, COUNT(*) AS Rows, SUM(BudgetYear) AS TotalBudget
        FROM dbo.TownOfWileyBudget2026
        GROUP BY FundOrDepartment
        ORDER BY FundOrDepartment;
"@

        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()

        $command = $connection.CreateCommand()
        $command.CommandText = $verifyQuery
        $reader = $command.ExecuteReader()

        $results = @()
        while ($reader.Read()) {
            $results += [PSCustomObject]@{
                FundOrDepartment = $reader[0]
                Rows = $reader[1]
                TotalBudget = $reader[2]
            }
        }

        $connection.Close()

        if ($results.Count -gt 0) {
            $results | Format-Table -AutoSize
            Write-Host "Total funds/departments imported: $($results.Count)" -ForegroundColor Green
        } else {
            Write-Warning "No data found in table after import."
        }

    } catch {
        Write-Error "Error during import: $_"
        exit 1
    }
}

Write-Host ""
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "Import process complete!" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
