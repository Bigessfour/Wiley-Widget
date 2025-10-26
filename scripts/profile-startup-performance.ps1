# Performance Profiling Script for Wiley Widget
# Measures startup time and module loading performance

param(
    [int]$Iterations = 5,
    [switch]$Detailed,
    [string]$OutputPath = "logs/performance/startup-profile-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
)

$ErrorActionPreference = "Stop"

Write-Host "🔍 Wiley Widget Startup Performance Profiler" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Ensure output directory exists
$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$results = @{
    Timestamp    = Get-Date -Format "o"
    Iterations   = $Iterations
    Measurements = @()
    Summary      = @{}
}

# Function to measure startup time
function Measure-StartupTime {
    param([int]$IterationNumber)

    Write-Host "📊 Iteration $IterationNumber of $Iterations..." -ForegroundColor Yellow

    # Clean up any existing processes
    Get-Process -Name "WileyWidget" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500

    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    # Start the application
    $process = Start-Process -FilePath ".\bin\Debug\net9.0-windows10.0.19041.0\WileyWidget.exe" `
        -PassThru `
        -WindowStyle Hidden `
        -ErrorAction Stop

    # Wait for main window to be ready
    $timeout = 30
    $elapsed = 0
    $ready = $false

    while ($elapsed -lt $timeout -and -not $ready) {
        Start-Sleep -Milliseconds 100
        $elapsed += 0.1

        # Check if main window is loaded
        if ($process.MainWindowHandle -ne 0) {
            $ready = $true
        }
    }

    $sw.Stop()
    $startupTime = $sw.ElapsedMilliseconds

    # Parse log file for module loading times
    $logFile = Get-ChildItem "logs/startup-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $moduleLoadTimes = @{}

    if ($logFile -and $Detailed) {
        $logContent = Get-Content $logFile.FullName
        foreach ($line in $logContent) {
            if ($line -match "Module '(\w+)' loaded in (\d+)ms") {
                $moduleName = $Matches[1]
                $loadTime = [int]$Matches[2]
                $moduleLoadTimes[$moduleName] = $loadTime
            }
        }
    }

    # Stop the application
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500

    return @{
        IterationNumber = $IterationNumber
        StartupTimeMs   = $startupTime
        ModuleLoadTimes = $moduleLoadTimes
        Success         = $ready
    }
}

# Run iterations
Write-Host ""
for ($i = 1; $i -le $Iterations; $i++) {
    try {
        $measurement = Measure-StartupTime -IterationNumber $i
        $results.Measurements += $measurement

        $status = if ($measurement.Success) { "✅" } else { "❌" }
        Write-Host "  $status Startup time: $($measurement.StartupTimeMs)ms" -ForegroundColor $(if ($measurement.Success) { "Green" } else { "Red" })
    }
    catch {
        Write-Host "  ❌ Error in iteration $i`: $_" -ForegroundColor Red
    }
}

# Calculate summary statistics
$successfulMeasurements = $results.Measurements | Where-Object { $_.Success }
if ($successfulMeasurements) {
    $startupTimes = $successfulMeasurements | ForEach-Object { $_.StartupTimeMs }

    $results.Summary = @{
        TotalIterations      = $Iterations
        SuccessfulIterations = $successfulMeasurements.Count
        AverageStartupMs     = ($startupTimes | Measure-Object -Average).Average
        MinStartupMs         = ($startupTimes | Measure-Object -Minimum).Minimum
        MaxStartupMs         = ($startupTimes | Measure-Object -Maximum).Maximum
        MedianStartupMs      = ($startupTimes | Sort-Object)[[Math]::Floor($startupTimes.Count / 2)]
    }

    # Module statistics (if detailed)
    if ($Detailed) {
        $allModules = @{}
        foreach ($measurement in $successfulMeasurements) {
            foreach ($module in $measurement.ModuleLoadTimes.Keys) {
                if (-not $allModules.ContainsKey($module)) {
                    $allModules[$module] = @()
                }
                $allModules[$module] += $measurement.ModuleLoadTimes[$module]
            }
        }

        $moduleStats = @{}
        foreach ($module in $allModules.Keys) {
            $times = $allModules[$module]
            $moduleStats[$module] = @{
                Average = ($times | Measure-Object -Average).Average
                Min     = ($times | Measure-Object -Minimum).Minimum
                Max     = ($times | Measure-Object -Maximum).Maximum
            }
        }
        $results.Summary.ModuleStatistics = $moduleStats
    }
}

# Save results
$results | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputPath -Encoding UTF8

Write-Host ""
Write-Host "📈 Performance Profile Summary" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan
Write-Host "Total Iterations: $($results.Summary.TotalIterations)"
Write-Host "Successful: $($results.Summary.SuccessfulIterations)"
Write-Host "Average Startup: $([Math]::Round($results.Summary.AverageStartupMs, 2))ms"
Write-Host "Min Startup: $($results.Summary.MinStartupMs)ms"
Write-Host "Max Startup: $($results.Summary.MaxStartupMs)ms"
Write-Host "Median Startup: $($results.Summary.MedianStartupMs)ms"
Write-Host ""

# Performance assessment
$avgStartup = $results.Summary.AverageStartupMs
if ($avgStartup -lt 2000) {
    Write-Host "✅ Performance: EXCELLENT (< 2s cold start)" -ForegroundColor Green
}
elseif ($avgStartup -lt 5000) {
    Write-Host "⚠️  Performance: GOOD (2-5s cold start)" -ForegroundColor Yellow
}
else {
    Write-Host "❌ Performance: NEEDS OPTIMIZATION (> 5s cold start)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Results saved to: $OutputPath" -ForegroundColor Cyan

# Display module statistics if detailed
if ($Detailed -and $results.Summary.ModuleStatistics) {
    Write-Host ""
    Write-Host "📦 Module Load Times (Average)" -ForegroundColor Cyan
    Write-Host "==============================" -ForegroundColor Cyan

    $moduleStats = $results.Summary.ModuleStatistics
    $sortedModules = $moduleStats.GetEnumerator() | Sort-Object { $_.Value.Average } -Descending

    foreach ($module in $sortedModules) {
        $avg = [Math]::Round($module.Value.Average, 2)
        Write-Host "  $($module.Key): ${avg}ms" -ForegroundColor White
    }
}

Write-Host ""
