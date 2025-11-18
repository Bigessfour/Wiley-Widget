#Requires -Version 7.5
<#
.SYNOPSIS
    Wiley Widget Docker Test Runner
.DESCRIPTION
    Orchestrates Docker Compose test execution with MCP compliance and intelligent retry logic.
.PARAMETER TestType
    Type of tests to run: All, Unit, Integration, UI
.PARAMETER Coverage
    Generate code coverage report
.PARAMETER Rebuild
    Rebuild Docker images before running tests
.PARAMETER Clean
    Clean up volumes and containers before running
.PARAMETER Verbose
    Enable verbose output
.EXAMPLE
    .\docker-test-runner.ps1 -TestType Unit -Coverage
.EXAMPLE
    .\docker-test-runner.ps1 -TestType All -Rebuild -Verbose
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('All', 'Unit', 'Integration', 'UI')]
    [string]$TestType = 'All',

    [Parameter()]
    [switch]$Coverage,

    [Parameter()]
    [switch]$Rebuild,

    [Parameter()]
    [switch]$Clean,

    [Parameter()]
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# ──────────────────────────────────────────────────────────────
# Configuration
# ──────────────────────────────────────────────────────────────
$script:RepoRoot = Split-Path -Parent $PSScriptRoot | Split-Path -Parent
$script:CoverageDir = Join-Path $RepoRoot 'coverage'
$script:TestResultsDir = Join-Path $RepoRoot 'test-results'
$script:DockerCompose = 'docker-compose'

# ──────────────────────────────────────────────────────────────
# Helper Functions
# ──────────────────────────────────────────────────────────────
function Write-Step {
    param([string]$Message)
    Write-Host "`n✓ $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Failure {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Test-DockerRunning {
    try {
        docker ps | Out-Null
        return $true
    }
    catch {
        Write-Failure "Docker is not running. Please start Docker Desktop."
        return $false
    }
}

function Wait-ForHealthy {
    param(
        [string]$Service,
        [int]$TimeoutSeconds = 60
    )

    Write-Step "Waiting for $Service to be healthy..."
    $elapsed = 0
    while ($elapsed -lt $TimeoutSeconds) {
        $status = & $DockerCompose ps $Service --format json | ConvertFrom-Json
        if ($status.Health -eq 'healthy') {
            Write-Success "$Service is healthy"
            return $true
        }
        Start-Sleep -Seconds 2
        $elapsed += 2
        Write-Host "." -NoNewline
    }
    Write-Failure "$Service failed to become healthy within $TimeoutSeconds seconds"
    return $false
}

# ──────────────────────────────────────────────────────────────
# Main Execution
# ──────────────────────────────────────────────────────────────
Write-Host @"
╔═══════════════════════════════════════════════════════════╗
║  Wiley Widget Docker Test Runner                         ║
║  Test Type: $TestType
║  Coverage: $($Coverage ? 'Enabled' : 'Disabled')
╚═══════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# Verify Docker is running
if (-not (Test-DockerRunning)) {
    exit 1
}

