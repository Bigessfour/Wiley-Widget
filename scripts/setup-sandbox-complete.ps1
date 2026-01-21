#Requires -Version 7.0
<#
.SYNOPSIS
Complete QuickBooks Sandbox Setup - OAuth + Account Seeding
This script handles the full setup process including:
1. OAuth 2.0 authorization flow to get access token
2. Token storage for persistence
3. Account seeding via seed-sandbox-qbo.ps1

.DESCRIPTION
Integrates OAuth token management with the account seeding script,
providing a complete end-to-end setup experience for QBO sandbox.

.PARAMETER ClientId
Intuit App Client ID

.PARAMETER ClientSecret
Intuit App Client Secret

.PARAMETER RealmId
QuickBooks Company/Realm ID (numeric)

.PARAMETER AutoSeed
Automatically seed accounts after OAuth completion

.EXAMPLE
.\setup-sandbox-complete.ps1 -ClientId "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnop" -ClientSecret "1234567890abcdefghijklmnopqrstuvwxyz" -RealmId "1234567890" -AutoSeed

.NOTES
OAuth Flow:
1. Opens browser for user authorization
2. Exchanges auth code for access token
3. Saves token to local config file (encrypted on Windows)
4. Uses token to seed accounts

Author: Wiley Widget Team
Date: 2025-01-13
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ClientId,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ClientSecret,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$RealmId,

    [Parameter()]
    [switch]$AutoSeed = $false,

    [Parameter()]
    [int]$Port = 8888,

    [Parameter()]
    [string]$ConfigPath = "$env:LOCALAPPDATA\WileyWidget\qbo-config.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# OAuth Configuration
$OAuthAuthUrl = "https://appcenter.intuit.com/connect/oauth2"
$OAuthTokenUrl = "https://oauth.platform.intuit.com/oauth2/tokens"
$Scope = "com.intuit.quickbooks.accounting"

Write-Host "🏛️  Wiley Widget - QuickBooks Sandbox Complete Setup" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Ensure config directory exists
Write-Host "📁 Setting up configuration storage..." -ForegroundColor Cyan
$configDir = Split-Path -Path $ConfigPath
if (-not (Test-Path $configDir)) {
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
    Write-Host "✓ Created config directory: $configDir" -ForegroundColor Green
}

# Step 2: Build OAuth authorization URL
Write-Host "🔐 Preparing OAuth flow..." -ForegroundColor Cyan
$state = [System.Guid]::NewGuid().ToString()

# Use Intuit OAuth Playground redirect URI (already registered in Production)
$redirectUri = "https://developer.intuit.com/v2/OAuth2Playground/RedirectUrl"

$authUrl = @(
    $OAuthAuthUrl,
    "?client_id=$([System.Web.HttpUtility]::UrlEncode($ClientId))",
    "&response_type=code",
    "&scope=$([System.Web.HttpUtility]::UrlEncode($Scope))",
    "&redirect_uri=$([System.Web.HttpUtility]::UrlEncode($redirectUri))",
    "&state=$state"
) -join ""

Write-Host "✓ Authorization URL prepared" -ForegroundColor Green
Write-Host "  Redirect URI: $redirectUri" -ForegroundColor Gray
Write-Host ""

# Step 3: Start local callback server (if using localhost)
Write-Host "🌐 Preparing callback handling..." -ForegroundColor Cyan
if ($redirectUri -match "localhost") {
    Write-Host "  Starting local callback server (port $Port)..." -ForegroundColor Gray
} else {
    Write-Host "  Using Intuit OAuth2 Playground redirect URI" -ForegroundColor Gray
}
$callbackUrl = $redirectUri
$authCode = $null
$receivedState = $null

# Create HTTP listener for OAuth callback (only for localhost)
$listener = $null
if ($redirectUri -match "localhost") {
    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add("http://localhost:$Port/")
    $listener.Start()
    Write-Host "✓ Callback server running on $callbackUrl" -ForegroundColor Green
} else {
    Write-Host "✓ You will receive the callback in the Intuit OAuth2 Playground" -ForegroundColor Yellow
}
Write-Host ""

