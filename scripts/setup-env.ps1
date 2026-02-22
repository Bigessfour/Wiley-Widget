#!/usr/bin/env pwsh
<#!
.SYNOPSIS
    Align Wiley Widget environment variables to Machine scope (canonical source).

.DESCRIPTION
    Sets canonical Wiley Widget variables in Machine scope from an env file and/or
    user-scope fallback values. Designed for administrators.

.PARAMETER EnvFilePath
    Path to a KEY=VALUE file (default: .env.machine).

.PARAMETER PromoteFromUser
    If Machine value is missing and env file has no value, copy from User scope.

.PARAMETER IncludeCompatibilityAliases
    Also writes legacy alias variables to Machine scope for migration compatibility.

.PARAMETER WhatIf
    Show intended actions without writing environment variables.

.EXAMPLE
    .\scripts\setup-env.ps1 -EnvFilePath .env.machine -PromoteFromUser -IncludeCompatibilityAliases
#>

[CmdletBinding()]
param(
    [string]$EnvFilePath = ".env.machine",
    [switch]$PromoteFromUser,
    [switch]$IncludeCompatibilityAliases,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Read-EnvFile {
    param([string]$Path)

    $map = @{}
    if (-not (Test-Path -Path $Path -PathType Leaf)) {
        return $map
    }

    foreach ($line in Get-Content -Path $Path) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }
        if ($trimmed.StartsWith('#')) { continue }

        $eqIndex = $trimmed.IndexOf('=')
        if ($eqIndex -lt 1) { continue }

        $name = $trimmed.Substring(0, $eqIndex).Trim()
        $value = $trimmed.Substring($eqIndex + 1).Trim()

        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        $map[$name] = $value
    }

    return $map
}

if (-not (Test-IsAdministrator)) {
    throw "Machine-scope alignment requires an elevated PowerShell session (Run as Administrator)."
}

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

$envFileValues = Read-EnvFile -Path $EnvFilePath
$results = New-Object System.Collections.Generic.List[psobject]

foreach ($name in $canonicalVars) {
    $machineValue = [Environment]::GetEnvironmentVariable($name, 'Machine')
    $fileValue = $null
    if ($envFileValues.ContainsKey($name)) {
        $fileValue = $envFileValues[$name]
    }

    $valueToSet = $null
    $source = 'none'

    if (-not [string]::IsNullOrWhiteSpace($fileValue)) {
        $valueToSet = $fileValue
        $source = 'env-file'
    } elseif ($PromoteFromUser) {
        $userValue = [Environment]::GetEnvironmentVariable($name, 'User')
        if (-not [string]::IsNullOrWhiteSpace($userValue)) {
            $valueToSet = $userValue
            $source = 'user-scope'
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($valueToSet)) {
        if ($WhatIf) {
            $action = if ([string]::IsNullOrWhiteSpace($machineValue)) { 'would-set' } else { 'would-update' }
        } else {
            [Environment]::SetEnvironmentVariable($name, $valueToSet, 'Machine')
            $action = if ([string]::IsNullOrWhiteSpace($machineValue)) { 'set' } else { 'updated' }
        }
    } else {
        $action = if ([string]::IsNullOrWhiteSpace($machineValue)) { 'missing' } else { 'kept' }
    }

    $results.Add([pscustomobject]@{
            Name           = $name
            Action         = $action
            Source         = $source
            MachinePresent = if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name, 'Machine'))) { 'No' } else { 'Yes' }
        }) | Out-Null
}

if ($IncludeCompatibilityAliases) {
    foreach ($canonicalName in $aliasMap.Keys) {
        $canonicalValue = [Environment]::GetEnvironmentVariable($canonicalName, 'Machine')
        if ([string]::IsNullOrWhiteSpace($canonicalValue)) { continue }

        foreach ($alias in $aliasMap[$canonicalName]) {
            $existingAlias = [Environment]::GetEnvironmentVariable($alias, 'Machine')
            if ([string]::IsNullOrWhiteSpace($existingAlias)) {
                if (-not $WhatIf) {
                    [Environment]::SetEnvironmentVariable($alias, $canonicalValue, 'Machine')
                }
                $results.Add([pscustomobject]@{
                        Name           = $alias
                        Action         = if ($WhatIf) { 'would-set-alias' } else { 'set-alias' }
                        Source         = "alias:$canonicalName"
                        MachinePresent = 'Yes'
                    }) | Out-Null
            }
        }
    }
}

$results | Sort-Object Name | Format-Table -AutoSize

$missing = $results | Where-Object { $_.Action -eq 'missing' }
if ($missing.Count -gt 0) {
    Write-Warning "Missing canonical machine variables: $($missing.Name -join ', ')"
}

Write-Host "\nAlignment complete. Restart VS Code and terminals to pick up Machine-scope changes." -ForegroundColor Cyan
