# Wiley Widget CI/CD Success Rate Monitor
# Tracks pipeline performance and provides insights for 90% success rate goal

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Daily", "Weekly", "Monthly", "All")]
    [string]$ReportPeriod = "Daily",

    [Parameter(Mandatory=$false)]
    [switch]$GenerateReport,

    [Parameter(Mandatory=$false)]
    [switch]$SendAlerts,

    [Parameter(Mandatory=$false)]
    [switch]$OptimizePipeline,

    [Parameter(Mandatory=$false)]
    [int]$SuccessThreshold = 90
)

$ErrorActionPreference = "Stop"

# Configuration
$projectRoot = Split-Path -Parent $PSScriptRoot
$metricsFile = Join-Path $projectRoot "cicd-metrics.json"
$alertsFile = Join-Path $projectRoot "cicd-alerts.json"

# Initialize metrics storage
$metrics = @{
    runs = @()
    success_rate_trend = @()
    failure_patterns = @{}
    performance_metrics = @{}
    last_updated = $null
}

# Load existing metrics
if (Test-Path $metricsFile) {
    try {
        $metrics = Get-Content $metricsFile | ConvertFrom-Json
    } catch {
        Write-Host "Warning: Could not load existing metrics, starting fresh" -ForegroundColor Yellow
    }
}

