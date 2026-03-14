<#
.SYNOPSIS
Imports Town of Wiley Colorado 2026 budget data into SQL Server database.

.DESCRIPTION
Executes the SQL import script to populate the app's live TownOfWileyBudgetData table
with budget data from multiple sources.

.EXAMPLE
PS> .\run-budget-import.ps1 -ServerName "(LocalDb)\MSSQLLocalDB" -DatabaseName "WileyWidget"

.NOTES
Requires SQL Server and sqlcmd or SQL Server Management Studio.
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$ServerName = "localhost\SQLEXPRESS",

    [Parameter(Mandatory = $false)]
    [string]$DatabaseName = "WileyWidget",

    [Parameter(Mandatory = $false)]
    [string]$SqlScriptPath = "$PSScriptRoot\..\sql\TownOfWileyBudget2026_Import.sql"
)

# Resolve absolute path
$SqlScriptPath = Resolve-Path $SqlScriptPath -ErrorAction Stop

Add-Type -AssemblyName System.Data

$connectionString = "Server=$ServerName;Database=$DatabaseName;Integrated Security=true;Encrypt=false;"

$syncLiveTableQuery = @"
SET NOCOUNT ON;

IF OBJECT_ID('dbo.TownOfWileyBudget2026', 'U') IS NULL
    THROW 50002, 'Legacy import table dbo.TownOfWileyBudget2026 was not created by the import script.', 1;

IF OBJECT_ID('dbo.TownOfWileyBudgetData', 'U') IS NULL
    THROW 50003, 'Live app table dbo.TownOfWileyBudgetData does not exist. Apply the current database migrations before running this import.', 1;

DELETE FROM dbo.TownOfWileyBudgetData;

INSERT INTO dbo.TownOfWileyBudgetData
(
    SourceFile,
    FundOrDepartment,
    AccountCode,
    Description,
    PriorYearActual,
    SevenMonthActual,
    EstimateCurrentYr,
    BudgetYear,
    ActualYTD,
    Remaining,
    PercentOfBudget,
    Category,
    MappedDepartment
)
SELECT
    legacy.SourceFile,
    legacy.FundOrDepartment,
    legacy.AccountCode,
    legacy.Description,
    legacy.PriorYearActual,
    legacy.SevenMonthActual,
    legacy.EstimateCurrentYr,
    legacy.BudgetYear,
    derived.ActualYTD,
    CASE
        WHEN legacy.BudgetYear IS NULL THEN NULL
        ELSE legacy.BudgetYear - derived.ActualYTD
    END AS Remaining,
    CASE
        WHEN legacy.BudgetYear IS NULL OR legacy.BudgetYear = 0 THEN NULL
        ELSE CAST(ROUND((derived.ActualYTD / legacy.BudgetYear) * 100.0, 0) AS int)
    END AS PercentOfBudget,
    CASE
        WHEN legacy.Description LIKE '%REVENUE%'
          OR legacy.Description LIKE '%SALES%'
          OR legacy.Description LIKE '%FEE%'
          OR legacy.Description LIKE '%FEES%'
          OR legacy.Description LIKE '%TAX%'
          OR legacy.Description LIKE '%INTEREST%'
          OR legacy.Description LIKE '%GRANT%'
          OR legacy.Description LIKE '%DONATION%'
          OR legacy.Description LIKE '%DIVIDEND%'
          OR legacy.Description LIKE '%LEASE%'
          OR legacy.Description LIKE '%LICENSE%'
          OR legacy.Description LIKE '%PERMIT%'
          OR legacy.Description LIKE '%APPORTIONMENT%'
          OR legacy.Description LIKE '%INCOME%'
          OR legacy.Description LIKE '%TRANSFER%'
          OR legacy.Description LIKE '%DUES%'
          THEN 'Revenue'
        WHEN legacy.Description LIKE '%EXPENDITURE%'
          OR legacy.Description LIKE '%EXPENSE%'
          OR legacy.Description LIKE '%SALAR%'
          OR legacy.Description LIKE '%SUPPL%'
          OR legacy.Description LIKE '%REPAIR%'
          OR legacy.Description LIKE '%MAINT%'
          OR legacy.Description LIKE '%UTILIT%'
          OR legacy.Description LIKE '%INSURANCE%'
          OR legacy.Description LIKE '%PAYROLL%'
          OR legacy.Description LIKE '%BENEFIT%'
          OR legacy.Description LIKE '%SERVICE%'
          OR legacy.Description LIKE '%EQUIPMENT%'
          OR legacy.Description LIKE '%CAPITAL OUTLAY%'
          OR legacy.Description LIKE '%TRAVEL%'
          OR legacy.Description LIKE '%POSTAGE%'
          OR legacy.Description LIKE '%AUDIT%'
          OR legacy.Description LIKE '%LEGAL%'
          OR legacy.Description LIKE '%FUEL%'
          OR legacy.Description LIKE '%LABOR%'
          OR legacy.Description LIKE '%BANK SERVICE%'
          THEN 'Expense'
        ELSE NULL
    END AS Category,
    CASE
        WHEN legacy.FundOrDepartment LIKE '%Water%'
          OR legacy.Description LIKE '%WATER%'
          OR legacy.Description LIKE '%WELL%'
          OR legacy.Description LIKE '%PUMP%'
          THEN 'Water'
        WHEN legacy.FundOrDepartment LIKE '%Sewer%'
          OR legacy.FundOrDepartment LIKE '%Sanitation%'
          OR legacy.Description LIKE '%SEWER%'
          OR legacy.Description LIKE '%SEWAGE%'
          OR legacy.Description LIKE '%LIFT-STATION%'
          THEN 'Sewer'
        WHEN legacy.FundOrDepartment LIKE '%Trash%'
          OR legacy.Description LIKE '%TRASH%'
          OR legacy.Description LIKE '%DUMP%'
          OR legacy.Description LIKE '%DISPOSAL%'
          THEN 'Trash'
        WHEN legacy.FundOrDepartment LIKE '%Apartment%'
          OR legacy.FundOrDepartment LIKE '%Housing%'
          OR legacy.Description LIKE '%APARTMENT%'
          OR legacy.Description LIKE '%HOUSING%'
          THEN 'Apartments'
        WHEN legacy.Description LIKE '%CAPITAL OUTLAY%'
          THEN 'Capital Projects'
        WHEN legacy.Description LIKE '%BANK%'
          OR legacy.Description LIKE '%AUDIT%'
          OR legacy.Description LIKE '%LEGAL%'
          OR legacy.Description LIKE '%OFFICE%'
          OR legacy.Description LIKE '%INSURANCE%'
          OR legacy.Description LIKE '%DUES%'
          OR legacy.Description LIKE '%EDUCATION%'
          OR legacy.Description LIKE '%TREASURER%'
          OR legacy.Description LIKE '%ADMIN%'
          THEN 'Administration'
        ELSE NULLIF(legacy.FundOrDepartment, '')
    END AS MappedDepartment
