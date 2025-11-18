<#
.SYNOPSIS
    Official CI monitoring script for Wiley Widget project.

.DESCRIPTION
    Monitors the latest CI run for the ci-optimized.yml workflow.
    If failed, retrieves detailed logs and applies Trunk auto-fixes.

.PARAMETER WorkflowName
    Name of the workflow to monitor (default: ci-optimized.yml)

.EXAMPLE
    .\monitor-ci.ps1

.NOTES
    Requires: PowerShell 7.5.4+, GitHub CLI, Trunk CLI
#>

#Requires -Version 7.5

[CmdletBinding()]
param(
    [string]$WorkflowName = "ci-optimized.yml"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

try {
    # 1. Check local Trunk status
    Write-Information "Checking local Trunk status..."
    & trunk check --monitor

    # 2. Get latest CI run via GitHub CLI (corrected to use databaseId)
    Write-Information "Retrieving latest CI run..."
    $latestRunJson = & gh run list --workflow=$WorkflowName --limit=1 --json=databaseId,status,conclusion
    $latestRun = $latestRunJson | ConvertFrom-Json

    # 3. If failed, get details using the correct databaseId
    if ($latestRun.conclusion -eq "failure") {
        Write-Information "CI run failed. Retrieving logs for run ID: $($latestRun.databaseId)"
        & gh run view $latestRun.databaseId --log-failed

        # 4. Run Trunk fixes for common issues
        Write-Information "Applying Trunk auto-fixes..."
        & trunk check --fix --filter=security,quality

        # 5. Re-commit if fixes applied
        if (& git status --porcelain) {
            Write-Information "Committing auto-fixes..."
            & git add .
            & git commit -m "fix: Apply Trunk automated fixes"
            & git push
        }
    } else {
        Write-Information "Latest CI run concluded with: $($latestRun.conclusion)"
    }
}
catch {
    Write-Error "Monitoring failed: $_"
    exit 1
}