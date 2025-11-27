#Requires -Version 7.5
<#
.SYNOPSIS
    Monitor dashboard CI/CD pipeline with GitHub MCP integration
.DESCRIPTION
    Monitors the dashboard feature branch CI runs using GitHub CLI and MCP tools.
    Provides real-time status updates and automatic failure analysis.
.PARAMETER Branch
    Branch name to monitor (default: feature/dashboard-production-ready)
.PARAMETER Watch
    Enable continuous monitoring mode
.PARAMETER FailedOnly
    Only show failed runs
.EXAMPLE
    .\monitor-dashboard-ci.ps1 -Watch
#>

[CmdletBinding()]
param(
    [string]$Branch = "feature/dashboard-production-ready",
    [switch]$Watch,
    [switch]$FailedOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Status {
    param([string]$Message, [string]$Type = "Info")
    $color = switch ($Type) {
        "Success" { "Green" }
        "Error" { "Red" }
        "Warning" { "Yellow" }
        default { "Cyan" }
    }
    Write-Host "[$Type] $Message" -ForegroundColor $color
}

function Get-DashboardWorkflowRuns {
    Write-Status "Fetching workflow runs for branch: $Branch"
    
    $runs = gh run list `
        --workflow="ci-optimized.yml" `
        --branch=$Branch `
        --limit=5 `
        --json=databaseId,status,conclusion,createdAt,headBranch,displayTitle,workflowName
    
    if ($LASTEXITCODE -ne 0) {
        Write-Status "Failed to fetch workflow runs" "Error"
        return $null
    }
    
    return $runs | ConvertFrom-Json
}

function Get-FailedJobLogs {
    param([string]$RunId)
    
    Write-Status "Analyzing failed jobs for run: $RunId"
    
    $jobs = gh run view $RunId --json=jobs | ConvertFrom-Json
    
    $failedJobs = $jobs.jobs | Where-Object { $_.conclusion -eq "failure" }
    
    if ($failedJobs) {
        Write-Status "Found $($failedJobs.Count) failed job(s)" "Warning"
        
        foreach ($job in $failedJobs) {
            Write-Host "`n--- Failed Job: $($job.name) ---" -ForegroundColor Red
            
            $logContent = gh run view $RunId --log-failed 2>&1
            
            # Extract dashboard-specific errors
            $dashboardErrors = $logContent | Select-String -Pattern "Dashboard|Export|Syncfusion|DashboardForm" -Context 2
            
            if ($dashboardErrors) {
                Write-Host "Dashboard-specific errors found:" -ForegroundColor Yellow
                $dashboardErrors | ForEach-Object { Write-Host $_.Line }
            }
        }
    } else {
        Write-Status "No failed jobs found" "Success"
    }
}

function Show-RunSummary {
    param($Runs)
    
    Write-Host "`n=== Dashboard CI/CD Summary ===" -ForegroundColor Cyan
    Write-Host "Branch: $Branch" -ForegroundColor White
    Write-Host "Total Runs: $($Runs.Count)`n" -ForegroundColor White
    
    foreach ($run in $Runs) {
        $statusIcon = switch ($run.conclusion) {
            "success" { "✅" }
            "failure" { "❌" }
            "cancelled" { "⚠️" }
            default { "⏳" }
        }
        
        $color = switch ($run.conclusion) {
            "success" { "Green" }
            "failure" { "Red" }
            "cancelled" { "Yellow" }
            default { "Gray" }
        }
        
        Write-Host "$statusIcon Run #$($run.databaseId) - $($run.status) ($($run.conclusion))" -ForegroundColor $color
        Write-Host "   Title: $($run.displayTitle)" -ForegroundColor Gray
        Write-Host "   Created: $($run.createdAt)" -ForegroundColor Gray
        Write-Host ""
    }
}

function Start-WatchMode {
    Write-Status "Starting watch mode (Ctrl+C to exit)" "Info"
    
    while ($true) {
        Clear-Host
        Write-Host "🔄 Dashboard CI Monitor - $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Cyan
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
        
        $runs = Get-DashboardWorkflowRuns
        
        if ($runs) {
            Show-RunSummary -Runs $runs
            
            # Check latest run for failures
            $latestRun = $runs[0]
            if ($latestRun.conclusion -eq "failure") {
                Write-Status "Latest run failed! Analyzing..." "Error"
                Get-FailedJobLogs -RunId $latestRun.databaseId
            }
        }
        
        Write-Host "`nRefreshing in 30 seconds..." -ForegroundColor DarkGray
        Start-Sleep -Seconds 30
    }
}

# Main execution
try {
    Write-Host @"
╔═══════════════════════════════════════════════════════════╗
║         Dashboard CI/CD Monitor (GitHub MCP)              ║
║         Branch: $Branch                                   ║
╚═══════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

    # Verify GitHub CLI is available
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Status "GitHub CLI (gh) not found. Please install: https://cli.github.com/" "Error"
        exit 1
    }
    
    # Verify authentication
    $authStatus = gh auth status 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Status "GitHub CLI not authenticated. Run: gh auth login" "Error"
        exit 1
    }
    
    if ($Watch) {
        Start-WatchMode
    } else {
        $runs = Get-DashboardWorkflowRuns
        
        if ($runs) {
            if ($FailedOnly) {
                $runs = $runs | Where-Object { $_.conclusion -eq "failure" }
            }
            
            Show-RunSummary -Runs $runs
            
            # Analyze failures if requested
            $failedRuns = $runs | Where-Object { $_.conclusion -eq "failure" }
            if ($failedRuns) {
                Write-Status "Found $($failedRuns.Count) failed run(s). Analyzing..." "Warning"
                foreach ($run in $failedRuns) {
                    Get-FailedJobLogs -RunId $run.databaseId
                }
            }
        } else {
            Write-Status "No workflow runs found for branch: $Branch" "Warning"
        }
    }
    
    Write-Status "Monitor complete" "Success"
}
catch {
    Write-Status "Error: $_" "Error"
    Write-Status "Stack trace: $($_.ScriptStackTrace)" "Error"
    exit 1
}
