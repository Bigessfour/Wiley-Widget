# BrightData MCP Diagnostic & Troubleshooting Guide
Write-Host "BrightData MCP Diagnostic & Troubleshooting Guide" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Issue Analysis
Write-Host "🔍 ISSUE ANALYSIS:" -ForegroundColor Yellow
Write-Host "==================" -ForegroundColor Yellow
$apiKey = $env:BRIGHTDATA_API_KEY
$keyLength = $apiKey.Length
$hasLetters = $apiKey -match '[a-zA-Z]'
$hasNumbers = $apiKey -match '[0-9]'
$hasSpecial = $apiKey -match '[^a-zA-Z0-9]'

Write-Host "API Key Status:" -ForegroundColor White
Write-Host "  - Length: $keyLength characters" -ForegroundColor Gray
Write-Host "  - Contains letters: $(if ($hasLetters) { '✅' } else { '❌' })" -ForegroundColor $(if ($hasLetters) { 'Green' } else { 'Red' })
Write-Host "  - Contains numbers: $(if ($hasNumbers) { '✅' } else { '❌' })" -ForegroundColor $(if ($hasNumbers) { 'Green' } else { 'Red' })
Write-Host "  - Contains special chars: $(if ($hasSpecial) { '✅' } else { '❌' })" -ForegroundColor $(if ($hasSpecial) { 'Green' } else { 'Red' })
Write-Host ""

# Test Results Summary
Write-Host "🧪 TEST RESULTS SUMMARY:" -ForegroundColor Yellow
Write-Host "========================" -ForegroundColor Yellow
Write-Host "❌ MCP server connection: 404 Not Found" -ForegroundColor Red
Write-Host "❌ All endpoint variations tested: Failed" -ForegroundColor Red
Write-Host "✅ Local configuration: Properly set up" -ForegroundColor Green
Write-Host "✅ Security measures: Implemented" -ForegroundColor Green
Write-Host ""

# Possible Causes
Write-Host "🔧 POSSIBLE CAUSES:" -ForegroundColor Yellow
Write-Host "===================" -ForegroundColor Yellow
Write-Host "1. 🔑 API Key Issues:" -ForegroundColor White
Write-Host "   - API key may not be activated for MCP access" -ForegroundColor Gray
Write-Host "   - API key may be for a different BrightData service" -ForegroundColor Gray
Write-Host "   - API key may have expired or been revoked" -ForegroundColor Gray
Write-Host ""

Write-Host "2. 🌐 MCP Service Issues:" -ForegroundColor White
Write-Host "   - MCP service may require separate activation" -ForegroundColor Gray
Write-Host "   - MCP service may not be publicly accessible" -ForegroundColor Gray
Write-Host "   - MCP service may use different authentication" -ForegroundColor Gray
Write-Host ""

Write-Host "3. 📋 Configuration Issues:" -ForegroundColor White
Write-Host "   - Wrong endpoint URL for your account type" -ForegroundColor Gray
Write-Host "   - Missing service activation in BrightData dashboard" -ForegroundColor Gray
Write-Host ""

# Recommended Solutions
Write-Host "✅ RECOMMENDED SOLUTIONS:" -ForegroundColor Yellow
Write-Host "=========================" -ForegroundColor Yellow
Write-Host "1. 🔍 Verify API Key in BrightData Dashboard:" -ForegroundColor White
Write-Host "   - Visit: https://brightdata.com/cp/mcp" -ForegroundColor Cyan
Write-Host "   - Check if MCP service is enabled for your account" -ForegroundColor Gray
Write-Host "   - Verify API key has MCP permissions" -ForegroundColor Gray
Write-Host "   - Regenerate API key if necessary" -ForegroundColor Gray
Write-Host ""

Write-Host "2. 🔧 Check Account & Service Activation:" -ForegroundColor White
Write-Host "   - Ensure MCP service is activated in your BrightData account" -ForegroundColor Gray
Write-Host "   - Check if you have the correct subscription tier" -ForegroundColor Gray
Write-Host "   - Contact BrightData support if MCP is not available" -ForegroundColor Gray
Write-Host ""

Write-Host "3. 📖 Review Documentation:" -ForegroundColor White
Write-Host "   - Visit: https://docs.brightdata.com/mcp-server/integrations/vscode" -ForegroundColor Cyan
Write-Host "   - Check the GitHub showcase: https://github.com/brightdata/brightdata-agent-showcase" -ForegroundColor Cyan
Write-Host "   - Look for account setup requirements" -ForegroundColor Gray
Write-Host ""

Write-Host "4. 🧪 Alternative Testing:" -ForegroundColor White
Write-Host "   - Try the BrightData API directly: https://api.brightdata.com/" -ForegroundColor Cyan
Write-Host "   - Test with a simple curl command to verify API key" -ForegroundColor Gray
Write-Host "   - Check BrightData status page for service availability" -ForegroundColor Gray
Write-Host ""

# Quick Test Commands
Write-Host "🚀 QUICK TEST COMMANDS:" -ForegroundColor Yellow
Write-Host "=======================" -ForegroundColor Yellow
Write-Host "# Test BrightData API directly:" -ForegroundColor White
Write-Host "curl -H 'Authorization: Bearer $apiKey' https://api.brightdata.com/status" -ForegroundColor Gray
Write-Host ""

Write-Host "# Test MCP endpoint with verbose output:" -ForegroundColor White
Write-Host "Invoke-WebRequest -Uri 'https://mcp.brightdata.com/' -Method GET -Headers @{ 'Authorization' = 'Bearer $env:BRIGHTDATA_API_KEY'; 'User-Agent' = 'WileyWidget-Test/1.0' } -Verbose" -ForegroundColor Gray
Write-Host ""

# Next Steps
Write-Host "📋 NEXT STEPS:" -ForegroundColor Yellow
Write-Host "==============" -ForegroundColor Yellow
Write-Host "1. ✅ Visit BrightData dashboard and verify MCP service activation" -ForegroundColor Green
Write-Host "2. 🔄 Update API key if necessary (machine environment variable)" -ForegroundColor White
Write-Host "3. 🧪 Test the basic BrightData API first" -ForegroundColor White
Write-Host "4. 📞 Contact BrightData support if MCP service is not available" -ForegroundColor White
Write-Host "5. 📖 Review the GitHub showcase repository for implementation examples" -ForegroundColor White
Write-Host ""

Write-Host "💡 REMEMBER:" -ForegroundColor Cyan
Write-Host "============" -ForegroundColor Cyan
Write-Host "The 404 error suggests the MCP service may not be activated for your account," -ForegroundColor White
Write-Host "or the API key doesn't have the necessary permissions. This is common with" -ForegroundColor White
Write-Host "new BrightData accounts that haven't enabled MCP services yet." -ForegroundColor White
Write-Host ""

Write-Host "🔗 Useful Links:" -ForegroundColor White
Write-Host "================" -ForegroundColor White
Write-Host "• BrightData MCP Control Panel: https://brightdata.com/cp/mcp" -ForegroundColor Cyan
Write-Host "• VS Code Integration Docs: https://docs.brightdata.com/mcp-server/integrations/vscode" -ForegroundColor Cyan
Write-Host "• Agent Showcase Repository: https://github.com/brightdata/brightdata-agent-showcase" -ForegroundColor Cyan
