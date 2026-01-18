#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Auto-captures diagnostic dumps when WileyWidget crashes.
.DESCRIPTION
    This script:
    - Monitors WileyWidget process
    - Captures full memory dump on crash
    - Collects event log entries
    - Archives diagnostics
    - Generates crash report
.EXAMPLE
    .\scripts\auto-dump-on-crash.ps1
.NOTES
    Run this in a separate terminal while debugging.
#>

param(
    [string]$OutputDir = "./diagnostics",
    [int]$CheckIntervalMs = 1000
)

$ErrorActionPreference = "Continue"

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$crashLogFile = Join-Path $OutputDir "crash-events-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  🔬 Automatic Crash Dump Capture" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "🎯 Monitoring:" -ForegroundColor Yellow
Write-Host "   • Process exit" -ForegroundColor Yellow
Write-Host "   • Unhandled exceptions" -ForegroundColor Yellow
Write-Host "   • Memory dumps" -ForegroundColor Yellow
Write-Host ""
Write-Host "📁 Crash Log: $crashLogFile" -ForegroundColor Green
Write-Host "📁 Dumps Dir: $(Join-Path $OutputDir 'crash-dumps')" -ForegroundColor Green
Write-Host ""
Write-Host "Press Ctrl+C to stop monitoring..." -ForegroundColor Yellow
Write-Host ""

# Create crash dumps directory
$dumpDir = Join-Path $OutputDir "crash-dumps"
if (-not (Test-Path $dumpDir)) {
    New-Item -ItemType Directory -Path $dumpDir -Force | Out-Null
}

# Configure dump capture
$env:DOTNET_DbgEnableMiniDump = 1
$env:DOTNET_DbgMiniDumpType = 4 # Full dump
$env:DOTNET_DbgMiniDumpDir = $dumpDir

function Write-DiagnosticLog {
    param([string]$Message, [string]$Severity = "INFO")

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $logLine = "[$timestamp] [$Severity] $Message"

    Add-Content -Path $crashLogFile -Value $logLine -ErrorAction SilentlyContinue

    switch ($Severity) {
        "CRASH" { Write-Host $logLine -ForegroundColor Red }
        "DUMP" { Write-Host $logLine -ForegroundColor Yellow }
        "ERROR" { Write-Host $logLine -ForegroundColor Red }
        "SUCCESS" { Write-Host $logLine -ForegroundColor Green }
        default { Write-Host $logLine -ForegroundColor Cyan }
    }
}

Write-DiagnosticLog "Crash monitoring started" "INFO"
Write-DiagnosticLog "Dump directory: $dumpDir" "INFO"

try {
    while ($true) {
        Start-Sleep -Milliseconds $CheckIntervalMs

        # Check for WileyWidget process
        $process = Get-Process -Name WileyWidget.WinForms -ErrorAction SilentlyContinue | Select-Object -First 1

        # Check for new dump files
        $dumpFiles = Get-ChildItem -Path $dumpDir -Filter "*.dmp" -ErrorAction SilentlyContinue
        if ($dumpFiles.Count -gt 0) {
            Log-Event "Crash dump detected: $($dumpFiles.Count) dump(s)" "DUMP"

            foreach ($dump in $dumpFiles) {
                $size = [math]::Round($dump.Length / 1MB, 1)
                Log-Event "  Dump file: $($dump.Name) ($size MB)" "DUMP"

                # Generate crash report
                $reportFile = Join-Path $OutputDir "crash-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"

                @"
═══════════════════════════════════════════════════════════════
  💥 Crash Report
═══════════════════════════════════════════════════════════════

Timestamp: $(Get-Date)
Dump File: $($dump.FullName)
Dump Size: $size MB

System Info:
────────────────────────────────────────────────────────────────
$(Get-CimInstance -ClassName Win32_OperatingSystem | Select-Object Caption, Version | Format-List | Out-String)

Process Info:
────────────────────────────────────────────────────────────────
$(Get-CimInstance -ClassName Win32_OperatingSystem | Select-Object TotalVisibleMemorySize, FreePhysicalMemory | Format-List | Out-String)

Windows Event Log (Last 10 errors):
────────────────────────────────────────────────────────────────
$(Get-WinEvent -FilterHashtable @{LogName="Application"; Level=2,3} -MaxEvents 10 -ErrorAction SilentlyContinue | Format-Table -AutoSize | Out-String)

Debugging Instructions:
────────────────────────────────────────────────────────────────
1. Analyze dump with VS: File > Open > Project/Solution
2. Or use dotnet-dump: dotnet dump analyze $($dump.FullName)
3. Look for:
   - Exception stack trace
   - Thread state
   - Managed heap status

Next Steps:
────────────────────────────────────────────────────────────────
1. Review crash report: $reportFile
2. Check full dump: $($dump.FullName)
3. Analyze with debugger or dotnet-dump
4. Compare with previous crashes
5. Review Windows Event Viewer
"@ | Out-File -FilePath $reportFile -Encoding UTF8

                Log-Event "Crash report generated: $reportFile" "SUCCESS"
            }
        }

        # Update window title
        if ((Get-Random -Minimum 0 -Maximum 10) -eq 0) {
            $processRunning = if ($process) { "Running" } else { "Not Running" }
            $dumpCount = (Get-ChildItem -Path $dumpDir -Filter "*.dmp" -ErrorAction SilentlyContinue).Count
            $host.UI.RawUI.WindowTitle = "Crash Monitor | App: $processRunning | Dumps: $dumpCount"
        }
    }
}
catch {
    Log-Event "Monitor error: $_" "ERROR"
}
finally {
    Log-Event "Crash monitoring stopped" "INFO"
    Write-Host ""
    Write-Host "Final Summary:" -ForegroundColor Yellow
    $dumps = Get-ChildItem -Path $dumpDir -Filter "*.dmp" -ErrorAction SilentlyContinue
    Write-Host "  Total dump files collected: $($dumps.Count)" -ForegroundColor Yellow
    if ($dumps.Count -gt 0) {
        Write-Host "  Total dump size: $([math]::Round(($dumps | Measure-Object -Property Length -Sum).Sum / 1MB, 1)) MB" -ForegroundColor Yellow
    }
}
