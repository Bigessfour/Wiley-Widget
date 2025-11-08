#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Kills hanging testhost and dotnet processes before running tests
.DESCRIPTION
    This script terminates any testhost or long-running dotnet processes
    that might be locking test DLLs or causing test hangs.
#>

[CmdletBinding()]
param()

Write-Host "🔍 Checking for hanging test processes..." -ForegroundColor Cyan

# Get testhost processes
$testhostProcesses = Get-Process -Name "testhost" -ErrorAction SilentlyContinue

if ($testhostProcesses) {
    Write-Host "⚠️  Found $($testhostProcesses.Count) testhost process(es)" -ForegroundColor Yellow
    foreach ($process in $testhostProcesses) {
        Write-Host "   Killing testhost PID: $($process.Id)" -ForegroundColor Yellow
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    Write-Host "✅ Killed testhost processes" -ForegroundColor Green
} else {
    Write-Host "✅ No testhost processes found" -ForegroundColor Green
}

# Get dotnet processes older than 2 minutes (likely hung)
$dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue |
    Where-Object {
        $_.StartTime -and ((Get-Date) - $_.StartTime).TotalMinutes -gt 2
    }

if ($dotnetProcesses) {
    Write-Host "⚠️  Found $($dotnetProcesses.Count) long-running dotnet process(es)" -ForegroundColor Yellow
    foreach ($process in $dotnetProcesses) {
        $runtime = [math]::Round(((Get-Date) - $process.StartTime).TotalMinutes, 1)
        Write-Host "   Killing dotnet PID: $($process.Id) (running for $runtime minutes)" -ForegroundColor Yellow
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    Write-Host "✅ Killed long-running dotnet processes" -ForegroundColor Green
} else {
    Write-Host "✅ No long-running dotnet processes found" -ForegroundColor Green
}

Write-Host "🎯 Ready to run tests" -ForegroundColor Green
