#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs unit tests for Wiley Widget with the same configuration as CI/CD.

.DESCRIPTION
    Executes unit tests using dotnet test with coverage collection and TRX logging.
    Mimics the CI/CD environment for consistent local testing.

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Debug

.PARAMETER Filter
    Test filter expression (e.g., "FullyQualifiedName~QuickBooks")

.PARAMETER Verbose
    Show detailed test output

.PARAMETER NoCoverage
    Skip code coverage collection for faster runs

.EXAMPLE
    .\run-tests.ps1
    # Run all tests with coverage (Debug)

.EXAMPLE
    .\run-tests.ps1 -Configuration Release -Verbose
    # Run all tests in Release mode with detailed output

.EXAMPLE
    .\run-tests.ps1 -Filter "FullyQualifiedName~DashboardViewModel"
    # Run only DashboardViewModel tests

.EXAMPLE
    .\run-tests.ps1 -NoCoverage
    # Run tests without coverage for faster execution

.NOTES
    Requires: PowerShell 7.5+, .NET 9.0 SDK
#>

#Requires -Version 7.5

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [Parameter()]
    [string]$Filter,

    [Parameter()]
    [switch]$Verbose,

    [Parameter()]
    [switch]$NoCoverage
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

# Initialize colors
$script:ColorGreen = $PSStyle.Foreground.Green
$script:ColorRed = $PSStyle.Foreground.Red
$script:ColorYellow = $PSStyle.Foreground.Yellow
$script:ColorCyan = $PSStyle.Foreground.Cyan
$script:ColorReset = $PSStyle.Reset

function Write-Section {
    param([string]$Message)
    Write-Information "${script:ColorCyan}▶ $Message${script:ColorReset}"
}

function Write-Success {
    param([string]$Message)
    Write-Information "${script:ColorGreen}✓ $Message${script:ColorReset}"
}

function Write-Failure {
    param([string]$Message)
    Write-Information "${script:ColorRed}✗ $Message${script:ColorReset}"
}

function Write-Warning2 {
    param([string]$Message)
    Write-Information "${script:ColorYellow}⚠ $Message${script:ColorReset}"
}

try {
    # Get solution root
    $solutionRoot = Split-Path -Parent $PSScriptRoot
    $testResultsDir = Join-Path $solutionRoot "TestResults"
    $runSettingsFile = Join-Path $solutionRoot "tests\.runsettings"

    Write-Section "Wiley Widget Test Runner"
    Write-Information "Configuration: $Configuration"
    Write-Information "Coverage: $(if ($NoCoverage) { 'Disabled' } else { 'Enabled' })"
    if ($Filter) {
        Write-Information "Filter: $Filter"
    }
    Write-Information ""

    # Verify solution exists
    $solutionFile = Join-Path $solutionRoot "WileyWidget.sln"
    if (-not (Test-Path $solutionFile)) {
        throw "Solution file not found: $solutionFile"
    }

    # Clean previous test results
    if (Test-Path $testResultsDir) {
        Write-Section "Cleaning previous test results"
        Remove-Item $testResultsDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Success "Test results directory cleaned"
    }

    # Build test arguments
    $testArgs = @(
        'test'
        $solutionFile
        '--no-restore'
        '--configuration', $Configuration
        '--logger', 'trx;LogFileName=test-results.trx'
        '--logger', 'console;verbosity=normal'
        '--results-directory', $testResultsDir
    )

    if ($Verbose) {
        $testArgs += '--verbosity', 'detailed'
    } else {
        $testArgs += '--verbosity', 'normal'
    }

    if (-not $NoCoverage) {
        $testArgs += '--collect:"XPlat Code Coverage"'
    }

    if (Test-Path $runSettingsFile) {
        $testArgs += '--settings', $runSettingsFile
        Write-Information "Using run settings: $runSettingsFile"
    } else {
        Write-Warning2 "Run settings file not found: $runSettingsFile"
    }

    if ($Filter) {
        $testArgs += '--filter', $Filter
    }

    Write-Section "Running tests"
    Write-Information ""

    # Run tests
    $testStartTime = Get-Date
    & dotnet @testArgs

    $testExitCode = $LASTEXITCODE
    $testEndTime = Get-Date
    $testDuration = $testEndTime - $testStartTime

    Write-Information ""
    Write-Section "Test Results"

    if ($testExitCode -eq 0) {
        Write-Success "All tests passed! ($($testDuration.TotalSeconds.ToString('F2'))s)"
    } else {
        Write-Failure "Tests failed with exit code: $testExitCode ($($testDuration.TotalSeconds.ToString('F2'))s)"
    }

    # Display coverage report location if collected
    if (-not $NoCoverage) {
        $coverageFiles = Get-ChildItem -Path $testResultsDir -Filter "coverage.cobertura.xml" -Recurse -ErrorAction SilentlyContinue
        if ($coverageFiles) {
            Write-Information ""
            Write-Section "Coverage Reports"
            foreach ($file in $coverageFiles) {
                Write-Information "  $($file.FullName)"
            }

            Write-Information ""
            Write-Information "${script:ColorCyan}Tip: Install 'dotnet tool install -g dotnet-reportgenerator-globaltool' for HTML coverage reports${script:ColorReset}"
            Write-Information "${script:ColorCyan}Then run: reportgenerator -reports:$($coverageFiles[0].FullName) -targetdir:$testResultsDir\CoverageReport${script:ColorReset}"
        }
    }

    # Display test results file location
    $trxFiles = Get-ChildItem -Path $testResultsDir -Filter "*.trx" -Recurse -ErrorAction SilentlyContinue
    if ($trxFiles) {
        Write-Information ""
        Write-Section "Test Result Files"
        foreach ($file in $trxFiles) {
            Write-Information "  $($file.FullName)"
        }
    }

    Write-Information ""
    exit $testExitCode
}
catch {
    Write-Failure "Error running tests: $_"
    Write-Error $_.Exception.ToString()
    exit 1
}
