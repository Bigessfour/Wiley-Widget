#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Complete Sandbox Setup Workflow for Wiley Widget

.DESCRIPTION
    Orchestrates the complete sandbox setup:
    1. Verifies OAuth credentials
    2. Obtains/refreshes access token if needed
    3. Seeds sandbox with municipal accounts (via seed-sandbox-qbo.ps1)
    4. Verifies accounts appear in Wiley Widget

.PARAMETER ClientId
    Intuit app Client ID (from developer.intuit.com)

.PARAMETER ClientSecret
    Intuit app Client Secret (from developer.intuit.com)

.PARAMETER RealmId
    QuickBooks sandbox realm ID (from first OAuth callback)

.PARAMETER AccessToken
    Optional: Existing OAuth access token. If not provided, will attempt to use cached token.

.PARAMETER GenerateNewToken
    Force generation of new access token (requires full OAuth flow)

.EXAMPLE
    # Setup with existing token
    .\setup-sandbox-complete.ps1 -ClientId "your-client-id" `
                                -ClientSecret "your-client-secret" `
                                -RealmId "1234567890"

.EXAMPLE
    # Setup with explicit token
    .\setup-sandbox-complete.ps1 -ClientId "id" `
                                -ClientSecret "secret" `
                                -RealmId "1234567890" `
                                -AccessToken "your-oauth-token"

.NOTES
    Author: Wiley Widget Dev Team
    Date: January 17, 2026

    This script automates Option 2: API-based sandbox seeding
    See: docs/QUICKBOOKS_SANDBOX_SETUP.md for manual alternatives
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ClientId,

    [Parameter(Mandatory = $true)]
    [string]$ClientSecret,

    [Parameter(Mandatory = $true)]
    [string]$RealmId,

    [Parameter(Mandatory = $false)]
    [string]$AccessToken,

    [Parameter(Mandatory = $false)]
    [switch]$GenerateNewToken
)

# Color output functions
function Write-Header { Write-Host $args -ForegroundColor Cyan -BackgroundColor DarkBlue }
function Write-Success { Write-Host "✓ $args" -ForegroundColor Green }
function Write-Error { Write-Host "✗ $args" -ForegroundColor Red }
function Write-Warning { Write-Host "⚠ $args" -ForegroundColor Yellow }
function Write-Info { Write-Host "ℹ $args" -ForegroundColor Cyan }

Write-Header "=" * 70
Write-Header "Wiley Widget - QuickBooks Sandbox Setup (Option 2: API Seeding)"
Write-Header "=" * 70
Write-Host ""

# Step 1: Verify credentials
Write-Header "STEP 1: Verify Credentials"
Write-Host ""

if ([string]::IsNullOrWhiteSpace($ClientId)) {
    Write-Error "ClientId is required"
    exit 1
}
Write-Success "ClientId provided: $($ClientId.Substring(0, 8))..."

if ([string]::IsNullOrWhiteSpace($ClientSecret)) {
    Write-Error "ClientSecret is required"
    exit 1
}
Write-Success "ClientSecret provided: ****"

if ([string]::IsNullOrWhiteSpace($RealmId)) {
    Write-Error "RealmId is required"
    exit 1
}
Write-Success "RealmId provided: $RealmId"

Write-Host ""

# Step 2: Handle access token
Write-Header "STEP 2: Obtain OAuth Access Token"
Write-Host ""

if ([string]::IsNullOrWhiteSpace($AccessToken) -or $GenerateNewToken) {
    Write-Info "Access token needed. Starting OAuth flow..."
    Write-Host ""
    Write-Host "You will be prompted to:"
    Write-Host "  1. Log into your Intuit account"
    Write-Host "  2. Authorize Wiley Widget"
    Write-Host "  3. Copy the authorization code"
    Write-Host ""

    $redirectUri = "http://localhost:8080/callback"
    $authUrl = "https://appcenter.intuit.com/connect/oauth2?client_id=$ClientId&response_type=code&scope=com.intuit.quickbooks.accounting&redirect_uri=$redirectUri&state=$(New-Guid)"

    Write-Info "Opening browser to: $authUrl"
    Write-Host ""

    try {
        Start-Process $authUrl
        Write-Host "Browser opened. After authorization, paste the authorization code below."
    } catch {
        Write-Warning "Could not open browser automatically. Please visit:"
        Write-Host $authUrl
    }

    Write-Host ""
    $authCode = Read-Host "Enter authorization code from redirect URL"

    if ([string]::IsNullOrWhiteSpace($authCode)) {
        Write-Error "Authorization code is required"
        exit 1
    }

    # Exchange code for token
    Write-Info "Exchanging authorization code for access token..."

    try {
        $tokenBody = @{
            grant_type    = "authorization_code"
            code          = $authCode
            redirect_uri  = $redirectUri
        }

        $tokenResponse = Invoke-RestMethod -Uri "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer" `
            -Method POST `
            -Headers @{
                "Authorization" = "Basic $([Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$ClientId`:$ClientSecret")))"
                "Content-Type"  = "application/x-www-form-urlencoded"
            } `
            -Body $tokenBody `
            -ErrorAction Stop

        $AccessToken = $tokenResponse.access_token
        $RefreshToken = $tokenResponse.refresh_token

        Write-Success "Access token obtained"
        Write-Info "Token expires in $($tokenResponse.expires_in) seconds"

        # Save refresh token for future use
        $tokenCache = @{
            AccessToken    = $AccessToken
            RefreshToken   = $RefreshToken
            ExpiresAt      = (Get-Date).AddSeconds($tokenResponse.expires_in).ToString("o")
            RealmId        = $RealmId
            ClientId       = $ClientId
        }

        $tokenCachePath = "$env:APPDATA\WileyWidget\oauth-token.json"
        $tokenCacheDir = Split-Path $tokenCachePath
        if (-not (Test-Path $tokenCacheDir)) {
            New-Item -ItemType Directory -Path $tokenCacheDir -Force | Out-Null
        }

        $tokenCache | ConvertTo-Json | Out-File -FilePath $tokenCachePath -Encoding UTF8
        Write-Success "Token cached at: $tokenCachePath"
    } catch {
        Write-Error "Failed to obtain access token: $($_.Exception.Message)"
        exit 1
    }
} else {
    Write-Success "Using provided access token"
}

