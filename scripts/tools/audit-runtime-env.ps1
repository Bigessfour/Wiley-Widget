#!/usr/bin/env pwsh
<#!
.SYNOPSIS
    Audits runtime environment variable alignment for Wiley Widget.

.DESCRIPTION
    Reports canonical variables and compatibility aliases across Process/User/Machine scopes.
    Machine scope is treated as canonical policy target.

.PARAMETER OutputCsv
    Optional path to write full audit results as CSV.

.EXAMPLE
    .\scripts\tools\audit-runtime-env.ps1

.EXAMPLE
    .\scripts\tools\audit-runtime-env.ps1 -OutputCsv tmp/runtime-env-audit-canonical.csv
#>

[CmdletBinding()]
param(
    [string]$OutputCsv
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$canonicalVars = @(
    'GITHUB_PERSONAL_ACCESS_TOKEN',
    'MSSQL_CONNECTION_STRING',
    'DATABASE_CONNECTION_STRING',
    'SYNCFUSION_MCP_API_KEY',
    'SYNCFUSION_LICENSE_KEY',
    'XAI__ApiKey',
    'XAI_BASE_URL',
    'QBO_CLIENT_ID',
    'QBO_CLIENT_SECRET',
    'QBO_REDIRECT_URI',
    'QBO_ENVIRONMENT',
    'QBO_WEBHOOKS_VERIFIER',
    'WW_REPO_ROOT',
    'ASPNETCORE_ENVIRONMENT',
    'WILEYWIDGET_DEFAULT_FISCAL_YEAR',
    'WILEYWIDGET_SETTINGS_DIR',
    'SYNCFUSION_SILENT_LICENSE_VALIDATION'
)

$aliasMap = @{
    'GITHUB_PERSONAL_ACCESS_TOKEN' = @('GITHUB_TOKEN', 'GITHUB_PAT')
    'SYNCFUSION_MCP_API_KEY'       = @('SYNCFUSION_API_KEY')
    'XAI__ApiKey'                  = @('XAI_API_KEY', 'WILEYWIDGET_XAI_API_KEY')
    'QBO_CLIENT_ID'                = @('QUICKBOOKS_CLIENT_ID')
    'QBO_CLIENT_SECRET'            = @('QUICKBOOKS_CLIENT_SECRET')
    'QBO_REDIRECT_URI'             = @('QUICKBOOKS_REDIRECT_URI')
    'QBO_ENVIRONMENT'              = @('QUICKBOOKS_ENVIRONMENT')
}

$rows = New-Object System.Collections.Generic.List[psobject]

foreach ($name in $canonicalVars) {
    $processValue = [Environment]::GetEnvironmentVariable($name, 'Process')
    $userValue = [Environment]::GetEnvironmentVariable($name, 'User')
    $machineValue = [Environment]::GetEnvironmentVariable($name, 'Machine')

    $aliases = @()
    if ($aliasMap.ContainsKey($name)) {
        $aliases = $aliasMap[$name]
    }

    $aliasPresent = 'No'
    foreach ($alias in $aliases) {
        $aliasUser = [Environment]::GetEnvironmentVariable($alias, 'User')
        $aliasMachine = [Environment]::GetEnvironmentVariable($alias, 'Machine')
        if (-not [string]::IsNullOrWhiteSpace($aliasUser) -or -not [string]::IsNullOrWhiteSpace($aliasMachine)) {
            $aliasPresent = 'Yes'
            break
        }
    }

    $status = if (-not [string]::IsNullOrWhiteSpace($machineValue)) {
        'OK'
    } elseif (-not [string]::IsNullOrWhiteSpace($userValue) -or -not [string]::IsNullOrWhiteSpace($processValue)) {
        'DRIFT'
    } else {
        'MISSING'
    }

    $rows.Add([pscustomobject]@{
            CanonicalName = $name
            Machine       = if ([string]::IsNullOrWhiteSpace($machineValue)) { 'MISSING' } else { 'PRESENT' }
            User          = if ([string]::IsNullOrWhiteSpace($userValue)) { 'MISSING' } else { 'PRESENT' }
            Process       = if ([string]::IsNullOrWhiteSpace($processValue)) { 'MISSING' } else { 'PRESENT' }
            AliasPresent  = $aliasPresent
            Status        = $status
        }) | Out-Null
}

$rows | Sort-Object CanonicalName | Format-Table -AutoSize

$summary = $rows | Group-Object Status | Sort-Object Name
Write-Host "\nSummary:" -ForegroundColor Cyan
foreach ($item in $summary) {
    Write-Host "  $($item.Name): $($item.Count)"
}

if (-not [string]::IsNullOrWhiteSpace($OutputCsv)) {
    $dir = Split-Path -Parent $OutputCsv
    if (-not [string]::IsNullOrWhiteSpace($dir) -and -not (Test-Path $dir)) {
        New-Item -Path $dir -ItemType Directory -Force | Out-Null
    }
    $rows | Export-Csv -Path $OutputCsv -NoTypeInformation
    Write-Host "Audit CSV written: $OutputCsv" -ForegroundColor Green
}

$driftOrMissing = @($rows | Where-Object { $_.Status -ne 'OK' })
if ($driftOrMissing.Count -gt 0) {
    Write-Warning 'Machine-canonical policy violations detected (DRIFT or MISSING).'
    exit 1
}
