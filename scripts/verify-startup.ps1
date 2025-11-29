param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [int]
    $TimeoutSeconds = 20
)

# Basic smoke test for a published WinForms exe. The runner may be non-interactive; this script
# attempts to start the process, verifies it is running for a short period, then stops it.

if (-not (Test-Path $ExePath)) {
    Write-Error "Executable not found at path: $ExePath"
    exit 2
}

Write-Host "Starting smoke test for: $ExePath (timeout: $TimeoutSeconds seconds)"

try {
    $proc = Start-Process -FilePath $ExePath -PassThru -ErrorAction Stop
} catch {
    Write-Warning "Failed to start process: $_"
    exit 4
}

$start = Get-Date
$running = $false

while ((Get-Date) - $start).TotalSeconds -lt $TimeoutSeconds {
    try {
        $proc.Refresh()
        if (-not $proc.HasExited) {
            $running = $true
            break
        } else {
            Write-Warning "Process started and exited quickly (ExitCode: $($proc.ExitCode))"
            break
        }
    } catch {
        Write-Warning "Process status check failed: $_"
        break
    }
    Start-Sleep -Seconds 1
}

if ($running) {
    Write-Host "Process is running (PID: $($proc.Id)). Attempting to stop it gracefully."
    try {
        Stop-Process -Id $proc.Id -Force -ErrorAction Stop
        Write-Host "Stopped process (PID: $($proc.Id)). Smoke test succeeded."
        exit 0
    } catch {
        Write-Warning "Failed to stop process: $_"
        exit 5
    }
} else {
    if ($proc -and $proc.HasExited) {
        Write-Warning "Process exited during startup; smoke test inconclusive."
        exit 3
    }
    Write-Warning "Process did not start within timeout. Smoke test failed."
    # Attempt to tidy up if a process exists
    try {
        if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    } catch { }
    exit 6
}
