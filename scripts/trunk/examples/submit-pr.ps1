#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Example script to submit a PR to Trunk merge queue.

.DESCRIPTION
    Demonstrates how to use the TrunkMergeQueue module to submit
    a PR to the merge queue with optional priority.

.PARAMETER PrNumber
    PR number to submit (required).

.PARAMETER Priority
    Queue priority (0-255, where 0 is highest). Default is normal priority.

.EXAMPLE
    .\submit-pr.ps1 -PrNumber 123
    # Submit PR #123 with normal priority

.EXAMPLE
    .\submit-pr.ps1 -PrNumber 456 -Priority 0
    # Submit PR #456 with highest priority (skip the line)

.EXAMPLE
    .\submit-pr.ps1 -PrNumber 789 -Priority 100 -WhatIf
    # Preview what would happen (dry run)
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [int]$PrNumber,

    [Parameter(Mandatory = $false)]
    [ValidateRange(0, 255)]
    [int]$Priority
)

$ErrorActionPreference = 'Stop'

# Import the TrunkMergeQueue module
$modulePath = Join-Path $PSScriptRoot '..' 'TrunkMergeQueue.psm1'
Import-Module $modulePath -Force

try {
    # Check if trunk CLI is installed
    $cliCheck = Test-TrunkCli
    if (-not $cliCheck.Installed) {
        Write-Error "Trunk CLI not installed: $($cliCheck.Error)"
        exit 1
    }

    Write-Host "✓ Trunk CLI $($cliCheck.Version) installed" -ForegroundColor Green

    # Build parameters
    $params = @{
        PrNumber = $PrNumber
    }
    if ($PSBoundParameters.ContainsKey('Priority')) {
        $params['Priority'] = $Priority
        Write-Host "`nSubmitting PR #$PrNumber with priority $Priority..." -ForegroundColor Cyan
    }
    else {
        Write-Host "`nSubmitting PR #$PrNumber with normal priority..." -ForegroundColor Cyan
    }

    # Submit PR
    $result = Submit-TrunkMergeQueuePr @params

    if ($result.Success) {
        Write-Host "`n✓ PR #$PrNumber Submitted Successfully" -ForegroundColor Green
        Write-Host "`n$($result.Output)"
    }
    else {
        Write-Host "`n✗ Failed to submit PR #$PrNumber (exit code $($result.ExitCode))" -ForegroundColor Red
        Write-Host "`n$($result.Output)"
        exit $result.ExitCode
    }
}
catch {
    Write-Error "Error: $_"
    exit 1
}
