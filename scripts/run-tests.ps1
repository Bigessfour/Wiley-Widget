# WileyWidget Test Runner Script
# Runs unit tests, integration tests, and UI tests with proper WPF configuration

param(
    [Parameter(Mandatory=$false)]
        [ValidateSet("All", "Unit", "Integration", "UI", "Coverage", "Mutation", "EntityValidation")]
    [string]$TestType = "All",

    [Parameter(Mandatory=$false)]
    [string]$Filter = "",

    [Parameter(Mandatory=$false)]
    [switch]$NoBuild,

    [Parameter(Mandatory=$false)]
    [switch]$Verbose,

    [Parameter(Mandatory=$false)]
    [switch]$UseAdvancedTools
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

# Function to run tests with retry logic
function Run-Tests {
    param(
        [string]$ProjectPath,
        [string]$TestFilter = "",
        [string]$DisplayName,
        [int]$MaxRetries = 3,
        [int]$RetryDelay = 5
    )

    Write-TestLog "Running $DisplayName tests..." "INFO"

    $attempt = 1
    $success = $false

    while ($attempt -le $MaxRetries -and -not $success) {
        Write-TestLog "Attempt $attempt of $MaxRetries for $DisplayName" "INFO"

        $testArgs = @(
            "test",
            $ProjectPath,
            "--configuration", "Release",
            "--settings", $testSettingsFile,
            "--logger", "console;verbosity=minimal",
            "--logger", "trx",
            "--results-directory", "$projectRoot\TestResults",
            "--blame-hang-timeout", "5min",
            "--blame-crash"
        )

        if ($TestFilter) {
            $testArgs += "--filter", $TestFilter
        }

        if ($UseAdvancedTools) {
            $testArgs += "--collect", "XPlat Code Coverage"
        }

        try {
            $testResult = & dotnet $testArgs
            $exitCode = $LASTEXITCODE

            if ($exitCode -eq 0) {
                Write-TestLog "$DisplayName tests completed successfully on attempt $attempt" "SUCCESS"
                $success = $true
            } else {
                Write-TestLog "$DisplayName tests failed on attempt $attempt (Exit code: $exitCode)" "WARNING"

                if ($attempt -lt $MaxRetries) {
                    Write-TestLog "Retrying in $RetryDelay seconds..." "INFO"
                    Start-Sleep -Seconds $RetryDelay
                    $RetryDelay = [Math]::Min($RetryDelay * 2, 60)  # Exponential backoff
                }
            }
        } catch {
            Write-TestLog "Exception during $DisplayName test execution: $($_.Exception.Message)" "ERROR"

            if ($attempt -lt $MaxRetries) {
                Write-TestLog "Retrying in $RetryDelay seconds..." "INFO"
                Start-Sleep -Seconds $RetryDelay
                $RetryDelay = [Math]::Min($RetryDelay * 2, 60)
            }
        }

        $attempt++
    }

    if (-not $success) {
        Write-TestLog "$DisplayName tests failed after $MaxRetries attempts" "ERROR"
        return $false
    }

    return $true
}
    } else {
        Write-TestLog "$DisplayName tests failed" "ERROR"
        return $false
    }
}

# Function to run advanced coverage with Coverlet
function Run-AdvancedCoverage {
    Write-TestLog "Running advanced coverage analysis with Coverlet..." "INFO"

    if (!(Get-Command coverlet -ErrorAction SilentlyContinue)) {
        Write-TestLog "Coverlet not found. Installing..." "WARNING"
        dotnet tool install --global coverlet.console
    }

    $coverageOutput = "$projectRoot\TestResults\coverage"
    if (!(Test-Path $coverageOutput)) {
        New-Item -ItemType Directory -Path $coverageOutput | Out-Null
    }

    $coverletArgs = @(
        "$projectRoot\WileyWidget.Tests\bin\Release\net9.0-windows\WileyWidget.Tests.dll",
        "--target", "dotnet",
        "--targetargs", "test $unitTestProject --no-build --configuration Release",
        "--format", "lcov;html;opencover",
        "--output", "$coverageOutput\",
        "--exclude", "[xunit.*]*,[Moq]*,[FluentAssertions]*,[AutoFixture]*,[Bogus]*,[Microsoft.Extensions.*]*,[System.*]*",
        "--include", "[WileyWidget*]*"
    )

    if ($Filter) {
        $coverletArgs[3] = "test $unitTestProject --no-build --configuration Release --filter `"$Filter`""
    }

    & coverlet $coverletArgs

    if ($LASTEXITCODE -eq 0) {
        Write-TestLog "Advanced coverage analysis completed successfully" "SUCCESS"
        Write-TestLog "Coverage reports available at: $coverageOutput" "INFO"
        Write-TestLog "  - HTML Report: $coverageOutput\coverage.html" "INFO"
        Write-TestLog "  - LCOV Report: $coverageOutput\coverage.lcov" "INFO"
    } else {
        Write-TestLog "Advanced coverage analysis failed" "ERROR"
    }
}

# Function to run mutation testing with Stryker
function Run-MutationTests {
    Write-TestLog "Running mutation testing with Stryker.NET..." "INFO"

    if (!(Get-Command dotnet-stryker -ErrorAction SilentlyContinue)) {
        Write-TestLog "Stryker.NET not found. Installing..." "WARNING"
        dotnet tool install --global dotnet-stryker
    }

    Push-Location $projectRoot
    try {
        & dotnet-stryker
        if ($LASTEXITCODE -eq 0) {
            Write-TestLog "Mutation testing completed successfully" "SUCCESS"
        } else {
            Write-TestLog "Mutation testing failed or found surviving mutants" "WARNING"
        }
    } finally {
        Pop-Location
    }
}

# Function to run entity validation tests specifically
function Run-EntityValidationTests {
    Write-TestLog "Running entity validation tests..." "INFO"

    $entityTestArgs = @(
        "test",
        $unitTestProject,
        "--configuration", "Release",
        "--filter", "EntityValidationTests",
        "--settings", $testSettingsFile,
        "--logger", "console;verbosity=detailed",
        "--logger", "trx",
        "--results-directory", "$projectRoot\TestResults"
    )

    if ($Verbose) {
        $entityTestArgs += "--verbosity", "detailed"
    }

    $testResult = & dotnet $entityTestArgs

    if ($LASTEXITCODE -eq 0) {
        Write-TestLog "Entity validation tests completed successfully" "SUCCESS"
        return $true
    } else {
        Write-TestLog "Entity validation tests failed" "ERROR"
        return $false
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
        if ($UseAdvancedTools) {
            Run-AdvancedCoverage
        } else {
            Run-Coverage
        }
    }
    "Mutation" {
        Run-MutationTests
    }
    "EntityValidation" {
        $allTestsPassed = Run-EntityValidationTests
    }
    "All" {
        # Run unit tests
        $unitPassed = Run-Tests -ProjectPath $unitTestProject -TestFilter "Category!=UITest" -DisplayName "Unit"
        $allTestsPassed = $allTestsPassed -and $unitPassed

        # Run UI tests
        $uiPassed = Run-Tests -ProjectPath $uiTestProject -TestFilter "Category=UITest" -DisplayName "UI"
        $allTestsPassed = $allTestsPassed -and $uiPassed

        # Run entity validation tests
        $entityPassed = Run-EntityValidationTests
        $allTestsPassed = $allTestsPassed -and $entityPassed

        # Run coverage
        if ($UseAdvancedTools) {
            Run-AdvancedCoverage
        } else {
            Run-Coverage
        }

        # Run mutation tests if requested
        if ($UseAdvancedTools) {
            Run-MutationTests
        }
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
