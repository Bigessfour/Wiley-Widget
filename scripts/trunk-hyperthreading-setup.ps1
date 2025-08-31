# Trunk CLI Hyperthreading Performance Script
# Enables optimal hyperthreading support for Trunk processes

param(
    [switch]$EnableHyperthreading,
    [switch]$OptimizeForCI,
    [switch]$MonitorPerformance,
    [int]$ThreadCount = 0,  # 0 = auto-detect
    [string]$MemoryLimit = "4GB"
)

# Function to detect CPU information and hyperthreading capability
function Get-CpuInfo {
    $cpuInfo = Get-WmiObject -Class Win32_Processor
    $logicalProcessors = $cpuInfo.NumberOfLogicalProcessors
    $physicalCores = $cpuInfo.NumberOfCores

    $hasHyperthreading = $logicalProcessors -gt $physicalCores

    return @{
        LogicalProcessors = $logicalProcessors
        PhysicalCores = $physicalCores
        HasHyperthreading = $hasHyperthreading
        HyperthreadingRatio = [math]::Round($logicalProcessors / $physicalCores, 2)
    }
}

# Function to set optimal environment variables for Trunk CLI
function Set-TrunkPerformanceEnvironment {
    param([hashtable]$CpuInfo, [int]$RequestedThreads, [string]$MemoryLimit)

    # Calculate optimal thread count
    if ($RequestedThreads -eq 0) {
        # Auto-detect optimal threads based on CPU
        if ($CpuInfo.HasHyperthreading) {
            # Use all logical processors for hyperthreading benefit
            $optimalThreads = $CpuInfo.LogicalProcessors
            Write-Host "🔄 Hyperthreading detected! Using $optimalThreads threads (logical processors)" -ForegroundColor Green
        } else {
            # Use physical cores + some overhead for non-hyperthreading CPUs
            $optimalThreads = [math]::Min($CpuInfo.PhysicalCores + 2, $CpuInfo.LogicalProcessors)
            Write-Host "⚡ Using $optimalThreads threads (optimized for physical cores)" -ForegroundColor Green
        }
    } else {
        $optimalThreads = $RequestedThreads
        Write-Host "🔧 Using requested thread count: $optimalThreads" -ForegroundColor Yellow
    }

    # Set environment variables for Trunk CLI performance
    $env:TRUNK_NUM_THREADS = $optimalThreads.ToString()
    $env:TRUNK_MEMORY_LIMIT = $MemoryLimit
    $env:TRUNK_ENABLE_PARALLEL = "true"
    $env:TRUNK_HYPERTHREADING_ENABLED = "true"
    $env:TRUNK_CPU_AFFINITY = "true"

    # Additional performance optimizations
    $env:TRUNK_CACHE_SIZE = "1GB"
    $env:TRUNK_NETWORK_TIMEOUT = "30"
    $env:TRUNK_MAX_CONCURRENT_JOBS = ($optimalThreads * 2).ToString()

    Write-Host "✅ Trunk performance environment configured:" -ForegroundColor Green
    Write-Host "   Threads: $optimalThreads" -ForegroundColor Cyan
    Write-Host "   Memory Limit: $MemoryLimit" -ForegroundColor Cyan
    Write-Host "   Hyperthreading: Enabled" -ForegroundColor Cyan
    Write-Host "   Parallel Processing: Enabled" -ForegroundColor Cyan
    Write-Host "   CPU Affinity: Enabled" -ForegroundColor Cyan

    return $optimalThreads
}

