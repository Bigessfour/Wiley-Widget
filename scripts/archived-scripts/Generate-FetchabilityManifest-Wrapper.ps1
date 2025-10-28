# Generate-FetchabilityManifest.ps1
# PowerShell wrapper for automated fetchability manifest generation
# This script can be called from Git hooks, CI/CD pipelines, or manually

param(
    [string]$OutputPath = "fetchability-resources.json",
    [switch]$Force,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Get the script directory and project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

# Change to project root
Push-Location $ProjectRoot

try {
    if ($Verbose) {
        Write-Output "Generating fetchability manifest..."
        Write-Output "Project root: $ProjectRoot"
        Write-Output "Output path: $OutputPath"
    }

    # Check if manifest exists and is recent (unless Force is specified)
    if (-not $Force -and (Test-Path $OutputPath)) {
        $ManifestAge = (Get-Date) - (Get-Item $OutputPath).LastWriteTime
        if ($ManifestAge.TotalMinutes -lt 5) {
            if ($Verbose) {
                Write-Output "Manifest is recent ($($ManifestAge.TotalMinutes.ToString("F1")) minutes old), skipping generation"
            }
            return
        }
    }

    # Run the manifest generation
    $process = Start-Process -FilePath "pwsh" -ArgumentList "-ExecutionPolicy Bypass -File scripts/Generate-FetchabilityManifest.ps1" -NoNewWindow -Wait -PassThru

    if ($process.ExitCode -eq 0) {
        if ($Verbose) {
            Write-Output "Manifest generated successfully"
        }
    }
    else {
        throw "Manifest generation failed with exit code $($process.ExitCode)"
    }

}
catch {
    Write-Error "Failed to generate fetchability manifest: $($_.Exception.Message)"
    exit 1
}
finally {
    Pop-Location
}
