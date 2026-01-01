@echo off
REM Batch file to seed FY 2025 budget data
REM This is an alternative to the PowerShell script for users who prefer .bat files

echo Seeding FY 2025 budget data into WileyWidgetDev database...
echo.

set SERVER=.\SQLEXPRESS
set DATABASE=WileyWidgetDev

echo Executing SQL files...
echo.

REM List of SQL files to execute
REM NOTE: Seeding is now handled entirely by EF Core migrations in AppDbContext.OnModelCreating
REM These SQL files are kept for reference but are no longer executed
REM set SQL_FILES=insert_budget_period.sql insert_departments.sql insert_missing_municipal_accounts.sql insert_sewer_expenditures.sql insert_sewer_municipal_accounts.sql insert_sewer_revenues.sql insert_town_wiley_municipal_accounts.sql link_budget_entries.sql

REM Disable SQL file execution since seeding is now in EF migrations
set SQL_FILES=

for %%f in (%SQL_FILES%) do (
    echo Executing %%f...
    sqlcmd -S %SERVER% -d %DATABASE% -i sql\%%f
    if %ERRORLEVEL% neq 0 (
        echo ERROR: Failed to execute %%f
    ) else (
        echo SUCCESS: %%f completed
    )
    echo.
)

echo Budget data seeding completed!
echo Run verify-fy2025-data.ps1 to check the results.
pause
