# Wiley Widget - Merge Queue Management Script
# PowerShell script for managing GitHub Merge Queue operations

param(
    [switch]$CheckStatus,
    [switch]$AddToQueue,
    [int]$PullRequestNumber,
    [switch]$RemoveFromQueue,
    [switch]$MonitorQueue,
    [switch]$GenerateReport,
    [int]$Days = 7
)

Write-Host "🔄 Wiley Widget - Merge Queue Manager" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Function to check merge queue status
function Get-MergeQueueStatus {
    Write-Host "`n📊 Merge Queue Status:" -ForegroundColor Yellow

    try {
        # Get open pull requests
        $prs = gh pr list --state open --json number,title,mergeStateStatus,headRefName,updatedAt

        if ($prs) {
            $queueItems = $prs | Where-Object { $_.mergeStateStatus -eq "QUEUED" }
            $readyItems = $prs | Where-Object { $_.mergeStateStatus -eq "CLEAN" }

            Write-Host "Queued PRs: $($queueItems.Count)" -ForegroundColor Green
            Write-Host "Ready to Queue: $($readyItems.Count)" -ForegroundColor Blue

            if ($queueItems.Count -gt 0) {
                Write-Host "`nQueued Pull Requests:" -ForegroundColor Cyan
                foreach ($pr in $queueItems) {
                    Write-Host "  #$($pr.number): $($pr.title)" -ForegroundColor White
                    Write-Host "    Branch: $($pr.headRefName)" -ForegroundColor Gray
                    Write-Host "    Updated: $($pr.updatedAt)" -ForegroundColor Gray
                }
            }

            if ($readyItems.Count -gt 0) {
                Write-Host "`nReady to Add to Queue:" -ForegroundColor Cyan
                foreach ($pr in $readyItems) {
                    Write-Host "  #$($pr.number): $($pr.title)" -ForegroundColor White
                }
            }
        } else {
            Write-Host "No open pull requests found" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "Unable to check merge queue status" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Function to add PR to merge queue
function Add-PullRequestToQueue {
    param([int]$PRNumber)

    if (-not $PRNumber) {
        Write-Host "❌ Pull request number required. Use -PullRequestNumber parameter." -ForegroundColor Red
        return
    }

    Write-Host "`n➕ Adding PR #$PRNumber to Merge Queue:" -ForegroundColor Yellow

    try {
        # Check if PR is ready
        $pr = gh pr view $PRNumber --json mergeStateStatus,mergeable

        if ($pr.mergeStateStatus -eq "CLEAN" -and $pr.mergeable -eq "MERGEABLE") {
            Write-Host "✅ PR is ready for merge queue" -ForegroundColor Green

            # Add to queue (this would typically be done via GitHub UI or API)
            Write-Host "📝 To add to merge queue:" -ForegroundColor Cyan
            Write-Host "  1. Go to PR #$PRNumber on GitHub" -ForegroundColor White
            Write-Host "  2. Click 'Add to merge queue' button" -ForegroundColor White
            Write-Host "  3. Monitor the merge queue status" -ForegroundColor White
        } else {
            Write-Host "❌ PR is not ready for merge queue" -ForegroundColor Red
            Write-Host "Status: $($pr.mergeStateStatus)" -ForegroundColor Red
            Write-Host "Mergeable: $($pr.mergeable)" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "Unable to check PR status" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Function to monitor merge queue
function Watch-MergeQueue {
    Write-Host "`n👀 Monitoring Merge Queue (press Ctrl+C to stop):" -ForegroundColor Yellow

    $iteration = 0
    while ($true) {
        $iteration++
        Write-Host "`n--- Iteration $iteration ---" -ForegroundColor Gray

        try {
            $prs = gh pr list --state open --json number,title,mergeStateStatus,updatedAt
            $queuedCount = ($prs | Where-Object { $_.mergeStateStatus -eq "QUEUED" }).Count
            $cleanCount = ($prs | Where-Object { $_.mergeStateStatus -eq "CLEAN" }).Count

            Write-Host "Queued: $queuedCount | Ready: $cleanCount" -ForegroundColor Green

            if ($queuedCount -gt 0) {
                Write-Host "Active queue items:" -ForegroundColor Cyan
                $prs | Where-Object { $_.mergeStateStatus -eq "QUEUED" } | ForEach-Object {
                    Write-Host "  #$($_.number): $($_.title)" -ForegroundColor White
                }
            }
        }
        catch {
            Write-Host "Error checking queue status: $($_.Exception.Message)" -ForegroundColor Red
        }

        Start-Sleep -Seconds 30  # Check every 30 seconds
    }
}

# Function to generate merge queue report
function New-MergeQueueReport {
    param([int]$DaysBack = 7)

    Write-Host "`n📋 Generating Merge Queue Report (Last $DaysBack days):" -ForegroundColor Yellow

    try {
        # Get recent merge activity
        $merges = git log --oneline --since="$DaysBack days ago" --grep="Merge pull request"

        # Get PR data
        $prs = gh pr list --state closed --limit 50 --json number,title,mergedAt,mergeCommit

        $report = @{
            Period = "$DaysBack days"
            GeneratedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            Summary = @{
                TotalMerges = ($merges | Measure-Object).Count
                SuccessfulMerges = ($prs | Where-Object { $_.mergedAt }).Count
                AverageMergeTime = "N/A"  # Would need more complex calculation
            }
            RecentMerges = @()
        }

        # Add recent merge details
        foreach ($merge in ($merges | Select-Object -First 10)) {
            $report.RecentMerges += @{
                Commit = $merge.Split(' ')[0]
                Message = $merge.Substring($merge.IndexOf(' ') + 1)
            }
        }

        # Save report
        $reportPath = "merge-queue-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
        $report | ConvertTo-Json -Depth 10 | Out-File $reportPath -Encoding UTF8

        Write-Host "Report saved to: $reportPath" -ForegroundColor Green

        # Display summary
        Write-Host "`n📊 Summary:" -ForegroundColor Cyan
        Write-Host "Total Merges: $($report.Summary.TotalMerges)" -ForegroundColor White
        Write-Host "Successful Merges: $($report.Summary.SuccessfulMerges)" -ForegroundColor White

    }
    catch {
        Write-Host "Unable to generate report" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Main execution logic
if ($CheckStatus) {
    Get-MergeQueueStatus
}

if ($AddToQueue) {
    Add-PullRequestToQueue -PRNumber $PullRequestNumber
}

if ($MonitorQueue) {
    Watch-MergeQueue
}

if ($GenerateReport) {
    New-MergeQueueReport -DaysBack $Days
}

# Default behavior - show help
if (-not ($CheckStatus -or $AddToQueue -or $MonitorQueue -or $GenerateReport)) {
    Write-Host "`n💡 Available Commands:" -ForegroundColor Cyan
    Write-Host "====================" -ForegroundColor Cyan
    Write-Host "-CheckStatus        : Show current merge queue status" -ForegroundColor White
    Write-Host "-AddToQueue         : Add a PR to merge queue (requires -PullRequestNumber)" -ForegroundColor White
    Write-Host "-PullRequestNumber  : Specify PR number for queue operations" -ForegroundColor White
    Write-Host "-MonitorQueue       : Continuously monitor queue status" -ForegroundColor White
    Write-Host "-GenerateReport     : Create merge queue performance report" -ForegroundColor White
    Write-Host "-Days <number>      : Number of days for reports (default: 7)" -ForegroundColor White

    Write-Host "`n📖 Examples:" -ForegroundColor Yellow
    Write-Host "=============" -ForegroundColor Yellow
    Write-Host ".\merge-queue-manager.ps1 -CheckStatus" -ForegroundColor White
    Write-Host ".\merge-queue-manager.ps1 -AddToQueue -PullRequestNumber 123" -ForegroundColor White
    Write-Host ".\merge-queue-manager.ps1 -MonitorQueue" -ForegroundColor White
    Write-Host ".\merge-queue-manager.ps1 -GenerateReport -Days 30" -ForegroundColor White

    Write-Host "`n🔗 GitHub Merge Queue:" -ForegroundColor Green
    Write-Host "https://github.com/Bigessfour/Wiley-Widget/queue" -ForegroundColor White
}

Write-Host "`n✅ Merge Queue Manager Complete!" -ForegroundColor Green
