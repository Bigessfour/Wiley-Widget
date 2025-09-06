# Load API Keys from Azure Key Vault
# This script retrieves secrets from Azure Key Vault and sets them as environment variables

param(
    [string]$VaultName = "wiley-widget-secrets"
)

Write-Host "🔐 Loading secrets from Azure Key Vault: $VaultName" -ForegroundColor Cyan

# Function to get secret from Key Vault
function Get-KeyVaultSecret {
    param([string]$SecretName)
    try {
        $secret = az keyvault secret show --vault-name $VaultName --name $SecretName --query value -o tsv 2>$null
        if ($LASTEXITCODE -eq 0 -and $secret) {
            return $secret
        } else {
            Write-Warning "Failed to retrieve secret: $SecretName"
            return $null
        }
    } catch {
        Write-Warning "Error retrieving secret $SecretName`: $($_.Exception.Message)"
        return $null
    }
}

# Load Bright Data API Key
$brightDataKey = Get-KeyVaultSecret "BRIGHTDATA-API-KEY"
if ($brightDataKey) {
    $env:BRIGHTDATA_API_KEY = $brightDataKey
    $env:API_TOKEN = $brightDataKey  # Also set API_TOKEN for Bright Data MCP
    Write-Host "✅ Bright Data API Key loaded" -ForegroundColor Green
} else {
    Write-Warning "❌ Failed to load Bright Data API Key"
}

# Load Syncfusion License Key
$syncfusionKey = Get-KeyVaultSecret "SYNCFUSION-LICENSE-KEY"
if ($syncfusionKey) {
    $env:SYNCFUSION_LICENSE_KEY = $syncfusionKey
    Write-Host "✅ Syncfusion License Key loaded" -ForegroundColor Green
} else {
    Write-Warning "❌ Failed to load Syncfusion License Key"
}

# Load XAI API Key
$xaiKey = Get-KeyVaultSecret "XAI-API-KEY"
if ($xaiKey) {
    $env:XAI_API_KEY = $xaiKey
    Write-Host "✅ XAI API Key loaded" -ForegroundColor Green
} else {
    Write-Warning "❌ Failed to load XAI API Key"
}

# Load GitHub Personal Access Token
$githubToken = Get-KeyVaultSecret "GITHUB-PAT"
if ($githubToken) {
    $env:GITHUB_TOKEN = $githubToken
    Write-Host "✅ GitHub Personal Access Token loaded" -ForegroundColor Green
} else {
    Write-Warning "❌ Failed to load GitHub Personal Access Token"
}

Write-Host "🎉 Secrets loaded from Azure Key Vault" -ForegroundColor Green
Write-Host "Note: These are session-only environment variables. Use this script at startup." -ForegroundColor Yellow
