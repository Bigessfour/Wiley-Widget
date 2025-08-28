<#
.SYNOPSIS
    CI/CD integration script for generating fetchability manifest

.DESCRIPTION
    This script is designed to be called from CI/CD pipelines to generate
    a fetchability manifest before builds or deployments. It ensures all
    files are properly cataloged with SHA256 hashes for integrity verification.

.PARAMETER OutputPath
    Path where the manifest file will be created

.PARAMETER FailOnUntracked
    If specified, the script will fail if untracked files are found

.EXAMPLE
    .\ci-generate-manifest.ps1

.EXAMPLE
    .\ci-generate-manifest.ps1 -FailOnUntracked
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "fetchability-resources.json",

    [Parameter(Mandatory = $false)]
    [switch]$FailOnUntracked
)

# Ensure we're in the repository root
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

Write-Host "🤖 CI/CD: Generating fetchability manifest..." -ForegroundColor Cyan

try {
    # Generate the manifest
    & "$PSScriptRoot\Generate-FetchabilityManifest.ps1" -OutputPath $OutputPath

    # Read and validate the manifest
    $manifest = Get-Content $OutputPath | ConvertFrom-Json

    # Check for untracked files if requested
    if ($FailOnUntracked -and $manifest.metadata.statistics.untrackedFiles -gt 0) {
        Write-Error "❌ CI/CD: Found $($manifest.metadata.statistics.untrackedFiles) untracked files. Failing build."
        exit 1
    }

    # Output summary for CI/CD logs
    Write-Host "✅ CI/CD: Manifest generated successfully" -ForegroundColor Green
    Write-Host "📊 Summary:" -ForegroundColor Cyan
    Write-Host "   • Commit: $($manifest.metadata.repository.commitHash)" -ForegroundColor White
    Write-Host "   • Branch: $($manifest.metadata.repository.branch)" -ForegroundColor White
    Write-Host "   • Files: $($manifest.metadata.statistics.totalFiles)" -ForegroundColor White
    Write-Host "   • Size: $([math]::Round($manifest.metadata.statistics.totalSize / 1MB, 2)) MB" -ForegroundColor White

    # Set output variables for GitHub Actions
    if ($env:GITHUB_ACTIONS) {
        Write-Host "::set-output name=manifest-path::$OutputPath"
        Write-Host "::set-output name=file-count::$($manifest.metadata.statistics.totalFiles)"
        Write-Host "::set-output name=commit-hash::$($manifest.metadata.repository.commitHash)"
    }

} catch {
    Write-Error "❌ CI/CD: Failed to generate manifest: $($_.Exception.Message)"
    exit 1
}

Write-Host "🎉 CI/CD: Fetchability manifest ready for deployment!" -ForegroundColor Green
