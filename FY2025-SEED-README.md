# FY 2025 Budget Data Seeding

This directory contains scripts to seed the WileyWidgetDev database with FY 2025 budget data.

## Problem

The Wiley Widget application dashboard shows "0 budget entries for FY 2025" because the database contains seed data for FY 2026, but the application is configured to display FY 2025 data.

## Solution

The provided scripts will populate the WileyWidgetDev database with FY 2025 budget data by:

1. Creating a budget period for FY 2025
2. Adding departments, municipal accounts, and budget entries
3. Linking budget entries to the appropriate municipal accounts

## Files

- `seed-fy2025-data.ps1` - Main seeding script that executes all SQL files
- `verify-fy2025-data.ps1` - Verification script to check if data was seeded correctly
- `sql/insert_budget_period.sql` - Creates FY 2025 budget period (modified from FY 2026)
- `sql/insert_departments.sql` - Inserts department data
- `sql/insert_missing_municipal_accounts.sql` - Adds missing municipal accounts
- `sql/insert_sewer_expenditures.sql` - Adds sewer fund expenditures
- `sql/insert_sewer_municipal_accounts.sql` - Adds sewer-specific accounts
- `sql/insert_sewer_revenues.sql` - Adds sewer fund revenues
- `sql/insert_town_wiley_municipal_accounts.sql` - Adds town municipal accounts
- `sql/link_budget_entries.sql` - Links budget entries to municipal accounts

## Usage

### Prerequisites

- SQL Server Express installed and running
- WileyWidgetDev database exists and is accessible
- PowerShell with Invoke-Sqlcmd available

### Step 1: Seed the Data

Run the seeding script:

```powershell
.\seed-fy2025-data.ps1
```

Or with custom server/database:

```powershell
.\seed-fy2025-data.ps1 -ServerInstance "YOUR_SERVER" -Database "YOUR_DATABASE"
```

### Step 2: Verify the Data

Check that the data was seeded correctly:

```powershell
.\verify-fy2025-data.ps1
```

### Step 3: Test the Application

Run the Wiley Widget application. The dashboard should now show budget data for FY 2025 instead of empty data.

## Expected Results

After seeding, you should see:

- 1 Budget Period for FY 2025
- 72+ Municipal Accounts
- 40+ Budget Entries for FY 2025 (mix of revenues and expenditures)
- Dashboard showing populated data instead of "0 budget entries"

## Troubleshooting

If the scripts fail:

1. Check SQL Server is running and accessible
2. Verify database permissions
3. Ensure Invoke-Sqlcmd is available (install SQL Server Management Studio or sqlcmd utilities)
4. Check for existing data conflicts (scripts use IDENTITY_INSERT)

## Alternative Manual Approach

If PowerShell scripts don't work, you can execute the SQL files manually using SQL Server Management Studio or sqlcmd:

```cmd
sqlcmd -S .\SQLEXPRESS -d WileyWidgetDev -i sql\insert_budget_period.sql
sqlcmd -S .\SQLEXPRESS -d WileyWidgetDev -i sql\insert_departments.sql
... (execute remaining files in order)
```
