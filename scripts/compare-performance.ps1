#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Compares current performance against baseline measurements.
.DESCRIPTION
    This script:
    - Collects current performance metrics
    - Compares against saved baseline
    - Identifies regressions
    - Generates comparison report
.EXAMPLE
    .\scripts\compare-performance.ps1
#>

param(
    [string]$BaselineDir = "./diagnostics/baseline",
    [string]$CurrentDir = "./diagnostics",
    [string]$OutputDir = "./diagnostics"
)

$ErrorActionPreference = "Stop"

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  📊 Performance Comparison: Baseline vs Current" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Check if baseline exists
if (-not (Test-Path $BaselineDir)) {
    Write-Host "❌ Baseline not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Create baseline first:" -ForegroundColor Yellow
    Write-Host "  1. mkdir -p $BaselineDir" -ForegroundColor Yellow
    Write-Host "  2. .\scripts\analyze-startup-timeline.ps1" -ForegroundColor Yellow
    Write-Host "  3. .\scripts\profile-di-validation.ps1" -ForegroundColor Yellow
    Write-Host "  4. Copy generated files to $BaselineDir" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host "✓ Baseline directory: $BaselineDir" -ForegroundColor Green
Write-Host "✓ Current directory: $CurrentDir" -ForegroundColor Green
Write-Host ""

# Find latest reports
$baselineStartup = Get-ChildItem -Path $BaselineDir -Filter "startup-timeline-*.txt" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$currentStartup = Get-ChildItem -Path $CurrentDir -Filter "startup-timeline-*.txt" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1

$baselineDI = Get-ChildItem -Path $BaselineDir -Filter "di-validation-profile-*.txt" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$currentDI = Get-ChildItem -Path $CurrentDir -Filter "di-validation-profile-*.txt" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $baselineStartup -or -not $currentStartup) {
    Write-Host "❌ Missing startup timeline reports" -ForegroundColor Red
    Write-Host "   Baseline: $baselineStartup" -ForegroundColor Yellow
    Write-Host "   Current: $currentStartup" -ForegroundColor Yellow
    exit 1
}

Write-Host "📊 Analyzing reports..." -ForegroundColor Yellow
Write-Host ""

# Extract metrics
$baselineStartupTime = if ($baselineStartup) {
    (Get-Content $baselineStartup.FullName | Select-String "Total Startup Time:" | Select-Object -First 1) -replace '[^0-9.]', '' | ForEach-Object { [double]$_ }
}

$currentStartupTime = if ($currentStartup) {
    (Get-Content $currentStartup.FullName | Select-String "Total Startup Time:" | Select-Object -First 1) -replace '[^0-9.]', '' | ForEach-Object { [double]$_ }
}

# Calculate diff
$startupDiff = 0
$startupPercent = 0
if ($baselineStartupTime -and $currentStartupTime) {
    $startupDiff = $currentStartupTime - $baselineStartupTime
    $startupPercent = ($startupDiff / $baselineStartupTime) * 100
}

# Generate comparison report
$reportFile = Join-Path $OutputDir "performance-comparison-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"

@"
═══════════════════════════════════════════════════════════════
  📊 Performance Comparison Report
═══════════════════════════════════════════════════════════════

Generated: $(Get-Date)

Startup Time Analysis:
────────────────────────────────────────────────────────────────
Baseline:   $($baselineStartupTime)s (from $($baselineStartup.Name))
Current:    $($currentStartupTime)s (from $($currentStartup.Name))
Difference: $([math]::Round($startupDiff, 2))s ($([math]::Round($startupPercent, 1))%)

$(
    if ($startupDiff -gt 0.5) {
        "⚠️  REGRESSION: Startup time increased by $([math]::Round($startupDiff, 2))s ($([math]::Round($startupPercent, 1))%)"
    } elseif ($startupDiff -lt -0.5) {
        "✅ IMPROVEMENT: Startup time decreased by $([math]::Round([math]::Abs($startupDiff), 2))s ($([math]::Round([math]::Abs($startupPercent), 1))%)"
    } else {
        "➡️  STABLE: Startup time within normal variance (±0.5s)"
    }
)

DI Validation Analysis:
────────────────────────────────────────────────────────────────
$(
    if ($baselineDI -and $currentDI) {
        "Baseline Report: $($baselineDI.Name)`nCurrent Report: $($currentDI.Name)`n`nCompare reports manually for detailed analysis."
    } else {
        "DI validation reports not available"
    }
)

Recommendations:
────────────────────────────────────────────────────────────────
$(
    if ($startupDiff -gt 1.0) {
        @"
1. ⚠️  CRITICAL: Startup regression detected
   - Review recent code changes
   - Check DI service registration order
   - Profile with: .\scripts\profile-di-validation.ps1
   - Analyze with CPU trace if needed

"@
    } elseif ($startupDiff -gt 0.5) {
        @"
1. 🟡 WARNING: Startup time increased
   - Minor regression detected
   - Monitor for further degradation
   - Consider code optimization
   - Check for new dependencies

"@
    } elseif ($startupDiff -lt -0.5) {
        @"
1. 🟢 EXCELLENT: Performance improved!
   - Startup time decreased
   - Update baseline if improvement is permanent
   - Document changes for future reference

"@
    }
)

2. Create new baseline when satisfied with performance:
   mkdir -p $BaselineDir
   cp $CurrentDir/startup-timeline-*.txt $BaselineDir/
   cp $CurrentDir/di-validation-profile-*.txt $BaselineDir/

3. Set up continuous monitoring:
   - Run comparison regularly during development
   - Alert on > 10% regression
   - Track metrics over time

Historical Data:
────────────────────────────────────────────────────────────────
Baseline Date:     $($baselineStartup.LastWriteTime)
Current Date:      $($currentStartup.LastWriteTime)
Time Elapsed:      $(($currentStartup.LastWriteTime - $baselineStartup.LastWriteTime).ToString())

Files:
────────────────────────────────────────────────────────────────
Baseline Startup: $($baselineStartup.FullName)
Current Startup:  $($currentStartup.FullName)
Report:           $reportFile
"@ | Out-File -FilePath $reportFile -Encoding UTF8

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  📊 Comparison Complete" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Startup Time:" -ForegroundColor Yellow
Write-Host "  Baseline: $($baselineStartupTime)s" -ForegroundColor Cyan
Write-Host "  Current:  $($currentStartupTime)s" -ForegroundColor Cyan
Write-Host "  Change:   $([math]::Round($startupDiff, 2))s ($([math]::Round($startupPercent, 1))%)" -ForegroundColor $(if ($startupDiff -gt 0.5) { "Red" } elseif ($startupDiff -lt -0.5) { "Green" } else { "Yellow" })
Write-Host ""
Write-Host "📄 Full Report: $reportFile" -ForegroundColor Green
