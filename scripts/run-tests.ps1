# WileyWidget Test Runner Script
# Runs unit tests, integration tests, and UI tests with proper WPF configuration

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("All", "Unit", "Integration", "UI", "Coverage")]
    [string]$TestType = "All",

    [Parameter(Mandatory=$false)]
    [string]$Filter = "",

    [Parameter(Mandatory=$false)]
    [switch]$NoBuild,

    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

# Configuration
$projectRoot = Split-Path -Parent $PSScriptRoot
$testSettingsFile = Join-Path $projectRoot "WileyWidget.TestSettings.runsettings"
$unitTestProject = Join-Path $projectRoot "WileyWidget.Tests\WileyWidget.Tests.csproj"
$uiTestProject = Join-Path $projectRoot "WileyWidget.UiTests\WileyWidget.UiTests.csproj"

# Logging function
function Write-TestLog {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        "ERROR" { "Red" }
        "WARNING" { "Yellow" }
        "SUCCESS" { "Green" }
        default { "White" }
    }
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

# Build projects if needed
if (-not $NoBuild) {
    Write-TestLog "Building projects..." "INFO"

    # Build main project
    $buildResult = dotnet build $projectRoot\WileyWidget.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) {
        Write-TestLog "Failed to build main project" "ERROR"
        exit 1
    }

    # Build test projects
    $buildResult = dotnet build $unitTestProject --configuration Release
    if ($LASTEXITCODE -ne 0) {
        Write-TestLog "Failed to build unit test project" "ERROR"
        exit 1
    }

    $buildResult = dotnet build $uiTestProject --configuration Release
    if ($LASTEXITCODE -ne 0) {
        Write-TestLog "Failed to build UI test project" "ERROR"
        exit 1
    }

    Write-TestLog "Build completed successfully" "SUCCESS"
}

# Function to run tests
function Run-Tests {
    param(
        [string]$ProjectPath,
        [string]$TestFilter = "",
        [string]$DisplayName
    )

    Write-TestLog "Running $DisplayName tests..." "INFO"

    $testArgs = @(
        "test",
        $ProjectPath,
        "--configuration", "Release",
        "--settings", $testSettingsFile,
        "--logger", "console;verbosity=detailed",
        "--logger", "trx",
        "--results-directory", "$projectRoot\TestResults"
    )

    if ($TestFilter) {
        $testArgs += "--filter", $TestFilter
    }

    if ($Verbose) {
        $testArgs += "--verbosity", "detailed"
    }

    $testResult = & dotnet $testArgs

    if ($LASTEXITCODE -eq 0) {
        Write-TestLog "$DisplayName tests completed successfully" "SUCCESS"
        return $true
    } else {
        Write-TestLog "$DisplayName tests failed" "ERROR"
        return $false
    }
}

# Function to run coverage
function Run-Coverage {
    Write-TestLog "Running code coverage analysis..." "INFO"

    $coverageArgs = @(
        "test",
        $unitTestProject,
        "--configuration", "Release",
        "--settings", $testSettingsFile,
        "--collect", "XPlat Code Coverage",
        "--results-directory", "$projectRoot\TestResults",
        "--logger", "console;verbosity=detailed"
    )

    if ($Verbose) {
        $coverageArgs += "--verbosity", "detailed"
    }

    $coverageResult = & dotnet $coverageArgs

    if ($LASTEXITCODE -eq 0) {
        Write-TestLog "Coverage analysis completed successfully" "SUCCESS"

        # Generate coverage report
        $coverageFiles = Get-ChildItem -Path "$projectRoot\TestResults" -Filter "*.coverage" -Recurse
        if ($coverageFiles) {
            Write-TestLog "Coverage reports generated in: $projectRoot\TestResults" "INFO"
        }
    } else {
        Write-TestLog "Coverage analysis failed" "ERROR"
    }
}

# Main execution logic
$allTestsPassed = $true

switch ($TestType) {
    "Unit" {
        $allTestsPassed = Run-Tests -ProjectPath $unitTestProject -TestFilter "Category!=UITest" -DisplayName "Unit"
    }
    "Integration" {
        $allTestsPassed = Run-Tests -ProjectPath $unitTestProject -TestFilter "Category=IntegrationTest" -DisplayName "Integration"
    }
    "UI" {
        $allTestsPassed = Run-Tests -ProjectPath $uiTestProject -TestFilter "Category=UITest" -DisplayName "UI"
    }
    "Coverage" {
        Run-Coverage
    }
    "All" {
        # Run unit tests
        $unitPassed = Run-Tests -ProjectPath $unitTestProject -TestFilter "Category!=UITest" -DisplayName "Unit"
        $allTestsPassed = $allTestsPassed -and $unitPassed

        # Run UI tests
        $uiPassed = Run-Tests -ProjectPath $uiTestProject -TestFilter "Category=UITest" -DisplayName "UI"
        $allTestsPassed = $allTestsPassed -and $uiPassed

        # Run coverage
        Run-Coverage
    }
}

# Summary
if ($allTestsPassed) {
    Write-TestLog "All tests completed successfully!" "SUCCESS"
    exit 0
} else {
    Write-TestLog "Some tests failed. Check the output above for details." "ERROR"
    exit 1
}
