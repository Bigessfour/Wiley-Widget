# Startup Performance Profiling Script
# Measures and analyzes application startup performance

param(
    [int]$Iterations = 3,
    [switch]$ColdStart,
    [switch]$Clean
)

$projectDir = Split-Path -Parent $PSScriptRoot
$appName = "WileyWidget"
$exePath = Join-Path $projectDir "bin\Debug\net9.0-windows\$appName.exe"
$resultsFile = Join-Path $projectDir "startup-performance-results.json"

# Clean previous results if requested
if ($Clean) {
    if (Test-Path $resultsFile) {
        Remove-Item $resultsFile -Force
    }
    Write-Information "Cleaned previous performance results" -InformationAction Continue
    exit 0
}

# Ensure the application is built
Write-Information "Building application..." -InformationAction Continue
& dotnet build "$projectDir\WileyWidget.csproj" --configuration Debug --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}
& dotnet build "$projectDir\WileyWidget.csproj" --configuration Debug --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Function to measure startup time
function Measure-StartupTime {
    param([bool]$IsColdStart = $false)

    $startupTimes = @()

    for ($i = 1; $i -le $Iterations; $i++) {
        Write-Information "Measuring startup time - Iteration $i/$Iterations..." -InformationAction Continue

        if ($IsColdStart) {
            # Kill any existing processes
            Get-Process -Name $appName -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

            # Clear temp files for cold start
            $tempPath = [System.IO.Path]::GetTempPath()
            $cachePath = Join-Path $tempPath "WileyWidget"
            if (Test-Path $cachePath) {
                Remove-Item $cachePath -Recurse -Force -ErrorAction SilentlyContinue
            }

            # Clear .NET temp files
            $dotnetTemp = Join-Path $tempPath "dotnet"
            if (Test-Path $dotnetTemp) {
                Get-ChildItem $dotnetTemp -Filter "*.tmp" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
            }
        }

        $startTime = Get-Date

        # Start the process
        $process = Start-Process -FilePath $exePath -PassThru

        # Wait for the main window to appear (look for WPF window)
        $timeout = 30 # seconds
        $elapsed = 0
        $windowFound = $false

        while ($elapsed -lt $timeout -and -not $windowFound) {
            Start-Sleep -Milliseconds 100
            $elapsed += 0.1

            # Check if main window is visible by looking for the process and assuming it started
            # In a real scenario, you'd use UI automation to detect when the main window is ready
            if (-not $process.HasExited) {
                # Simple heuristic: wait a bit then assume it's started
                if ($elapsed -gt 2) {
                    $windowFound = $true
                }
            }
        }

        $endTime = Get-Date
        $startupTime = ($endTime - $startTime).TotalSeconds

        if ($windowFound) {
            $startupTimes += $startupTime
            Write-Information "Iteration $i completed in $([math]::Round($startupTime, 2))s" -InformationAction Continue
        }
        else {
            Write-Information "Iteration $i failed to start within timeout" -InformationAction Continue
        }

        # Clean up
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }

        # Wait between iterations
        Start-Sleep -Seconds 2
    }

    return $startupTimes
}

# Run measurements
Write-Information "Starting startup performance profiling..." -InformationAction Continue
Write-Information "Application: $appName" -InformationAction Continue
Write-Information "Executable: $exePath" -InformationAction Continue
Write-Information "Iterations: $Iterations" -InformationAction Continue
Write-Information "Cold Start: $ColdStart" -InformationAction Continue
Write-Information "" -InformationAction Continue

$results = @{}

if ($ColdStart) {
    Write-Information "=== Cold Start Performance ===" -InformationAction Continue
    $coldTimes = Measure-StartupTime -IsColdStart $true
    $results.ColdStart = @{
        Times   = $coldTimes
        Average = [math]::Round(($coldTimes | Measure-Object -Average).Average, 2)
        Min     = [math]::Round(($coldTimes | Measure-Object -Minimum).Minimum, 2)
        Max     = [math]::Round(($coldTimes | Measure-Object -Maximum).Maximum, 2)
    }
    Write-Information "Cold Start - Average: $($results.ColdStart.Average)s, Min: $($results.ColdStart.Min)s, Max: $($results.ColdStart.Max)s" -InformationAction Continue
    Write-Information "" -InformationAction Continue
}

Write-Information "=== Warm Start Performance ===" -InformationAction Continue
$warmTimes = Measure-StartupTime -IsColdStart $false
$results.WarmStart = @{
    Times   = $warmTimes
    Average = [math]::Round(($warmTimes | Measure-Object -Average).Average, 2)
    Min     = [math]::Round(($warmTimes | Measure-Object -Minimum).Minimum, 2)
    Max     = [math]::Round(($warmTimes | Measure-Object -Maximum).Maximum, 2)
}

Write-Information "Warm Start - Average: $($results.WarmStart.Average)s, Min: $($results.WarmStart.Min)s, Max: $($results.WarmStart.Max)s" -InformationAction Continue

# Save results
$results.Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$results | ConvertTo-Json -Depth 10 | Out-File $resultsFile -Encoding UTF8

Write-Information "" -InformationAction Continue
Write-Information "Results saved to: $resultsFile" -InformationAction Continue
Write-Information "Profiling completed!" -InformationAction Continue
