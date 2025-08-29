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

Write-Information "🤖 CI/CD: Generating fetchability manifest..." -InformationAction Continue

try {
    # Generate the manifest using the wrapper script
    & pwsh -ExecutionPolicy Bypass -File "$PSScriptRoot\Generate-FetchabilityManifest-Wrapper.ps1" -OutputPath $OutputPath -Verbose

    # Read and validate the manifest
    $manifest = Get-Content $OutputPath | ConvertFrom-Json

    # Check for untracked files if requested
    if ($FailOnUntracked -and $manifest.metadata.statistics.untrackedFiles -gt 0) {
        Write-Error "❌ CI/CD: Found $($manifest.metadata.statistics.untrackedFiles) untracked files. Failing build."
        exit 1
    }

    # Output summary for CI/CD logs
    Write-Information "✅ CI/CD: Manifest generated successfully" -InformationAction Continue
    Write-Information "📊 Summary:" -InformationAction Continue
    Write-Information "   • Commit: $($manifest.metadata.repository.commitHash)" -InformationAction Continue
    Write-Information "   • Branch: $($manifest.metadata.repository.branch)" -InformationAction Continue
    Write-Information "   • Files: $($manifest.metadata.statistics.totalFiles)" -InformationAction Continue
    Write-Information "   • Size: $([math]::Round($manifest.metadata.statistics.totalSize / 1MB, 2)) MB" -InformationAction Continue

    # Set output variables for GitHub Actions
    if ($env:GITHUB_ACTIONS) {
        Write-Information "::set-output name=manifest-path::$OutputPath" -InformationAction Continue
        Write-Information "::set-output name=file-count::$($manifest.metadata.statistics.totalFiles)" -InformationAction Continue
        Write-Information "::set-output name=commit-hash::$($manifest.metadata.repository.commitHash)" -InformationAction Continue
    }

}
catch {
    Write-Error "❌ CI/CD: Failed to generate manifest: $($_.Exception.Message)"
    exit 1
}

Write-Information "🎉 CI/CD: Fetchability manifest ready for deployment!" -InformationAction Continue
