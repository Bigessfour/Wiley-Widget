#!/usr/bin/env pwsh
#Requires -Version 7.5.4
<#!
.SYNOPSIS
    Validates the local prerequisites for seeding the QuickBooks sandbox.

.DESCRIPTION
    Performs a non-destructive preflight for the current Wiley Widget sandbox
    seeding path. It validates sandbox configuration, redirect URI, URL ACL,
    and seed input files so you know whether the environment is ready before
    attempting OAuth connect or running the COA upsert script.

.PARAMETER ManifestPath
    Path to the sandbox seed manifest JSON file. Defaults to the template in
    scripts/quickbooks.

.PARAMETER ChartOfAccountsCsvPath
    Optional override for the COA CSV path.

.EXAMPLE
    ./prepare-sandbox-seed.ps1

.EXAMPLE
    ./prepare-sandbox-seed.ps1 -ManifestPath ./scripts/quickbooks/sandbox-seed-manifest.template.json
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ManifestPath = (Join-Path $PSScriptRoot 'sandbox-seed-manifest.template.json'),

    [Parameter(Mandatory = $false)]
    [string]$ChartOfAccountsCsvPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-EnvironmentVariable {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Names
    )

    foreach ($name in $Names) {
        $processValue = [Environment]::GetEnvironmentVariable($name)
        if (-not [string]::IsNullOrWhiteSpace($processValue)) {
            return $processValue
        }

        try {
            $userValue = [Environment]::GetEnvironmentVariable($name, [EnvironmentVariableTarget]::User)
            if (-not [string]::IsNullOrWhiteSpace($userValue)) {
                return $userValue
            }
        } catch {
        }

        try {
            $machineValue = [Environment]::GetEnvironmentVariable($name, [EnvironmentVariableTarget]::Machine)
            if (-not [string]::IsNullOrWhiteSpace($machineValue)) {
                return $machineValue
            }
        } catch {
        }
    }

    return $null
}

function Resolve-ManifestPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return $PathValue
    }

    return (Join-Path $PSScriptRoot $PathValue)
}

function Test-UrlAclRegistration {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ListenerPrefix
    )

    try {
        $output = & netsh http show urlacl url=$ListenerPrefix 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0 -and $output -match [regex]::Escape($ListenerPrefix)) {
            return $true
        }
    } catch {
    }

    return $false
}

function Add-CheckResult {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IList]$Results,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [bool]$Passed,

        [Parameter(Mandatory = $true)]
        [string]$Detail,

        [Parameter(Mandatory = $false)]
        [bool]$Blocker = $true
    )

    $Results.Add([pscustomobject]@{
            Name    = $Name
            Passed  = $Passed
            Detail  = $Detail
            Blocker = $Blocker
        }) | Out-Null
}

$resolvedManifestPath = Resolve-ManifestPath -PathValue $ManifestPath
$manifest = $null
if (Test-Path -Path $resolvedManifestPath) {
    $manifest = Get-Content -Path $resolvedManifestPath -Raw | ConvertFrom-Json -AsHashtable
}

$manifestMeta = if ($null -ne $manifest -and $manifest.ContainsKey('meta')) { $manifest['meta'] } else { @{} }
$manifestInputs = if ($null -ne $manifest -and $manifest.ContainsKey('inputs')) { $manifest['inputs'] } else { @{} }

$environment = Resolve-EnvironmentVariable -Names @('QBO_ENVIRONMENT', 'QUICKBOOKS_ENVIRONMENT')
if ([string]::IsNullOrWhiteSpace($environment) -and $manifestMeta.ContainsKey('environment')) {
    $environment = [string]$manifestMeta['environment']
}

$redirectUri = Resolve-EnvironmentVariable -Names @('QBO_REDIRECT_URI', 'QUICKBOOKS_REDIRECT_URI')
if ([string]::IsNullOrWhiteSpace($redirectUri) -and $manifestMeta.ContainsKey('redirectUri')) {
    $redirectUri = [string]$manifestMeta['redirectUri']
}

$realmId = Resolve-EnvironmentVariable -Names @('QBO_REALM_ID', 'QUICKBOOKS_REALM_ID')
if ([string]::IsNullOrWhiteSpace($realmId) -and $manifestMeta.ContainsKey('realmId')) {
    $realmId = [string]$manifestMeta['realmId']
}

$clientId = Resolve-EnvironmentVariable -Names @('QBO_CLIENT_ID', 'QUICKBOOKS_CLIENT_ID')
$clientSecret = Resolve-EnvironmentVariable -Names @('QBO_CLIENT_SECRET', 'QUICKBOOKS_CLIENT_SECRET')
$accessToken = Resolve-EnvironmentVariable -Names @('QBO_ACCESS_TOKEN', 'QUICKBOOKS_ACCESS_TOKEN')

if ([string]::IsNullOrWhiteSpace($ChartOfAccountsCsvPath)) {
    if ($manifestInputs.ContainsKey('chartOfAccountsCsvPath')) {
        $ChartOfAccountsCsvPath = [string]$manifestInputs['chartOfAccountsCsvPath']
    } else {
        $ChartOfAccountsCsvPath = 'templates/town-of-wiley-coa.seed.csv'
    }
}

$resolvedCoaCsvPath = if ([System.IO.Path]::IsPathRooted($ChartOfAccountsCsvPath)) {
    $ChartOfAccountsCsvPath
} else {
    Join-Path $PSScriptRoot $ChartOfAccountsCsvPath
}

$results = [System.Collections.Generic.List[object]]::new()

$manifestDetail = if ($null -ne $manifest) {
    "Loaded $resolvedManifestPath"
} else {
    "Manifest not found at $resolvedManifestPath"
}
Add-CheckResult -Results $results -Name 'Manifest' -Passed ($null -ne $manifest) -Detail $manifestDetail