function Write-MonitorLog {
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

function Add-PipelineRun {
    param(
        [string]$RunId,
        [string]$Status,
        [DateTime]$StartTime,
        [DateTime]$EndTime,
        [hashtable]$JobResults,
        [hashtable]$Metrics
    )

    $run = @{
        run_id = $RunId
        status = $Status
        start_time = $StartTime.ToString("o")
        end_time = $EndTime.ToString("o")
        duration_seconds = ($EndTime - $StartTime).TotalSeconds
        job_results = $JobResults
        metrics = $Metrics
        timestamp = (Get-Date).ToString("o")
    }

    $metrics.runs += $run
    $metrics.last_updated = (Get-Date).ToString("o")

    # Keep only last 100 runs
    if ($metrics.runs.Count -gt 100) {
        $metrics.runs = $metrics.runs | Select-Object -Last 100
    }

    Write-MonitorLog "Added pipeline run: $RunId ($Status)" "INFO"
}

function Calculate-SuccessRate {
    param([int]$Days = 7)

    $recentRuns = $metrics.runs | Where-Object {
        [DateTime]::Parse($_.timestamp) -gt (Get-Date).AddDays(-$Days)
    }

    if ($recentRuns.Count -eq 0) {
        return 0
    }

    $successfulRuns = ($recentRuns | Where-Object { $_.status -eq "success" }).Count
    $successRate = [math]::Round(($successfulRuns / $recentRuns.Count) * 100, 2)

    Write-MonitorLog "Success rate over last $Days days: $successRate% ($successfulRuns/$($recentRuns.Count) runs)" "INFO"

    return $successRate
}

function Analyze-FailurePatterns {
    $failedRuns = $metrics.runs | Where-Object { $_.status -ne "success" }

    $failurePatterns = @{}

    foreach ($run in $failedRuns) {
        foreach ($job in $run.job_results.GetEnumerator()) {
            if ($job.Value -ne "success") {
                $jobName = $job.Key
                if (-not $failurePatterns.ContainsKey($jobName)) {
                    $failurePatterns[$jobName] = 0
                }
                $failurePatterns[$jobName]++
            }
        }
    }

    $metrics.failure_patterns = $failurePatterns

    Write-MonitorLog "Failure pattern analysis complete" "INFO"
    foreach ($pattern in $failurePatterns.GetEnumerator() | Sort-Object Value -Descending) {
        Write-MonitorLog "  $($pattern.Key): $($pattern.Value) failures" "WARNING"
    }
}

function Generate-PerformanceReport {
    param([string]$Period = "Daily")

    Write-MonitorLog "Generating $Period performance report..." "INFO"

    $dateFilter = switch ($Period) {
        "Daily" { (Get-Date).AddDays(-1) }
        "Weekly" { (Get-Date).AddDays(-7) }
        "Monthly" { (Get-Date).AddDays(-30) }
        default { (Get-Date).AddYears(-1) }
    }

    $periodRuns = $metrics.runs | Where-Object {
        [DateTime]::Parse($_.timestamp) -gt $dateFilter
    }

    $report = @{
        period = $Period
        total_runs = $periodRuns.Count
        successful_runs = ($periodRuns | Where-Object { $_.status -eq "success" }).Count
        failed_runs = ($periodRuns | Where-Object { $_.status -ne "success" }).Count
        success_rate = 0
        average_duration = 0
        failure_patterns = $metrics.failure_patterns
        recommendations = @()
        generated_at = (Get-Date).ToString("o")
    }

    if ($report.total_runs -gt 0) {
        $report.success_rate = [math]::Round(($report.successful_runs / $report.total_runs) * 100, 2)
        $totalDuration = ($periodRuns | Measure-Object -Property duration_seconds -Sum).Sum
        $report.average_duration = [math]::Round($totalDuration / $report.total_runs, 2)
    }

    # Generate recommendations
    if ($report.success_rate -lt $SuccessThreshold) {
        $report.recommendations += "Success rate ($($report.success_rate)%) is below target ($SuccessThreshold%)"

        if ($report.failure_patterns.Count -gt 0) {
            $topFailure = $report.failure_patterns.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1
            $report.recommendations += "Focus on fixing $($topFailure.Key) job (failed $($topFailure.Value) times)"
        }

        if ($report.average_duration -gt 600) { # 10 minutes
            $report.recommendations += "Pipeline is slow ($($report.average_duration)s avg). Consider optimization"
        }
    } else {
        $report.recommendations += "Success rate target met! Current: $($report.success_rate)%"
    }

    # Export report
    $reportFile = Join-Path $projectRoot "cicd-performance-report-$Period.json"
    $report | ConvertTo-Json -Depth 10 | Out-File $reportFile -Encoding UTF8

    Write-MonitorLog "Performance report saved to: $reportFile" "SUCCESS"

    return $report
}

function Send-Alerts {
    param([hashtable]$Report)

    if ($Report.success_rate -lt $SuccessThreshold) {
        Write-MonitorLog "🚨 ALERT: Success rate below threshold!" "ERROR"
        Write-MonitorLog "Current: $($Report.success_rate)% | Target: $SuccessThreshold%" "ERROR"

        # Create alert record
        $alert = @{
            timestamp = (Get-Date).ToString("o")
            type = "success_rate_below_threshold"
            current_rate = $Report.success_rate
            threshold = $SuccessThreshold
            period = $Report.period
            recommendations = $Report.recommendations
        }

        # Load existing alerts
        $alerts = @()
        if (Test-Path $alertsFile) {
            try {
                $alerts = Get-Content $alertsFile | ConvertFrom-Json
            } catch {
                Write-MonitorLog "Warning: Could not load existing alerts" "WARNING"
            }
        }

        $alerts += $alert

        # Keep only last 50 alerts
        if ($alerts.Count -gt 50) {
            $alerts = $alerts | Select-Object -Last 50
        }

        $alerts | ConvertTo-Json -Depth 10 | Out-File $alertsFile -Encoding UTF8

        Write-MonitorLog "Alert recorded and saved" "WARNING"
    } else {
        Write-MonitorLog "✅ Success rate is within acceptable range" "SUCCESS"
    }
}

function Optimize-Pipeline {
    Write-MonitorLog "🔧 Analyzing pipeline for optimization opportunities..." "INFO"

    $optimizations = @()

    # Analyze build times
    $recentRuns = $metrics.runs | Where-Object {
        [DateTime]::Parse($_.timestamp) -gt (Get-Date).AddDays(-7)
    }

    if ($recentRuns.Count -gt 0) {
        $avgDuration = ($recentRuns | Measure-Object -Property duration_seconds -Average).Average

        if ($avgDuration -gt 900) { # 15 minutes
            $optimizations += "Pipeline is slow ($([math]::Round($avgDuration/60, 1))min avg). Consider parallel jobs"
        }

        # Check for flaky tests
        $testFailures = $metrics.failure_patterns.GetEnumerator() | Where-Object { $_.Key -like "*test*" }
        if ($testFailures.Count -gt 0) {
            $optimizations += "Detected flaky tests. Consider implementing retry logic"
        }

        # Check cache effectiveness
        $cacheMetrics = $recentRuns | Where-Object { $_.metrics.ContainsKey("cache_hit") }
        if ($cacheMetrics.Count -gt 0) {
            $cacheHitRate = ($cacheMetrics | Where-Object { $_.metrics.cache_hit -eq $true } | Measure-Object).Count / $cacheMetrics.Count * 100
            if ($cacheHitRate -lt 80) {
                $optimizations += "Low cache hit rate ($([math]::Round($cacheHitRate, 1))%). Optimize caching strategy"
            }
        }
    }

    if ($optimizations.Count -eq 0) {
        Write-MonitorLog "✅ No optimization opportunities identified" "SUCCESS"
    } else {
        Write-MonitorLog "📋 Optimization recommendations:" "INFO"
        foreach ($opt in $optimizations) {
            Write-MonitorLog "  • $opt" "WARNING"
        }
    }

    return $optimizations
}

# Main execution
Write-MonitorLog "📊 Wiley Widget CI/CD Success Rate Monitor" "INFO"
Write-MonitorLog "==========================================" "INFO"

# Analyze failure patterns
Analyze-FailurePatterns

# Calculate current success rate
$currentSuccessRate = Calculate-SuccessRate -Days 7
Write-MonitorLog "Current 7-day success rate: $currentSuccessRate%" $(if ($currentSuccessRate -ge $SuccessThreshold) { "SUCCESS" } else { "WARNING" })

# Generate report if requested
if ($GenerateReport) {
    $report = Generate-PerformanceReport -Period $ReportPeriod
    Write-MonitorLog "Report generated for period: $($report.period)" "SUCCESS"
    Write-MonitorLog "Success rate: $($report.success_rate)%" "INFO"
    Write-MonitorLog "Total runs: $($report.total_runs)" "INFO"
}

# Send alerts if requested
if ($SendAlerts) {
    if (-not $GenerateReport) {
        $report = Generate-PerformanceReport -Period "Daily"
    }
    Send-Alerts -Report $report
}

# Optimize pipeline if requested
if ($OptimizePipeline) {
    $optimizations = Optimize-Pipeline
    if ($optimizations.Count -gt 0) {
        Write-MonitorLog "Optimization analysis complete" "INFO"
    }
}

# Save metrics
$metrics | ConvertTo-Json -Depth 10 | Out-File $metricsFile -Encoding UTF8
Write-MonitorLog "Metrics saved to: $metricsFile" "SUCCESS"

# Display summary
Write-MonitorLog "`n📈 Summary:" "INFO"
Write-MonitorLog "  • Total runs tracked: $($metrics.runs.Count)" "INFO"
Write-MonitorLog "  • Current success rate: $currentSuccessRate%" "INFO"
Write-MonitorLog "  • Target threshold: $SuccessThreshold%" "INFO"

if ($currentSuccessRate -ge $SuccessThreshold) {
    Write-MonitorLog "  ✅ Success rate target ACHIEVED!" "SUCCESS"
} else {
    $gap = $SuccessThreshold - $currentSuccessRate
    Write-MonitorLog "  ⚠️ $gap% below target. Focus on reliability improvements." "WARNING"
}

Write-MonitorLog "`nMonitor complete. Use -GenerateReport, -SendAlerts, or -OptimizePipeline for more actions." "INFO"
