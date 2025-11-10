# Diagnose Startup Crash
# Purpose: Run the application and capture any crash information

Write-Host "=== Wiley Widget Startup Crash Diagnostic ===" -ForegroundColor Cyan
Write-Host ""

# Clear old logs
Write-Host "Clearing old test logs..." -ForegroundColor Yellow
Remove-Item "$PSScriptRoot\..\..\logs\wiley-widget-*.log" -ErrorAction SilentlyContinue

# Run with error capturing
Write-Host "Starting application with error capture..." -ForegroundColor Yellow
Write-Host ""

try {
    $process = Start-Process -FilePath "dotnet" `
        -ArgumentList "run --project $PSScriptRoot\..\..\src\WileyWidget\WileyWidget.csproj --no-build" `
        -WorkingDirectory "$PSScriptRoot\..\.." `
        -RedirectStandardError "$PSScriptRoot\..\..\logs\startup-error.log" `
        -RedirectStandardOutput "$PSScriptRoot\..\..\logs\startup-output.log" `
        -PassThru `
        -NoNewWindow

    Write-Host "Process started (PID: $($process.Id)). Waiting 10 seconds..." -ForegroundColor Green
    Start-Sleep -Seconds 10

    if (!$process.HasExited) {
        Write-Host "✓ Application is still running after 10 seconds" -ForegroundColor Green
        Write-Host "  Stopping process..." -ForegroundColor Yellow
        Stop-Process -Id $process.Id -Force
    } else {
        Write-Host "❌ Application exited with code: $($process.ExitCode)" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ ERROR: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Checking Logs ===" -ForegroundColor Cyan

# Check error log
if (Test-Path "$PSScriptRoot\..\..\logs\startup-error.log") {
    $errorContent = Get-Content "$PSScriptRoot\..\..\logs\startup-error.log" -Raw
    if ($errorContent) {
        Write-Host "STDERR Output:" -ForegroundColor Red
        Write-Host $errorContent
    }
}

# Check output log
if (Test-Path "$PSScriptRoot\..\..\logs\startup-output.log") {
    $outputContent = Get-Content "$PSScriptRoot\..\..\logs\startup-output.log" -Raw
    if ($outputContent) {
        Write-Host "STDOUT Output:" -ForegroundColor Yellow
        Write-Host $outputContent
    }
}

# Check application log
$latestLog = Get-ChildItem "$PSScriptRoot\..\..\logs\wiley-widget-*.log" |
Sort-Object LastWriteTime -Descending |
Select-Object -First 1

if ($latestLog) {
    Write-Host ""
    Write-Host "Latest Application Log ($($latestLog.Name)):" -ForegroundColor Cyan
    Get-Content $latestLog.FullName
}

Write-Host ""
Write-Host "=== Diagnostic Complete ===" -ForegroundColor Cyan