$environmentIsSandbox = -not [string]::IsNullOrWhiteSpace($environment) -and $environment -eq 'sandbox'
$environmentDetail = if ($environmentIsSandbox) {
    'Environment is sandbox'
} else {
    "Environment must be sandbox; current value: '$environment'"
}
Add-CheckResult -Results $results -Name 'Environment' -Passed $environmentIsSandbox -Detail $environmentDetail

$hasClientId = -not [string]::IsNullOrWhiteSpace($clientId)
$clientIdDetail = if ($hasClientId) {
    'QuickBooks client ID is configured'
} else {
    'Missing QBO_CLIENT_ID / QUICKBOOKS_CLIENT_ID'
}
Add-CheckResult -Results $results -Name 'Client ID' -Passed $hasClientId -Detail $clientIdDetail

$hasClientSecret = -not [string]::IsNullOrWhiteSpace($clientSecret)
$clientSecretDetail = if ($hasClientSecret) {
    'QuickBooks client secret is configured'
} else {
    'Missing QBO_CLIENT_SECRET / QUICKBOOKS_CLIENT_SECRET'
}
Add-CheckResult -Results $results -Name 'Client Secret' -Passed $hasClientSecret -Detail $clientSecretDetail

$redirectUriValid = $false
$redirectUriDetail = 'Redirect URI is missing'
if (-not [string]::IsNullOrWhiteSpace($redirectUri)) {
    $redirectUriValid = $redirectUri -eq 'http://localhost:5000/callback/'
    $redirectUriDetail = if ($redirectUriValid) {
        'Redirect URI matches Wiley Widget sandbox localhost callback'
    } else {
        "Redirect URI should be http://localhost:5000/callback/ for the canonical sandbox flow; current value: $redirectUri"
    }
}
Add-CheckResult -Results $results -Name 'Redirect URI' -Passed $redirectUriValid -Detail $redirectUriDetail

$realmIdKnown = -not [string]::IsNullOrWhiteSpace($realmId) -and ($realmId -notmatch '^YOUR_')
$realmIdDetail = if ($realmIdKnown) {
    "Realm ID available: $realmId"
} else {
    'Realm ID is not set yet; connect to sandbox first or update the manifest after realm capture'
}
Add-CheckResult -Results $results -Name 'Realm ID' -Passed $realmIdKnown -Detail $realmIdDetail -Blocker $false

$urlAclReady = $false
$urlAclDetail = 'URL ACL check skipped because redirect URI is not the canonical localhost callback'
if ($redirectUri -eq 'http://localhost:5000/callback/') {
    $urlAclReady = Test-UrlAclRegistration -ListenerPrefix $redirectUri
    $urlAclDetail = if ($urlAclReady) {
        'URL ACL registration is present'
    } else {
        'URL ACL missing. Run: netsh http add urlacl url=http://localhost:5000/callback/ user=%USERNAME%'
    }
}
Add-CheckResult -Results $results -Name 'URL ACL' -Passed $urlAclReady -Detail $urlAclDetail

$coaCsvReady = Test-Path -Path $resolvedCoaCsvPath
$coaCsvDetail = if ($coaCsvReady) {
    "Found COA CSV at $resolvedCoaCsvPath"
} else {
    "COA CSV not found at $resolvedCoaCsvPath"
}
Add-CheckResult -Results $results -Name 'COA CSV' -Passed $coaCsvReady -Detail $coaCsvDetail

$hasAccessToken = -not [string]::IsNullOrWhiteSpace($accessToken)
$accessTokenDetail = if ($hasAccessToken) {
    'QBO access token available for current shell session'
} else {
    'No QBO access token in environment yet. Connect in Wiley Widget first, then run export-qbo-session.ps1.'
}
Add-CheckResult -Results $results -Name 'Access Token' -Passed $hasAccessToken -Detail $accessTokenDetail -Blocker $false

$hardBlockers = @($results | Where-Object { -not $_.Passed -and $_.Blocker })

Write-Host ''
Write-Host 'QuickBooks Sandbox Seed Preflight' -ForegroundColor Cyan
Write-Host '================================' -ForegroundColor Cyan
Write-Host ''

foreach ($result in $results) {
    $icon = if ($result.Passed) { 'PASS' } elseif ($result.Blocker) { 'FAIL' } else { 'WARN' }
    $color = if ($result.Passed) { 'Green' } elseif ($result.Blocker) { 'Red' } else { 'Yellow' }
    Write-Host ("[{0}] {1}: {2}" -f $icon, $result.Name, $result.Detail) -ForegroundColor $color
}

Write-Host ''
if ($hardBlockers.Count -eq 0) {
    Write-Host 'Seed preflight is ready for the current supported sandbox path.' -ForegroundColor Green
} else {
    Write-Host 'Seed preflight is not ready. Fix the blocking items above before attempting sandbox connect or seed.' -ForegroundColor Red
}

Write-Host ''
Write-Host 'Recommended next steps:' -ForegroundColor Cyan
Write-Host '  1. Run QuickBooks diagnostics inside Wiley Widget and confirm Redirect OK is YES.'
Write-Host '  2. Connect to the sandbox in Wiley Widget to capture the current access token and realm ID.'
Write-Host '  3. In the same PowerShell session, run ./scripts/quickbooks/export-qbo-session.ps1'
Write-Host ("  4. Review the COA CSV seed source at {0}" -f $resolvedCoaCsvPath)
Write-Host ("  5. When ready, upsert the COA with ./scripts/quickbooks/upsert-coa-from-csv.ps1 -CsvPath '{0}' -Environment sandbox" -f $resolvedCoaCsvPath)

if ($hardBlockers.Count -gt 0) {
    exit 1
}

exit 0
