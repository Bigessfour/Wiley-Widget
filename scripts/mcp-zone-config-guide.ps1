# BrightData MCP Browser Zone Configuration Guide
Write-Host "BrightData MCP Browser Zone Configuration Guide" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""

# Check current API key status
Write-Host "🔑 API Key Status Check:" -ForegroundColor Yellow
$apiKey = $env:BRIGHTDATA_API_KEY
if ($apiKey -and $apiKey -ne "your_actual_token_here" -and $apiKey -ne "YOUR_ACTUAL_BRIGHTDATA_API_KEY_HERE") {
    Write-Host "✅ API key appears to be properly configured" -ForegroundColor Green
    $keyConfigured = $true
} else {
    Write-Host "❌ API key is still placeholder - needs to be updated" -ForegroundColor Red
    Write-Host "   Current value: $apiKey" -ForegroundColor Gray
    $keyConfigured = $false
}
Write-Host ""

# MCP Browser Zone Information
Write-Host "🌐 MCP Browser Zone Information:" -ForegroundColor Yellow
Write-Host "===============================" -ForegroundColor Yellow
Write-Host "The URL you visited (https://brightdata.com/cp/zones/mcp_browser/stats)" -ForegroundColor White
Write-Host "shows your MCP browser zones configuration and statistics." -ForegroundColor White
Write-Host ""

Write-Host "What this page tells you:" -ForegroundColor White
Write-Host "• 📊 Usage statistics for your MCP browser zones" -ForegroundColor Gray
Write-Host "• ⚙️ Configuration status of browser zones" -ForegroundColor Gray
Write-Host "• 🔄 Active/inactive zone status" -ForegroundColor Gray
Write-Host "• 📈 Performance metrics and success rates" -ForegroundColor Gray
Write-Host ""

# Required Actions
Write-Host "📋 REQUIRED ACTIONS:" -ForegroundColor Yellow
Write-Host "====================" -ForegroundColor Yellow

if (-not $keyConfigured) {
    Write-Host "1. 🔑 Update API Key:" -ForegroundColor White
    Write-Host "   - Copy your API key from BrightData dashboard" -ForegroundColor Gray
    Write-Host "   - Update machine environment variable: BRIGHTDATA_API_KEY" -ForegroundColor Gray
    Write-Host "   - Or update .env file with actual key" -ForegroundColor Gray
    Write-Host ""

    Write-Host "2. 🔧 Verify Zone Configuration:" -ForegroundColor White
    Write-Host "   - Ensure MCP browser zones are active" -ForegroundColor Gray
    Write-Host "   - Check zone permissions and access" -ForegroundColor Gray
    Write-Host "   - Verify zone is assigned to your API key" -ForegroundColor Gray
    Write-Host ""
}

Write-Host "3. 🧪 Test MCP Connection:" -ForegroundColor White
Write-Host "   - Run diagnostic script after API key is updated" -ForegroundColor Gray
Write-Host "   - Check MCP server connectivity" -ForegroundColor Gray
Write-Host "   - Verify JSON-RPC protocol works" -ForegroundColor Gray
Write-Host ""

# Testing Commands
Write-Host "🚀 TESTING COMMANDS:" -ForegroundColor Yellow
Write-Host "====================" -ForegroundColor Yellow

if ($keyConfigured) {
    Write-Host "# Test MCP connectivity:" -ForegroundColor White
    Write-Host ".\scripts\brightdata-diagnostic.ps1" -ForegroundColor Gray
    Write-Host ""

    Write-Host "# Test with actual API call:" -ForegroundColor White
    Write-Host '$headers = @{ "Authorization" = "Bearer $env:BRIGHTDATA_API_KEY"; "Content-Type" = "application/json" }' -ForegroundColor Gray
    Write-Host '$body = @{ jsonrpc = "2.0"; id = "test-$(Get-Date -Format HHmmss)"; method = "tools/list"; params = @{} } | ConvertTo-Json' -ForegroundColor Gray
    Write-Host 'Invoke-RestMethod -Uri "https://mcp.brightdata.com/" -Method Post -Headers $headers -Body $body' -ForegroundColor Gray
} else {
    Write-Host "# After updating API key, run:" -ForegroundColor White
    Write-Host ".\scripts\brightdata-diagnostic.ps1" -ForegroundColor Gray
}
Write-Host ""

# Troubleshooting
Write-Host "🔧 TROUBLESHOOTING:" -ForegroundColor Yellow
Write-Host "===================" -ForegroundColor Yellow
Write-Host "If you still get 404 errors after updating the API key:" -ForegroundColor White
Write-Host ""

Write-Host "1. 🌐 Check Zone Status:" -ForegroundColor White
Write-Host "   - Visit: https://brightdata.com/cp/zones/mcp_browser/stats" -ForegroundColor Cyan
Write-Host "   - Ensure zones are 'Active' status" -ForegroundColor Gray
Write-Host "   - Check if zones are assigned to your account" -ForegroundColor Gray
Write-Host ""

Write-Host "2. 🔐 Verify Permissions:" -ForegroundColor White
Write-Host "   - API key must have MCP browser permissions" -ForegroundColor Gray
Write-Host "   - Zone must allow API access" -ForegroundColor Gray
Write-Host "   - Check zone whitelist/blacklist settings" -ForegroundColor Gray
Write-Host ""

Write-Host "3. 📞 Contact Support:" -ForegroundColor White
Write-Host "   - If zones are configured but still not working" -ForegroundColor Gray
Write-Host "   - BrightData support can check backend configuration" -ForegroundColor Gray
Write-Host ""

# Success Indicators
Write-Host "✅ SUCCESS INDICATORS:" -ForegroundColor Yellow
Write-Host "======================" -ForegroundColor Yellow
Write-Host "When MCP is working properly, you should see:" -ForegroundColor White
Write-Host "• 🟢 MCP server responds with JSON-RPC 2.0 format" -ForegroundColor Green
Write-Host "• 🟢 tools/list method returns available tools" -ForegroundColor Green
Write-Host "• 🟢 No 404 or authentication errors" -ForegroundColor Green
Write-Host "• 🟢 Zone statistics show successful requests" -ForegroundColor Green
Write-Host ""

Write-Host "💡 NEXT STEPS:" -ForegroundColor Cyan
Write-Host "==============" -ForegroundColor Cyan
if (-not $keyConfigured) {
    Write-Host "1. Update your BRIGHTDATA_API_KEY with the actual value" -ForegroundColor White
    Write-Host "2. Verify MCP browser zones are active and configured" -ForegroundColor White
    Write-Host "3. Run the diagnostic script to test connectivity" -ForegroundColor White
} else {
    Write-Host "1. Run .\scripts\brightdata-diagnostic.ps1" -ForegroundColor White
    Write-Host "2. Check zone statistics for successful connections" -ForegroundColor White
    Write-Host "3. Test MCP functionality in your WPF application" -ForegroundColor White
}
Write-Host ""

Write-Host "🔗 Useful Links:" -ForegroundColor White
Write-Host "• MCP Browser Zones: https://brightdata.com/cp/zones/mcp_browser/stats" -ForegroundColor Cyan
Write-Host "• BrightData Dashboard: https://brightdata.com/cp/mcp" -ForegroundColor Cyan
Write-Host "• VS Code Integration: https://docs.brightdata.com/mcp-server/integrations/vscode" -ForegroundColor Cyan
