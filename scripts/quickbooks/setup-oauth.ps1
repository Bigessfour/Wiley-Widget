#Requires -Version 7.5.4
<#
.SYNOPSIS
    Establishes QuickBooks Online OAuth connection for sandbox environment.

.DESCRIPTION
    This script orchestrates the complete OAuth 2.0 flow for QuickBooks Online:
    1. Loads credentials from environment variables or secrets
    2. Builds and displays the authorization URL
    3. Accepts the authorization code from user
    4. Exchanges code for access/refresh tokens
    5. Persists tokens to settings.json
    6. Validates the connection by calling QBO API

.PARAMETER ClientId
    QuickBooks client ID (optional, reads from QBO_CLIENT_ID env var if not provided)

.PARAMETER ClientSecret
    QuickBooks client secret (optional, reads from QBO_CLIENT_SECRET env var if not provided)

.PARAMETER RedirectUri
    OAuth redirect URI (optional, defaults to OAuth Playground URL)

.PARAMETER RealmId
    QuickBooks company/realm ID (optional, reads from QBO_REALM_ID env var if not provided)

.PARAMETER Environment
    QuickBooks environment: 'sandbox' or 'production' (default: sandbox)

.PARAMETER SettingsDir
    Directory for settings.json (optional, reads from WILEYWIDGET_SETTINGS_DIR or defaults to AppData)

.EXAMPLE
    .\setup-oauth.ps1
    Runs interactive OAuth setup using environment variables for credentials

.EXAMPLE
    .\setup-oauth.ps1 -ClientId "ABC123" -ClientSecret "XYZ789" -RealmId "9341455168020461"
    Runs OAuth setup with explicit credentials
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ClientId,

    [Parameter(Mandatory = $false)]
    [string]$ClientSecret,

    [Parameter(Mandatory = $false)]
    [string]$RedirectUri = "https://developer.intuit.com/v2/OAuth2Playground/RedirectUrl",

    [Parameter(Mandatory = $false)]
    [string]$RealmId,

    [Parameter(Mandatory = $false)]
    [ValidateSet('sandbox', 'production')]
    [string]$Environment = 'sandbox',

    [Parameter(Mandatory = $false)]
    [string]$SettingsDir
)

$ErrorActionPreference = 'Stop'

# ANSI color codes for output formatting
$script:ColorReset = "`e[0m"
$script:ColorGreen = "`e[32m"
$script:ColorYellow = "`e[33m"
$script:ColorRed = "`e[31m"
$script:ColorCyan = "`e[36m"
$script:ColorBold = "`e[1m"

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = $script:ColorReset
    )
    Write-Host "${Color}${Message}${script:ColorReset}"
}

function Write-Step {
    param([string]$Message)
    Write-ColorOutput "`n▶ $Message" $script:ColorCyan
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "✓ $Message" $script:ColorGreen
}

function Write-Warning {
    param([string]$Message)
    Write-ColorOutput "⚠ $Message" $script:ColorYellow
}

function Write-Error {
    param([string]$Message)
    Write-ColorOutput "✗ $Message" $script:ColorRed
}

# Step 1: Load credentials
Write-Step "Loading QuickBooks credentials..."

if ([string]::IsNullOrWhiteSpace($ClientId)) {
    $ClientId = $env:QBO_CLIENT_ID
    if ([string]::IsNullOrWhiteSpace($ClientId)) {
        Write-Error "QBO_CLIENT_ID not set. Set environment variable or pass -ClientId parameter."
        Write-Host "`nTo set environment variable (current session):"
        Write-Host '  $env:QBO_CLIENT_ID = "your-client-id"'
        exit 1
    }
}

if ([string]::IsNullOrWhiteSpace($ClientSecret)) {
    $ClientSecret = $env:QBO_CLIENT_SECRET
    if ([string]::IsNullOrWhiteSpace($ClientSecret)) {
        Write-Error "QBO_CLIENT_SECRET not set. Set environment variable or pass -ClientSecret parameter."
        Write-Host "`nTo set environment variable (current session):"
        Write-Host '  $env:QBO_CLIENT_SECRET = "your-client-secret"'
        exit 1
    }
}

if ([string]::IsNullOrWhiteSpace($RealmId)) {
    $RealmId = $env:QBO_REALM_ID
}

