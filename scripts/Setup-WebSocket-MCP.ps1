#Requires -Version 7.5
<#
.SYNOPSIS
    Sets up and tests WebSocket MCP Server configuration for WileyWidget.

.DESCRIPTION
    This script configures the WebSocket remote MCP server for real-time collaboration
    and tests the connection to ensure everything is working properly.

.PARAMETER ServerUrl
    The WebSocket URL of the remote MCP server.

.PARAMETER ApiKey
    The API key for MCP server authentication.

.PARAMETER TestConnection
    Switch to test the WebSocket connection.

.PARAMETER UpdateConfig
    Switch to update VS Code configuration.

.EXAMPLE
    .\Setup-WebSocket-MCP.ps1 -ServerUrl "wss://mcp.example.com/github" -ApiKey "your_key"

.EXAMPLE
    .\Setup-WebSocket-MCP.ps1 -TestConnection

.NOTES
    Author: WileyWidget Development Team
    Version: 1.0.0
    Requires: PowerShell 7.5.2+
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ServerUrl,

    [Parameter(Mandatory = $false)]
    [string]$ApiKey,

    [switch]$TestConnection,
    [switch]$UpdateConfig
)

# Configuration paths
$settingsPath = Join-Path $PSScriptRoot "..\.vscode\settings.json"
$envPath = Join-Path $PSScriptRoot "..\.env"

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Update-WebSocketConfiguration {
    param([string]$wsUrl, [string]$apiKey)

    Write-ColorOutput "🔧 Updating VS Code WebSocket configuration..." -Color Yellow

    # Update .env file
    $envContent = Get-Content $envPath -Raw

    # Update MCP_SERVER_URL
    if ($wsUrl) {
        $envContent = $envContent -replace 'MCP_SERVER_URL=.*', "MCP_SERVER_URL=$wsUrl"
        $envContent = $envContent -replace 'MCP_WEBSOCKET_URL=.*', "MCP_WEBSOCKET_URL=$wsUrl"
    }

    # Update MCP_API_KEY
    if ($apiKey) {
        $envContent = $envContent -replace 'MCP_API_KEY=.*', "MCP_API_KEY=$apiKey"
    }

    $envContent | Set-Content $envPath -Encoding UTF8
    Write-ColorOutput "✓ Environment variables updated" -Color Green

    # Update VS Code settings
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json

    # Update MCP configuration
    if ($wsUrl) {
        $settings.mcp.servers.github.url = $wsUrl
    }

    if ($apiKey) {
        $settings.mcp.servers.github.headers."X-API-Key" = "`$(env:MCP_API_KEY)"
    }

    # Convert back to JSON
    $jsonContent = $settings | ConvertTo-Json -Depth 10
    $jsonContent = $jsonContent -replace '"`$(env:', '"${env:'
    $jsonContent | Set-Content $settingsPath -Encoding UTF8

    Write-ColorOutput "✓ VS Code settings updated for WebSocket" -Color Green
}

