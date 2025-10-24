# MCP Server Status Checker
# Run this script to verify MCP server configuration and status

Write-Information "🔍 MCP Server Status Check" -InformationAction Continue
Write-Information "==========================" -InformationAction Continue

# Check environment variables
Write-Information "`n📋 Environment Variables Status:" -InformationAction Continue
$envVars = @('GITHUB_TOKEN', 'GITHUB_PERSONAL_ACCESS_TOKEN', 'XAI_API_KEY', 'AZURE_CLIENT_ID', 'AZURE_CLIENT_SECRET', 'AZURE_TENANT_ID', 'AZURE_SUBSCRIPTION_ID')
$allSet = $true

foreach ($var in $envVars) {
    $value = [Environment]::GetEnvironmentVariable($var, 'User')
    if ($value) {
        Write-Output "✅ $($var): Configured"
    }
    else {
        Write-Warning "❌ $($var): Missing"
        $allSet = $false
    }
}

# Check MCP configuration file
Write-Information "`n📄 MCP Configuration:" -InformationAction Continue
$mcpConfigPath = "$PSScriptRoot\..\.vscode\mcp.json"
if (Test-Path $mcpConfigPath) {
    Write-Output "✅ MCP config file exists: $mcpConfigPath"

    try {
        $config = Get-Content $mcpConfigPath | ConvertFrom-Json
        Write-Output "✅ Config is valid JSON"
        Write-Output "📊 Configured servers: $($config.servers.PSObject.Properties.Name -join ', ')"
    }
    catch {
        Write-Error "❌ Config file contains invalid JSON: $($_.Exception.Message)"
    }
}
else {
    Write-Error "❌ MCP config file not found: $mcpConfigPath"
}

# Check Azure MCP binary
Write-Information "`n🔧 Azure MCP Binary:" -InformationAction Continue
try {
    $azmcp = Get-Command azmcp-win32-x64 -ErrorAction Stop
    Write-Output "✅ Azure MCP binary found: $($azmcp.Source)"
}
catch {
    Write-Error "❌ Azure MCP binary not found in PATH"
    Write-Warning "   This may cause Azure MCP server to fail"
}

# Summary
Write-Information "`n📊 Summary:" -InformationAction Continue
if ($allSet) {
    Write-Output "✅ All environment variables are configured"
    Write-Output "🚀 MCP servers should start successfully after VS Code restart"
}
else {
    Write-Warning "⚠️  Some environment variables are missing"
    Write-Output "💡 Run setup-mcp-environment.ps1 to configure missing variables"
}

Write-Information "`n🔄 To test MCP servers:" -InformationAction Continue
Write-Output "1. Restart VS Code completely"
Write-Output "2. Open MCP output panel (View → Output → MCP)"
Write-Output "3. Check for server startup messages"
Write-Output "4. Try using MCP features in chat"
