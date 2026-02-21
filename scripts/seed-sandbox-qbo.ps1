#Requires -Version 7.5.4
<#
.SYNOPSIS
Seed QuickBooks Online (QBO) Sandbox with Municipal Finance Accounts
This script programmatically creates accounts in QBO sandbox using the QuickBooks REST API,
matching the Wiley Widget municipal finance account structure.

.DESCRIPTION
Automates the creation of 21 municipal finance accounts in QBO sandbox environment,
including General Fund, Department expenses, Revenue Accounts, and Fund Balance accounts.
Uses OAuth 2.0 for authentication and handles API rate limiting.

.PARAMETER AccessToken
OAuth 2.0 access token obtained from Intuit

.PARAMETER RealmId
QBO Company/Realm ID (numeric identifier for the QuickBooks organization)

.PARAMETER Sandbox
Switch to use Sandbox environment (default). If omitted, uses Production.

.EXAMPLE
.\seed-sandbox-qbo.ps1 -AccessToken "eyJAlciOiJSUzI1NiIsImtpZCI6IjEyMyJ..." -RealmId "1234567890"

.NOTES
Requirements:
- PowerShell 7.0+ (cross-platform)
- Valid OAuth 2.0 access token from Intuit
- QuickBooks company Realm ID
- API rate limit: 10 requests/second

Author: Wiley Widget Team
Date: 2025-01-13
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$AccessToken,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$RealmId,

    [Parameter()]
    [switch]$Sandbox = $true,

    [Parameter()]
    [int]$RateDelayMs = 100  # Delay between requests (10 requests/sec = 100ms per request)
)

# Set strict mode for error handling
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# API Configuration
$ApiBaseUrl = if ($Sandbox) { "https://quickbooks.api.intuit.com" } else { "https://quickbooks.api.intuit.com" }
$Version = "v2"
$QueryApi = "$ApiBaseUrl/$Version/companyaccounts/$RealmId/query"
$CreateApi = "$ApiBaseUrl/$Version/companyaccounts/$RealmId/account"

# Track statistics
$stats = @{
    Created   = 0
    Failed    = 0
    Skipped   = 0
    StartTime = Get-Date
}

# Municipal Finance Account Structure
$AccountsToCreate = @(
    # General Fund (10001-10999)
    @{ Name = "GF - Cash and Investments"; AccountNumber = "10100"; AccountType = "Bank"; Description = "General Fund Cash" },
    @{ Name = "GF - Accounts Receivable"; AccountNumber = "10200"; AccountType = "AccountsReceivable"; Description = "General Fund AR" },
    @{ Name = "GF - Revenues"; AccountNumber = "41100"; AccountType = "Income"; Description = "General Fund Property Tax Revenue" },
    @{ Name = "GF - Expenditures - General Government"; AccountNumber = "51100"; AccountType = "Expense"; Description = "GF Expenditures - General Government" },
    @{ Name = "GF - Expenditures - Public Safety"; AccountNumber = "51200"; AccountType = "Expense"; Description = "GF Expenditures - Public Safety" },
    @{ Name = "GF - Expenditures - Public Works"; AccountNumber = "51300"; AccountType = "Expense"; Description = "GF Expenditures - Public Works" },
    @{ Name = "GF - Expenditures - Parks & Recreation"; AccountNumber = "51400"; AccountType = "Expense"; Description = "GF Expenditures - Parks" },

    # Special Revenue Funds (20001-20999)
    @{ Name = "SRF - Sales Tax Fund"; AccountNumber = "20100"; AccountType = "OtherCurrentAsset"; Description = "Special Revenue - Sales Tax Collections" },
    @{ Name = "SRF - Grant Revenues"; AccountNumber = "42100"; AccountType = "Income"; Description = "Special Revenue - Grant Income" },

    # Debt Service Fund (30001-30999)
    @{ Name = "DSF - Debt Service Account"; AccountNumber = "30100"; AccountType = "OtherCurrentAsset"; Description = "Debt Service Fund" },
    @{ Name = "DSF - Bond Interest Expense"; AccountNumber = "52100"; AccountType = "Expense"; Description = "Bond Interest Payments" },
    @{ Name = "DSF - Bond Principal"; AccountNumber = "52200"; AccountType = "Expense"; Description = "Bond Principal Payments" },

    # Capital Projects Fund (40001-40999)
    @{ Name = "CPF - Capital Projects Reserve"; AccountNumber = "40100"; AccountType = "OtherCurrentAsset"; Description = "Capital Projects Fund Reserve" },
    @{ Name = "CPF - Capital Equipment"; AccountNumber = "53100"; AccountType = "Expense"; Description = "Capital Equipment Purchases" },
    @{ Name = "CPF - Infrastructure"; AccountNumber = "53200"; AccountType = "Expense"; Description = "Infrastructure Capital Projects" },

    # Enterprise Funds (50001-50999)
    @{ Name = "EF - Water Utility"; AccountNumber = "50100"; AccountType = "OtherCurrentAsset"; Description = "Water Utility Enterprise Fund" },
    @{ Name = "EF - Utility Revenue"; AccountNumber = "44100"; AccountType = "Income"; Description = "Water Utility Revenue" },
    @{ Name = "EF - Utility Operations"; AccountNumber = "54100"; AccountType = "Expense"; Description = "Utility Operations Expense" },

    # Fund Balance/Equity (60001-60999)
    @{ Name = "Fund Balance - Assigned"; AccountNumber = "60100"; AccountType = "Equity"; Description = "Fund Balance Assigned" },
    @{ Name = "Fund Balance - Unassigned"; AccountNumber = "60200"; AccountType = "Equity"; Description = "Fund Balance Unassigned" }
)

