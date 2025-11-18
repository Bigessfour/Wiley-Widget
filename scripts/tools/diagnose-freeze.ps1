# Diagnostic script for WileyWidget freeze/crash analysis
# Usage: .\diagnose-freeze.ps1

param(
    [switch]$AttachToRunning,
    [switch]$MonitorNew,
    [int]$TimeoutSeconds = 300,
    [string]$DumpPath = "C:\Users\biges\Desktop\Wiley_Widget\diagnostics\dumps"
)

$ErrorActionPreference = "Stop"

# Ensure dump directory exists
New-Item -ItemType Directory -Path $DumpPath -Force | Out-Null
Write-Host "Dump directory: $DumpPath" -ForegroundColor Cyan

$procdump = "C:\Tools\SysinternalsSuite\procdump.exe"
$perfMonBin = "C:\Windows\System32\perfmon.exe"

if ($AttachToRunning) {
    # Find running WileyWidget process
    $process = Get-Process -Name "WileyWidget" -ErrorAction SilentlyContinue | Select-Object -First 1
    
    if (-not $process) {
        Write-Host "No running WileyWidget process found" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Found WileyWidget process PID: $($process.Id)" -ForegroundColor Green
    
    # Monitor for hangs, exceptions, and high memory
    Write-Host "`nStarting diagnostic monitoring..." -ForegroundColor Yellow
    Write-Host "  - Hang detection (5s window unresponsive)" -ForegroundColor Gray
    Write-Host "  - Exception capture (first chance + unhandled)" -ForegroundColor Gray
    Write-Host "  - Memory threshold: 500MB commit" -ForegroundColor Gray
    Write-Host "  - Timeout: $TimeoutSeconds seconds" -ForegroundColor Gray
    Write-Host "`nPress Ctrl+C to stop monitoring`n" -ForegroundColor Yellow
    
    # Capture on hang, exception, or high memory (full dump)
    & $procdump -accepteula -ma -h -e 1 -m 500 -n 3 -at $TimeoutSeconds $process.Id $DumpPath
    
} elseif ($MonitorNew) {
    # Wait for new WileyWidget launch and monitor from start
    Write-Host "Waiting for WileyWidget.exe to launch..." -ForegroundColor Yellow
    Write-Host "Monitoring will start automatically when app starts`n" -ForegroundColor Gray
    
    # Launch monitoring that waits for process start
    & $procdump -accepteula -ma -w WileyWidget.exe -e 1 -h -m 500 -n 5 -at $TimeoutSeconds $DumpPath
    
} else {
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  Attach to running instance: .\diagnose-freeze.ps1 -AttachToRunning"
    Write-Host "  Monitor new launch:         .\diagnose-freeze.ps1 -MonitorNew"
    Write-Host "`nOptions:" -ForegroundColor Yellow
    Write-Host "  -TimeoutSeconds <N>   Stop monitoring after N seconds (default: 300)"
    Write-Host "  -DumpPath <path>      Custom dump directory"
    exit 0
}

Write-Host "`n=== Diagnostic Capture Complete ===" -ForegroundColor Green
Write-Host "Dump files saved to: $DumpPath" -ForegroundColor Cyan

# List captured dumps
$dumps = Get-ChildItem -Path $DumpPath -Filter "*.dmp" | Sort-Object LastWriteTime -Descending | Select-Object -First 5
if ($dumps) {
    Write-Host "`nRecent dumps:" -ForegroundColor Cyan
    $dumps | Format-Table Name, @{Label="Size (MB)";Expression={[math]::Round($_.Length/1MB, 2)}}, LastWriteTime -AutoSize
    
    Write-Host "`nAnalysis commands:" -ForegroundColor Yellow
    Write-Host "  WinDbg:  windbg -z `"$($dumps[0].FullName)`""
    Write-Host "  VS:      devenv /debugexe `"$($dumps[0].FullName)`""
}