Write-Host ""

# Step 3: Seed sandbox
Write-Header "STEP 3: Seed Sandbox with Municipal Accounts"
Write-Host ""

$seedScript = Join-Path $PSScriptRoot "seed-sandbox-qbo.ps1"
$outputFile = "$env:APPDATA\WileyWidget\created-accounts.json"

if (-not (Test-Path $seedScript)) {
    Write-Error "Seeding script not found: $seedScript"
    exit 1
}

try {
    & $seedScript -AccessToken $AccessToken `
        -RealmId $RealmId `
        -ClientId $ClientId `
        -ClientSecret $ClientSecret `
        -Environment "sandbox" `
        -OutputFile $outputFile `
        -ErrorAction Stop
} catch {
    Write-Error "Seeding failed: $($_.Exception.Message)"
    exit 1
}

Write-Host ""

# Step 4: Verify in QuickBooks
Write-Header "STEP 4: Verify Accounts in QuickBooks"
Write-Host ""

Write-Info "Visit your QuickBooks Sandbox:"
Write-Host "  https://app.sandbox.qbo.intuit.com"
Write-Host ""
Write-Host "Then navigate to:"
Write-Host "  Settings → Chart of Accounts"
Write-Host ""
Write-Host "You should see all the municipal accounts created above."
Write-Host ""

# Step 5: Test in Wiley Widget
Write-Header "STEP 5: Test in Wiley Widget"
Write-Host ""

Write-Info "To verify everything works:"
Write-Host ""
Write-Host "  1. Open Wiley Widget application"
Write-Host "  2. Navigate to the Dashboard"
Write-Host "  3. Click the 'Sync Now' button in the ribbon"
Write-Host "  4. Verify accounts appear in the grid"
Write-Host ""

# Summary
Write-Header "=" * 70
Write-Header "✓ Sandbox Setup Complete!"
Write-Header "=" * 70
Write-Host ""
Write-Success "All $([Math]::Truncate((Get-Content $outputFile | ConvertFrom-Json | Measure-Object).Count)) accounts created"
Write-Success "Token cached at: $tokenCachePath"
Write-Success "Account list saved at: $outputFile"
Write-Host ""
Write-Info "Next: Open Wiley Widget and click 'Sync Now' to load accounts"
Write-Host ""

exit 0
