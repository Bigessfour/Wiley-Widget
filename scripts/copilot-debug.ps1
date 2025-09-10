# Copilot Debug Script for dotnet build
# Usage: pwsh ./scripts/copilot-debug.ps1

param(
    [string]$Command = "dotnet build"
)

Write-Host "[Copilot Debug] Running: $Command" -ForegroundColor Cyan

try {
    & $Command
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[Copilot Debug] Build succeeded." -ForegroundColor Green
    } else {
        Write-Host "[Copilot Debug] Build failed with exit code $LASTEXITCODE." -ForegroundColor Red
    }
} catch {
    Write-Host "[Copilot Debug] ERROR: $_" -ForegroundColor Red
    exit 1
}
