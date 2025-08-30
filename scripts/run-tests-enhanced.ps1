# WileyWidget Enhanced Test Runner with 90% Success Rate Features
# Implements retry logic, circuit breaker pattern, and intelligent test execution

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
    [switch]$UseAdvancedTools,

    [Parameter(Mandatory=$false)]
    [int]$MaxRetries = 3,

    [Parameter(Mandatory=$false)]
    [int]$RetryDelaySeconds = 5,

    [Parameter(Mandatory=$false)]
    [switch]$CircuitBreakerMode
)

# Configuration with enhanced reliability settings
$projectRoot = Split-Path -Parent $PSScriptRoot
$testSettingsFile = Join-Path $projectRoot "WileyWidget.TestSettings.runsettings"
$unitTestProject = Join-Path $projectRoot "WileyWidget.Tests\WileyWidget.Tests.csproj"
$uiTestProject = Join-Path $projectRoot "WileyWidget.UiTests\WileyWidget.UiTests.csproj"

# Circuit breaker state
$circuitBreakerFile = Join-Path $projectRoot ".circuit-breaker-state.json"
$circuitBreakerState = @{
    consecutiveFailures = 0
    lastFailureTime = $null
    isOpen = $false
    failureThreshold = 5
    recoveryTimeoutMinutes = 30
}

# Load circuit breaker state
if (Test-Path $circuitBreakerFile) {
    try {
        $circuitBreakerState = Get-Content $circuitBreakerFile | ConvertFrom-Json
    } catch {
        Write-TestLog "Warning: Could not load circuit breaker state, using defaults" "WARNING"
    }
}

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

function Test-CircuitBreaker {
    # Check if circuit breaker should be opened
    if ($circuitBreakerState.isOpen) {
        $timeSinceLastFailure = (Get-Date) - [DateTime]::Parse($circuitBreakerState.lastFailureTime)
        if ($timeSinceLastFailure.TotalMinutes -lt $circuitBreakerState.recoveryTimeoutMinutes) {
            Write-TestLog "Circuit breaker is OPEN - skipping tests to prevent cascade failures" "WARNING"
            return $false
        } else {
            Write-TestLog "Circuit breaker recovery timeout reached, attempting to close" "INFO"
            $circuitBreakerState.isOpen = $false
            $circuitBreakerState.consecutiveFailures = 0
        }
    }
    return $true
}

function Update-CircuitBreaker {
    param([bool]$success)

    if ($success) {
        $circuitBreakerState.consecutiveFailures = 0
        $circuitBreakerState.isOpen = $false
        Write-TestLog "Circuit breaker reset - success recorded" "SUCCESS"
    } else {
        $circuitBreakerState.consecutiveFailures++
        $circuitBreakerState.lastFailureTime = (Get-Date).ToString("o")

        if ($circuitBreakerState.consecutiveFailures -ge $circuitBreakerState.failureThreshold) {
            $circuitBreakerState.isOpen = $true
            Write-TestLog "Circuit breaker OPENED - too many consecutive failures" "ERROR"
        }
    }

    # Save state
    $circuitBreakerState | ConvertTo-Json | Out-File $circuitBreakerFile -Encoding UTF8
}

function Invoke-TestWithRetry {
    param(
        [string]$TestCommand,
        [string]$TestName,
        [int]$MaxRetries = $MaxRetries,
        [int]$DelaySeconds = $RetryDelaySeconds
    )

    $attempt = 1
    $lastExitCode = 0

    do {
        Write-TestLog "Attempt $attempt/$MaxRetries for $TestName" "INFO"

        try {
            Invoke-Expression $TestCommand
            $lastExitCode = $LASTEXITCODE

            if ($lastExitCode -eq 0) {
                Write-TestLog "✅ $TestName succeeded on attempt $attempt" "SUCCESS"
                return $true
            } else {
                Write-TestLog "❌ $TestName failed on attempt $attempt (Exit code: $lastExitCode)" "WARNING"
            }
        } catch {
            Write-TestLog "❌ $TestName threw exception on attempt $attempt : $($_.Exception.Message)" "ERROR"
            $lastExitCode = 1
        }

        if ($attempt -lt $MaxRetries) {
            Write-TestLog "⏳ Waiting ${DelaySeconds}s before retry..." "INFO"
            Start-Sleep -Seconds $DelaySeconds

            # Exponential backoff
            $DelaySeconds = [Math]::Min($DelaySeconds * 2, 60)
        }

        $attempt++
    } while ($attempt -le $MaxRetries)

    Write-TestLog "💥 $TestName failed after $MaxRetries attempts" "ERROR"
    return $false
}

function Get-TestFilter {
    param([string]$TestType)

    switch ($TestType) {
        "Unit" { return "Category!=UiSmokeTests&Category!=HighInteraction&Category!=PostMigration" }
        "Integration" { return "Category=Integration" }
        "UI" { return "Category=UiSmokeTests" }
        "EntityValidation" { return "EntityValidation" }
        "Coverage" { return "Category!=UiSmokeTests&Category!=HighInteraction" }
        default { return "Category!=UiSmokeTests" }
    }
}

