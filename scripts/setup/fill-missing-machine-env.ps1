#!/usr/bin/env pwsh
<#!
.SYNOPSIS
    Fill remaining missing canonical machine-scope environment variables with safe defaults.

.DESCRIPTION
    Intended to be run elevated after scripts/setup-env.ps1.
    Uses existing machine MSSQL connection string as source for DATABASE_CONNECTION_STRING when available.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Set-MachineIfMissing {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $current = [Environment]::GetEnvironmentVariable($Name, 'Machine')
    if ([string]::IsNullOrWhiteSpace($current)) {
        [Environment]::SetEnvironmentVariable($Name, $Value, 'Machine')
        Write-Host "Set $Name" -ForegroundColor Green
    } else {
        Write-Host "Keep $Name (already present)" -ForegroundColor DarkYellow
    }
}

$mssql = [Environment]::GetEnvironmentVariable('MSSQL_CONNECTION_STRING', 'Machine')
if (-not [string]::IsNullOrWhiteSpace($mssql)) {
    Set-MachineIfMissing -Name 'DATABASE_CONNECTION_STRING' -Value $mssql
}

Set-MachineIfMissing -Name 'XAI_BASE_URL' -Value 'https://api.x.ai/v1/'
Set-MachineIfMissing -Name 'SYNCFUSION_SILENT_LICENSE_VALIDATION' -Value 'true'
Set-MachineIfMissing -Name 'WILEYWIDGET_DEFAULT_FISCAL_YEAR' -Value ([DateTime]::UtcNow.Year.ToString())
Set-MachineIfMissing -Name 'WILEYWIDGET_SETTINGS_DIR' -Value '%ProgramData%\WileyWidget'

Write-Host "Fill operation complete." -ForegroundColor Cyan
