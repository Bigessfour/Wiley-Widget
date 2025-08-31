# Simple Trunk Hyperthreading Environment Setup
# Sets up environment variables for hyperthreading support without executing trunk

param(
    [switch]$Quiet,
    [int]$ThreadCount = 0,  # 0 = auto-detect
    [string]$MemoryLimit = "4GB"
)

# Function to detect CPU information
function Get-CpuInfo {
    try {
        $cpuInfo = Get-WmiObject -Class Win32_Processor -ErrorAction Stop
        $logicalProcessors = $cpuInfo.NumberOfLogicalProcessors
        $physicalCores = $cpuInfo.NumberOfCores

        $hasHyperthreading = $logicalProcessors -gt $physicalCores

        return @{
            LogicalProcessors = $logicalProcessors
            PhysicalCores = $physicalCores
            HasHyperthreading = $hasHyperthreading
            HyperthreadingRatio = [math]::Round($logicalProcessors / $physicalCores, 2)
        }
    } catch {
        Write-Warning "Unable to detect CPU information, using defaults"
        return @{
            LogicalProcessors = 4
            PhysicalCores = 2
            HasHyperthreading = $true
            HyperthreadingRatio = 2.0
        }
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
            if (-not $Quiet) {
                Write-Host "🔄 Hyperthreading detected! Using $optimalThreads threads (logical processors)" -ForegroundColor Green
            }
        } else {
            # Use physical cores + some overhead for non-hyperthreading CPUs
            $optimalThreads = [math]::Min($CpuInfo.PhysicalCores + 2, $CpuInfo.LogicalProcessors)
            if (-not $Quiet) {
                Write-Host "⚡ Using $optimalThreads threads (optimized for physical cores)" -ForegroundColor Green
            }
        }
    } else {
        $optimalThreads = $RequestedThreads
        if (-not $Quiet) {
            Write-Host "🔧 Using requested thread count: $optimalThreads" -ForegroundColor Yellow
        }
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

    if (-not $Quiet) {
        Write-Host "✅ Trunk performance environment configured:" -ForegroundColor Green
        Write-Host "   Threads: $optimalThreads" -ForegroundColor Cyan
        Write-Host "   Memory Limit: $MemoryLimit" -ForegroundColor Cyan
        Write-Host "   Hyperthreading: Enabled" -ForegroundColor Cyan
        Write-Host "   Parallel Processing: Enabled" -ForegroundColor Cyan
        Write-Host "   CPU Affinity: Enabled" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "🎯 Environment ready! You can now run trunk commands with hyperthreading support." -ForegroundColor Magenta
        Write-Host "💡 Example: trunk check --all --ci" -ForegroundColor Cyan
    }

    return $optimalThreads
}

# Main execution
if (-not $Quiet) {
    Write-Host "🔧 Simple Trunk Hyperthreading Environment Setup" -ForegroundColor Magenta
    Write-Host "=================================================" -ForegroundColor Magenta
}

# Detect CPU information
$cpuInfo = Get-CpuInfo

if (-not $Quiet) {
    Write-Host "🖥️  CPU Information:" -ForegroundColor Blue
    Write-Host "   Physical Cores: $($cpuInfo.PhysicalCores)" -ForegroundColor White
    Write-Host "   Logical Processors: $($cpuInfo.LogicalProcessors)" -ForegroundColor White
    Write-Host "   Hyperthreading: $(if ($cpuInfo.HasHyperthreading) { '✅ Enabled' } else { '❌ Disabled' })" -ForegroundColor $(if ($cpuInfo.HasHyperthreading) { 'Green' } else { 'Red' })
    if ($cpuInfo.HasHyperthreading) {
        Write-Host "   Hyperthreading Ratio: $($cpuInfo.HyperthreadingRatio)x" -ForegroundColor Green
    }
    Write-Host ""
}

# Configure performance environment
$optimalThreads = Set-TrunkPerformanceEnvironment -CpuInfo $cpuInfo -RequestedThreads $ThreadCount -MemoryLimit $MemoryLimit

# Export to current session and future sessions
if (-not $Quiet) {
    Write-Host ""
    Write-Host "💾 Environment variables set for current session." -ForegroundColor Green
    Write-Host "🔄 To make permanent, add these to your PowerShell profile:" -ForegroundColor Yellow
    Write-Host "   `$env:TRUNK_NUM_THREADS = '$optimalThreads'" -ForegroundColor White
    Write-Host "   `$env:TRUNK_MEMORY_LIMIT = '$MemoryLimit'" -ForegroundColor White
    Write-Host "   `$env:TRUNK_ENABLE_PARALLEL = 'true'" -ForegroundColor White
    Write-Host "   `$env:TRUNK_HYPERTHREADING_ENABLED = 'true'" -ForegroundColor White
}
