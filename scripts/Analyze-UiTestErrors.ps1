#!/usr/bin/env pwsh
<#
.SYNOPSIS
    UI Test Error Log Analyzer
.DESCRIPTION
    This script helps analyze UI test error logs for debugging failed tests
#>

param(
    [string]$LogPath = "./test-logs",
    [string]$TestResultsPath = "./TestResults",
    [int]$MaxErrors = 20
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "UI Test Error Log Analyzer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if logs directory exists
if (-not (Test-Path $LogPath)) {
    Write-Warning "No test-logs directory found at $LogPath. Run UI tests first."
    exit 1
}

# Analyze test output log
Write-Host "=== Test Output Summary ===" -ForegroundColor Yellow
$outputLog = Join-Path $LogPath "test-output.log"
if (Test-Path $outputLog) {
    $content = Get-Content $outputLog -Raw
    $errorCount = ($content | Select-String -Pattern "error\(s\)" -AllMatches).Matches.Count
    $warningCount = ($content | Select-String -Pattern "warning\(s\)" -AllMatches).Matches.Count
    $failedTests = ($content | Select-String -Pattern "Failed.*:").Lines.Count

    Write-Host "Errors found: $errorCount" -ForegroundColor Red
    Write-Host "Warnings found: $warningCount" -ForegroundColor Yellow
    Write-Host "Failed tests: $failedTests" -ForegroundColor Red

    # Show recent failures
    Write-Host "`nRecent test failures:" -ForegroundColor Red
    Get-Content $outputLog | Select-String -Pattern "Failed.*:" -Context 2 | Select-Object -Last 5
}
else {
    Write-Warning "test-output.log not found"
}

Write-Host "`n=== UI Test Error Log ===" -ForegroundColor Yellow
$errorLog = Join-Path $LogPath "ui-test-errors.log"
if (Test-Path $errorLog) {
    Write-Host "Recent errors ($MaxErrors most recent):" -ForegroundColor Red
    Get-Content $errorLog -Tail $MaxErrors | ForEach-Object {
        if ($_ -match "ERROR") {
            Write-Host $_ -ForegroundColor Red
        }
        elseif ($_ -match "DIAGNOSTIC") {
            Write-Host $_ -ForegroundColor Blue
        }
        else {
            Write-Host $_
        }
    }
}
else {
    Write-Warning "ui-test-errors.log not found"
}

Write-Host "`n=== Test Results Files ===" -ForegroundColor Yellow
if (Test-Path $TestResultsPath) {
    $trxFiles = Get-ChildItem $TestResultsPath -Filter "*.trx" -Recurse
    if ($trxFiles) {
        $trxFiles | ForEach-Object {
            Write-Host "  $($_.FullName)" -ForegroundColor Green
        }
        Write-Host "`nTo view detailed results, open the .trx files in Visual Studio Test Explorer" -ForegroundColor Gray
    }
    else {
        Write-Warning "No .trx test result files found"
    }
}
else {
    Write-Warning "No TestResults directory found"
}

Write-Host "`n=== Common Error Patterns ===" -ForegroundColor Yellow
Write-Host "1. 'No overload for method Show' - Views need Window wrapper" -ForegroundColor Cyan
Write-Host "2. 'MainWindow could not be found' - Missing type reference" -ForegroundColor Cyan
Write-Host "3. 'Bitmap.ToStream does not exist' - Missing extension method" -ForegroundColor Cyan
Write-Host "4. 'Assert.Skip does not exist' - Missing test framework method" -ForegroundColor Cyan
Write-Host "5. WPF threading issues - Check STA thread requirements" -ForegroundColor Cyan

Write-Host "`n=== Next Steps ===" -ForegroundColor Yellow
Write-Host "1. Review ui-test-errors.log for stack traces" -ForegroundColor White
Write-Host "2. Check test-logs/test-output.log for build errors" -ForegroundColor White
Write-Host "3. Fix view instantiation issues (wrap UserControls in Windows)" -ForegroundColor White
Write-Host "4. Resolve missing type references and methods" -ForegroundColor White
Write-Host "5. Run individual failing tests with debugger attached" -ForegroundColor White

Write-Host "`nPress any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
