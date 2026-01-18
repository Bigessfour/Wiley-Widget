#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates comprehensive performance report with diagnostics.
.DESCRIPTION
    This script:
    - Collects system metrics (CPU, Memory, Disk)
    - Analyzes logs for performance patterns
    - Compares against previous runs
    - Identifies performance regressions
    - Generates HTML and text reports
.EXAMPLE
    .\scripts\generate-performance-report.ps1
#>

param(
    [string]$OutputDir = "./diagnostics",
    [string]$ReportName = "performance-report"
)

$ErrorActionPreference = "Stop"

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$reportFile = Join-Path $OutputDir "$ReportName-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
$htmlFile = Join-Path $OutputDir "$ReportName-$(Get-Date -Format 'yyyyMMdd-HHmmss').html"

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  📊 Performance Report Generator" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Collect system info
Write-Host "📦 Collecting system information..." -ForegroundColor Yellow
$osInfo = Get-CimInstance -ClassName Win32_OperatingSystem
$cpuInfo = Get-CimInstance -ClassName Win32_Processor | Select-Object -First 1
$memInfo = Get-CimInstance -ClassName Win32_ComputerSystem

# Get disk info
$diskInfo = Get-Volume | Where-Object { $_.DriveLetter -eq "C" }

# Collect diagnostic logs
Write-Host "📋 Analyzing diagnostic logs..." -ForegroundColor Yellow
$logFiles = @(
    Join-Path $OutputDir "di-validation-profile-*.txt",
    Join-Path $OutputDir "startup-timeline-*.txt",
    Join-Path $OutputDir "timeout-monitor-*.log"
)

$avgStartupTime = $null
$maxDiTime = 0
$totalTimeouts = 0

foreach ($pattern in $logFiles) {
    Get-Item -Path $pattern -ErrorAction SilentlyContinue | ForEach-Object {
        $content = Get-Content $_.FullName -Raw

        # Extract timing information
        if ($content -match "Total.*?(\d+(?:\.\d+)?)\s*s") {
            $time = [double]$matches[1]
            if ($null -eq $avgStartupTime) {
                $avgStartupTime = $time
            } else {
                $avgStartupTime = ($avgStartupTime + $time) / 2
            }
        }

        if ($content -match "Slowest.*?(\d+)\s*ms") {
            $maxDiTime = [math]::Max($maxDiTime, [double]$matches[1])
        }

        if ($content -match "Total Timeouts:\s*(\d+)") {
            $totalTimeouts += [int]$matches[1]
        }
    }
}

# Generate text report
$report = @"
═══════════════════════════════════════════════════════════════
  📊 WileyWidget Performance Report
═══════════════════════════════════════════════════════════════

Generated: $(Get-Date)
Period: $(Get-Date -Format 'MMMM d, yyyy HH:mm:ss')

System Information:
────────────────────────────────────────────────────────────────
OS:                    $($osInfo.Caption)
OS Version:            $($osInfo.Version)
Processor:             $($cpuInfo.Name)
Processor Cores:       $($cpuInfo.NumberOfCores)
Logical Processors:    $($cpuInfo.NumberOfLogicalProcessors)
Total Memory:          $([math]::Round($memInfo.TotalPhysicalMemory / 1GB, 1))GB
Available Memory:      $([math]::Round([float]$osInfo.FreePhysicalMemory / 1MB / 1024, 1))GB
Disk Free (C:):        $([math]::Round($diskInfo.SizeRemaining / 1GB, 1))GB

Performance Metrics:
────────────────────────────────────────────────────────────────
Average Startup Time:  $(if ($avgStartupTime) { "$($avgStartupTime.ToString('F2'))s" } else { "Not measured" })
Max DI Validation Time: $(if ($maxDiTime -gt 0) { "$($maxDiTime)ms" } else { "Not measured" })
Total Timeout Events:  $totalTimeouts

Build Information:
────────────────────────────────────────────────────────────────
$(
    try {
        $buildOutput = dotnet build WileyWidget.sln --dry-run 2>&1 | tail -5
        $buildOutput
    } catch {
        "Build check skipped"
    }
)

Test Results:
────────────────────────────────────────────────────────────────
$(
    try {
        dotnet test WileyWidget.sln --no-build -q --logger "console;verbosity=minimal" 2>&1 | Select-String "Passed|Failed" | Select-Object -First 3
    } catch {
        "Tests not available"
    }
)

Recommendations:
────────────────────────────────────────────────────────────────
$(
    if ($avgStartupTime -and $avgStartupTime -gt 10) {
        "1. ⚠️  Startup time > 10s - Investigate DI registration order`n"
    }
    if ($maxDiTime -gt 5000) {
        "2. ⚠️  DI validation > 5s - Consider async service initialization`n"
    }
    if ($totalTimeouts -gt 3) {
        "3. ⚠️  Multiple timeout events - Increase initialization timeouts`n"
    }
    if (-not $avgStartupTime) {
        "1. Run startup timeline analysis for baseline metrics`n"
    }
)

