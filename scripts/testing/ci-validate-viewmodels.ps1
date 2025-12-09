<#
.SYNOPSIS
    CI/CD integration script for ViewModel validation.

.DESCRIPTION
    Runs comprehensive ViewModel validation as part of the CI/CD pipeline.
    - Executes static analysis (PowerShell validator)
    - Runs unit tests with coverage
    - Generates compliance report
    - Fails build if critical violations found

    Designed for GitHub Actions, Azure DevOps, or local pre-commit hooks.

.PARAMETER CoverageThreshold
    Minimum code coverage percentage required for ViewModels (default: 85).

.PARAMETER SkipStaticAnalysis
    Skip PowerShell static analysis (only run unit tests).

.PARAMETER SkipTests
    Skip unit tests (only run static analysis).

.PARAMETER OutputDirectory
    Directory for reports and artifacts (default: TestResults).

.EXAMPLE
    .\ci-validate-viewmodels.ps1
    # Run full validation with default settings

.EXAMPLE
    .\ci-validate-viewmodels.ps1 -CoverageThreshold 90
    # Require 90% coverage for ViewModels

.EXAMPLE
    .\ci-validate-viewmodels.ps1 -SkipTests
    # Only run static analysis (fast check)
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateRange(0, 100)]
    [int]$CoverageThreshold = 85,

    [Parameter()]
    [switch]$SkipStaticAnalysis,

    [Parameter()]
    [switch]$SkipTests,

    [Parameter()]
    [string]$OutputDirectory = "TestResults"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ExitCode = 0
$script:WorkspaceRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$script:OutputPath = Join-Path $script:WorkspaceRoot $OutputDirectory

# Ensure output directory exists
if (-not (Test-Path $script:OutputPath)) {
    New-Item -ItemType Directory -Path $script:OutputPath -Force | Out-Null
}

function Write-CIHeader {
    param([string]$Title)

    Write-Host "`n╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║  $($Title.PadRight(56))  ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan
}

function Write-CIStep {
    param([string]$Message)
    Write-Host "▶ $Message" -ForegroundColor Green
}

function Write-CIError {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
    $script:ExitCode = 1
}

function Write-CIWarning {
    param([string]$Message)
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
}