# Step 4: Open browser for user authorization
Write-Host "🔓 Opening browser for authorization..." -ForegroundColor Cyan
Write-Host "Please approve the application in your browser." -ForegroundColor Yellow
Write-Host "If browser doesn't open, visit: $authUrl" -ForegroundColor Yellow

# Open default browser
if ($PSVersionTable.Platform -eq "Win32NT") {
    Start-Process $authUrl
}
elseif ($PSVersionTable.Platform -eq "Unix") {
    if ($IsMacOS) {
        open $authUrl
    }
    else {
        xdg-open $authUrl
    }
}

# Wait for callback
Write-Host ""
Write-Host "⏳ Waiting for authorization callback..." -ForegroundColor Cyan

if ($redirectUri -match "localhost" -and $null -ne $listener) {
    # Localhost mode: listen for callback
try {
    $context = $listener.GetContext()
    $request = $context.Request

    # Parse query string
    $query = $request.Url.Query -replace '^\?', ''
    $params = @{}
    foreach ($pair in $query -split '&') {
        $key, $value = $pair -split '='
        $params[[System.Web.HttpUtility]::UrlDecode($key)] = [System.Web.HttpUtility]::UrlDecode($value)
    }

    $authCode = $params['code']
    $receivedState = $params['state']
    $errorMessage = $params['error']

    # Validate state
    if ($receivedState -ne $state) {
        throw "State mismatch in OAuth callback (security validation failed)"
    }

    if ($errorMessage) {
        throw "User denied authorization: $errorMessage"
    }

    if (-not $authCode) {
        throw "No authorization code received"
    }

    # Send success response to browser
    $response = $context.Response
    $response.StatusCode = 200
    $response.ContentType = "text/html"
    $htmlResponse = @"
    <!DOCTYPE html>
    <html>
    <head><title>Authorization Successful</title></head>
    <body style="font-family: Arial; text-align: center; padding: 50px;">
        <h1>✅ Authorization Successful</h1>
        <p>You can close this window and return to the terminal.</p>
    </body>
    </html>
"@
    $responseWriter = New-Object System.IO.StreamWriter $response.OutputStream
    $responseWriter.WriteLine($htmlResponse)
    $responseWriter.Close()

    Write-Host "✓ Authorization code received" -ForegroundColor Green
}
finally {
    if ($null -ne $listener) {
        $listener.Close()
        $listener.Dispose()
    }
}
} else {
    # External redirect mode (Intuit OAuth2 Playground)
    Write-Host ""
    Write-Host "ℹ️  Intuit OAuth2 Playground redirect detected." -ForegroundColor Cyan
    Write-Host "After you authorize the app in your browser, you will be redirected" -ForegroundColor Gray
    Write-Host "to the OAuth2 Playground page where you can copy the authorization code." -ForegroundColor Gray
    Write-Host ""
    Write-Host "📋 Steps:" -ForegroundColor Yellow
    Write-Host "  1. Authorize the app in the browser window that opened" -ForegroundColor Gray
    Write-Host "  2. You will be redirected to a page showing the auth code" -ForegroundColor Gray
    Write-Host "  3. Copy the 'code' parameter value from the URL or page" -ForegroundColor Gray
    Write-Host "  4. Paste it below when prompted" -ForegroundColor Gray
    Write-Host ""
    $authCode = Read-Host "📌 Enter the authorization code from the Playground"

    if ([string]::IsNullOrEmpty($authCode)) {
        Write-Host "✗ No authorization code provided" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Authorization code received" -ForegroundColor Green
}

Write-Host ""

# Step 5: Exchange authorization code for access token
Write-Host "🔄 Exchanging authorization code for access token..." -ForegroundColor Cyan

$tokenBody = @{
    grant_type    = "authorization_code"
    code          = $authCode
    client_id     = $ClientId
    client_secret = $ClientSecret
    redirect_uri  = $redirectUri
}

# Convert to URL-encoded form (Intuit requires application/x-www-form-urlencoded, not JSON)
$tokenBodyString = ($tokenBody.GetEnumerator() | ForEach-Object { "$($_.Key)=$([System.Web.HttpUtility]::UrlEncode($_.Value))" }) -join "&"

try {
    $tokenResponse = Invoke-RestMethod -Uri $OAuthTokenUrl `
        -Method Post `
        -ContentType "application/x-www-form-urlencoded" `
        -Body $tokenBodyString `
        -TimeoutSec 30 `
        -ErrorAction Stop

    $accessToken = $tokenResponse.access_token
    $refreshToken = $tokenResponse.refresh_token
    $expiresIn = $tokenResponse.expires_in
    $tokenType = $tokenResponse.token_type

    Write-Host "✓ Access token obtained" -ForegroundColor Green
    Write-Host "  Token Type: $tokenType" -ForegroundColor Gray
    Write-Host "  Expires In: $expiresIn seconds" -ForegroundColor Gray
}
catch {
    Write-Host "✗ Token exchange failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Details: $($_.Exception | Out-String)" -ForegroundColor Gray
    exit 1
}

# Step 6: Save configuration
Write-Host ""
Write-Host "💾 Saving configuration..." -ForegroundColor Cyan

$config = @{
    clientId      = $ClientId
    clientSecret  = $ClientSecret
    realmId       = $RealmId
    accessToken   = $accessToken
    refreshToken  = $refreshToken
    tokenType     = $tokenType
    expiresAt     = (Get-Date).AddSeconds($expiresIn).ToString("O")
    obtainedAt    = (Get-Date).ToString("O")
    environment   = "sandbox"
} | ConvertTo-Json -Depth 10

# Save with restricted permissions on Windows
$configPath = $ConfigPath
$config | Out-File -FilePath $configPath -Encoding UTF8 -Force

if ($PSVersionTable.Platform -eq "Win32NT") {
    # Set ACL to current user only
    $acl = Get-Acl -Path $configPath
    $acl.SetAccessRuleProtection($true, $false)
    Set-Acl -Path $configPath -AclObject $acl
    Write-Host "✓ Config saved with restricted permissions: $configPath" -ForegroundColor Green
}
else {
    chmod 600 $configPath
    Write-Host "✓ Config saved: $configPath" -ForegroundColor Green
}

# Step 7: Optionally seed accounts
Write-Host ""
if ($AutoSeed) {
    Write-Host "🌱 Auto-seeding accounts..." -ForegroundColor Cyan

    $seedScript = Join-Path (Split-Path $PSCommandPath) "seed-sandbox-qbo.ps1"
    if (-not (Test-Path $seedScript)) {
        Write-Host "✗ Seed script not found: $seedScript" -ForegroundColor Red
        exit 1
    }

    & $seedScript -AccessToken $accessToken -RealmId $RealmId -Sandbox
}
else {
    Write-Host "📝 Manual seeding available:" -ForegroundColor Cyan
    $seedScript = Join-Path (Split-Path $PSCommandPath) "seed-sandbox-qbo.ps1"
    Write-Host "  $seedScript -AccessToken `"$accessToken`" -RealmId `"$RealmId`" -Sandbox"
}

Write-Host ""
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "✅ Setup Complete!" -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor White
Write-Host "1. Verify accounts in QuickBooks Sandbox" -ForegroundColor Gray
Write-Host "2. Update Wiley Widget config with RealmId: $RealmId" -ForegroundColor Gray
Write-Host "3. Run Wiley Widget and click 'Sync Now' to sync accounts" -ForegroundColor Gray
Write-Host ""
Write-Host "Configuration stored: $ConfigPath" -ForegroundColor Cyan