FROM dbo.TownOfWileyBudget2026 AS legacy
CROSS APPLY
(
    SELECT CAST(COALESCE(legacy.SevenMonthActual, legacy.EstimateCurrentYr, legacy.PriorYearActual, 0) AS decimal(19, 4)) AS ActualYTD
) AS derived;
"@

$verifyLiveTableQuery = @"
SELECT FundOrDepartment, COUNT(*) AS Rows, SUM(BudgetYear) AS TotalBudget
FROM dbo.TownOfWileyBudgetData
GROUP BY FundOrDepartment
ORDER BY FundOrDepartment;
"@

function Invoke-SqlNonQuery {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConnectionString,

        [Parameter(Mandatory = $true)]
        [string]$CommandText
    )

    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $CommandText
        $command.CommandTimeout = 300
        $command.ExecuteNonQuery() | Out-Null
    } finally {
        $connection.Dispose()
    }
}

function Read-SqlObjects {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConnectionString,

        [Parameter(Mandatory = $true)]
        [string]$CommandText
    )

    $results = @()
    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $CommandText
        $command.CommandTimeout = 300
        $reader = $command.ExecuteReader()

        while ($reader.Read()) {
            $results += [PSCustomObject]@{
                FundOrDepartment = $reader[0]
                Rows             = $reader[1]
                TotalBudget      = $reader[2]
            }
        }

        $reader.Close()
        return $results
    } finally {
        $connection.Dispose()
    }
}

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

            Write-Host "Synchronizing legacy import data into dbo.TownOfWileyBudgetData..." -ForegroundColor Cyan
            Invoke-SqlNonQuery -ConnectionString $connectionString -CommandText $syncLiveTableQuery

            # Query to verify
            Write-Host "Verifying data..." -ForegroundColor Cyan
            $results = Read-SqlObjects -ConnectionString $connectionString -CommandText $verifyLiveTableQuery
            if ($results.Count -gt 0) {
                $results | Format-Table -AutoSize
            } else {
                Write-Warning "No data found in dbo.TownOfWileyBudgetData after synchronization."
            }
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

        Write-Host "Synchronizing legacy import data into dbo.TownOfWileyBudgetData..." -ForegroundColor Cyan
        Invoke-SqlNonQuery -ConnectionString $connectionString -CommandText $syncLiveTableQuery

        Write-Host "Verifying data..." -ForegroundColor Cyan
        $results = Read-SqlObjects -ConnectionString $connectionString -CommandText $verifyLiveTableQuery
        if ($results.Count -gt 0) {
            $results | Format-Table -AutoSize
            Write-Host "Total funds/departments imported: $($results.Count)" -ForegroundColor Green
        } else {
            Write-Warning "No data found in dbo.TownOfWileyBudgetData after synchronization."
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