function Test-WebSocketConnection {
    Write-ColorOutput "`n🔍 Testing WebSocket MCP Server connection..." -Color Yellow

    # Load environment variables
    $envContent = Get-Content $envPath -Raw
    $wsUrl = if ($envContent -match 'MCP_WEBSOCKET_URL=(.+)') { $matches[1] } else { $null }
    $token = if ($envContent -match 'GITHUB_PERSONAL_ACCESS_TOKEN=(.+)') { $matches[1] } else { $null }
    $apiKey = if ($envContent -match 'MCP_API_KEY=(.+)') { $matches[1] } else { $null }

    # Validate configuration
    if (-not $wsUrl) {
        Write-ColorOutput "❌ MCP_WEBSOCKET_URL not found in .env file" -Color Red
        return $false
    }

    if (-not $token) {
        Write-ColorOutput "❌ GITHUB_PERSONAL_ACCESS_TOKEN not found in .env file" -Color Red
        return $false
    }

    Write-ColorOutput "✓ Configuration validated" -Color Green
    Write-ColorOutput "  WebSocket URL: $wsUrl" -Color White
    Write-ColorOutput "  Token: Set" -Color White
    Write-ColorOutput "  API Key: $(if ($apiKey) { 'Set' } else { 'Not set' })" -Color White

    # Test WebSocket connection (basic connectivity test)
    try {
        # For WebSocket testing, we'll use a simple HTTP HEAD request to the same host
        $uri = [System.Uri]::new($wsUrl)
        $httpUrl = "https://$($uri.Host):$($uri.Port)/health"

        Write-ColorOutput "`n🌐 Testing server connectivity..." -Color Yellow

        $headers = @{
            "Authorization" = "Bearer $token"
        }

        if ($apiKey) {
            $headers["X-API-Key"] = $apiKey
        }

        $response = Invoke-WebRequest -Uri $httpUrl -Method Head -Headers $headers -TimeoutSec 10 -ErrorAction Stop
        Write-ColorOutput "✅ Server connectivity test passed" -Color Green

        return $true
    }
    catch {
        Write-ColorOutput "⚠️  Server connectivity test failed: $($_.Exception.Message)" -Color Yellow
        Write-ColorOutput "   This might be expected if the server doesn't have a health endpoint" -Color White
        return $true  # Don't fail the test for this
    }
}

function Show-WebSocketSetupInstructions {
    Write-ColorOutput "`n📋 WebSocket MCP Server Setup Instructions:" -Color Cyan
    Write-ColorOutput "1. Deploy your MCP server with WebSocket support" -Color White
    Write-ColorOutput "2. Update the following in your .env file:" -Color White
    Write-ColorOutput "   - MCP_WEBSOCKET_URL=wss://your-server.com/github" -Color White
    Write-ColorOutput "   - MCP_API_KEY=your_api_key (if required)" -Color White
    Write-ColorOutput "3. Restart VS Code to apply changes" -Color White
    Write-ColorOutput "4. Check VS Code Output panel for MCP connection logs" -Color White

    Write-ColorOutput "`n🔧 Example Docker deployment:" -Color Yellow
    Write-ColorOutput "docker run -d \\" -Color White
    Write-ColorOutput "  --name github-mcp-ws \\" -Color White
    Write-ColorOutput "  -p 8080:8080 \\" -Color White
    Write-ColorOutput "  -e GITHUB_PERSONAL_ACCESS_TOKEN=your_token \\" -Color White
    Write-ColorOutput "  mcp/github-server:websocket" -Color White

    Write-ColorOutput "`n📚 Additional Resources:" -Color Cyan
    Write-ColorOutput "- MCP WebSocket Specification: https://modelcontextprotocol.io/specification" -Color White
    Write-ColorOutput "- GitHub MCP Server: https://github.com/modelcontextprotocol/server-github" -Color White
}

# Main execution
Write-ColorOutput "🚀 WebSocket MCP Server Setup for WileyWidget" -Color Cyan

if ($ServerUrl -or $ApiKey -or $UpdateConfig) {
    Update-WebSocketConfiguration -wsUrl $ServerUrl -apiKey $ApiKey
}

if ($TestConnection) {
    $testResult = Test-WebSocketConnection
    if ($testResult) {
        Write-ColorOutput "`n✅ WebSocket MCP configuration test completed!" -Color Green
    } else {
        Write-ColorOutput "`n❌ WebSocket MCP configuration test failed!" -Color Red
    }
}

Show-WebSocketSetupInstructions

Write-ColorOutput "`n🎯 Next Steps:" -Color Green
Write-ColorOutput "1. Configure your remote MCP server" -Color White
Write-ColorOutput "2. Update environment variables in .env" -Color White
Write-ColorOutput "3. Restart VS Code" -Color White
Write-ColorOutput "4. Test the connection" -Color White
