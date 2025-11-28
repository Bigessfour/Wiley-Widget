#Requires -Version 7.0

<#
.SYNOPSIS
    Cleans up lingering .NET build processes (MSBuild nodes, VBCSCompiler, test hosts).

.DESCRIPTION
    Kills .NET build servers, compiler servers, and test processes to free memory.
    Safe to run - only targets build infrastructure, not running applications.

.PARAMETER Force
    Skip confirmation prompt

.EXAMPLE
    .\cleanup-dotnet-processes.ps1
    .\cleanup-dotnet-processes.ps1 -Force
#>

[CmdletBinding()]
param(
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "`n🧹 Cleaning up .NET build processes..." -ForegroundColor Cyan

# Get all .NET processes except the current one and running applications
$processes = Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $_.ProcessName -match 'dotnet|MSBuild|VBCSCompiler|testhost' -and
    $_.Id -ne $PID
}

if (-not $processes) {
    Write-Host "✅ No .NET build processes found to clean up." -ForegroundColor Green
    exit 0
}

# Show what will be killed
Write-Host "`nFound $($processes.Count) processes:" -ForegroundColor Yellow
$processes | Select-Object ProcessName, Id, @{Name='Memory(MB)';Expression={[math]::Round($_.WorkingSet64/1MB,2)}} | Format-Table -AutoSize

# Confirm unless -Force
if (-not $Force) {
    $response = Read-Host "Kill these processes? (y/n)"
    if ($response -ne 'y') {
        Write-Host "Cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# Kill processes
$killed = 0
$failed = 0

foreach ($proc in $processes) {
    try {
        # Try to get command line to avoid killing important processes
        $cmdLine = (Get-CimInstance Win32_Process -Filter "ProcessId = $($proc.Id)" -ErrorAction SilentlyContinue).CommandLine
        
        # Skip if it looks like a running application (not a build tool)
        if ($cmdLine -and $cmdLine -notmatch 'MSBuild|VBCSCompiler|testhost|vstest|BuildHost|OmniSharp') {
            Write-Host "⏭️  Skipping PID $($proc.Id) (appears to be running app)" -ForegroundColor Gray
            continue
        }

        Stop-Process -Id $proc.Id -Force -ErrorAction Stop
        Write-Host "✅ Killed $($proc.ProcessName) (PID $($proc.Id))" -ForegroundColor Green
        $killed++
    }
    catch {
        Write-Host "⚠️  Failed to kill PID $($proc.Id): $_" -ForegroundColor Yellow
        $failed++
    }
}

# Also shutdown the build server explicitly
Write-Host "`n🔧 Shutting down .NET build server..." -ForegroundColor Cyan
try {
    dotnet build-server shutdown | Out-Null
    Write-Host "✅ Build server shutdown complete" -ForegroundColor Green
}
catch {
    Write-Host "⚠️  Build server shutdown failed: $_" -ForegroundColor Yellow
}

Write-Host "`n📊 Summary: Killed $killed, Failed $failed, Skipped $($processes.Count - $killed - $failed)" -ForegroundColor Cyan
Write-Host "✅ Cleanup complete!`n" -ForegroundColor Green
