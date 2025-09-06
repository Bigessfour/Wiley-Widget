param(
    [string]$ProjectPath = "$PSScriptRoot\WileyWidget.csproj"
)

Write-Host 'Enhanced Build Process Started' -ForegroundColor Cyan
$buildTimer = [System.Diagnostics.Stopwatch]::StartNew()

# Step 1: Kill any existing .NET processes that might interfere
Write-Host 'Killing existing .NET processes...' -ForegroundColor Yellow
try {
    $dotnetProcesses = Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue
    if ($dotnetProcesses) {
        $dotnetProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
        Write-Host 'NET processes terminated' -ForegroundColor Green
    } else {
        Write-Host 'No .NET processes found to terminate' -ForegroundColor Gray
    }
} catch {
    Write-Host 'Could not terminate .NET processes (may not exist)' -ForegroundColor Yellow
}

# Step 2: Clean previous build artifacts
Write-Host 'Cleaning previous build artifacts...' -ForegroundColor Yellow
dotnet clean $ProjectPath 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Clean failed, continuing...' -ForegroundColor Yellow
}

# Step 3: Restore packages
Write-Host 'Restoring NuGet packages...' -ForegroundColor Yellow
dotnet restore $ProjectPath 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Package restore failed' -ForegroundColor Red
    exit $LASTEXITCODE
}

# Step 4: Build with enhanced diagnostics
Write-Host 'Building project...' -ForegroundColor Yellow
$buildStart = Get-Date
dotnet build $ProjectPath /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary /verbosity:minimal 2>&1
$buildExitCode = $LASTEXITCODE
$buildEnd = Get-Date
$buildDuration = ($buildEnd - $buildStart).TotalMilliseconds

if ($buildExitCode -eq 0) {
    Write-Host "Build successful in $([math]::Round($buildDuration))ms" -ForegroundColor Green

    # Step 5: Validation checks
    Write-Host 'Running post-build validation...' -ForegroundColor Yellow

    # Check if output files exist
    $outputDir = Join-Path $PSScriptRoot 'bin\Debug\net9.0-windows'
    if (Test-Path $outputDir) {
        $exePath = Join-Path $outputDir 'WileyWidget.exe'
        if (Test-Path $exePath) {
            $fileInfo = Get-Item $exePath
            Write-Host "Executable found: $([math]::Round($fileInfo.Length / 1MB, 2))MB" -ForegroundColor Green
        } else {
            Write-Host 'Executable not found in expected location' -ForegroundColor Yellow
        }
    } else {
        Write-Host 'Output directory not found' -ForegroundColor Yellow
    }

    # Check for common build issues
    $warnings = dotnet build $ProjectPath /property:GenerateFullPaths=true /consoleloggerparameters:WarningsOnly 2>&1
    if ($warnings -match 'warning') {
        Write-Host 'Build warnings detected:' -ForegroundColor Yellow
        $warnings | Where-Object { $_ -match 'warning' } | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    } else {
        Write-Host 'No build warnings detected' -ForegroundColor Green
    }

    $buildTimer.Stop()
    Write-Host "Enhanced build completed successfully in $([math]::Round($buildTimer.Elapsed.TotalMilliseconds))ms" -ForegroundColor Green
    exit 0
} else {
    Write-Host 'Build failed' -ForegroundColor Red
    $buildTimer.Stop()
    exit $buildExitCode
}
