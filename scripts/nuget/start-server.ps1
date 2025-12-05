<#
.SYNOPSIS
    Starts the Wiley Widget private NuGet server (BaGet) in Docker.

.DESCRIPTION
    Launches the BaGet NuGet server container for hosting Syncfusion patches
    and custom packages. The server runs on http://localhost:5000.

.PARAMETER Detached
    Run container in background (default: true)

.PARAMETER Force
    Force recreate container even if running

.PARAMETER Stop
    Stop the running NuGet server

.EXAMPLE
    .\start-server.ps1
    Starts the NuGet server in background

.EXAMPLE
    .\start-server.ps1 -Stop
    Stops the running NuGet server

.NOTES
    Requires Docker Desktop to be running.
    API key must be set in NUGET_API_KEY environment variable.
#>

[CmdletBinding()]
param(
    [switch]$Detached = $true,
    [switch]$Force,
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
Push-Location $repoRoot

try {
    # Check Docker is running
    $dockerInfo = docker info 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Docker is not running. Please start Docker Desktop."
        exit 1
    }

    if ($Stop) {
        Write-Host "Stopping NuGet server..." -ForegroundColor Yellow
        docker-compose stop nuget-server
        Write-Host "NuGet server stopped." -ForegroundColor Green
        exit 0
    }

    # Validate API key is set
    if (-not $env:NUGET_API_KEY) {
        Write-Warning "NUGET_API_KEY environment variable is not set."
        Write-Host @"

To set the API key:
  `$env:NUGET_API_KEY = "your-secure-key-here"

Or generate a new one:
  `$env:NUGET_API_KEY = [System.Guid]::NewGuid().ToString("N")

See secrets/nuget_api_key.example for more details.
"@ -ForegroundColor Cyan
        
        $response = Read-Host "Generate a temporary API key for this session? (y/N)"
        if ($response -eq 'y') {
            $env:NUGET_API_KEY = [System.Guid]::NewGuid().ToString("N")
            Write-Host "Generated temporary API key: $($env:NUGET_API_KEY)" -ForegroundColor Green
            Write-Warning "This key is only valid for this session. Set it permanently for production use."
        } else {
            exit 1
        }
    }

    Write-Host "Starting NuGet server..." -ForegroundColor Cyan
    
    $composeArgs = @("up")
    if ($Detached) { $composeArgs += "-d" }
    if ($Force) { $composeArgs += "--force-recreate" }
    $composeArgs += "nuget-server"

    docker-compose @composeArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "`nNuGet server is running!" -ForegroundColor Green
        Write-Host "  Browse packages: http://localhost:5000/" -ForegroundColor Cyan
        Write-Host "  API endpoint:    http://localhost:5000/v3/index.json" -ForegroundColor Cyan
        Write-Host "`nTo push a package:" -ForegroundColor Yellow
        Write-Host "  .\scripts\nuget\push-package.ps1 -PackagePath `".\package.nupkg`"" -ForegroundColor White
    }
}
finally {
    Pop-Location
}
