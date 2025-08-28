# Wiley Widget Profile Loader
# PowerShell 7.5.2 Compatible
# Use this to load the Wiley Widget development profile

<#
.SYNOPSIS
    Loads the Wiley Widget PowerShell development profile.

.DESCRIPTION
    This script loads the Wiley Widget development environment profile,
    providing convenient functions and aliases for project development.

.EXAMPLE
    # Load the profile
    . .\scripts\Load-WileyWidgetProfile.ps1

    # Or from project root
    . .\Load-WileyWidgetProfile.ps1
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Force
)

$profilePath = Join-Path $PSScriptRoot 'WileyWidget.Profile.ps1'

if (-not (Test-Path $profilePath)) {
    # Try relative to parent directory (if running from scripts folder)
    $profilePath = Join-Path (Split-Path $PSScriptRoot -Parent) 'WileyWidget.Profile.ps1'
}

if (-not (Test-Path $profilePath)) {
    Write-Error "Wiley Widget profile not found at: $profilePath"
    return
}

Write-Verbose "Loading Wiley Widget development profile..."

try {
    . $profilePath
    Write-Verbose "Wiley Widget profile loaded successfully!"
    Write-Verbose "Available commands:"
    Write-Verbose "  ww-build    - Build the project"
    Write-Verbose "  ww-test     - Run tests"
    Write-Verbose "  ww-run      - Start the application"
    Write-Verbose "  ww-info     - Show project information"
    Write-Verbose "  ww-license  - Check Syncfusion license"
    Write-Verbose "  ww-env      - Setup development environment"
}
catch {
    Write-Error "Failed to load Wiley Widget profile: $_"
}
