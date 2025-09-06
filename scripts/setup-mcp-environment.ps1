# MCP Environment Setup Script
# This script ensures all MCP servers have the required environment variables

Write-Host "🔧 Setting up MCP Environment Variables..." -ForegroundColor Cyan

# BrightData MCP - Set API_TOKEN from BRIGHTDATA_API_KEY
if ($env:BRIGHTDATA_API_KEY) {
    $env:API_TOKEN = $env:BRIGHTDATA_API_KEY
    Write-Host "✅ BrightData API_TOKEN set from BRIGHTDATA_API_KEY" -ForegroundColor Green
} else {
    Write-Host "⚠️ BRIGHTDATA_API_KEY not found - BrightData MCP will not work" -ForegroundColor Yellow
}

# Azure MCP - Check for required credentials
$azureVars = @('AZURE_CLIENT_ID', 'AZURE_CLIENT_SECRET', 'AZURE_TENANT_ID', 'AZURE_SUBSCRIPTION_ID')
$azureMissing = @()

foreach ($var in $azureVars) {
    if (-not (Get-Item "env:$var" -ErrorAction SilentlyContinue)) {
        $azureMissing += $var
    }
}

if ($azureMissing.Count -eq 0) {
    Write-Host "✅ All Azure MCP credentials found" -ForegroundColor Green
} else {
    Write-Host "⚠️ Missing Azure MCP credentials: $($azureMissing -join ', ')" -ForegroundColor Yellow
    Write-Host "   Azure MCP will require authentication setup" -ForegroundColor Gray
}

# GitHub MCP - Check for token
if ($env:GITHUB_TOKEN) {
    Write-Host "✅ GitHub MCP token found" -ForegroundColor Green
} else {
    Write-Host "⚠️ GITHUB_TOKEN not found - GitHub MCP will not work" -ForegroundColor Yellow
}

# Microsoft Docs MCP - Check endpoint
try {
    $response = Invoke-WebRequest -Uri "https://learn.microsoft.com/mcp/" -Method HEAD -TimeoutSec 5
    if ($response.StatusCode -eq 200) {
        Write-Host "✅ Microsoft Docs MCP endpoint accessible" -ForegroundColor Green
    } else {
        Write-Host "⚠️ Microsoft Docs MCP endpoint returned status: $($response.StatusCode)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Microsoft Docs MCP endpoint not accessible: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n📊 MCP Status Summary:" -ForegroundColor Cyan
Write-Host "• GitHub MCP: $(if ($env:GITHUB_TOKEN) { 'Ready' } else { 'Needs Token' })" -ForegroundColor $(if ($env:GITHUB_TOKEN) { 'Green' } else { 'Yellow' })
Write-Host "• BrightData MCP: $(if ($env:BRIGHTDATA_API_KEY) { 'Ready' } else { 'Needs API Key' })" -ForegroundColor $(if ($env:BRIGHTDATA_API_KEY) { 'Green' } else { 'Yellow' })
Write-Host "• Azure MCP: $(if ($azureMissing.Count -eq 0) { 'Ready' } else { 'Needs Credentials' })" -ForegroundColor $(if ($azureMissing.Count -eq 0) { 'Green' } else { 'Yellow' })
Write-Host "• Microsoft Docs MCP: Checking..." -ForegroundColor Gray

Write-Host "`n🎯 Next Steps:" -ForegroundColor Cyan
if ($azureMissing.Count -gt 0) {
    Write-Host "1. Set up Azure Service Principal credentials for Azure MCP" -ForegroundColor Yellow
}
if (-not $env:GITHUB_TOKEN) {
    Write-Host "2. Configure GITHUB_TOKEN for GitHub MCP" -ForegroundColor Yellow
}
Write-Host "3. Test MCP servers individually" -ForegroundColor White
Write-Host "4. Apply documentation discipline to working systems" -ForegroundColor White
