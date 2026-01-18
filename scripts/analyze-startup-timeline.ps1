#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Analyzes WileyWidget startup timeline by monitoring logs and measuring initialization stages.
.DESCRIPTION
    This script measures key startup milestones:
    - DI container creation
    - Service registration
    - Database connection
    - Theme initialization
    - Form creation and showing
    - Async initialization completion
.EXAMPLE
    .\scripts\analyze-startup-timeline.ps1
#>

param(
    [int]$TimeoutSeconds = 30,
    [string]$OutputDir = "./diagnostics"
)

$ErrorActionPreference = "Stop"

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$reportFile = Join-Path $OutputDir "startup-timeline-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
$startTime = Get-Date

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  🚀 WileyWidget Startup Timeline Analysis" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "⏱️  Starting application and monitoring initialization..." -ForegroundColor Yellow
Write-Host "    Timeout: ${TimeoutSeconds}s" -ForegroundColor Yellow
Write-Host "    Output: $reportFile" -ForegroundColor Yellow
Write-Host ""

$milestones = @()

# Start the application
$process = Start-Process -FilePath "dotnet" `
    -ArgumentList "run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj" `
    -PassThru `
    -RedirectStandardOutput (Join-Path $OutputDir "app-stdout-tmp.txt") `
    -RedirectStandardError (Join-Path $OutputDir "app-stderr-tmp.txt")

$processId = $process.Id
Write-Host "✓ Application started (PID: $processId)" -ForegroundColor Green

$appStartTime = Get-Date
$outputPath = Join-Path $OutputDir "app-stdout-tmp.txt"

# Monitor for startup milestones
$timeout = $startTime.AddSeconds($TimeoutSeconds)
$lastCheck = Get-Date

while ($lastCheck -lt $timeout -and -not $process.HasExited) {
    Start-Sleep -Milliseconds 100
    $lastCheck = Get-Date
    
    if (Test-Path $outputPath) {
        $content = Get-Content $outputPath -ErrorAction SilentlyContinue
        if ($content) {
            # Check for key milestones
            if ($content -match "DI container created" -and (-not ($milestones | Where-Object { $_.event -eq "DI Created" }))) {
                $milestones += [PSCustomObject]@{
                    event = "DI Created"
                    time = (Get-Date)
                    elapsed = ((Get-Date) - $appStartTime).TotalSeconds
                }
                Write-Host "✓ DI container created" -ForegroundColor Green
            }
            if ($content -match "Services registered" -and (-not ($milestones | Where-Object { $_.event -eq "Services Registered" }))) {
                $milestones += [PSCustomObject]@{
                    event = "Services Registered"
                    time = (Get-Date)
                    elapsed = ((Get-Date) - $appStartTime).TotalSeconds
                }
                Write-Host "✓ Services registered" -ForegroundColor Green
            }
            if ($content -match "MainForm shown" -and (-not ($milestones | Where-Object { $_.event -eq "MainForm Shown" }))) {
                $milestones += [PSCustomObject]@{
                    event = "MainForm Shown"
                    time = (Get-Date)
                    elapsed = ((Get-Date) - $appStartTime).TotalSeconds
                }
                Write-Host "✓ MainForm shown (UI visible)" -ForegroundColor Green
            }
            if ($content -match "Async initialization complete" -and (-not ($milestones | Where-Object { $_.event -eq "Async Init Complete" }))) {
                $milestones += [PSCustomObject]@{
                    event = "Async Init Complete"
                    time = (Get-Date)
                    elapsed = ((Get-Date) - $appStartTime).TotalSeconds
                }
                Write-Host "✓ Async initialization complete" -ForegroundColor Green
            }
        }
    }
}

# Kill the process
Write-Host ""
Write-Host "⏹️  Stopping application..." -ForegroundColor Yellow
Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

$totalTime = (Get-Date) - $appStartTime

# Generate report
@"
═══════════════════════════════════════════════════════════════
  🚀 WileyWidget Startup Timeline Report
═══════════════════════════════════════════════════════════════

Generated: $(Get-Date)
Total Startup Time: $($totalTime.TotalSeconds.ToString('F2'))s

Milestones:
────────────────────────────────────────────────────────────────
$($milestones | ConvertTo-Csv -NoTypeInformation -Delimiter "`t" | ForEach-Object { $_ -replace '"""', '' })

Timeline:
────────────────────────────────────────────────────────────────
$(
    if ($milestones.Count -gt 0) {
        $milestones | ForEach-Object {
            "[$($_.elapsed.ToString('F2'))s] $($_.event)"
        }
    } else {
        "No milestones detected. Check if application is logging startup events."
    }
)

Performance Summary:
────────────────────────────────────────────────────────────────
Total Time to UI: $($milestones | Where-Object { $_.event -eq "MainForm Shown" } | ForEach-Object { $_.elapsed.ToString('F2') + 's' } -ErrorAction SilentlyContinue || "Not measured")
Total Time to Ready: $($milestones | Where-Object { $_.event -eq "Async Init Complete" } | ForEach-Object { $_.elapsed.ToString('F2') + 's' } -ErrorAction SilentlyContinue || "Not measured")

Recommendations:
────────────────────────────────────────────────────────────────
- If startup time > 10s, investigate DI service registration
- If MainForm shown > 5s, check database/telemetry initialization
- If async init > 5s, profile background services (prefetch, Grok)
"@ | Out-File -FilePath $reportFile -Encoding UTF8

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  📊 Report Generated" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "📁 Location: $reportFile" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total Startup Time: $($totalTime.TotalSeconds.ToString('F2'))s" -ForegroundColor Yellow
Write-Host ""
Write-Host "Milestone Summary:" -ForegroundColor Yellow
$milestones | ForEach-Object { Write-Host "  ✓ $($_.event) @ $($_.elapsed.ToString('F2'))s" -ForegroundColor Green }

# Cleanup
Remove-Item (Join-Path $OutputDir "app-stdout-tmp.txt") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $OutputDir "app-stderr-tmp.txt") -Force -ErrorAction SilentlyContinue
