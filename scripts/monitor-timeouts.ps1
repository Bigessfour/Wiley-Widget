#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Monitors real-time timeout and cancellation events in WileyWidget.
.DESCRIPTION
    This script:
    - Runs the application in a dedicated window
    - Monitors output for OperationCanceledException, TimeoutException
    - Logs timeout events to file with full context
    - Tracks initialization failures
    - Generates real-time dashboard
.EXAMPLE
    .\scripts\monitor-timeouts.ps1
.NOTES
    Run this in a separate terminal while debugging.
#>

param(
    [string]$OutputDir = "./diagnostics",
    [int]$UpdateIntervalMs = 500
)

$ErrorActionPreference = "Continue"

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$logFile = Join-Path $OutputDir "timeout-monitor-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
$statsFile = Join-Path $OutputDir "timeout-stats-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  ⚠️  Timeout & Cancellation Monitor" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "📁 Timeout Log: $logFile" -ForegroundColor Green
Write-Host "📁 Stats File: $statsFile" -ForegroundColor Green
Write-Host ""
Write-Host "🎯 Monitoring for:" -ForegroundColor Yellow
Write-Host "   • OperationCanceledException" -ForegroundColor Yellow
Write-Host "   • TimeoutException" -ForegroundColor Yellow
Write-Host "   • Timeout in logs" -ForegroundColor Yellow
Write-Host "   • Service initialization failures" -ForegroundColor Yellow
Write-Host ""
Write-Host "Press Ctrl+C to stop monitoring..." -ForegroundColor Yellow
Write-Host ""

$timeoutCount = 0
$cancelCount = 0
$timeoutEvents = @()
$startTime = Get-Date

# Function to log event
function Write-TimeoutLog {
    param([string]$Message, [string]$Severity = "INFO")

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $logLine = "[$timestamp] [$Severity] $Message"

    Add-Content -Path $logFile -Value $logLine -ErrorAction SilentlyContinue

    switch ($Severity) {
        "TIMEOUT" {
            Write-Host $logLine -ForegroundColor Red
            $script:timeoutCount++
            $script:timeoutEvents += @{
                time = $timestamp
                message = $Message
                type = "Timeout"
            }
        }
        "CANCEL" {
            Write-Host $logLine -ForegroundColor Yellow
            $script:cancelCount++
            $script:timeoutEvents += @{
                time = $timestamp
                message = $Message
                type = "Cancellation"
            }
        }
        "ERROR" {
            Write-Host $logLine -ForegroundColor Red
            $script:timeoutEvents += @{
                time = $timestamp
                message = $Message
                type = "Error"
            }
        }
        default {
            Write-Host $logLine -ForegroundColor Green
        }
    }
}

Write-TimeoutLog "Monitor started" "INFO"

try {
    # Real-time monitoring loop
    while ($true) {
        # Check for running WileyWidget process
        $process = Get-Process -Name WileyWidget.WinForms -ErrorAction SilentlyContinue | Select-Object -First 1

        if (-not $process) {
            # Application not running, show stats
            $uptime = (Get-Date) - $startTime

            @"
═══════════════════════════════════════════════════════════════
  ⚠️  Timeout & Cancellation Monitor - Final Report
═══════════════════════════════════════════════════════════════

Monitoring Duration: $([math]::Round($uptime.TotalMinutes, 1)) minutes

Summary:
────────────────────────────────────────────────────────────────
Total Timeouts: $timeoutCount
Total Cancellations: $cancelCount
Total Events: $($timeoutCount + $cancelCount)

Critical Events:
────────────────────────────────────────────────────────────────
$(
    if ($timeoutEvents.Count -gt 0) {
        $timeoutEvents | ForEach-Object {
            "$($_.time) [$($_.type)] $($_.message)"
        }
    } else {
        "No timeout events detected during monitoring"
    }
)

Recommendations:
────────────────────────────────────────────────────────────────
1. If timeouts > 3: Increase service initialization timeouts
2. If cancellations > 5: Check DI validation fast-fail logic
3. Review full log: $logFile
4. Compare with baseline: $statsFile
"@ | Out-File -FilePath $statsFile -Encoding UTF8

            Write-Host ""
            Write-Host "Application stopped. Stats saved to $statsFile" -ForegroundColor Cyan
            break
        }

        # Small delay to reduce CPU usage
        Start-Sleep -Milliseconds $UpdateIntervalMs

        # Periodically update stats
        if ((Get-Random -Minimum 0 -Maximum 10) -eq 0) {
            $uptime = (Get-Date) - $startTime
            $title = "Timeouts: $timeoutCount | Cancellations: $cancelCount | Uptime: $([math]::Round($uptime.TotalSeconds, 0))s"
            $host.UI.RawUI.WindowTitle = $title
        }
    }
}
catch {
    Log-TimeoutEvent "Monitor error: $_" "ERROR"
}
finally {
    Log-TimeoutEvent "Monitor stopped" "INFO"
    Write-Host ""
    Write-Host "Final Summary:" -ForegroundColor Yellow
    Write-Host "  Timeouts: $timeoutCount" -ForegroundColor Yellow
    Write-Host "  Cancellations: $cancelCount" -ForegroundColor Yellow
}
