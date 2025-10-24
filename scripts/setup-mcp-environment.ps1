# MCP Environment Setup Script
# This script helps configure the required environment variables for MCP servers

Write-Information "🔧 MCP Environment Setup" -InformationAction Continue
Write-Information "=========================" -InformationAction Continue

# Check current environment variables
Write-Information "`n📋 Current MCP Environment Variables:" -InformationAction Continue
$envVars = @('GITHUB_TOKEN', 'GITHUB_PERSONAL_ACCESS_TOKEN', 'XAI_API_KEY', 'AZURE_CLIENT_ID', 'AZURE_CLIENT_SECRET', 'AZURE_TENANT_ID', 'AZURE_SUBSCRIPTION_ID')

foreach ($var in $envVars) {
    $value = [Environment]::GetEnvironmentVariable($var, 'User')
    if ($value) {
        Write-Output "✅ $($var): Set (length: $($value.Length))"
    }
    else {
        Write-Warning "❌ $($var): NOT SET"
    }
}

Write-Information "`n🔑 To set up MCP servers, you need to configure these environment variables:" -InformationAction Continue
Write-Output "1. GITHUB_TOKEN or GITHUB_PERSONAL_ACCESS_TOKEN - Get from: https://github.com/settings/tokens"
Write-Output "2. XAI_API_KEY - Your XAI API key (already set)"
Write-Output "3. Azure variables - Get from Azure portal or service principal"

Write-Information "`n💡 Quick Setup Commands:" -InformationAction Continue
Write-Output "# Set GitHub Token (replace YOUR_TOKEN with actual token)"
Write-Output '[Environment]::SetEnvironmentVariable("GITHUB_TOKEN", "YOUR_TOKEN", "User")'
Write-Output "# Or, if you prefer the alternate variable name used in some tools"
Write-Output '[Environment]::SetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN", "YOUR_TOKEN", "User")'

Write-Output "`n# Set Azure Variables (replace with your values)"
Write-Output '[Environment]::SetEnvironmentVariable("AZURE_CLIENT_ID", "your-client-id", "User")'
Write-Output '[Environment]::SetEnvironmentVariable("AZURE_CLIENT_SECRET", "your-client-secret", "User")'
Write-Output '[Environment]::SetEnvironmentVariable("AZURE_TENANT_ID", "your-tenant-id", "User")'
Write-Output '[Environment]::SetEnvironmentVariable("AZURE_SUBSCRIPTION_ID", "your-subscription-id", "User")'

Write-Warning "`n⚠️  After setting variables, restart VS Code for MCP servers to pick them up."

# Test PowerShell shell integration
Write-Information "`n🔌 Testing PowerShell Shell Integration:" -InformationAction Continue
try {
    $psVersion = $PSVersionTable.PSVersion
    Write-Output "✅ PowerShell Version: $psVersion"

    # Test if we can run basic commands
    $test = & pwsh -Command "Write-Output 'Shell integration test'"
    if ($test -eq 'Shell integration test') {
        Write-Output "✅ Shell Integration: Working"
    }
    else {
        Write-Warning "⚠️  Shell Integration: Limited"
    }
}
catch {
    Write-Error "❌ Shell Integration: Failed - $($_.Exception.Message)"
}

Write-Information "`n🎯 Next Steps:" -InformationAction Continue
Write-Output "1. Set the required environment variables using the commands above"
Write-Output "2. Restart VS Code completely"
Write-Output "3. Check MCP server status in VS Code settings"
Write-Output "4. Verify servers are running in the MCP output panel"