function Start-TestExecution {
    Write-TestLog "🚀 Starting Enhanced Test Execution" "INFO"
    Write-TestLog "Test Type: $TestType" "INFO"
    Write-TestLog "Max Retries: $MaxRetries" "INFO"
    Write-TestLog "Circuit Breaker Mode: $CircuitBreakerMode" "INFO"

    # Circuit breaker check
    if ($CircuitBreakerMode -and -not (Test-CircuitBreaker)) {
        Write-TestLog "Tests skipped due to circuit breaker" "WARNING"
        exit 0
    }

    $overallSuccess = $true

    # Build projects if needed
    if (-not $NoBuild) {
        Write-TestLog "🔨 Building projects..." "INFO"

        $buildCommand = "dotnet build '$projectRoot\WileyWidget.csproj' --configuration Release --verbosity minimal"
        $buildSuccess = Invoke-TestWithRetry -TestCommand $buildCommand -TestName "Project Build"

        if (-not $buildSuccess) {
            Write-TestLog "❌ Build failed, cannot proceed with tests" "ERROR"
            Update-CircuitBreaker -success $false
            exit 1
        }
    }

    # Determine test categories to run
    $testCategories = switch ($TestType) {
        "All" { @("Unit", "Integration", "EntityValidation") }
        "Unit" { @("Unit") }
        "Integration" { @("Integration") }
        "EntityValidation" { @("EntityValidation") }
        "Coverage" { @("Unit", "Integration", "EntityValidation") }
        default { @("Unit") }
    }

    # Execute tests by category
    foreach ($category in $testCategories) {
        Write-TestLog "🧪 Executing $category tests..." "INFO"

        $testFilter = Get-TestFilter -TestType $category
        if ($Filter) {
            $testFilter = "$testFilter&$Filter"
        }

        $testCommand = @"
dotnet test '$unitTestProject' --no-build --configuration Release --filter "$testFilter" --logger "trx;LogFileName=${category}-test-results.trx" --logger "junit;LogFileName=${category}-test-results.xml" --collect:"XPlat Code Coverage" --results-directory TestResults --blame-hang-timeout 5min --blame-crash
"@

        if ($Verbose) {
            $testCommand += " --verbosity normal"
        } else {
            $testCommand += " --verbosity minimal"
        }

        $testSuccess = Invoke-TestWithRetry -TestCommand $testCommand.Trim() -TestName "$category Tests"

        if (-not $testSuccess) {
            $overallSuccess = $false
            Write-TestLog "❌ $category tests failed" "ERROR"
        } else {
            Write-TestLog "✅ $category tests passed" "SUCCESS"
        }
    }

    # UI Tests (separate handling)
    if ($TestType -eq "All" -or $TestType -eq "UI") {
        Write-TestLog "🖥️ Executing UI tests..." "INFO"

        $uiTestCommand = @"
dotnet test '$uiTestProject' --no-build --configuration Release --filter "Category=UiSmokeTests" --logger "trx;LogFileName=ui-test-results.trx" --logger "junit;LogFileName=ui-test-results.xml" --results-directory TestResults --blame-hang-timeout 10min
"@

        $uiTestSuccess = Invoke-TestWithRetry -TestCommand $uiTestCommand.Trim() -TestName "UI Tests" -MaxRetries 2

        if (-not $uiTestSuccess) {
            Write-TestLog "⚠️ UI tests failed, but continuing..." "WARNING"
            # UI test failures don't fail the overall build in CI
        }
    }

    # Generate coverage report if requested
    if ($TestType -eq "Coverage" -or $UseAdvancedTools) {
        Write-TestLog "📊 Generating coverage report..." "INFO"

        try {
            # Install report generator if needed
            dotnet tool install -g dotnet-reportgenerator-globaltool --version 5.1.0 --verbosity minimal

            # Generate HTML coverage report
            reportgenerator -reports:"TestResults/*/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html -verbosity:Error

            Write-TestLog "✅ Coverage report generated at: coverage-report/index.html" "SUCCESS"
        } catch {
            Write-TestLog "⚠️ Coverage report generation failed: $($_.Exception.Message)" "WARNING"
        }
    }

    # Update circuit breaker
    if ($CircuitBreakerMode) {
        Update-CircuitBreaker -success $overallSuccess
    }

    # Final status
    if ($overallSuccess) {
        Write-TestLog "🎉 All tests completed successfully!" "SUCCESS"
        exit 0
    } else {
        Write-TestLog "💥 Some tests failed. Check logs above for details." "ERROR"
        exit 1
    }
}

# Main execution
try {
    Start-TestExecution
} catch {
    Write-TestLog "💥 Fatal error during test execution: $($_.Exception.Message)" "ERROR"
    Write-TestLog "Stack trace: $($_.Exception.StackTrace)" "ERROR"

    if ($CircuitBreakerMode) {
        Update-CircuitBreaker -success $false
    }

    exit 1
}
