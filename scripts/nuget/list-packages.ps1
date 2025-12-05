<#
.SYNOPSIS
    Lists packages available on the Wiley Widget private NuGet feed.

.DESCRIPTION
    Queries the BaGet server for available packages and displays them
    in a formatted table. Can filter by package name pattern.

.PARAMETER Filter
    Optional filter pattern for package names (supports wildcards)

.PARAMETER Source
    NuGet source URL (default: http://localhost:5000/v3/index.json)

.PARAMETER IncludeVersions
    Show all available versions, not just the latest

.EXAMPLE
    .\list-packages.ps1
    Lists all packages

.EXAMPLE
    .\list-packages.ps1 -Filter "Syncfusion*"
    Lists only Syncfusion packages

.EXAMPLE
    .\list-packages.ps1 -IncludeVersions
    Lists all packages with all available versions
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Filter = "*",

    [Parameter()]
    [string]$Source = "http://localhost:5000/v3/index.json",

    [switch]$IncludeVersions
)

$ErrorActionPreference = 'Stop'

# Check if server is reachable
try {
    $null = Invoke-WebRequest -Uri "http://localhost:5000/" -Method Head -TimeoutSec 3
}
catch {
    Write-Error @"
NuGet server is not reachable at http://localhost:5000/

Start it with:
  .\scripts\nuget\start-server.ps1

Or:
  docker-compose up -d nuget-server
"@
    exit 1
}

Write-Host "Querying packages from: $Source" -ForegroundColor Cyan
Write-Host ""

# Use dotnet nuget to list package sources and verify our source exists
$searchArgs = @("package", "search", $Filter, "--source", $Source)

if ($IncludeVersions) {
    $searchArgs += "--include-prerelease"
}

try {
    # Note: dotnet package search requires .NET 8+ SDK
    $output = & dotnet @searchArgs 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        # Parse and display output
        $output | ForEach-Object { Write-Host $_ }
    } else {
        # Fallback: Query the BaGet API directly
        Write-Host "Using BaGet API directly..." -ForegroundColor Yellow
        
        $searchUrl = "http://localhost:5000/v3/search?q=$Filter&take=100"
        $response = Invoke-RestMethod -Uri $searchUrl -Method Get
        
        if ($response.data.Count -eq 0) {
            Write-Host "No packages found matching: $Filter" -ForegroundColor Yellow
        } else {
            Write-Host "Found $($response.totalHits) package(s):" -ForegroundColor Green
            Write-Host ""
            
            foreach ($pkg in $response.data) {
                $versions = $pkg.versions | ForEach-Object { $_.version }
                $latestVersion = $versions | Select-Object -Last 1
                
                Write-Host "  $($pkg.id)" -ForegroundColor White -NoNewline
                Write-Host " @ $latestVersion" -ForegroundColor Cyan
                
                if ($IncludeVersions -and $versions.Count -gt 1) {
                    Write-Host "    All versions: $($versions -join ', ')" -ForegroundColor Gray
                }
                
                if ($pkg.description) {
                    Write-Host "    $($pkg.description)" -ForegroundColor DarkGray
                }
            }
        }
    }
}
catch {
    Write-Error "Failed to query packages: $($_.Exception.Message)"
    exit 1
}
