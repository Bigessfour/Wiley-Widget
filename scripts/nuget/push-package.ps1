<#
.SYNOPSIS
    Pushes a NuGet package to the Wiley Widget private feed.

.DESCRIPTION
    Pushes a .nupkg file to the local BaGet server using the configured API key.
    Supports pushing Syncfusion patches or custom packages.

.PARAMETER PackagePath
    Path to the .nupkg file to push (required)

.PARAMETER Source
    NuGet source URL (default: http://localhost:5000/v3/index.json)

.PARAMETER ApiKey
    API key for authentication (default: from NUGET_API_KEY env var)

.PARAMETER SkipDuplicate
    Skip if package version already exists

.EXAMPLE
    .\push-package.ps1 -PackagePath ".\Syncfusion.SfChart.WinForms.31.2.17.nupkg"

.EXAMPLE
    .\push-package.ps1 -PackagePath ".\packages\local\syncfusion-patches\*.nupkg"
    Pushes all packages in the patches directory

.NOTES
    Requires the NuGet server to be running (use start-server.ps1)
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$PackagePath,

    [Parameter()]
    [string]$Source = "http://localhost:5000/v3/index.json",

    [Parameter()]
    [string]$ApiKey = $env:NUGET_API_KEY,

    [switch]$SkipDuplicate
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Validate API key
if (-not $ApiKey) {
    Write-Error @"
NUGET_API_KEY is not set. 

To set it:
  `$env:NUGET_API_KEY = "your-api-key"

Or pass it directly:
  .\push-package.ps1 -PackagePath ".\package.nupkg" -ApiKey "your-key"
"@
    exit 1
}

# Resolve package paths (supports wildcards)
$packages = Get-Item -Path $PackagePath -ErrorAction SilentlyContinue
if (-not $packages) {
    Write-Error "No packages found matching: $PackagePath"
    exit 1
}

$successCount = 0
$failCount = 0

foreach ($package in $packages) {
    Write-Host "Pushing: $($package.Name)" -ForegroundColor Cyan
    
    $pushArgs = @(
        "nuget", "push",
        $package.FullName,
        "--source", $Source,
        "--api-key", $ApiKey
    )
    
    if ($SkipDuplicate) {
        $pushArgs += "--skip-duplicate"
    }

    try {
        $output = & dotnet @pushArgs 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ Successfully pushed: $($package.Name)" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "  ✗ Failed to push: $($package.Name)" -ForegroundColor Red
            Write-Host "    $output" -ForegroundColor Yellow
            $failCount++
        }
    }
    catch {
        Write-Host "  ✗ Error pushing: $($package.Name)" -ForegroundColor Red
        Write-Host "    $($_.Exception.Message)" -ForegroundColor Yellow
        $failCount++
    }
}

Write-Host "`n--- Summary ---" -ForegroundColor Cyan
Write-Host "Pushed: $successCount" -ForegroundColor Green
if ($failCount -gt 0) {
    Write-Host "Failed: $failCount" -ForegroundColor Red
    exit 1
}
