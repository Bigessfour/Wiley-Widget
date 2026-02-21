# Verify FY 2025 Budget Data in WileyWidget Database
# This script checks if the budget data was properly seeded

param(
    [string]$ServerInstance = ".\SQLEXPRESS",
    [string]$Database = "WileyWidget"
)

Write-Host "Verifying FY 2025 budget data in $Database on $ServerInstance..." -ForegroundColor Green

# Query to check budget periods
Write-Host "`n1. Checking Budget Periods:" -ForegroundColor Yellow
$query = "SELECT Id, Year, Name, StartDate, EndDate, IsActive FROM BudgetPeriods ORDER BY Year;"
Invoke-Sqlcmd -ConnectionString "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;" -Query $query | Format-Table -AutoSize

# Query to check budget entries count
Write-Host "`n2. Budget Entries Summary:" -ForegroundColor Yellow
$query = "SELECT COUNT(*) as TotalBudgetEntries FROM BudgetEntries;"
Invoke-Sqlcmd -ConnectionString "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;" -Query $query | Format-Table -AutoSize

$query = "SELECT COUNT(*) as FY2025BudgetEntries FROM BudgetEntries WHERE BudgetPeriodId = (SELECT Id FROM BudgetPeriods WHERE Year = 2025);"
Invoke-Sqlcmd -ConnectionString "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;" -Query $query | Format-Table -AutoSize

# Query to check municipal accounts
Write-Host "`n3. Municipal Accounts Summary:" -ForegroundColor Yellow
$query = "SELECT COUNT(*) as TotalMunicipalAccounts FROM MunicipalAccounts;"
Invoke-Sqlcmd -ConnectionString "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;" -Query $query | Format-Table -AutoSize

$query = "SELECT Type, COUNT(*) as Count FROM MunicipalAccounts GROUP BY Type ORDER BY Type;"
Invoke-Sqlcmd -ConnectionString "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;" -Query $query | Format-Table -AutoSize

# Query to check sample budget entries
Write-Host "`n4. Sample FY 2025 Budget Entries:" -ForegroundColor Yellow
$query = "SELECT TOP 10 be.Id, ma.AccountNumber, ma.Name, be.EntryType, be.Amount, be.Notes FROM BudgetEntries be INNER JOIN MunicipalAccounts ma ON be.MunicipalAccountId = ma.Id WHERE be.BudgetPeriodId = (SELECT Id FROM BudgetPeriods WHERE Year = 2025) ORDER BY be.Id;"
Invoke-Sqlcmd -ConnectionString "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;" -Query $query | Format-Table -AutoSize

Write-Host "`nVerification completed!" -ForegroundColor Green