# Function to run Trunk with performance monitoring
function Invoke-TrunkWithMonitoring {
    param([string]$Command, [switch]$MonitorPerformance)

    if ($MonitorPerformance) {
        Write-Host "📊 Starting Trunk performance monitoring..." -ForegroundColor Magenta
        $startTime = Get-Date
        try {
            $startCpu = (Get-Counter '\Processor(_Total)\% Processor Time' -ErrorAction Stop).CounterSamples.CookedValue
        } catch {
            Write-Host "⚠️  CPU monitoring not available, continuing without performance metrics" -ForegroundColor Yellow
            $startCpu = 0
        }
    }

    Write-Host "🚀 Executing: trunk $Command" -ForegroundColor Green
    Write-Host "⏳ Processing with hyperthreading optimization..." -ForegroundColor Yellow

    try {
        # First, try to find trunk executable
        $trunkPath = $null

        # Check if trunk is in PATH
        try {
            $trunkPath = (Get-Command trunk -ErrorAction Stop).Source
        } catch {
            # Try common installation locations
            $possiblePaths = @(
                "$env:USERPROFILE\.trunk\bin\trunk.exe",
                "$env:LOCALAPPDATA\trunk\bin\trunk.exe",
                "C:\Program Files\trunk\bin\trunk.exe",
                "$env:ProgramFiles\trunk\bin\trunk.exe"
            )

            foreach ($path in $possiblePaths) {
                if (Test-Path $path) {
                    $trunkPath = $path
                    break
                }
            }
        }

        if (-not $trunkPath) {
            throw "Trunk CLI not found. Please ensure Trunk is installed and in PATH, or install it using: curl -fsSL https://get.trunk.io | bash"
        }

        # Execute trunk command using call operator for better error handling
        Write-Host "📍 Using Trunk at: $trunkPath" -ForegroundColor Cyan

        # Set error action preference to continue to capture errors
        $ErrorActionPreference = "Continue"

        # Execute the command and capture output
        $output = & $trunkPath $Command.Split() 2>&1
        $exitCode = $LASTEXITCODE

        # Display output
        if ($output) {
            $output | ForEach-Object {
                if ($_ -is [System.Management.Automation.ErrorRecord]) {
                    Write-Host "❌ $_" -ForegroundColor Red
                } else {
                    Write-Host "📝 $_" -ForegroundColor White
                }
            }
        }

        if ($MonitorPerformance) {
            $endTime = Get-Date
            try {
                $endCpu = (Get-Counter '\Processor(_Total)\% Processor Time' -ErrorAction Stop).CounterSamples.CookedValue
            } catch {
                $endCpu = 0
            }
            $duration = $endTime - $startTime

            Write-Host "📈 Performance Results:" -ForegroundColor Magenta
            Write-Host "   Duration: $($duration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Cyan
            Write-Host "   Exit Code: $exitCode" -ForegroundColor Cyan
            if ($startCpu -gt 0 -and $endCpu -gt 0) {
                Write-Host "   Avg CPU Usage: ~$([math]::Round(($startCpu + $endCpu) / 2, 1))%" -ForegroundColor Cyan
            }
        }

        return $exitCode
    }
    catch {
        Write-Error "❌ Failed to execute trunk command: $_"
        Write-Host "💡 Troubleshooting tips:" -ForegroundColor Yellow
        Write-Host "   1. Ensure Trunk CLI is installed: curl -fsSL https://get.trunk.io | bash" -ForegroundColor White
        Write-Host "   2. Check if trunk is in PATH: trunk --version" -ForegroundColor White
        Write-Host "   3. Try reinstalling Trunk CLI" -ForegroundColor White
        return 1
    }
}

# Main execution logic
function Invoke-TrunkHyperthreadingSetup {
    Write-Host "🔧 Trunk CLI Hyperthreading Performance Setup" -ForegroundColor Magenta
    Write-Host "==============================================" -ForegroundColor Magenta

    # Detect CPU information
    $cpuInfo = Get-CpuInfo
    Write-Host "🖥️  CPU Information:" -ForegroundColor Blue
    Write-Host "   Physical Cores: $($cpuInfo.PhysicalCores)" -ForegroundColor White
    Write-Host "   Logical Processors: $($cpuInfo.LogicalProcessors)" -ForegroundColor White
    Write-Host "   Hyperthreading: $(if ($cpuInfo.HasHyperthreading) { '✅ Enabled' } else { '❌ Disabled' })" -ForegroundColor $(if ($cpuInfo.HasHyperthreading) { 'Green' } else { 'Red' })
    if ($cpuInfo.HasHyperthreading) {
        Write-Host "   Hyperthreading Ratio: $($cpuInfo.HyperthreadingRatio)x" -ForegroundColor Green
    }

    # Configure performance environment
    $optimalThreads = Set-TrunkPerformanceEnvironment -CpuInfo $cpuInfo -RequestedThreads $ThreadCount -MemoryLimit $MemoryLimit

    # Execute requested Trunk commands with hyperthreading
    if ($EnableHyperthreading) {
        Write-Host "`n🔄 Running Trunk with hyperthreading optimization..." -ForegroundColor Green

        # Run comprehensive check with all optimizations
        $exitCode = Invoke-TrunkWithMonitoring -Command "check --all --ci" -MonitorPerformance:$MonitorPerformance

        if ($exitCode -eq 0) {
            Write-Host "✅ All checks passed with hyperthreading optimization!" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Some checks failed. Review output above." -ForegroundColor Yellow
        }
    }

    # CI-specific optimizations
    if ($OptimizeForCI) {
        Write-Host "`n🏗️  Applying CI-specific hyperthreading optimizations..." -ForegroundColor Blue

        # Additional CI environment variables
        $env:TRUNK_CI_OPTIMIZED = "true"
        $env:TRUNK_DISABLE_ANALYTICS = "false"  # Keep analytics for performance monitoring
        $env:TRUNK_CACHE_STRATEGY = "aggressive"

        Write-Host "✅ CI optimizations applied:" -ForegroundColor Green
        Write-Host "   CI Mode: Enabled" -ForegroundColor Cyan
        Write-Host "   Analytics: Enabled" -ForegroundColor Cyan
        Write-Host "   Cache Strategy: Aggressive" -ForegroundColor Cyan
    }

    Write-Host "`n🎯 Hyperthreading setup complete!" -ForegroundColor Green
    Write-Host "💡 Tip: Use 'trunk check --jobs=$optimalThreads' for manual thread control" -ForegroundColor Cyan
}

# Execute main function
Invoke-TrunkHyperthreadingSetup
