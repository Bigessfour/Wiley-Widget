<#
.SYNOPSIS
    Verify WileyWidget.WinForms application startup (smoke test)

.DESCRIPTION
    Launches the application, waits for the main window to appear, then closes it.
    Used in CI to verify the application can start without crashing.

.PARAMETER ExePath
    Path to WileyWidget.WinForms.exe

.PARAMETER TimeoutSeconds
    Maximum time to wait for startup (default: 20 seconds)

.EXAMPLE
    .\scripts\verify-startup.ps1 -ExePath ./publish/WileyWidget.WinForms.exe -TimeoutSeconds 20
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [Parameter(Mandatory = $false)]
    [int]$TimeoutSeconds = 20
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ExePath)) {
    Write-Error "Executable not found: $ExePath"
    exit 1
}

Write-Host "Starting application: $ExePath"
Write-Host "Timeout: $TimeoutSeconds seconds"

try {
    # Start the process without waiting
    $process = Start-Process -FilePath $ExePath -PassThru -WindowStyle Normal

    # Wait for main window to appear
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $windowFound = $false

    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        Start-Sleep -Milliseconds 500

        # Refresh process info
        if ($process.HasExited) {
            Write-Error "Application exited prematurely with exit code: $($process.ExitCode)"
            exit 1
        }

        # Check if main window handle exists
        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            $windowFound = $true
            Write-Host "✓ Main window appeared after $([math]::Round($stopwatch.Elapsed.TotalSeconds, 1))s"
            break
        }
    }

    if (-not $windowFound) {
        Write-Warning "Main window did not appear within ${TimeoutSeconds}s (process still running)"
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        exit 1
    }

    # Give it a moment to fully initialize
    Start-Sleep -Milliseconds 1000

    # Close the application gracefully
    Write-Host "Closing application..."
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 2000

        # Force kill if still running
        if (-not $process.HasExited) {
            Write-Host "Force stopping process..."
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Host "✓ Startup verification successful"
    exit 0
}
catch {
    Write-Error "Startup verification failed: $_"
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    exit 1
}
