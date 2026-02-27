#!/usr/bin/env pwsh
<#
.SYNOPSIS
Upserts Chart of Accounts rows in QuickBooks Online using AccountNumber (AcctNum).

.DESCRIPTION
Reads a CSV with columns AccountNumber, Name, QboAccountType and upserts each row:
- If AcctNum exists in QBO, updates name/type using sparse update.
- If AcctNum does not exist, creates a new account.

.PARAMETER CsvPath
Path to extracted COA CSV.

.PARAMETER AccessToken
QuickBooks OAuth access token.

.PARAMETER RealmId
QuickBooks company realm ID.

.PARAMETER Environment
sandbox (default) or production.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$CsvPath,

    [Parameter(Mandatory = $false)]
    [string]$AccessToken = $env:QBO_ACCESS_TOKEN,

    [Parameter(Mandatory = $false)]
    [string]$RealmId = $env:QBO_REALM_ID,

    [Parameter(Mandatory = $false)]
    [ValidateSet('sandbox', 'production')]
    [string]$Environment = 'sandbox'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -Path $CsvPath)) {
    throw "CSV file not found: $CsvPath"
}

if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    throw 'AccessToken is required (parameter or QBO_ACCESS_TOKEN env var).'
}

if ([string]::IsNullOrWhiteSpace($RealmId)) {
    throw 'RealmId is required (parameter or QBO_REALM_ID env var).'
}

$baseUrl = if ($Environment -eq 'sandbox') { 'https://sandbox-quickbooks.api.intuit.com' } else { 'https://quickbooks.api.intuit.com' }
$accountUrl = "$baseUrl/v3/company/$RealmId/account"

$headers = @{
    'Authorization' = "Bearer $AccessToken"
    'Accept'        = 'application/json'
    'Content-Type'  = 'application/json'
}

$rows = Import-Csv -Path $CsvPath
if (-not $rows -or $rows.Count -eq 0) {
    throw "No rows found in $CsvPath"
}

function Get-StringOrDefault {
    param(
        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [object]$Value,

        [Parameter(Mandatory = $false)]
        [string]$Default = ''
    )

    if ($null -eq $Value) {
        return $Default
    }

    $text = "$Value".Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $Default
    }

    return $text
}

$created = 0
$updated = 0
$failed = 0

foreach ($row in $rows) {
    try {
        $accountNumber = Get-StringOrDefault -Value $row.AccountNumber -Default ''
        $name = Get-StringOrDefault -Value $row.Name -Default ''
        $qboType = Get-StringOrDefault -Value $row.QboAccountType -Default 'Expense'

        if ([string]::IsNullOrWhiteSpace($accountNumber) -or [string]::IsNullOrWhiteSpace($name)) {
            continue
        }

        $escapedNum = $accountNumber.Replace("'", "''")
        $query = "select * from Account where AcctNum = '$escapedNum'"
        $encodedQuery = [uri]::EscapeDataString($query)
        $existingResponse = Invoke-RestMethod -Uri "$baseUrl/v3/company/$RealmId/query?query=$encodedQuery" -Method GET -Headers $headers

        $existing = $null
        if ($existingResponse.QueryResponse -and $existingResponse.QueryResponse.Account) {
            $existing = @($existingResponse.QueryResponse.Account)[0]
        }

        if ($null -ne $existing) {
            $updateBody = @{
                Id          = $existing.Id
                SyncToken   = $existing.SyncToken
                sparse      = $true
                Name        = $name
                AcctNum     = $accountNumber
                AccountType = $qboType
                Active      = $true
            } | ConvertTo-Json -Depth 6

            Invoke-RestMethod -Uri "$accountUrl?operation=update" -Method POST -Headers $headers -Body $updateBody | Out-Null
            $updated++
            Write-Output "Updated: $accountNumber - $name"
        } else {
            $createBody = @{
                Name        = $name
                AcctNum     = $accountNumber
                AccountType = $qboType
                Active      = $true
            } | ConvertTo-Json -Depth 6

            Invoke-RestMethod -Uri $accountUrl -Method POST -Headers $headers -Body $createBody | Out-Null
            $created++
            Write-Output "Created: $accountNumber - $name"
        }
    } catch {
        $failed++
        Write-Warning "Failed for [$($row.AccountNumber)] $($row.Name): $($_.Exception.Message)"
    }

    Start-Sleep -Milliseconds 250
}

Write-Output ""
Write-Output "QuickBooks COA upsert complete. Created=$created Updated=$updated Failed=$failed Total=$($rows.Count)"
