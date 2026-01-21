#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Seed QuickBooks Sandbox with Municipal Finance Accounts (Wiley Widget compatible)

.DESCRIPTION
    Populates a QuickBooks Online sandbox environment with a municipal finance account
    structure that matches Wiley Widget's requirements. Uses the QuickBooks REST API
    (Intuit IPP SDK REST API) to create accounts programmatically.

    Prerequisites:
    - Valid QuickBooks sandbox OAuth token
    - Realm ID from OAuth callback
    - Client ID and Secret registered at developer.intuit.com

.PARAMETER AccessToken
    OAuth 2.0 access token (from successful authorization)

.PARAMETER RealmId
    QuickBooks company/realm ID (from OAuth callback as realmId parameter)

.PARAMETER ClientId
    Intuit app Client ID (from developer.intuit.com app settings)

.PARAMETER ClientSecret
    Intuit app Client Secret (from developer.intuit.com app settings)

.PARAMETER Environment
    QuickBooks environment: "sandbox" or "production" (default: "sandbox")

.PARAMETER OutputFile
    Optional: Save created accounts JSON to file for reference

.EXAMPLE
    # Load credentials from environment and seed sandbox
    .\seed-sandbox-qbo.ps1 -AccessToken $env:QBO_ACCESS_TOKEN `
                          -RealmId $env:QBO_REALM_ID `
                          -ClientId $env:QBO_CLIENT_ID `
                          -ClientSecret $env:QBO_CLIENT_SECRET

.EXAMPLE
    # Seed and save results
    .\seed-sandbox-qbo.ps1 -AccessToken $token `
                          -RealmId $realm `
                          -ClientId $id `
                          -ClientSecret $secret `
                          -OutputFile "created-accounts.json"

.NOTES
    Author: Wiley Widget Dev Team
    Date: January 17, 2026

    Account Structure Created:
    - 1000: General Fund Checking (Bank)
    - 1100: General Fund Savings (Bank)
    - 1500: Equipment & Vehicles (Fixed Asset)
    - 2000: Accounts Payable (AP)
    - 3000: Fund Balance (Equity)
    - 4000: Property Tax Revenue (Income)
    - 4100: License & Permit Revenue (Income)
    - 4200: Intergovernmental Revenue (Income)
    - 5100: Police Department Salaries (Expense)
    - 5110: Fire Department Salaries (Expense)
    - 5120: Public Works Salaries (Expense)
    - 5200: Police Department Supplies (Expense)
    - 5210: Fire Department Supplies (Expense)
    - 5300: Utilities Expense (Expense)
    - 5400: Equipment Maintenance (Expense)
    - 5500: Depreciation Expense (Expense)
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$AccessToken,

    [Parameter(Mandatory = $true)]
    [string]$RealmId,

    [Parameter(Mandatory = $false)]
    [string]$ClientId,

    [Parameter(Mandatory = $false)]
    [string]$ClientSecret,

    [Parameter(Mandatory = $false)]
    [ValidateSet("sandbox", "production")]
    [string]$Environment = "sandbox",

    [Parameter(Mandatory = $false)]
    [string]$OutputFile
)

# QuickBooks API base URL
$baseUrl = if ($Environment -eq "sandbox") {
    "https://sandbox-quickbooks.api.intuit.com"
} else {
    "https://quickbooks.api.intuit.com"
}

# Prepare headers with OAuth token
$headers = @{
    "Authorization" = "Bearer $AccessToken"
    "Accept"        = "application/json"
    "Content-Type"  = "application/json"
}

Write-Host "🌱 Seeding QuickBooks $Environment Sandbox for Wiley Widget"
Write-Host "   RealmId: $RealmId"
Write-Host "   Base URL: $baseUrl"
Write-Host ""

# Array to store created accounts
$createdAccounts = @()
$accountCounter = 0
$errorCounter = 0

# Municipal Finance Account Structure for Wiley Widget
$municipalAccounts = @(
    # --- ASSETS (1000-1999) ---
    @{
        Name        = "General Fund Checking"
        AcctNum     = "1000"
        Description = "Primary operating checking account for General Fund"
        Type        = "Bank"
        Active      = $true
    },
    @{
        Name        = "General Fund Savings"
        AcctNum     = "1100"
        Description = "Reserve/savings account for General Fund"
        Type        = "Bank"
        Active      = $true
    },
    @{
        Name        = "Equipment & Vehicles"
        AcctNum     = "1500"
        Description = "Capital equipment and vehicle assets"
        Type        = "FixedAsset"
        Active      = $true
    },
    @{
        Name        = "Accumulated Depreciation"
        AcctNum     = "1550"
        Description = "Accumulated depreciation on fixed assets"
        Type        = "OtherCurrentAsset"
        Active      = $true
    },
    # --- LIABILITIES (2000-2999) ---
    @{
        Name        = "Accounts Payable"
        AcctNum     = "2000"
        Description = "Vendor and service provider payables"
        Type        = "AccountsPayable"
        Active      = $true
    },
    @{
        Name        = "Accrued Payroll"
        AcctNum     = "2100"
        Description = "Accrued employee salaries and benefits"
        Type        = "OtherCurrentLiability"
        Active      = $true
    },
    # --- EQUITY (3000-3999) ---
    @{
        Name        = "Fund Balance"
        AcctNum     = "3000"
        Description = "General Fund undesignated balance"
        Type        = "Equity"
        Active      = $true
    },
    # --- REVENUE (4000-4999) ---
    @{
        Name        = "Property Tax Revenue"
        AcctNum     = "4000"
        Description = "Property tax collections"
        Type        = "Income"
        Active      = $true
    },
    @{
        Name        = "License & Permit Revenue"
        AcctNum     = "4100"
        Description = "Building permits, business licenses, fees"
        Type        = "Income"
        Active      = $true
    },
    @{
        Name        = "Intergovernmental Revenue"
        AcctNum     = "4200"
        Description = "State grants, federal funding, shared revenues"
        Type        = "Income"
        Active      = $true
    },
    @{
        Name        = "Fines & Forfeitures"
        AcctNum     = "4300"
        Description = "Traffic fines, court fees, parking violations"
        Type        = "Income"
        Active      = $true
    },
    # --- EXPENSES (5000-5999) ---
    @{
        Name        = "Police Department Salaries"
        AcctNum     = "5100"
        Description = "Police officer salaries and benefits"
        Type        = "Expense"
        Active      = $true
    },
    @{
        Name        = "Fire Department Salaries"
        AcctNum     = "5110"
        Description = "Firefighter salaries and benefits"
        Type        = "Expense"
        Active      = $true
    },
    @{
        Name        = "Public Works Salaries"
        AcctNum     = "5120"
        Description = "Road crew and maintenance worker salaries"
        Type        = "Expense"
        Active      = $true
    },
    @{
        Name        = "Police Department Supplies"
        AcctNum     = "5200"
        Description = "Uniforms, equipment, consumables for police"
        Type        = "Expense"
        Active      = $true
    },
    @{
        Name        = "Fire Department Supplies"
        AcctNum     = "5210"
        Description = "Equipment and supplies for fire department"
        Type        = "Expense"
        Active      = $true
    },
    @{
        Name        = "Utilities Expense"
        AcctNum     = "5300"
        Description = "Electric, water, gas, telecommunications"
        Type        = "Expense"
        Active      = $true
    },
    @{
        Name        = "Equipment Maintenance"
        AcctNum     = "5400"
        Description = "Vehicle maintenance, equipment repair, upkeep"
        Type        = "Expense"
        Active      = $true
    },
    @{
        Name        = "Depreciation Expense"
        AcctNum     = "5500"
        Description = "Annual depreciation of capital assets"
        Type        = "Expense"
        Active      = $true
    },
    @{
        Name        = "Professional Services"
        AcctNum     = "5600"
        Description = "Legal, accounting, consulting services"
        Type        = "Expense"
        Active      = $true
    },
    @{
        Name        = "Training & Development"
        AcctNum     = "5700"
        Description = "Employee training, conferences, professional development"
        Type        = "Expense"
        Active      = $true
    }
)

Write-Host "📋 Creating $($municipalAccounts.Count) accounts..."
Write-Host ""

foreach ($account in $municipalAccounts) {
    try {
        # Build request body according to QuickBooks Account entity schema
        $requestBody = @{
            Name           = $account.Name
            AcctNum        = $account.AcctNum
            Description    = $account.Description
            AccountType    = $account.Type
            Active         = $account.Active
        } | ConvertTo-Json

        # POST to QuickBooks Account endpoint
        $url = "$baseUrl/v2/company/$RealmId/account"

        Write-Host "  ➤ Creating: $($account.AcctNum) - $($account.Name)" -ForegroundColor Cyan

        $response = Invoke-RestMethod -Uri $url `
            -Method POST `
            -Headers $headers `
            -Body $requestBody `
            -ErrorAction Stop

        if ($response) {
            $accountCounter++
            $createdAccounts += @{
                Name     = $account.Name
                AcctNum  = $account.AcctNum
                Type     = $account.Type
                Created  = $response.Account.CreateTime
                Id       = $response.Account.Id
            }
            Write-Host "    ✓ Success (ID: $($response.Account.Id))" -ForegroundColor Green
        }
    }
    catch {
        $errorCounter++
        Write-Host "    ✗ Failed: $($_.Exception.Message)" -ForegroundColor Red

        # Continue to next account on failure
        if ($_.ErrorDetails) {
            Write-Host "    Details: $($_.ErrorDetails)" -ForegroundColor Yellow
        }
    }

    # Small delay between requests to respect API rate limits
    Start-Sleep -Milliseconds 500
}

Write-Host ""
Write-Host "=" * 60
Write-Host "📊 Seeding Complete"
Write-Host "=" * 60
Write-Host "✓ Created: $accountCounter accounts"
Write-Host "✗ Failed:  $errorCounter accounts"
Write-Host "Total:    $($accountCounter + $errorCounter) accounts"
Write-Host ""

# Save results to file if requested
if ($OutputFile) {
    $createdAccounts | ConvertTo-Json | Out-File -FilePath $OutputFile -Encoding UTF8
    Write-Host "💾 Results saved to: $OutputFile"
}

# Summary table
if ($createdAccounts.Count -gt 0) {
    Write-Host ""
    Write-Host "Created Accounts Summary:"
    Write-Host ""
    $createdAccounts | Format-Table -AutoSize -Property AcctNum, Name, Type
}

# Next steps
Write-Host ""
Write-Host "🎯 Next Steps:"
Write-Host "  1. Verify accounts in QuickBooks Sandbox:"
Write-Host "     https://app.sandbox.qbo.intuit.com → Settings → Chart of Accounts"
Write-Host ""
Write-Host "  2. Run Wiley Widget and sync accounts:"
Write-Host "     Dashboard → 'Sync Now' button"
Write-Host ""
Write-Host "  3. Verify in Wiley Widget Dashboard grid:"
Write-Host "     All $accountCounter accounts should appear"
Write-Host ""

# Return success/failure
exit if ($errorCounter -eq 0) { 0 } else { 1 }
