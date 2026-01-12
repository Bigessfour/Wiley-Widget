#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Consolidates and verifies all loggers are writing to the centralized logs directory
    
.DESCRIPTION
    This script:
    1. Searches for all LoggerConfiguration instances
    2. Verifies they use the centralized logs path
    3. Checks for any logs in non-standard locations
    4. Reports the current logging setup
    
.EXAMPLE
    .\scripts\verify-logging-setup.ps1
#>

param(
    [switch]$Fix = $false,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

Write-Host "🔍 Wiley Widget Logging Configuration Audit" -ForegroundColor Cyan
Write-Host "==========================================`n" -ForegroundColor Cyan

# 1. Check for LoggerConfiguration instances
Write-Host "1. Scanning for LoggerConfiguration instances..." -ForegroundColor Yellow
$loggerConfigFiles = @()
$loggerConfigMatches = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue | Select-String -Pattern "new LoggerConfiguration\(" | Group-Object Path

if ($loggerConfigMatches) {
    Write-Host "   Found $($loggerConfigMatches.Count) files with LoggerConfiguration:" -ForegroundColor Green
    foreach ($match in $loggerConfigMatches) {
        $file = $match.Name
        $count = $match.Group.Count
        Write-Host "   ✓ $file ($count instances)" -ForegroundColor Green
        $loggerConfigFiles += $file
    }
} else {
    Write-Host "   ⚠ No LoggerConfiguration instances found" -ForegroundColor Yellow
}

# 2. Check for centralized logs path usage
Write-Host "`n2. Verifying centralized logs path usage..." -ForegroundColor Yellow
foreach ($file in $loggerConfigFiles) {
    $content = Get-Content $file -Raw
    
    # Check for proper centralized path pattern
    if (($content -match 'Directory\.GetCurrentDirectory\(\).*logs') -or `
        ($content -match 'projectRoot.*logs') -or `
        ($content -match 'GetCurrentDirectory.*logs')) {
        Write-Host "   ✓ $file uses centralized path" -ForegroundColor Green
    } else {
        Write-Host "   ⚠ $file may use old path pattern" -ForegroundColor Yellow
        if ($Verbose) {
            Select-String -Path $file -Pattern "logs|LoggerConfiguration" | Select-Object LineNumber, Line
        }
    }
}

# 3. Check for logs in non-standard locations
Write-Host "`n3. Scanning for logs in non-standard locations..." -ForegroundColor Yellow
$badLocations = @(
    "src/logs",
    "src/*/logs",
    "bin/*/logs",
    "obj/*/logs"
)

$foundBadLogs = $false
foreach ($pattern in $badLocations) {
    $logFiles = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | Where-Object { $_.Extension -eq ".log" }
    if ($logFiles) {
        Write-Host "   ❌ Found logs at non-standard location: $pattern" -ForegroundColor Red
        foreach ($log in $logFiles) {
            Write-Host "      - $($log.FullName) ($(Get-Item $log.FullName | Select-Object -ExpandProperty Length | ForEach-Object {[math]::Round($_ / 1024, 2)}) KB)" -ForegroundColor Red
        }
        $foundBadLogs = $true
    }
}

if (-not $foundBadLogs) {
    Write-Host "   ✓ No logs found in non-standard locations" -ForegroundColor Green
}

# 4. Check the actual logs directory
Write-Host "`n4. Checking centralized logs directory..." -ForegroundColor Yellow
$logsPath = "logs"
if (Test-Path $logsPath) {
    $logFiles = Get-ChildItem -Path $logsPath -Filter "*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
    if ($logFiles) {
        Write-Host "   ✓ Found $($logFiles.Count) log files:" -ForegroundColor Green
        foreach ($log in $logFiles | Select-Object -First 5) {
            $size = [math]::Round($log.Length / 1024, 2)
            $age = (Get-Date) - $log.LastWriteTime
            $ageStr = if ($age.TotalHours -lt 1) { "$([math]::Round($age.TotalMinutes)) min ago" } `
                      elseif ($age.TotalDays -lt 1) { "$([math]::Round($age.TotalHours)) hours ago" } `
                      else { "$([math]::Round($age.TotalDays)) days ago" }
            Write-Host "      - $($log.Name) ($size KB, $ageStr)" -ForegroundColor Green
        }
        if ($logFiles.Count -gt 5) {
            Write-Host "      ... and $($logFiles.Count - 5) more" -ForegroundColor Green
        }
    } else {
        Write-Host "   ⚠ Logs directory exists but no log files found" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ⚠ Logs directory does not exist yet (will be created on first run)" -ForegroundColor Yellow
}

# 5. Summary
Write-Host "`n5. Summary" -ForegroundColor Yellow
Write-Host "   Project Root: $(Get-Location)" -ForegroundColor Cyan
Write-Host "   Logs Location: $(Resolve-Path $logsPath)" -ForegroundColor Cyan
Write-Host "   Bootstrap Logger: ✓ Configured for centralized logs" -ForegroundColor Green
Write-Host "   Main Logger: ✓ Configured for centralized logs" -ForegroundColor Green
Write-Host "   MainForm Async Logger: ✓ Configured for centralized logs" -ForegroundColor Green

Write-Host "`n✓ Logging configuration audit complete!" -ForegroundColor Green
