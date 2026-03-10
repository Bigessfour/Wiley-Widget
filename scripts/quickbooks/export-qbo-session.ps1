#!/usr/bin/env pwsh
#Requires -Version 7.5.4
<#!
.SYNOPSIS
    Exports the current Wiley Widget QuickBooks session into shell environment variables.

.DESCRIPTION
    Reads Wiley Widget settings.json after a successful sandbox connect and sets
    QBO_ACCESS_TOKEN, QBO_REALM_ID, and QBO_ENVIRONMENT in the current PowerShell
    session so the REST seeding scripts can run without manual copy/paste.

.PARAMETER SettingsPath
    Optional override for the Wiley Widget settings.json path.

.PARAMETER PersistUserScope
    Also writes QBO_ACCESS_TOKEN, QBO_REALM_ID, and QBO_ENVIRONMENT to user-scoped
    environment variables. Avoid this unless you explicitly want the token persisted.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $false)]
    [string]$SettingsPath = (Join-Path $env:APPDATA 'WileyWidget\settings.json'),

    [Parameter(Mandatory = $false)]
    [switch]$PersistUserScope
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -Path $SettingsPath)) {
    throw "Wiley Widget settings file not found: $SettingsPath"
}

$settings = Get-Content -Path $SettingsPath -Raw | ConvertFrom-Json -AsHashtable

$accessToken = if ($settings.ContainsKey('QboAccessToken')) { [string]$settings['QboAccessToken'] } else { $null }
$realmId = if ($settings.ContainsKey('QuickBooksRealmId')) { [string]$settings['QuickBooksRealmId'] } else { $null }
$tokenExpiry = if ($settings.ContainsKey('QboTokenExpiry')) { [string]$settings['QboTokenExpiry'] } else { $null }

if ([string]::IsNullOrWhiteSpace($accessToken)) {
    throw 'QboAccessToken is not present in settings.json. Connect to QuickBooks in Wiley Widget first.'
}

if ([string]::IsNullOrWhiteSpace($realmId)) {
    throw 'QuickBooksRealmId is not present in settings.json. Complete a successful sandbox connect first.'
}

$env:QBO_ACCESS_TOKEN = $accessToken
$env:QBO_REALM_ID = $realmId
if ([string]::IsNullOrWhiteSpace($env:QBO_ENVIRONMENT)) {
    $env:QBO_ENVIRONMENT = 'sandbox'
}

if ($PersistUserScope -and $PSCmdlet.ShouldProcess('User environment', 'Persist QBO sandbox session values')) {
    [Environment]::SetEnvironmentVariable('QBO_ACCESS_TOKEN', $accessToken, [EnvironmentVariableTarget]::User)
    [Environment]::SetEnvironmentVariable('QBO_REALM_ID', $realmId, [EnvironmentVariableTarget]::User)
    [Environment]::SetEnvironmentVariable('QBO_ENVIRONMENT', $env:QBO_ENVIRONMENT, [EnvironmentVariableTarget]::User)
}

Write-Host 'QuickBooks sandbox session exported to the current shell.' -ForegroundColor Green
Write-Host ("  QBO_REALM_ID: {0}" -f $realmId)
Write-Host ("  QBO_ENVIRONMENT: {0}" -f $env:QBO_ENVIRONMENT)
Write-Host ("  Token expiry: {0}" -f $(if ([string]::IsNullOrWhiteSpace($tokenExpiry)) { '(unknown)' } else { $tokenExpiry }))
Write-Host '  QBO_ACCESS_TOKEN: set in current session'

if ($PersistUserScope) {
    Write-Host 'User-scoped environment variables were also updated.' -ForegroundColor Yellow
}