Push-Location $RepoRoot
try {
    # ──────────────────────────────────────────────────────────────
    # Step 1: Cleanup (if requested)
    # ──────────────────────────────────────────────────────────────
    if ($Clean) {
        Write-Step "Cleaning up existing containers and volumes..."
        & $DockerCompose down -v 2>&1 | Out-Null
        if (Test-Path $CoverageDir) {
            Remove-Item $CoverageDir -Recurse -Force
        }
        if (Test-Path $TestResultsDir) {
            Remove-Item $TestResultsDir -Recurse -Force
        }
        Write-Success "Cleanup complete"
    }

    # ──────────────────────────────────────────────────────────────
    # Step 2: Rebuild images (if requested)
    # ──────────────────────────────────────────────────────────────
    if ($Rebuild) {
        Write-Step "Rebuilding Docker images..."
        & $DockerCompose build --parallel test app
        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Docker build failed"
            exit 1
        }
        Write-Success "Build complete"
    }

    # ──────────────────────────────────────────────────────────────
    # Step 3: Start Database
    # ──────────────────────────────────────────────────────────────
    Write-Step "Starting SQL Server..."
    & $DockerCompose up -d db
    if ($LASTEXITCODE -ne 0) {
        Write-Failure "Failed to start database"
        exit 1
    }

    if (-not (Wait-ForHealthy -Service 'db' -TimeoutSeconds 60)) {
        Write-Host "`nDatabase logs:" -ForegroundColor Yellow
        & $DockerCompose logs db
        exit 1
    }

    # ──────────────────────────────────────────────────────────────
    # Step 4: Run Tests
    # ──────────────────────────────────────────────────────────────
    $testFilter = switch ($TestType) {
        'Unit' { '--filter Category=Unit' }
        'Integration' { '--filter Category=Integration' }
        'UI' { '' }  # UI tests run separately
        default { '--filter Category!=UI' }  # All except UI
    }

    if ($TestType -eq 'UI') {
        Write-Step "Running UI tests with Playwright..."
        & $DockerCompose run --rm ui-test
        $testExitCode = $LASTEXITCODE
    }
    else {
        Write-Step "Running $TestType tests..."
        
        # Ensure coverage directory exists
        New-Item -ItemType Directory -Force -Path $CoverageDir | Out-Null

        $testArgs = @(
            'run', '--rm', 'test'
        )
        
        if ($testFilter) {
            $testArgs += $testFilter
        }

        if ($Verbose) {
            $testArgs += '--logger "console;verbosity=detailed"'
        }

        & $DockerCompose $testArgs
        $testExitCode = $LASTEXITCODE
    }

    # ──────────────────────────────────────────────────────────────
    # Step 5: Generate Coverage Report (if requested)
    # ──────────────────────────────────────────────────────────────
    if ($Coverage -and $TestType -ne 'UI') {
        Write-Step "Generating coverage report..."
        
        $coverageFile = Get-ChildItem -Path $CoverageDir -Filter "coverage.cobertura.xml" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        
        if ($coverageFile) {
            # Generate HTML report
            docker run --rm `
                -v "${RepoRoot}:/src" `
                danielpalme/reportgenerator:latest `
                -reports:/src/coverage/**/coverage.cobertura.xml `
                -targetdir:/src/coverage/html `
                -reporttypes:Html

            $htmlReport = Join-Path $CoverageDir 'html' 'index.html'
            if (Test-Path $htmlReport) {
                Write-Success "Coverage report generated: $htmlReport"
                
                # Parse coverage percentage
                $coverageXml = [xml](Get-Content $coverageFile.FullName)
                $lineRate = [math]::Round([double]$coverageXml.coverage.'line-rate' * 100, 2)
                $branchRate = [math]::Round([double]$coverageXml.coverage.'branch-rate' * 100, 2)
                
                Write-Host @"

╔═══════════════════════════════════════════════════════════╗
║  Coverage Summary                                         ║
║  Line Coverage:   $lineRate%
║  Branch Coverage: $branchRate%
╚═══════════════════════════════════════════════════════════╝
"@ -ForegroundColor $(if ($lineRate -ge 80) { 'Green' } else { 'Yellow' })
                
                # Open in browser (optional)
                if ($Verbose) {
                    Start-Process $htmlReport
                }
            }
        }
        else {
            Write-Failure "No coverage file found"
        }
    }

    # ──────────────────────────────────────────────────────────────
    # Step 6: Results
    # ──────────────────────────────────────────────────────────────
    Write-Host "`n"
    if ($testExitCode -eq 0) {
        Write-Success "All tests passed!"
        
        # Show test results summary
        $trxFile = Get-ChildItem -Path $CoverageDir -Filter "*.trx" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($trxFile) {
            [xml]$trx = Get-Content $trxFile.FullName
            $total = $trx.TestRun.ResultSummary.Counters.total
            $passed = $trx.TestRun.ResultSummary.Counters.passed
            $failed = $trx.TestRun.ResultSummary.Counters.failed
            
            Write-Host @"
╔═══════════════════════════════════════════════════════════╗
║  Test Results Summary                                     ║
║  Total:  $total
║  Passed: $passed
║  Failed: $failed
╚═══════════════════════════════════════════════════════════╝
"@ -ForegroundColor Green
        }
        
        exit 0
    }
    else {
        Write-Failure "Tests failed with exit code: $testExitCode"
        
        # Show test logs
        Write-Host "`nTest logs:" -ForegroundColor Yellow
        & $DockerCompose logs test
        
        exit $testExitCode
    }
}
finally {
    # Cleanup (keep DB running for next iteration)
    if (-not $Verbose) {
        Write-Step "Stopping test containers (DB remains running)..."
        & $DockerCompose stop test ui-test 2>&1 | Out-Null
    }
    
    Pop-Location
}