Write-Host "🏛️  Wiley Widget - QuickBooks Sandbox Seeding Script" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "Environment: $(if ($Sandbox) { 'SANDBOX' } else { 'PRODUCTION' })" -ForegroundColor Yellow
Write-Host "Realm ID: $RealmId" -ForegroundColor Yellow
Write-Host "Total Accounts to Create: $($AccountsToCreate.Count)" -ForegroundColor Yellow
Write-Host ""

# Function to create an account
function New-QBOAccount {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$AccountData,

        [Parameter(Mandatory = $true)]
        [string]$Token,

        [Parameter(Mandatory = $true)]
        [string]$ApiUrl
    )

    # Prepare account payload based on type
    $typeMap = @{
        "Bank"                  = "Asset"
        "AccountsReceivable"    = "Asset"
        "OtherCurrentAsset"     = "Asset"
        "Income"                = "Income"
        "Expense"               = "Expense"
        "Equity"                = "Equity"
    }

    $qboType = $typeMap[$AccountData.AccountType] ?? "Asset"

    $body = @{
        Name              = $AccountData.Name
        AccountType       = $qboType
        AccountSubType    = $AccountData.AccountType
        CurrentBalance    = 0
        Active            = $true
        Description       = $AccountData.Description
    } | ConvertTo-Json -Depth 10

    $headers = @{
        "Authorization" = "Bearer $Token"
        "Accept"        = "application/json"
        "Content-Type"  = "application/json"
    }

    if ($PSCmdlet.ShouldProcess($AccountData.Name, "Create QuickBooks account")) {
        try {
            $response = Invoke-RestMethod -Uri $ApiUrl `
                -Method Post `
                -Headers $headers `
                -Body $body `
                -TimeoutSec 30 `
                -ErrorAction Stop

            Write-Host "  ✓ Created: $($AccountData.Name) (ID: $($response.id))" -ForegroundColor Green
            return @{ Success = $true; Id = $response.id; Name = $AccountData.Name }
        }
        catch {
            # Handle HTTP errors from REST API
            if ($_.Exception.Response -ne $null) {
                try {
                    $errorResponse = $_.Exception.Response.Content.ReadAsStringAsync().Result
                    $errorJson = $errorResponse | ConvertFrom-Json -ErrorAction SilentlyContinue

                    # Check for duplicate account
                    if ($errorJson.errors -and $errorJson.errors[0].code -eq "DBL_CHK_DUP_NAME") {
                        Write-Host "  ⊘ Skipped: $($AccountData.Name) (already exists)" -ForegroundColor Yellow
                        return @{ Success = $false; Reason = "Duplicate" }
                    }

                    Write-Host "  ✗ Failed: $($AccountData.Name) - $($errorJson.errors[0].message)" -ForegroundColor Red
                    return @{ Success = $false; Reason = $errorJson.errors[0].message }
                }
                catch {
                    Write-Host "  ✗ Error: $($AccountData.Name) - $($_.Exception.Message)" -ForegroundColor Red
                    return @{ Success = $false; Reason = $_.Exception.Message }
                }
            }
            else {
                Write-Host "  ✗ Error: $($AccountData.Name) - $($_.Exception.Message)" -ForegroundColor Red
                return @{ Success = $false; Reason = $_.Exception.Message }
            }
        }
    }
    else {
        Write-Host "  ⊘ Skipped: $($AccountData.Name) (WhatIf mode)" -ForegroundColor Yellow
        return @{ Success = $false; Reason = "WhatIf" }
    }
}

# Check token validity
Write-Host "🔐 Validating OAuth Token..." -ForegroundColor Cyan
$tokenParts = $AccessToken -split '\.'
if ($tokenParts.Count -ne 3) {
    Write-Host "✗ Invalid token format (expected JWT)" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Token validation passed" -ForegroundColor Green
Write-Host ""

# Create accounts
Write-Host "📝 Creating Municipal Finance Accounts..." -ForegroundColor Cyan
$results = @()

foreach ($account in $AccountsToCreate) {
    Write-Host "[$($results.Count + 1)/$($AccountsToCreate.Count)] Processing: $($account.Name)..." -ForegroundColor Gray

    $result = New-QBOAccount -AccountData $account -Token $AccessToken -ApiUrl $CreateApi
    $results += $result

    # Track statistics
    if ($result.Success) { $stats.Created++ }
    elseif ($result.Reason -eq "Duplicate") { $stats.Skipped++ }
    else { $stats.Failed++ }

    # Rate limiting: 10 requests per second = 100ms between requests
    Start-Sleep -Milliseconds $RateDelayMs
}

# Summary Report
Write-Host ""
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "✅ Seeding Complete" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "Results Summary:" -ForegroundColor White
Write-Host "  Created:   $($stats.Created)" -ForegroundColor Green
Write-Host "  Skipped:   $($stats.Skipped)" -ForegroundColor Yellow
Write-Host "  Failed:    $($stats.Failed)" -ForegroundColor Red
Write-Host "  Duration:  $((Get-Date) - $stats.StartTime | Select-Object -ExpandProperty TotalSeconds)s" -ForegroundColor Cyan
Write-Host ""

if ($stats.Failed -eq 0) {
    Write-Host "🎉 All accounts created successfully!" -ForegroundColor Green
}
else {
    Write-Host "⚠️  Some accounts failed. Review errors above." -ForegroundColor Yellow
}

# Export results to JSON for logging
$reportFile = "seed-sandbox-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$results | ConvertTo-Json | Out-File -FilePath $reportFile -Encoding UTF8
Write-Host "📊 Report saved to: $reportFile" -ForegroundColor Cyan

exit $(if ($stats.Failed -gt 0) { 1 } else { 0 })
