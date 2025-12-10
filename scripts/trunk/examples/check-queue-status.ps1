#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Example script to check Trunk merge queue status.

.DESCRIPTION
    Demonstrates how to use the TrunkMergeQueue module to query
    the status of the merge queue or a specific PR.

.PARAMETER PrNumber
    Optional PR number to check specific PR status.

.PARAMETER Verbose
    Show detailed output.

.EXAMPLE
    .\check-queue-status.ps1
    # Check overall queue status

.EXAMPLE
    .\check-queue-status.ps1 -PrNumber 123
    # Check status of PR #123

.EXAMPLE
    .\check-queue-status.ps1 -PrNumber 456 -Verbose
    # Check PR #456 with verbose output
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [int]$PrNumber,

    [Parameter(Mandatory = $false)]
    [switch]$VerboseOutput
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

    # Get queue status
    $params = @{}
    if ($PrNumber) {
        $params['PrNumber'] = $PrNumber
    }
    if ($VerboseOutput) {
        $params['VerboseOutput'] = $true
    }

    Write-Host "`nQuerying merge queue..." -ForegroundColor Cyan

    $result = Get-TrunkMergeQueueStatus @params

    if ($result.Success) {
        Write-Host "`n✓ Status Retrieved Successfully" -ForegroundColor Green
        Write-Host "`n$($result.Output)"
    }
    else {
        Write-Host "`n✗ Failed to retrieve status (exit code $($result.ExitCode))" -ForegroundColor Red
        Write-Host "`n$($result.Output)"
        exit $result.ExitCode
    }
}
catch {
    Write-Error "Error: $_"
    exit 1
}
