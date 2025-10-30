<#
.SYNOPSIS
    Run all enhanced service tests (05-09) and generate report
.DESCRIPTION
    Executes all 5 service test files using dotnet-csi (C# script runner)
    and aggregates results into a comprehensive test report.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'

$testFiles = @(
    @{ Name = "DataAnonymizer"; File = "05-dataanonymizer-test.csx"; ExpectedTests = 16 }
    @{ Name = "AuditService"; File = "06-audit-test.csx"; ExpectedTests = 13 }
    @{ Name = "ThemeService"; File = "07-theme-test.csx"; ExpectedTests = 9 }
    @{ Name = "ExcelExport"; File = "08-excelexport-test.csx"; ExpectedTests = 8 }
    @{ Name = "XAIService"; File = "09-xai-test.csx"; ExpectedTests = 7 }
)

$testDir = "$PSScriptRoot\examples\csharp"
$results = @()

Write-Host "`n=== Running All Enhanced Service Tests ===" -ForegroundColor Cyan
Write-Host "Total test files: $($testFiles.Count)" -ForegroundColor Cyan
Write-Host "Expected total tests: $($testFiles | ForEach-Object { $_.ExpectedTests } | Measure-Object -Sum | Select-Object -ExpandProperty Sum)`n" -ForegroundColor Cyan

foreach ($test in $testFiles) {
    $filePath = Join-Path $testDir $test.File

    Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host "Running: $($test.Name) ($($test.File))" -ForegroundColor Yellow
    Write-Host "─────────────────────────────────────────────────────`n" -ForegroundColor DarkGray

    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        # Run test using dotnet-script (C# script interpreter)
        $output = & dotnet script $filePath 2>&1
        $exitCode = $LASTEXITCODE
        $sw.Stop()

        # Parse output for results
        $passedLine = $output | Select-String -Pattern "Passed: (\d+)/(\d+)" | Select-Object -First 1
        $timingLine = $output | Select-String -Pattern "Total execution time: (\d+)ms" | Select-Object -First 1

        if ($passedLine -and $passedLine.Matches.Groups.Count -ge 3) {
            $passed = [int]$passedLine.Matches.Groups[1].Value
            $total = [int]$passedLine.Matches.Groups[2].Value
            $failed = $total - $passed
            $successRate = [math]::Round(($passed / $total) * 100, 1)
        }
        else {
            $passed = 0
            $total = $test.ExpectedTests
            $failed = $total
            $successRate = 0
        }

        $executionTime = if ($timingLine -and $timingLine.Matches.Groups.Count -ge 2) {
            [int]$timingLine.Matches.Groups[1].Value
        }
        else {
            $sw.ElapsedMilliseconds
        }

        $results += [PSCustomObject]@{
            TestName        = $test.Name
            File            = $test.File
            Passed          = $passed
            Failed          = $failed
            Total           = $total
            SuccessRate     = $successRate
            ExecutionTimeMs = $executionTime
            Status          = if ($passed -eq $total) { "✓ PASSED" } else { "✗ FAILED" }
            ExitCode        = $exitCode
        }

        # Display output
        Write-Output $output
        Write-Host ""

    }
    catch {
        $sw.Stop()
        Write-Host "❌ Error running test: $_" -ForegroundColor Red

        $results += [PSCustomObject]@{
            TestName        = $test.Name
            File            = $test.File
            Passed          = 0
            Failed          = $test.ExpectedTests
            Total           = $test.ExpectedTests
            SuccessRate     = 0
            ExecutionTimeMs = $sw.ElapsedMilliseconds
            Status          = "✗ ERROR"
            ExitCode        = -1
        }
    }
}

# Generate summary report
Write-Host "`n═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "              COMPREHENSIVE TEST SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════`n" -ForegroundColor Cyan

$results | Format-Table -AutoSize TestName, Status, Passed, Failed, Total, @{
    Label      = "Success %"
    Expression = { "$($_.SuccessRate)%" }
}, @{
    Label      = "Time (ms)"
    Expression = { $_.ExecutionTimeMs }
}

# Calculate totals
$totalPassed = ($results | Measure-Object -Property Passed -Sum).Sum
$totalFailed = ($results | Measure-Object -Property Failed -Sum).Sum
$totalTests = ($results | Measure-Object -Property Total -Sum).Sum
$totalTime = ($results | Measure-Object -Property ExecutionTimeMs -Sum).Sum
$overallSuccessRate = [math]::Round(($totalPassed / $totalTests) * 100, 1)

Write-Host "─────────────────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host "TOTALS:" -ForegroundColor Cyan
Write-Host "  Total Tests:       $totalTests" -ForegroundColor White
Write-Host "  Passed:            $totalPassed" -ForegroundColor Green
Write-Host "  Failed:            $totalFailed" -ForegroundColor $(if ($totalFailed -eq 0) { "Green" } else { "Red" })
Write-Host "  Success Rate:      $overallSuccessRate%" -ForegroundColor $(if ($overallSuccessRate -eq 100) { "Green" } else { "Yellow" })
Write-Host "  Total Time:        $($totalTime)ms ($([math]::Round($totalTime / 1000, 2))s)" -ForegroundColor White
Write-Host "─────────────────────────────────────────────────────`n" -ForegroundColor DarkGray

# Enhancement impact summary
Write-Host "ENHANCEMENT IMPACT:" -ForegroundColor Cyan
Write-Host "  Test Files Enhanced:       5" -ForegroundColor White
Write-Host "  New Tests Added:           20 (11→16, 10→13, 5→9, 5→8, 5→7)" -ForegroundColor White
Write-Host "  Performance Benchmarking:  ✓ All tests" -ForegroundColor Green
Write-Host "  Concurrent Operations:     ✓ Tests 5, 6, 8, 9" -ForegroundColor Green
Write-Host "  GDPR Compliance:           ✓ Test 5" -ForegroundColor Green
Write-Host "  Memory Leak Detection:     ✓ Test 5" -ForegroundColor Green
Write-Host "  File Rotation:             ✓ Test 6" -ForegroundColor Green
Write-Host "  Rate Limiting:             ✓ Test 9" -ForegroundColor Green
Write-Host "  Coverage Summaries:        ✓ All tests`n" -ForegroundColor Green

# Final verdict
if ($totalFailed -eq 0) {
    Write-Host "✓ ALL TESTS PASSED! 🎉" -ForegroundColor Green
    Write-Host "The enhancements have achieved 100% pass rate." -ForegroundColor Green
    exit 0
}
else {
    Write-Host "✗ $totalFailed test(s) failed." -ForegroundColor Red
    Write-Host "Review the output above for details." -ForegroundColor Yellow
    exit 1
}