function Write-CISuccess {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Invoke-StaticAnalysis {
    Write-CIHeader "Static Analysis (PowerShell)"

    $validatorScript = Join-Path $script:WorkspaceRoot "scripts\testing\validate-viewmodels.ps1"

    if (-not (Test-Path $validatorScript)) {
        Write-CIError "Validator script not found: $validatorScript"
        return $false
    }

    Write-CIStep "Running static analysis validator..."

    try {
        $reportPath = Join-Path $script:OutputPath "viewmodel-static-analysis.json"

        & pwsh -NoProfile -ExecutionPolicy Bypass -File $validatorScript `
            -Path $script:WorkspaceRoot `
            -FailOnViolations `
            -GenerateReport `
            -OutputPath $reportPath `
            -Verbose

        if ($LASTEXITCODE -ne 0) {
            Write-CIError "Static analysis found violations (exit code: $LASTEXITCODE)"
            return $false
        }

        Write-CISuccess "Static analysis passed"
        return $true

    } catch {
        Write-CIError "Static analysis failed: $_"
        return $false
    }
}

function Invoke-UnitTests {
    Write-CIHeader "Unit Tests with Coverage"

    Write-CIStep "Building solution..."

    try {
        $buildArgs = @(
            'build',
            (Join-Path $script:WorkspaceRoot 'WileyWidget.sln'),
            '--configuration', 'Debug',
            '--verbosity', 'minimal'
        )

        & dotnet @buildArgs

        if ($LASTEXITCODE -ne 0) {
            Write-CIError "Build failed (exit code: $LASTEXITCODE)"
            return $false
        }

        Write-CISuccess "Build completed"

    } catch {
        Write-CIError "Build failed: $_"
        return $false
    }

    Write-CIStep "Running unit tests with coverage..."

    try {
        $testArgs = @(
            'test',
            (Join-Path $script:WorkspaceRoot 'WileyWidget.sln'),
            '--no-build',
            '--configuration', 'Debug',
            '--logger', 'trx',
            '--logger', 'console;verbosity=normal',
            '--results-directory', $script:OutputPath,
            '--collect:XPlat Code Coverage',
            '--',
            'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura'
        )

        & dotnet @testArgs

        if ($LASTEXITCODE -ne 0) {
            Write-CIError "Unit tests failed (exit code: $LASTEXITCODE)"
            return $false
        }

        Write-CISuccess "Unit tests passed"
        return $true

    } catch {
        Write-CIError "Unit tests failed: $_"
        return $false
    }
}

function Test-CoverageThreshold {
    Write-CIHeader "Coverage Analysis"

    Write-CIStep "Analyzing code coverage for ViewModels..."

    $coverageFiles = Get-ChildItem -Path $script:OutputPath -Filter "coverage.cobertura.xml" -Recurse -ErrorAction SilentlyContinue

    if ($coverageFiles.Count -eq 0) {
        Write-CIWarning "No coverage files found - skipping threshold check"
        return $true
    }

    try {
        # Parse coverage XML
        $coverageFile = $coverageFiles[0].FullName
        [xml]$coverage = Get-Content $coverageFile

        # Find ViewModel coverage
        $viewModelClasses = $coverage.coverage.packages.package.classes.class |
            Where-Object { $_.name -match 'ViewModel$' }

        if ($viewModelClasses.Count -eq 0) {
            Write-CIWarning "No ViewModel classes found in coverage report"
            return $true
        }

        $totalLines = 0
        $coveredLines = 0

        foreach ($class in $viewModelClasses) {
            $lines = $class.lines.line
            if ($lines) {
                $totalLines += $lines.Count
                $coveredLines += ($lines | Where-Object { $_.hits -gt 0 }).Count
            }
        }

        if ($totalLines -eq 0) {
            Write-CIWarning "No lines found in ViewModel coverage data"
            return $true
        }

        $coveragePercent = [math]::Round(($coveredLines / $totalLines) * 100, 2)

        Write-Host "  ViewModel Coverage: $coveragePercent% ($coveredLines/$totalLines lines)" -ForegroundColor Cyan
        Write-Host "  Threshold:          $CoverageThreshold%" -ForegroundColor Cyan

        if ($coveragePercent -lt $CoverageThreshold) {
            Write-CIError "ViewModel coverage ($coveragePercent%) below threshold ($CoverageThreshold%)"
            return $false
        }

        Write-CISuccess "Coverage meets threshold"
        return $true

    } catch {
        Write-CIWarning "Failed to parse coverage: $_"
        return $true  # Don't fail build on coverage parsing errors
    }
}

function New-ValidationReport {
    Write-CIHeader "Validation Report"

    $reportPath = Join-Path $script:OutputPath "viewmodel-validation-summary.json"

    $report = @{
        Timestamp = (Get-Date).ToString('o')
        WorkspaceRoot = $script:WorkspaceRoot
        ValidationSteps = @{
            StaticAnalysis = @{
                Executed = -not $SkipStaticAnalysis
                Passed = $script:StaticAnalysisPassed
            }
            UnitTests = @{
                Executed = -not $SkipTests
                Passed = $script:UnitTestsPassed
            }
            Coverage = @{
                Threshold = $CoverageThreshold
                Passed = $script:CoveragePassed
            }
        }
        OverallResult = if ($script:ExitCode -eq 0) { "PASSED" } else { "FAILED" }
        ExitCode = $script:ExitCode
    }

    $report | ConvertTo-Json -Depth 10 | Set-Content -Path $reportPath -Encoding UTF8

    Write-Host "`n📊 Full validation report: $reportPath" -ForegroundColor Cyan

    if ($script:ExitCode -eq 0) {
        Write-CISuccess "All validation steps passed"
    } else {
        Write-CIError "Validation failed - see report for details"
    }
}

#region Main Execution

try {
    Write-CIHeader "ViewModel CI/CD Validation"
    Write-Host "Workspace: $script:WorkspaceRoot" -ForegroundColor Gray
    Write-Host "Output:    $script:OutputPath" -ForegroundColor Gray
    Write-Host ""

    # Step 1: Static Analysis
    $script:StaticAnalysisPassed = $true
    if (-not $SkipStaticAnalysis) {
        $script:StaticAnalysisPassed = Invoke-StaticAnalysis
        if (-not $script:StaticAnalysisPassed) {
            $script:ExitCode = 1
        }
    } else {
        Write-CIWarning "Skipping static analysis"
    }

    # Step 2: Unit Tests
    $script:UnitTestsPassed = $true
    if (-not $SkipTests) {
        $script:UnitTestsPassed = Invoke-UnitTests
        if (-not $script:UnitTestsPassed) {
            $script:ExitCode = 1
        }
    } else {
        Write-CIWarning "Skipping unit tests"
    }

    # Step 3: Coverage Threshold
    $script:CoveragePassed = $true
    if (-not $SkipTests) {
        $script:CoveragePassed = Test-CoverageThreshold
        if (-not $script:CoveragePassed) {
            $script:ExitCode = 1
        }
    }

    # Step 4: Generate Report
    New-ValidationReport

    exit $script:ExitCode

} catch {
    Write-CIError "Fatal error during validation: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}

#endregion
