#!/usr/bin/env pwsh
<#
.SYNOPSIS
Captures detailed diagnostics during app startup with code -1 exit
.DESCRIPTION
Runs WileyWidget.WinForms.exe with enhanced logging, captures full console output,
and collects logs from %APPDATA%\WileyWidget\logs\ to diagnose the exit code -1 issue.
.EXAMPLE
./diagnostic-startup.ps1
#>

param(
    [switch] $Force,
    [switch] $DebugMode
)

$ErrorActionPreference = "Continue"
$app = "$PSScriptRoot\src\WileyWidget.WinForms\bin\Debug\net10.0-windows\WileyWidget.WinForms.exe"
$logDir = "$env:APPDATA\WileyWidget\logs"

Write-Host "[DIAGNOSTICS] WileyWidget Startup Capture" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "App EXE: $app"
Write-Host "Log Dir: $logDir"
Write-Host "Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Green
Write-Host ""

# Step 1: Check build status
Write-Host "[1/5] Checking build status..." -ForegroundColor Yellow
if (-not (Test-Path $app)) {
    Write-Host "ERROR: EXE not found. Running build..." -ForegroundColor Red
    & dotnet build "$PSScriptRoot\WileyWidget.sln" -c Debug
    if (-not (Test-Path $app)) {
        Write-Error "Build failed - EXE still not found"
        exit 1
    }
}
Write-Host "✓ Build verified" -ForegroundColor Green

# Step 2: Backup old logs
Write-Host "[2/5] Managing logs..." -ForegroundColor Yellow
if (Test-Path $logDir) {
    $backupDir = "$logDir\backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Get-ChildItem $logDir -Filter "*.log" | Move-Item -Destination $backupDir -ErrorAction SilentlyContinue
    Write-Host "✓ Old logs backed up to $backupDir"
}

# Step 3: Run app and capture output
Write-Host "[3/5] Running app with console capture..." -ForegroundColor Yellow
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $output = & $app 2>&1 | Tee-Object -Variable consoleOutput
    $exitCode = $LASTEXITCODE
}
catch {
    $exitCode = -1
}
$stopwatch.Stop()

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "APP EXECUTION COMPLETE" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Exit Code: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { 'Green' } else { 'Red' })
Write-Host "Runtime: $($stopwatch.ElapsedMilliseconds) ms" -ForegroundColor Yellow
Write-Host ""

# Step 4: Analyze console output
Write-Host "[4/5] Analyzing console output..." -ForegroundColor Yellow
if ($null -ne $consoleOutput) {
    $lines = @($consoleOutput)
    Write-Host "Console output lines: $($lines.Count)" -ForegroundColor Cyan

    # Find critical markers
    $diagnosticMarkers = @(
        "DIAGNOSTIC",
        "CRITICAL",
        "ERROR",
        "FATAL",
        "Exception",
        "unhandled",
        "PANEL-VISIBILITY-DIAG",
        "PANEL-VISIBILITY-ISSUE",
        "Z-ORDER-ACTIVATION",
        "Z-ORDER-DOCKING"
    )

    foreach ($marker in $diagnosticMarkers) {
        $matches = $lines | Where-Object { $_ -match $marker }
        if ($matches) {
            Write-Host ""
            Write-Host "Lines containing '$marker':" -ForegroundColor Yellow
            $matches | ForEach-Object {
                if ($_ -match "ERROR|FATAL|CRITICAL|Exception") {
                    Write-Host "  $_" -ForegroundColor Red
                } elseif ($_ -match "DIAGNOSTIC") {
                    Write-Host "  $_" -ForegroundColor Cyan
                } else {
                    Write-Host "  $_"
                }
            }
        }
    }
}

# Step 5: Check and display log files
Write-Host ""
Write-Host "[5/5] Checking generated log files..." -ForegroundColor Yellow
if (Test-Path $logDir) {
    $logFiles = Get-ChildItem $logDir -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 5
    if ($logFiles) {
        Write-Host "Recent logs:" -ForegroundColor Green
        foreach ($file in $logFiles) {
            Write-Host "  - $($file.Name) ($(Get-Date $file.LastWriteTime -Format 'HH:mm:ss'), $([math]::Round($file.Length/1024, 2))KB)"
        }

        Write-Host ""
        Write-Host "=== MOST RECENT LOG FILE TAIL (last 50 lines) ===" -ForegroundColor Cyan
        $mostRecent = $logFiles | Select-Object -First 1
        Write-Host "File: $($mostRecent.Name)" -ForegroundColor Yellow
        Write-Host ""

        $content = Get-Content $mostRecent.FullName -Tail 50
        $content | ForEach-Object {
            if ($_ -match "\[DIAGNOSTIC\]") {
                Write-Host $_ -ForegroundColor Cyan
            } elseif ($_ -match "ERROR|FATAL|CRITICAL|Exception") {
                Write-Host $_ -ForegroundColor Red
            } else {
                Write-Host $_
            }
        }
    }
    else {
        Write-Host "No log files found in $logDir" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Exit Code: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { 'Green' } else { 'Red' })
Write-Host ""

if ($exitCode -eq -1) {
    Write-Host "⚠️  EXIT CODE -1 DETECTED" -ForegroundColor Red
    Write-Host ""
    Write-Host "This indicates an unhandled exception on the UI thread." -ForegroundColor Yellow
    Write-Host "Check the log file above for [DIAGNOSTIC] and ERROR messages." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Key diagnostic steps:" -ForegroundColor Cyan
    Write-Host "  1. Look for [DIAGNOSTIC] messages - they mark lifecycle checkpoints"
    Write-Host "  2. Find the LAST successful [DIAGNOSTIC] message"
    Write-Host "  3. Look for the FIRST ERROR or CRITICAL message after that"
    Write-Host "  4. That error is likely the root cause"
}

Write-Host ""
Write-Host "Full log directory: $logDir" -ForegroundColor Gray
Write-Host "Script completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