Write-Success "Credentials loaded"
Write-Host "  Client ID: $($ClientId.Substring(0, [Math]::Min(8, $ClientId.Length)))..."
Write-Host "  Environment: $Environment"
Write-Host "  Redirect URI: $RedirectUri"
if (-not [string]::IsNullOrWhiteSpace($RealmId)) {
    Write-Host "  Realm ID: $RealmId"
}

# Step 2: Build authorization URL
Write-Step "Building authorization URL..."

$state = [Guid]::NewGuid().ToString("N")
$scope = "com.intuit.quickbooks.accounting"
$authEndpoint = "https://appcenter.intuit.com/connect/oauth2"

$authUrl = "${authEndpoint}?client_id=$([Uri]::EscapeDataString($ClientId))&response_type=code&scope=$([Uri]::EscapeDataString($scope))&redirect_uri=$([Uri]::EscapeDataString($RedirectUri))&state=$([Uri]::EscapeDataString($state))"

Write-Success "Authorization URL generated"
Write-ColorOutput "`n$($script:ColorBold)Open this URL in your browser to authorize:$($script:ColorReset)" $script:ColorYellow
Write-Host $authUrl

# Copy to clipboard if available
try {
    Set-Clipboard -Value $authUrl
    Write-Success "URL copied to clipboard"
} catch {
    Write-Warning "Could not copy to clipboard (clipboard not available)"
}

# Step 3: Get authorization code from user
Write-Step "Waiting for authorization code..."
Write-Host "`nAfter authorizing in your browser, you'll be redirected to a URL containing the code."
Write-Host "Example: ${RedirectUri}?code=AB11762635910...&state=...&realmId=9341455168020461"
Write-Host ""

$code = Read-Host "Paste the authorization code here (just the code value, not the full URL)"
$code = $code.Trim()

if ([string]::IsNullOrWhiteSpace($code)) {
    Write-Error "No authorization code provided. Exiting."
    exit 1
}

# Extract realm ID if user pasted full URL
if ($code -match 'code=([^&]+)') {
    $code = $Matches[1]
}
if ($code -match 'realmId=(\d+)' -and [string]::IsNullOrWhiteSpace($RealmId)) {
    $RealmId = $Matches[1]
    Write-Success "Extracted Realm ID: $RealmId"
}

Write-Success "Authorization code received"

# Step 4: Exchange code for tokens
Write-Step "Exchanging authorization code for tokens..."

$tokenEndpoint = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer"
$credentials = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${ClientId}:${ClientSecret}"))

$tokenBody = @{
    grant_type   = 'authorization_code'
    code         = $code
    redirect_uri = $RedirectUri
}

