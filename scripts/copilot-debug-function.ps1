function copilot-debug {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Command
    )

    Write-Host "[Copilot Debug] Running: $Command" -ForegroundColor Cyan

    try {
        Invoke-Expression $Command
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[Copilot Debug] Command succeeded." -ForegroundColor Green
        } else {
            Write-Host "[Copilot Debug] Command failed with exit code $LASTEXITCODE." -ForegroundColor Red
        }
    } catch {
        Write-Host "[Copilot Debug] ERROR: $_" -ForegroundColor Red
        exit 1
    }
}
