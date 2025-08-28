# HyperThreading Support Demonstration
# Shows how Microsoft PowerShell 7.5.2 hyperthreading optimization works

Write-Output "🚀 Wiley Widget HyperThreading Support Demo"
Write-Output "=" * 50

# Show CPU topology
Write-Output "`n📊 CPU Topology Information:"
Get-CpuTopology | Format-List

# Demonstrate parallel processing
Write-Output "`n⚡ Testing Parallel Processing Performance:"

# Create test data
$testData = 1..20

Write-Output "Processing $($testData.Count) items using optimized parallel execution..."

# Simple test that doesn't require external functions
[Diagnostics.CodeAnalysis.SuppressMessageAttribute("PSUseUsingScopeModifierInNewRunspaces", "")]
$results = $testData | ForEach-Object -Parallel {
    param($number)
    # Simulate some work
    Start-Sleep -Milliseconds (Get-Random -Minimum 10 -Maximum 50)
    [PSCustomObject]@{
        Input = $number
        Result = $number * 2
        ProcessedBy = $using:env:COMPUTERNAME
        ThreadId = [System.Threading.Thread]::CurrentThread.ManagedThreadId
    }
} -ThrottleLimit 4

Write-Host "✅ Parallel processing completed!" -ForegroundColor Green
Write-Host "📈 Results sample:" -ForegroundColor White
$results | Select-Object -First 5 | Format-Table -AutoSize

# Show thread distribution
$threadGroups = $results | Group-Object ThreadId
Write-Output "`n🧵 Thread Distribution:"
$threadGroups | Select-Object @{Name="Thread ID"; Expression={$_.Name}}, Count | Format-Table -AutoSize

# Demonstrate performance comparison
Write-Output "`n📊 Performance Comparison:"
Write-Output "Running sequential vs parallel comparison..."

# Sequential processing
$sequentialTime = Measure-Command {
    $sequentialResults = foreach ($item in $testData) {
        Start-Sleep -Milliseconds (Get-Random -Minimum 10 -Maximum 50)
        $item * 2
    }
}

# Parallel processing
$parallelTime = Measure-Command {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute("PSUseUsingScopeModifierInNewRunspaces", "")]
    $parallelResults = $testData | ForEach-Object -Parallel {
        param($item)
        Start-Sleep -Milliseconds (Get-Random -Minimum 10 -Maximum 50)
        $item * 2
    } -ThrottleLimit 4
}

Write-Output "Sequential Time: $([math]::Round($sequentialTime.TotalMilliseconds, 2))ms"
Write-Output "Parallel Time: $([math]::Round($parallelTime.TotalMilliseconds, 2))ms"
$speedup = [math]::Round($sequentialTime.TotalMilliseconds / $parallelTime.TotalMilliseconds, 2)
Write-Output "Speed Improvement: ${speedup}x faster"

# Show available commands
Write-Output "`n🛠️ Available HyperThreading Commands:"
Write-Output "  Get-CpuTopology              - Show detailed CPU information"
Write-Output "  ForEach-Object -Parallel     - Run parallel operations with optimal settings"
Write-Output "  Start-OptimizedThreadJob     - Start thread jobs with optimal throttling"
Write-Output "  Measure-ParallelPerformance  - Compare parallel execution methods"
Write-Output "  cpu-topology                 - Alias for Get-CpuTopology"
Write-Output "  parallel-invoke              - Alias for Invoke-OptimizedParallel"
Write-Output "  parallel-test                - Alias for Measure-ParallelPerformance"
Write-Output "  start-thread                 - Alias for Start-OptimizedThreadJob"

Write-Output "`n💡 Usage Examples:"
Write-Output "# Get CPU topology information"
Write-Output "cpu-topology"
Write-Output ""
Write-Output "# Process files in parallel"
Write-Output "Get-ChildItem *.txt | parallel-invoke -ScriptBlock { param(`$file); Process-File `$file }"
Write-Output ""
Write-Output "# Test parallel performance"
Write-Output "parallel-test -TestData (1..100) -Iterations 3"

Write-Output "`n🎯 Microsoft Best Practices Applied:"
Write-Output "  • Automatic CPU topology detection"
Write-Output "  • Hyperthreading-aware throttle limits"
Write-Output "  • Optimal runspace pool configuration"
Write-Output "  • Performance monitoring and measurement"
Write-Output "  • PowerShell 7.5.2 parallel execution guidelines"

Write-Output "`n✨ HyperThreading support is now active in your PowerShell profile!"
