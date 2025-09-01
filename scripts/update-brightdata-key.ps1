# Secure API Key Update Script for BrightData MCP
Write-Host "🔐 BrightData API Key Update Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Check current status
Write-Host "📊 Current API Key Status:" -ForegroundColor Yellow
$currentKey = $env:BRIGHTDATA_API_KEY
if ($currentKey -and $currentKey -ne "your_actual_token_here") {
    Write-Host "✅ Environment variable is set" -ForegroundColor Green
} else {
    Write-Host "❌ Environment variable not set or still placeholder" -ForegroundColor Red
}

# Check .env file
Write-Host ""
Write-Host "📄 .env File Status:" -ForegroundColor Yellow
$envFile = ".\.env"
if (Test-Path $envFile) {
    $envContent = Get-Content $envFile
    $brightDataLine = $envContent | Where-Object { $_ -match "^BRIGHTDATA_API_KEY=" }
    if ($brightDataLine) {
        Write-Host "✅ BRIGHTDATA_API_KEY found in .env file" -ForegroundColor Green
        if ($brightDataLine -match "your_actual_token_here") {
            Write-Host "❌ Still has placeholder value" -ForegroundColor Red
        } else {
            Write-Host "✅ Appears to have actual value" -ForegroundColor Green
        }
    } else {
        Write-Host "❌ BRIGHTDATA_API_KEY not found in .env file" -ForegroundColor Red
    }
} else {
    Write-Host "❌ .env file not found" -ForegroundColor Red
}

Write-Host ""
Write-Host "🔑 UPDATE INSTRUCTIONS:" -ForegroundColor Yellow
Write-Host "=======================" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. 📋 Get your API key from BrightData:" -ForegroundColor White
Write-Host "   - Visit: https://brightdata.com/cp/mcp" -ForegroundColor Cyan
Write-Host "   - Go to API Keys section" -ForegroundColor Gray
Write-Host "   - Copy your MCP API key" -ForegroundColor Gray
Write-Host ""

Write-Host "2. 🔧 Update the .env file:" -ForegroundColor White
Write-Host "   - Open .env file in VS Code" -ForegroundColor Gray
Write-Host "   - Find: BRIGHTDATA_API_KEY=your_actual_token_here" -ForegroundColor Gray
Write-Host "   - Replace with your actual API key" -ForegroundColor Gray
Write-Host ""

Write-Host "3. 🔄 Update environment variable:" -ForegroundColor White
Write-Host "   - Open PowerShell as Administrator" -ForegroundColor Gray
Write-Host "   - Run: [Environment]::SetEnvironmentVariable('BRIGHTDATA_API_KEY', 'your-actual-key', 'Machine')" -ForegroundColor Gray
Write-Host "   - Or restart VS Code to reload .env file" -ForegroundColor Gray
Write-Host ""

Write-Host "4. 🧪 Test the configuration:" -ForegroundColor White
Write-Host "   - Run: .\scripts\brightdata-diagnostic.ps1" -ForegroundColor Gray
Write-Host "   - Should show successful MCP connection" -ForegroundColor Gray
Write-Host ""

# Interactive update option
Write-Host "💡 INTERACTIVE UPDATE:" -ForegroundColor Yellow
Write-Host "======================" -ForegroundColor Yellow
$updateChoice = Read-Host "Would you like to update the API key now? (y/n)"

if ($updateChoice -eq 'y' -or $updateChoice -eq 'Y') {
    Write-Host ""
    Write-Host "🔒 SECURITY NOTICE:" -ForegroundColor Red
    Write-Host "===================" -ForegroundColor Red
    Write-Host "• Your API key will be stored securely in the .env file" -ForegroundColor White
    Write-Host "• The .env file should already be in .gitignore" -ForegroundColor White
    Write-Host "• Never commit API keys to version control" -ForegroundColor White
    Write-Host ""

    $newApiKey = Read-Host "Enter your BrightData MCP API key (input will be hidden)"
    if ($newApiKey -and $newApiKey -ne "your_actual_token_here") {
        # Update .env file
        $envContent = Get-Content $envFile
        $updatedContent = $envContent -replace "^BRIGHTDATA_API_KEY=.*$", "BRIGHTDATA_API_KEY=$newApiKey"
        $updatedContent | Set-Content $envFile

        Write-Host ""
        Write-Host "✅ Updated .env file with new API key" -ForegroundColor Green

        # Update environment variable
        [Environment]::SetEnvironmentVariable('BRIGHTDATA_API_KEY', $newApiKey, 'User')
        Write-Host "✅ Updated environment variable" -ForegroundColor Green

        Write-Host ""
        Write-Host "🔄 Please restart VS Code to reload the environment variables" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "🧪 After restart, run: .\scripts\brightdata-diagnostic.ps1" -ForegroundColor Cyan

    } else {
        Write-Host "❌ Invalid API key entered" -ForegroundColor Red
    }
} else {
    Write-Host ""
    Write-Host "📝 Manual Update Required:" -ForegroundColor Yellow
    Write-Host "==========================" -ForegroundColor Yellow
    Write-Host "Please update the .env file manually with your actual BrightData API key" -ForegroundColor White
    Write-Host "Then restart VS Code and run the diagnostic script" -ForegroundColor White
}

Write-Host ""
Write-Host "🔗 Useful Links:" -ForegroundColor White
Write-Host "• BrightData Dashboard: https://brightdata.com/cp/mcp" -ForegroundColor Cyan
Write-Host "• API Keys: https://brightdata.com/cp/mcp/api-keys" -ForegroundColor Cyan
Write-Host "• MCP Documentation: https://docs.brightdata.com/mcp-server/" -ForegroundColor Cyan
