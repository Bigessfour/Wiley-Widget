#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run RegionViewRegistry xUnit tests in Docker isolation
.DESCRIPTION
    Builds and runs the RegionViewRegistry tests in an isolated Docker container.
    Supports multiple execution modes: standard, with coverage, watch mode, and dev mode.
.PARAMETER Mode
    Execution mode: Standard, Coverage, Watch, or Dev
.PARAMETER Rebuild
    Force rebuild of Docker image
.PARAMETER ShowLogs
    Display container logs after test execution
.EXAMPLE
    .\run-regionviewregistry-tests.ps1
    .\run-regionviewregistry-tests.ps1 -Mode Coverage
    .\run-regionviewregistry-tests.ps1 -Mode Dev -Rebuild
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Standard', 'Coverage', 'Watch', 'Dev')]
    [string]$Mode = 'Standard',

    [Parameter()]
    [switch]$Rebuild,

    [Parameter()]
    [switch]$ShowLogs
)

$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

Write-Information "=== RegionViewRegistry Test Runner ==="
Write-Information "Mode: $Mode"
Write-Information "Rebuild: $Rebuild"
Write-Information ""

# Ensure test results directory exists
$testResultsDir = Join-Path $PSScriptRoot "TestResults\RegionViewRegistry"
if (-not (Test-Path $testResultsDir)) {
    New-Item -Path $testResultsDir -ItemType Directory -Force | Out-Null
    Write-Information "Created test results directory: $testResultsDir"
}

try {
    # Clean previous test results if in standard or coverage mode
    if ($Mode -in 'Standard', 'Coverage') {
        Write-Information "Cleaning previous test results..."
        Get-ChildItem -Path $testResultsDir -Recurse | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
    }

    # Build arguments
    $dockerComposeFile = "docker-compose.regionviewregistry-tests.yml"
    $buildArgs = @('compose', '-f', $dockerComposeFile)

    if ($Rebuild) {
        Write-Information "Rebuilding Docker image..."
        $buildCommand = $buildArgs + @('build', '--no-cache')

        if ($Mode -eq 'Dev') {
            $buildCommand += 'regionviewregistry-tests-dev'
        }
        else {
            $buildCommand += 'regionviewregistry-tests'
        }

        & docker @buildCommand

        if ($LASTEXITCODE -ne 0) {
            throw "Docker build failed with exit code $LASTEXITCODE"
        }
    }

    # Run tests based on mode
    Write-Information "Running RegionViewRegistry tests in $Mode mode..."

    switch ($Mode) {
        'Standard' {
            $runCommand = $buildArgs + @('run', '--rm', 'regionviewregistry-tests')
            & docker @runCommand
        }

        'Coverage' {
            $runCommand = $buildArgs + @('run', '--rm', 'regionviewregistry-tests')
            & docker @runCommand

            # Check for coverage results
            $coverageFiles = Get-ChildItem -Path $testResultsDir -Filter "coverage.cobertura.xml" -Recurse
            if ($coverageFiles) {
                Write-Information "`nCoverage report generated: $($coverageFiles[0].FullName)"
            }
        }

        'Watch' {
            Write-Information "Starting watch mode... (Press Ctrl+C to stop)"
            $runCommand = $buildArgs + @('run', '--rm', 'regionviewregistry-tests-dev')

            # Run in loop for watch mode
            while ($true) {
                & docker @runCommand
                Start-Sleep -Seconds 2
                Write-Information "`nWaiting for changes... (Press Ctrl+C to stop)"
                Start-Sleep -Seconds 3
            }
        }

        'Dev' {
            Write-Information "Starting dev mode with live mounting..."
            $runCommand = $buildArgs + @('run', '--rm', '-it', 'regionviewregistry-tests-dev')
            & docker @runCommand
        }
    }

    $exitCode = $LASTEXITCODE

    # Show logs if requested
    if ($ShowLogs) {
        Write-Information "`n=== Container Logs ==="
        $logsCommand = $buildArgs + @('logs')
        & docker @logsCommand
    }

    # Display test results summary
    Write-Information "`n=== Test Results Summary ==="

    $testResultFiles = Get-ChildItem -Path $testResultsDir -Filter "*.trx" -Recurse
    if ($testResultFiles) {
        Write-Information "Test result files: $($testResultFiles.Count)"
        foreach ($file in $testResultFiles) {
            Write-Information "  - $($file.Name)"
        }
    }
    else {
        Write-Information "No .trx files found. Check console output above."
    }

    # Check for coverage
    if ($Mode -eq 'Coverage') {
        $coverageFiles = Get-ChildItem -Path $testResultsDir -Filter "*.xml" -Recurse
        if ($coverageFiles) {
            Write-Information "`nCoverage files: $($coverageFiles.Count)"
            foreach ($file in $coverageFiles) {
                Write-Information "  - $($file.Name)"
            }
        }
    }

    if ($exitCode -eq 0) {
        Write-Information "`n✓ All RegionViewRegistry tests passed!" -ForegroundColor Green
    }
    else {
        Write-Warning "`n✗ Some tests failed. Exit code: $exitCode"
    }

    exit $exitCode

}
catch {
    Write-Error "Test execution failed: $_"
    exit 1
}