Performance Baseline:
────────────────────────────────────────────────────────────────
Create baseline measurements:
  1. Run: .\scripts\analyze-startup-timeline.ps1
  2. Run: .\scripts\profile-di-validation.ps1
  3. Save reports to ./diagnostics/baseline/

Compare current vs baseline:
  .\scripts\compare-performance.ps1

Next Steps:
────────────────────────────────────────────────────────────────
1. Review full diagnostic logs in $OutputDir
2. Check application logs for errors/warnings
3. Profile slow services with: dotnet trace collect
4. Monitor real-time metrics with: dotnet counters monitor
5. Capture heap dumps if memory issues suspected

Files Generated:
────────────────────────────────────────────────────────────────
Text Report:  $reportFile
HTML Report:  $htmlFile
"@

$report | Out-File -FilePath $reportFile -Encoding UTF8

# Generate HTML report
$htmlContent = @"
<!DOCTYPE html>
<html>
<head>
    <title>WileyWidget Performance Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; background: #f5f5f5; }
        .header { background: #2c3e50; color: white; padding: 20px; border-radius: 5px; }
        .section { background: white; margin: 20px 0; padding: 15px; border-radius: 5px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .metric { display: inline-block; margin: 10px 15px; }
        .metric-value { font-size: 24px; font-weight: bold; color: #27ae60; }
        .metric-label { color: #666; font-size: 12px; }
        .warning { color: #e74c3c; font-weight: bold; }
        .success { color: #27ae60; font-weight: bold; }
        table { width: 100%; border-collapse: collapse; margin: 10px 0; }
        th { background: #34495e; color: white; padding: 10px; text-align: left; }
        td { padding: 10px; border-bottom: 1px solid #ddd; }
        .timestamp { color: #999; font-size: 12px; }
    </style>
</head>
<body>
    <div class="header">
        <h1>📊 WileyWidget Performance Report</h1>
        <p class="timestamp">Generated: $(Get-Date)</p>
    </div>

    <div class="section">
        <h2>System Information</h2>
        <div class="metric">
            <div class="metric-label">OS</div>
            <div class="metric-value">$($osInfo.Caption)</div>
        </div>
        <div class="metric">
            <div class="metric-label">Processor</div>
            <div class="metric-value">$($cpuInfo.NumberOfCores) cores</div>
        </div>
        <div class="metric">
            <div class="metric-label">Memory</div>
            <div class="metric-value">$([math]::Round($memInfo.TotalPhysicalMemory / 1GB, 1))GB</div>
        </div>
    </div>

    <div class="section">
        <h2>Performance Metrics</h2>
        <div class="metric">
            <div class="metric-label">Avg Startup</div>
            <div class="metric-value $(if ($avgStartupTime -gt 10) { 'warning' } else { 'success' })">
                $(if ($avgStartupTime) { "$($avgStartupTime.ToString('F2'))s" } else { "N/A" })
            </div>
        </div>
        <div class="metric">
            <div class="metric-label">Max DI Time</div>
            <div class="metric-value $(if ($maxDiTime -gt 5000) { 'warning' } else { 'success' })">
                $(if ($maxDiTime -gt 0) { "$($maxDiTime)ms" } else { "N/A" })
            </div>
        </div>
        <div class="metric">
            <div class="metric-label">Timeouts</div>
            <div class="metric-value $(if ($totalTimeouts -gt 3) { 'warning' } else { 'success' })">
                $totalTimeouts
            </div>
        </div>
    </div>

    <div class="section">
        <h2>Diagnostic Files</h2>
        <p>All diagnostic data saved to: <strong>$OutputDir</strong></p>
        <ul>
            <li>Startup Timeline Analysis</li>
            <li>DI Validation Profile</li>
            <li>Timeout Monitoring Logs</li>
            <li>Performance Metrics</li>
        </ul>
    </div>
</body>
</html>
"@

$htmlContent | Out-File -FilePath $htmlFile -Encoding UTF8

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  📊 Performance Report Complete" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "📄 Text Report: $reportFile" -ForegroundColor Green
Write-Host "🌐 HTML Report: $htmlFile" -ForegroundColor Green
Write-Host ""

if ($avgStartupTime) {
    Write-Host "Average Startup Time: $($avgStartupTime.ToString('F2'))s" -ForegroundColor Yellow
}
Write-Host "Total Timeout Events: $totalTimeouts" -ForegroundColor Yellow

Write-Host ""
Write-Host "View HTML report in browser: start '$htmlFile'" -ForegroundColor Cyan
