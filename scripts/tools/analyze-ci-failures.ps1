#!/usr/bin/env pwsh
<#
.SYNOPSIS
    CI Failure Analysis and Auto-Fix Script
.DESCRIPTION
    Analyzes local CI pipeline failures and applies automated fixes where possible.
    Integrates with Trunk CLI and local development tools.
.PARAMETER WorkflowRunId
    Local CI run ID to analyze (optional)
.PARAMETER AutoFix
    Automatically apply fixes when possible
.PARAMETER Verbose
    Enable verbose output
.EXAMPLE
    .\analyze-ci-failures.ps1 -AutoFix
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$WorkflowRunId,

    [Parameter(Mandatory = $false)]
    [switch]$AutoFix,

    [Parameter(Mandatory = $false)]
    [switch]$Verbose
)

#Requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Configuration
$script:Config = @{
    RepoOwner = "Bigessfour"
    RepoName = "Wiley-Widget"
    MaxRetries = 3
    RetryDelay = 5
}

# Logging function
function Write-Log {
    param(
        [string]$Message,
        [string]$Level = "INFO"
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"

    switch ($Level) {
        "ERROR" { Write-Host $logMessage -ForegroundColor Red }
        "WARN"  { Write-Host $logMessage -ForegroundColor Yellow }
        "INFO"  { Write-Host $logMessage -ForegroundColor Green }
        "DEBUG" { if ($Verbose) { Write-Host $logMessage -ForegroundColor Gray } }
        default { Write-Host $logMessage }
    }
}

# Get latest workflow run if not specified
function Get-LatestWorkflowRun {
    try {
        Write-Log "Getting latest workflow run..." -Level DEBUG

        $runInfo = gh run list --limit=1 --json=databaseId,conclusion,status,headSha --jq='.[0]' | ConvertFrom-Json

        if ($runInfo) {
            Write-Log "Found workflow run: $($runInfo.databaseId) ($($runInfo.conclusion))" -Level INFO
            return $runInfo
        }
    }
    catch {
        Write-Log "Failed to get workflow run: $($_.Exception.Message)" -Level ERROR
    }

    return $null
}

# Analyze workflow failure
function Analyze-WorkflowFailure {
    param([object]$RunInfo)

    Write-Log "Analyzing workflow failure for run $($RunInfo.databaseId)..." -Level INFO

    $failureCategories = @()

    try {
        # Get job logs
        $jobs = gh run view $RunInfo.databaseId --json=jobs --jq='.jobs[]' | ConvertFrom-Json

        foreach ($job in $jobs) {
            if ($job.conclusion -eq "failure") {
                Write-Log "Analyzing failed job: $($job.name)" -Level INFO

                # Get job logs
                $logContent = gh run view --job=$($job.databaseId) --log 2>$null

                # Categorize failure
                $category = Categorize-Failure -JobName $job.name -LogContent $logContent
                if ($category) {
                    $failureCategories += $category
                }
            }
        }
    }
    catch {
        Write-Log "Failed to analyze workflow: $($_.Exception.Message)" -Level ERROR
    }

    return $failureCategories
}

# Categorize failure type
function Categorize-Failure {
    param(
        [string]$JobName,
        [string]$LogContent
    )

    $category = @{
        JobName = $JobName
        Type = "Unknown"
        Severity = "Medium"
        AutoFixable = $false
        FixCommand = ""
        Description = ""
    }

    # Build/Test failures
    if ($LogContent -match "error CS\d+") {
        $category.Type = "Build"
        $category.Description = "C# compilation errors detected"
        $category.AutoFixable = $true
        $category.FixCommand = "dotnet build --no-restore"
    }
    elseif ($LogContent -match "MSB\d+") {
        $category.Type = "Build"
        $category.Description = "MSBuild errors detected"
        $category.AutoFixable = $true
        $category.FixCommand = "dotnet restore; dotnet build"
    }
    # Test failures
    elseif ($LogContent -match "Failed.*test") {
        $category.Type = "Test"
        $category.Description = "Unit test failures detected"
        $category.Severity = "High"
    }
    # Trunk quality issues
    elseif ($LogContent -match "trunk.*check") {
        $category.Type = "Quality"
        $category.Description = "Code quality issues detected"
        $category.AutoFixable = $true
        $category.FixCommand = "trunk check --fix"
    }
    # Security issues
    elseif ($LogContent -match "(gitleaks|trufflehog|security)") {
        $category.Type = "Security"
        $category.Description = "Security vulnerabilities detected"
        $category.Severity = "Critical"
    }
    # Docker/CSX issues
    elseif ($LogContent -match "docker.*error") {
        $category.Type = "Container"
        $category.Description = "Docker/CSX test failures"
        $category.AutoFixable = $true
        $category.FixCommand = "docker system prune -f; docker build -t wiley-widget/csx-mcp:ci -f docker/Dockerfile.csx-tests ."
    }

    return $category
}

# Apply automated fixes
function Apply-AutoFix {
    param([array]$FailureCategories)

    Write-Log "Applying automated fixes..." -Level INFO

    $fixesApplied = @()

    foreach ($category in $FailureCategories) {
        if ($category.AutoFixable -and $category.FixCommand) {
            Write-Log "Applying fix for $($category.Type): $($category.FixCommand)" -Level INFO

            try {
                # Execute fix command
                $result = Invoke-Expression $category.FixCommand 2>&1

                if ($LASTEXITCODE -eq 0) {
                    Write-Log "Fix applied successfully for $($category.Type)" -Level INFO
                    $fixesApplied += $category
                }
                else {
                    Write-Log "Fix failed for $($category.Type): $result" -Level WARN
                }
            }
            catch {
                Write-Log "Exception during fix application: $($_.Exception.Message)" -Level ERROR
            }
        }
    }

    return $fixesApplied
}

# Generate failure report
function Generate-FailureReport {
    param(
        [object]$RunInfo,
        [array]$FailureCategories,
        [array]$FixesApplied
    )

    $report = @"
# CI Failure Analysis Report

## Workflow Run: $($RunInfo.databaseId)
- Status: $($RunInfo.conclusion)
- Commit: $($RunInfo.headSha)
- Timestamp: $(Get-Date)

## Failure Categories

$(foreach ($category in $FailureCategories) {
    @"
### $($category.JobName) - $($category.Type)
- **Severity**: $($category.Severity)
- **Description**: $($category.Description)
- **Auto-fixable**: $($category.AutoFixable)
$(if ($category.FixCommand) { "- **Fix Command**: ``$($category.FixCommand)``" })

"@
})

## Automated Fixes Applied

$(if ($FixesApplied.Count -gt 0) {
    foreach ($fix in $FixesApplied) {
        "- ✅ $($fix.Type): $($fix.Description)"
    }
} else {
    "- No automated fixes were applied"
})

## Recommendations

1. Review the detailed logs in local CI output
2. Address any critical security issues immediately
3. Fix compilation errors before committing
4. Ensure all tests pass in local CI environment
5. Run `trunk check --ci` locally before pushing

---
*Generated by Local CI Failure Analysis Script*
"@

    return $report
}

# Main execution
function Main {
    Write-Log "Starting CI Failure Analysis..." -Level INFO

    # Get workflow run info
    $runInfo = if ($WorkflowRunId) {
        # Use specific run ID
        try {
            gh run view $WorkflowRunId --json=databaseId,conclusion,status,headSha | ConvertFrom-Json
        }
        catch {
            Write-Log "Failed to get workflow run $WorkflowRunId" -Level ERROR
            return
        }
    }
    else {
        Get-LatestWorkflowRun
    }

    if (-not $runInfo) {
        Write-Log "No workflow run found" -Level ERROR
        return
    }

    if ($runInfo.conclusion -ne "failure") {
        Write-Log "Workflow run $($runInfo.databaseId) did not fail (status: $($runInfo.conclusion))" -Level INFO
        return
    }

    # Analyze failures
    $failureCategories = Analyze-WorkflowFailure -RunInfo $runInfo

    if ($failureCategories.Count -eq 0) {
        Write-Log "No specific failure categories identified" -Level WARN
        return
    }

    # Apply auto-fixes if requested
    $fixesApplied = @()
    if ($AutoFix) {
        $fixesApplied = Apply-AutoFix -FailureCategories $failureCategories
    }

    # Generate and display report
    $report = Generate-FailureReport -RunInfo $runInfo -FailureCategories $failureCategories -FixesApplied $fixesApplied

    Write-Log "Failure Analysis Complete" -Level INFO
    Write-Host "`n$report"

    # Save report to file
    $reportPath = "ci-failure-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').md"
    $report | Out-File -FilePath $reportPath -Encoding UTF8
    Write-Log "Report saved to: $reportPath" -Level INFO
}

# Execute main function
try {
    Main
}
catch {
    Write-Log "Script execution failed: $($_.Exception.Message)" -Level ERROR
    exit 1
}