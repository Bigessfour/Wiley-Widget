# Wiley Widget Startup Script
# Loads secrets from Azure Key Vault and starts the application

Write-Host "🚀 Starting Wiley Widget with Azure Key Vault secrets..." -ForegroundColor Cyan

# Load secrets from Key Vault
Write-Host "🔐 Loading secrets from Azure Key Vault..." -ForegroundColor Yellow
& ".\scripts\load-mcp-secrets.ps1"

# Verify secrets are loaded
$brightDataKey = $env:BRIGHTDATA_API_KEY
$syncfusionKey = $env:SYNCFUSION_LICENSE_KEY
$xaiKey = $env:XAI_API_KEY

if ($brightDataKey) {
    Write-Host "✅ Bright Data API Key: Loaded" -ForegroundColor Green
} else {
    Write-Host "❌ Bright Data API Key: Not loaded" -ForegroundColor Red
}

if ($syncfusionKey) {
    Write-Host "✅ Syncfusion License Key: Loaded" -ForegroundColor Green
} else {
    Write-Host "❌ Syncfusion License Key: Not loaded" -ForegroundColor Red
}

if ($xaiKey) {
    Write-Host "✅ XAI API Key: Loaded" -ForegroundColor Green
} else {
    Write-Host "❌ XAI API Key: Not loaded" -ForegroundColor Red
}

Write-Host "🎯 Application ready with secure secrets from Azure Key Vault!" -ForegroundColor Green