try {
    $tokenResponse = Invoke-RestMethod -Uri $tokenEndpoint -Method Post `
        -Headers @{ Authorization = "Basic $credentials" } `
        -Body $tokenBody `
        -ContentType 'application/x-www-form-urlencoded'

    Write-Success "Tokens received successfully"

    $accessToken = $tokenResponse.access_token
    $refreshToken = $tokenResponse.refresh_token
    $expiresIn = $tokenResponse.expires_in
    $tokenExpiry = (Get-Date).ToUniversalTime().AddSeconds($expiresIn)

    Write-Host "  Access Token: $($accessToken.Substring(0, [Math]::Min(20, $accessToken.Length)))..."
    Write-Host "  Refresh Token: $($refreshToken.Substring(0, [Math]::Min(20, $refreshToken.Length)))..."
    Write-Host "  Expires: $($tokenExpiry.ToString('yyyy-MM-ddTHH:mm:ssZ'))"

} catch {
    Write-Error "Token exchange failed: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        $errorBody = $reader.ReadToEnd()
        Write-Host "Error response: $errorBody"
    }
    exit 1
}

# Step 5: Determine settings file location
Write-Step "Locating settings file..."

if ([string]::IsNullOrWhiteSpace($SettingsDir)) {
    $SettingsDir = $env:WILEYWIDGET_SETTINGS_DIR
    if ([string]::IsNullOrWhiteSpace($SettingsDir)) {
        $SettingsDir = Join-Path $env:APPDATA "WileyWidget"
    }
}

if (-not (Test-Path $SettingsDir)) {
    New-Item -ItemType Directory -Path $SettingsDir -Force | Out-Null
    Write-Success "Created settings directory: $SettingsDir"
}

$settingsFile = Join-Path $SettingsDir "settings.json"
Write-Host "  Settings file: $settingsFile"

# Step 6: Update or create settings file
Write-Step "Persisting tokens to settings file..."

$settings = @{
    QboAccessToken            = $accessToken
    QboRefreshToken           = $refreshToken
    QboTokenExpiry            = $tokenExpiry.ToString('yyyy-MM-ddTHH:mm:ssZ')
    QuickBooksAccessToken     = $null
    QuickBooksRefreshToken    = $null
    QuickBooksTokenExpiresUtc = $null
}

if ($RealmId) {
    $settings.QuickBooksRealmId = $RealmId
}

# Load existing settings if present
if (Test-Path $settingsFile) {
    try {
        $existingSettings = Get-Content $settingsFile -Raw | ConvertFrom-Json -AsHashtable
        # Merge with existing settings (preserve other fields)
        foreach ($key in $existingSettings.Keys) {
            if (-not $settings.ContainsKey($key)) {
                $settings[$key] = $existingSettings[$key]
            }
        }
        Write-Success "Loaded existing settings"
    } catch {
        Write-Warning "Could not parse existing settings file, creating new one"
    }
}

# Save settings
try {
    $settings | ConvertTo-Json -Depth 10 | Set-Content $settingsFile -Encoding UTF8
    Write-Success "Tokens persisted to $settingsFile"
} catch {
    Write-Error "Failed to save settings: $($_.Exception.Message)"
    exit 1
}

# Step 7: Validate connection
Write-Step "Validating QuickBooks connection..."

if ([string]::IsNullOrWhiteSpace($RealmId)) {
    Write-Warning "Realm ID not provided, skipping connection test"
    Write-Host "`nTo test the connection later, run:"
    Write-Host "  `$env:QBO_REALM_ID = 'your-realm-id'"
    Write-Host "  .\setup-oauth.ps1"
} else {
    $baseUrl = if ($Environment -eq 'sandbox') {
        "https://sandbox-quickbooks.api.intuit.com"
    } else {
        "https://quickbooks.api.intuit.com"
    }

    $companyInfoUrl = "${baseUrl}/v3/company/${RealmId}/companyinfo/${RealmId}"

    try {
        $companyInfo = Invoke-RestMethod -Uri $companyInfoUrl -Method Get `
            -Headers @{
            Authorization = "Bearer $accessToken"
            Accept        = 'application/json'
        }

        Write-Success "Connection validated successfully!"
        $companyName = $companyInfo.CompanyInfo.CompanyName
        Write-Host "  Company: $companyName"
        Write-Host "  Realm ID: $RealmId"

    } catch {
        Write-Warning "Connection test failed: $($_.Exception.Message)"
        Write-Host "`nThis might be normal if:"
        Write-Host "  - The realm ID is incorrect"
        Write-Host "  - You haven't connected your app to a QuickBooks company yet"
        Write-Host "  - The app doesn't have the required scopes"
    }
}

# Summary
Write-ColorOutput "`n$($script:ColorBold)═══════════════════════════════════════════════════════════$($script:ColorReset)" $script:ColorGreen
Write-ColorOutput "$($script:ColorBold)  QuickBooks OAuth Setup Complete!$($script:ColorReset)" $script:ColorGreen
Write-ColorOutput "$($script:ColorBold)═══════════════════════════════════════════════════════════$($script:ColorReset)" $script:ColorGreen

Write-Host "`nTokens have been saved to: $settingsFile"
Write-Host "`nEnvironment variables for future use:"
Write-Host "  `$env:QBO_CLIENT_ID = '$ClientId'"
Write-Host "  `$env:QBO_CLIENT_SECRET = '<redacted>'"
if ($RealmId) {
    Write-Host "  `$env:QBO_REALM_ID = '$RealmId'"
}
Write-Host "  `$env:QBO_ENVIRONMENT = '$Environment'"
Write-Host "  `$env:WILEYWIDGET_SETTINGS_DIR = '$SettingsDir'"

Write-Host "`nNext steps:"
Write-Host "  1. Run your tests with: dotnet test --filter SyncBudgetsToAppAsync"
Write-Host "  2. Set WW_SKIP_INTERACTIVE=1 for automated test runs"
Write-Host "  3. Tokens will auto-refresh when they expire"

Write-ColorOutput "`n✓ All done!`n" $script:ColorGreen
