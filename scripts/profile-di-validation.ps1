#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Profiles DI validation performance and identifies bottleneck services.
.DESCRIPTION
    This script:
    - Runs DI validation in isolation
    - Measures time per service category
    - Identifies services with long resolution times
    - Detects OperationCanceledException and timeout issues
    - Generates performance baseline
.EXAMPLE
    .\scripts\profile-di-validation.ps1
#>

param(
    [string]$OutputDir = "./diagnostics"
)

$ErrorActionPreference = "Stop"

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$reportFile = Join-Path $OutputDir "di-validation-profile-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  🔍 DI Validation Profile" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$env:Validation:DiValidationEnabled = "true"
$env:Validation:FastValidation = "false"

Write-Host "⏱️  Building solution..." -ForegroundColor Yellow
dotnet build WileyWidget.sln -c Debug -v minimal -q

Write-Host "✓ Build complete" -ForegroundColor Green
Write-Host ""
Write-Host "🧪 Running DI validation test..." -ForegroundColor Yellow
Write-Host ""

$testResult = dotnet test `
    "${env:TEMP}\WileyWidget.WinForms.Tests\bin\Debug\net10.0-windows10.0.26100.0\WileyWidget.WinForms.Tests.dll" `
    --filter "Name~DiValidation" `
    --logger "console;verbosity=detailed" `
    2>&1

# Parse output for timing information
$timings = @{}

$testResult | ForEach-Object {
    if ($_ -match '(\w+)\s+.*?(\d+(?:\.\d+)?)\s*ms') {
        $category = $matches[1]
        $duration = [double]$matches[2]
        if (-not $timings.ContainsKey($category)) {
            $timings[$category] = $duration
        }
    }
}

# Generate report
@"
═══════════════════════════════════════════════════════════════
  🔍 DI Validation Profile Report
═══════════════════════════════════════════════════════════════

Generated: $(Get-Date)

Service Category Performance:
────────────────────────────────────────────────────────────────
$(
    if ($timings.Count -gt 0) {
        $sorted = $timings.GetEnumerator() | Sort-Object -Property Value -Descending
        $sorted | ForEach-Object {
            $percent = [math]::Round(($_.Value / ($sorted | Measure-Object -Property Value -Sum).Sum * 100), 1)
            "$($_.Key): $($_.Value)ms ($percent%)"
        }
    } else {
        "No timing data captured. Run tests with detailed logging."
    }
)

Slowest Categories (Top 5):
────────────────────────────────────────────────────────────────
$(
    $sorted | Select-Object -First 5 | ForEach-Object {
        "  ⚠️  $($_.Key): $($_.Value)ms"
    }
)

Potential Issues:
────────────────────────────────────────────────────────────────
$(
    if ($testResult -match "OperationCanceled") {
        "  🔴 OperationCanceledException detected - Services timing out"
    }
    if ($testResult -match "Timeout") {
        "  🔴 Timeout exceptions detected - Increase timeout values"
    }
    if ($testResult -match "ERROR|FAIL") {
        "  🟡 Validation errors found - Check service registrations"
    }
    if ($timings.Values | Where-Object { $_ -gt 5000 }) {
        "  🟡 Service(s) taking > 5s to initialize - Consider lazy loading"
    }
)

Recommendations:
────────────────────────────────────────────────────────────────
1. Services taking > 1000ms should be investigated
2. Consider async initialization for slow services
3. Use AddHostedService for background tasks
4. Validate service dependencies for circular references
5. Check database connection timeout values
6. Monitor telemetry service initialization

Full Test Output:
────────────────────────────────────────────────────────────────
$testResult
"@ | Out-File -FilePath $reportFile -Encoding UTF8

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  📊 DI Validation Profile Complete" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "📁 Report: $reportFile" -ForegroundColor Green
Write-Host ""

# Display summary
if ($timings.Count -gt 0) {
    Write-Host "Top Bottlenecks:" -ForegroundColor Yellow
    $timings.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 3 | ForEach-Object {
        Write-Host "  ⚠️  $($_.Key): $($_.Value)ms" -ForegroundColor Yellow
    }
}
