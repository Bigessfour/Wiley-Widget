#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Wrapper script to run the startup validator with proper Python environment.

.DESCRIPTION
    Automatically detects and uses the correct Python installation to run the
    startup_validator.py script. Handles various Python installation locations
    on Windows including WindowsApps, standard installations, and Anaconda.

.PARAMETER ScanTypes
    Comma-separated list of scan types: licenses,assemblies,resources,controls,prism,deprecated,configuration,shell,services,viewmodels,database
    Use 'all' to run all scans (default).

.PARAMETER Path
    Root path to scan (defaults to current directory).

.PARAMETER Output
    Output JSON file path (defaults to logs/startup_validation_report.json).

.PARAMETER VerboseOutput
    Enable verbose output from the validator.

.EXAMPLE
    .\run-startup-validator.ps1

.EXAMPLE
    .\run-startup-validator.ps1 -ScanTypes "licenses,assemblies" -VerboseOutput

.EXAMPLE
    .\run-startup-validator.ps1 -Output "logs/custom_report.json"
#>

[CmdletBinding()]
param(
    [string]$ScanTypes = "all",
    [string]$Path = ".",
    [string]$Output = "logs/startup_validation_report.json",
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"

# Function to find Python executable
function Find-PythonExecutable {
    Write-Verbose "Searching for Python installation..."

    # Check common Python locations on Windows
    $pythonCandidates = @(
        # WindowsApps Python (Microsoft Store)
        "$env:USERPROFILE\AppData\Local\Microsoft\WindowsApps\python3.11.exe",
        "$env:USERPROFILE\AppData\Local\Microsoft\WindowsApps\python3.exe",
        "$env:USERPROFILE\AppData\Local\Microsoft\WindowsApps\python.exe",
        # Standard installations
        "C:\Python311\python.exe",
        "C:\Python310\python.exe",
        "C:\Python39\python.exe",
        # Anaconda
        "$env:USERPROFILE\anaconda3\python.exe",
        "$env:USERPROFILE\miniconda3\python.exe"
    )

    # Also check PATH
    $pathPython = Get-Command python, python3, py -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($pathPython) {
        $pythonCandidates += $pathPython.Source
    }

    foreach ($candidate in $pythonCandidates) {
        if ($candidate -and (Test-Path $candidate)) {
            try {
                $version = & $candidate --version 2>&1
                if ($LASTEXITCODE -eq 0 -and $version -match 'Python') {
                    Write-Verbose "✓ Found Python: $candidate ($version)"
                    return $candidate
                }
            } catch {
                continue
            }
        }
    }

    return $null
}

# Find Python
$pythonExe = Find-PythonExecutable

if (-not $pythonExe) {
    Write-Error @"
❌ Python not found!

Please install Python 3.9+ from:
- Microsoft Store (recommended): https://www.microsoft.com/store/productId/9PJPW5LDXLZ5
- Python.org: https://www.python.org/downloads/

Or install via winget:
    winget install Python.Python.3.11
"@
    exit 1
}

Write-Host "🐍 Using Python: $pythonExe" -ForegroundColor Cyan

# Build command arguments
$scriptPath = Join-Path $PSScriptRoot "startup_validator.py"

if (-not (Test-Path $scriptPath)) {
    Write-Error "startup_validator.py not found at: $scriptPath"
    exit 1
}

$arguments = @(
    $scriptPath,
    "--scan", $ScanTypes,
    "--path", $Path,
    "--output", $Output
)

if ($VerboseOutput) {
    $arguments += "--verbose"
}

# Run the validator
Write-Host "🔍 Running Wiley Widget Startup Validator..." -ForegroundColor Green
Write-Host "   Scan Types: $ScanTypes" -ForegroundColor Gray
Write-Host "   Output: $Output" -ForegroundColor Gray
Write-Host ""

try {
    & $pythonExe @arguments
    $exitCode = $LASTEXITCODE

    if ($exitCode -eq 0) {
        Write-Host "`n✓ Validation complete!" -ForegroundColor Green
        Write-Host "Report saved to: $Output" -ForegroundColor Cyan
    } else {
        Write-Warning "Validator exited with code: $exitCode"
    }

    exit $exitCode
} catch {
    Write-Error "Failed to run validator: $_"
    exit 1
}
