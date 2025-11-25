<#
.SYNOPSIS
  Verifies that the required .NET 10 SDK and MSBuild are available.

.DESCRIPTION
  Checks installed .NET SDKs for a 10.0 SDK (e.g. 10.0.100) and verifies
  that MSBuild's major version is >= 18 (required to target net10.0).
  Exits with a non-zero code if requirements are not met and prints install
  instructions.

.NOTES
  Run from the repo root: `pwsh -File .\scripts\maintenance\verify-dotnet-sdk.ps1`
#>

param (
    [string] $RequiredSdkPrefix = '10.0',
    [int] $RequiredMsbuildMajor = 18
)

function Write-FailureAndExit {
    param([string] $msg)
    Write-Host "ERROR: $msg" -ForegroundColor Red
    exit 1
}

try {
    $sdksRaw = & dotnet --list-sdks 2>&1
} catch {
    Write-FailureAndExit 'Unable to run `dotnet`. Ensure the .NET SDK is installed and `dotnet` is on PATH.'
}

if ($LASTEXITCODE -ne 0 -or -not $sdksRaw) {
    Write-FailureAndExit 'No .NET SDKs detected. Install .NET 10 SDK: https://dotnet.microsoft.com/en-us/download/dotnet/10.0'
}

$sdks = $sdksRaw -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }

$escapedPrefix = [regex]::Escape($RequiredSdkPrefix)
$found10 = $sdks | Where-Object { $_ -match "^$escapedPrefix" }

if (-not $found10) {
    Write-Host "Installed SDKs:`n$($sdks -join "`n")`n" -ForegroundColor Yellow
    Write-FailureAndExit "No installed SDK matching prefix '$RequiredSdkPrefix' found. Install .NET 10 SDK (example: 10.0.100). See https://dotnet.microsoft.com/en-us/download/dotnet/10.0"
}

Write-Host "Found .NET SDK(s) matching '$RequiredSdkPrefix':" -ForegroundColor Green
$found10 | ForEach-Object { Write-Host "  $_" }

# Check MSBuild version reported by `dotnet --info`
$info = & dotnet --info 2>&1
if ($LASTEXITCODE -ne 0 -or -not $info) {
    Write-FailureAndExit 'Unable to run `dotnet --info` to determine MSBuild version.'
}

$msbuildLine = $info | Select-String -Pattern 'MSBuild version' -SimpleMatch | Select-Object -First 1
if (-not $msbuildLine) {
    Write-Host "Full 'dotnet --info' output:`n$info`n" -ForegroundColor Yellow
    Write-FailureAndExit 'Could not determine MSBuild version from `dotnet --info` output.'
}

$msbuildVersionText = ($msbuildLine -split ':')[1].Trim()
# msbuildVersionText typically looks like '18.0.0+...'
$msbuildMajor = 0
if ($msbuildVersionText -match '^([0-9]+)') { $msbuildMajor = [int]$matches[1] }

if ($msbuildMajor -lt $RequiredMsbuildMajor) {
    Write-Host "MSBuild version detected: $msbuildVersionText" -ForegroundColor Yellow
    Write-FailureAndExit "MSBuild major version is $msbuildMajor — requires >= $RequiredMsbuildMajor (MSBuild from .NET 10 SDK). Install .NET 10 SDK: https://dotnet.microsoft.com/en-us/download/dotnet/10.0"
}

Write-Host "MSBuild version detected: $msbuildVersionText" -ForegroundColor Green
Write-Host 'All checks passed: .NET 10 SDK and MSBuild >= 18 are available.' -ForegroundColor Green
exit 0
