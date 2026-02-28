#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Clean up old GitHub Actions workflow runs.

.DESCRIPTION
    This script helps manage GitHub Actions workflow run history by listing and
    optionally deleting old workflow runs based on various criteria.

.PARAMETER List
    List all workflow runs without deleting.

.PARAMETER OlderThanDays
    Delete runs older than this many days (default: 90).

.PARAMETER Status
    Filter by run status: completed, failed, cancelled, success, or all.

.PARAMETER WorkflowName
    Filter by specific workflow name (e.g., "CI/CD Dashboard").

.PARAMETER DryRun
    Preview what would be deleted without actually deleting.

.PARAMETER Force
    Skip confirmation prompts.

.EXAMPLE
    .\cleanup-workflow-runs.ps1 -List
    Lists all workflow runs.

.EXAMPLE
    .\cleanup-workflow-runs.ps1 -OlderThanDays 90 -DryRun
    Preview deletion of runs older than 90 days.

.EXAMPLE
    .\cleanup-workflow-runs.ps1 -OlderThanDays 30 -Status failed
    Delete failed runs older than 30 days.

.NOTES
    Requires: GitHub CLI (gh) installed and authenticated
    Repository: Bigessfour/Wiley-Widget
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$List,

    [Parameter()]
    [int]$OlderThanDays = 90,

    [Parameter()]
    [ValidateSet('all', 'completed', 'failed', 'cancelled', 'success')]
    [string]$Status = 'all',

    [Parameter()]
    [string]$WorkflowName,

    [Parameter()]
    [switch]$DryRun,

    [Parameter()]
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repo = "Bigessfour/Wiley-Widget"

# Check if gh CLI is installed
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error "GitHub CLI (gh) is not installed. Install from https://cli.github.com/"
    exit 1
}

# Check authentication
$authStatus = gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Not authenticated with GitHub CLI. Run: gh auth login"
    exit 1
}

Write-Host "🔍 Fetching workflow runs from $repo..." -ForegroundColor Cyan

# Fetch workflow runs
$runs = @()
$page = 1
$perPage = 100

do {
    Write-Host "  Fetching page $page..." -ForegroundColor Gray
    $result = gh api "repos/$repo/actions/runs?per_page=$perPage&page=$page" | ConvertFrom-Json
    
    if ($result.workflow_runs.Count -eq 0) {
        break
    }
    
    $runs += $result.workflow_runs
    $page++
    
    # Limit to prevent excessive API calls
    if ($page -gt 20) {
        Write-Warning "Stopped after 20 pages (2000 runs). Use filters to narrow down."
        break
    }
} while ($result.workflow_runs.Count -eq $perPage)

Write-Host "✓ Found $($runs.Count) workflow runs" -ForegroundColor Green

# Filter by age
$cutoffDate = (Get-Date).AddDays(-$OlderThanDays)
$filteredRuns = $runs | Where-Object {
    [datetime]$_.created_at -lt $cutoffDate
}

# Filter by status
if ($Status -ne 'all') {
    if ($Status -eq 'success') {
        $filteredRuns = $filteredRuns | Where-Object { $_.conclusion -eq 'success' }
    } elseif ($Status -eq 'failed') {
        $filteredRuns = $filteredRuns | Where-Object { $_.conclusion -eq 'failure' }
    } elseif ($Status -eq 'cancelled') {
        $filteredRuns = $filteredRuns | Where-Object { $_.conclusion -eq 'cancelled' }
    } elseif ($Status -eq 'completed') {
        $filteredRuns = $filteredRuns | Where-Object { $_.status -eq 'completed' }
    }
}

# Filter by workflow name
if ($WorkflowName) {
    $filteredRuns = $filteredRuns | Where-Object { $_.name -like "*$WorkflowName*" }
}

Write-Host "`n📊 Filtered Results:" -ForegroundColor Cyan
Write-Host "  Total runs: $($runs.Count)"
Write-Host "  Runs older than $OlderThanDays days: $($filteredRuns.Count)"
Write-Host "  Status filter: $Status"
if ($WorkflowName) {
    Write-Host "  Workflow filter: $WorkflowName"
}

# List mode
if ($List) {
    Write-Host "`n📋 Workflow Runs:" -ForegroundColor Cyan
    $filteredRuns | Sort-Object created_at -Descending | ForEach-Object {
        $age = ((Get-Date) - [datetime]$_.created_at).Days
        $statusIcon = switch ($_.conclusion) {
            'success' { '✓' }
            'failure' { '✗' }
            'cancelled' { '○' }
            default { '·' }
        }
        Write-Host "  $statusIcon [$($_.id)] $($_.name) - $($_.conclusion) ($age days old)" -ForegroundColor $(
            switch ($_.conclusion) {
                'success' { 'Green' }
                'failure' { 'Red' }
                'cancelled' { 'Yellow' }
                default { 'Gray' }
            }
        )
    }
    exit 0
}

# Deletion mode
if ($filteredRuns.Count -eq 0) {
    Write-Host "`n✓ No runs to delete." -ForegroundColor Green
    exit 0
}

Write-Host "`n🗑️  Runs to delete:" -ForegroundColor Yellow
$filteredRuns | Select-Object -First 10 | ForEach-Object {
    $age = ((Get-Date) - [datetime]$_.created_at).Days
    Write-Host "  • [$($_.id)] $($_.name) - $($_.conclusion) ($age days old)"
}

if ($filteredRuns.Count -gt 10) {
    Write-Host "  ... and $($filteredRuns.Count - 10) more runs"
}

if ($DryRun) {
    Write-Host "`n🔍 DRY RUN - No runs will be deleted." -ForegroundColor Cyan
    Write-Host "Remove -DryRun to actually delete these runs."
    exit 0
}

# Confirm deletion
if (-not $Force) {
    Write-Host "`n⚠️  WARNING: This will permanently delete $($filteredRuns.Count) workflow runs!" -ForegroundColor Yellow
    $confirmation = Read-Host "Type 'DELETE' to confirm"
    
    if ($confirmation -ne 'DELETE') {
        Write-Host "❌ Cancelled." -ForegroundColor Red
        exit 0
    }
}

# Delete runs
Write-Host "`n🗑️  Deleting workflow runs..." -ForegroundColor Yellow
$deleted = 0
$failed = 0

foreach ($run in $filteredRuns) {
    try {
        Write-Host "  Deleting run $($run.id) ($($run.name))..." -NoNewline
        gh api --method DELETE "repos/$repo/actions/runs/$($run.id)" | Out-Null
        Write-Host " ✓" -ForegroundColor Green
        $deleted++
        
        # Rate limiting: sleep briefly between deletions
        Start-Sleep -Milliseconds 100
    }
    catch {
        Write-Host " ✗ Failed: $_" -ForegroundColor Red
        $failed++
    }
}

Write-Host "`n✓ Deletion complete!" -ForegroundColor Green
Write-Host "  Deleted: $deleted"
if ($failed -gt 0) {
    Write-Host "  Failed: $failed" -ForegroundColor Red
}

# Show remaining runs
$remainingRuns = gh api "repos/$repo/actions/runs?per_page=1" | ConvertFrom-Json
Write-Host "`n📊 Remaining workflow runs: ~$($remainingRuns.total_count)" -ForegroundColor Cyan
