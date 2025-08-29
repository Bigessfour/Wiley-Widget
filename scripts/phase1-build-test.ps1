# Phase 1 Build and Test Script
# Tests the Foundation & Data Backbone implementation

param(
    [switch]$Clean,
    [switch]$TestOnly,
    [switch]$SkipTests,
    [switch]$CI  # CI mode - non-interactive, always exit with code
)

Write-Host "🚀 Wiley Widget Phase 1 Build & Test" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

$projectDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$phase1TestDir = Join-Path $projectDir "WileyWidget.TestModels"

# Clean if requested
if ($Clean) {
    Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean "$projectDir\WileyWidget.csproj"
    if (Test-Path "$phase1TestDir\bin") {
        Remove-Item "$phase1TestDir\bin" -Recurse -Force
    }
    if (Test-Path "$phase1TestDir\obj") {
        Remove-Item "$phase1TestDir\obj" -Recurse -Force
    }
}

# Build main project
if (!$TestOnly) {
    Write-Host "🔨 Building main project..." -ForegroundColor Yellow
    $buildResult = dotnet build "$projectDir\WileyWidget.csproj" --verbosity quiet

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Main project build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Main project built successfully" -ForegroundColor Green
}

# Build and run Phase 1 test
if (!$SkipTests) {
    Write-Host "🧪 Building Phase 1 test project..." -ForegroundColor Yellow

    # Build test project
    $testBuildResult = dotnet build "$phase1TestDir\WileyWidget.TestModels.csproj" --verbosity quiet

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Phase 1 test build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Phase 1 test built successfully" -ForegroundColor Green

    # Run Phase 1 test
    Write-Host "🏃 Running Phase 1 tests..." -ForegroundColor Yellow
    $testExitCode = 0
    try {
        dotnet run --project "$phase1TestDir\WileyWidget.TestModels.csproj"
        $testExitCode = $LASTEXITCODE
    } catch {
        $testExitCode = 1
        Write-Host "❌ Phase 1 test execution failed: $($_.Exception.Message)" -ForegroundColor Red
    }

    if ($CI -and $testExitCode -ne 0) {
        Write-Host "::error::Phase 1 validation failed with exit code $testExitCode"
        exit $testExitCode
    }
}

Write-Host "🎉 Phase 1 Build & Test Complete!" -ForegroundColor Green
Write-Host "" -ForegroundColor Green

if (!$CI) {
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "1. Review test output for any issues" -ForegroundColor White
    Write-Host "2. If tests pass, proceed to Phase 2: UI Dashboards" -ForegroundColor White
    Write-Host "3. Update North Star document with Phase 1 completion" -ForegroundColor White
} else {
    Write-Host "::notice::Phase 1 validation completed successfully"
}
