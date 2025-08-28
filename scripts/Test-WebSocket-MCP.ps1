#Requires -Version 7.5
<#
.SYNOPSIS
    Tests WebSocket MCP Server connection for WileyWidget.

.DESCRIPTION
    This script performs comprehensive tests on the WebSocket MCP server connection
    including connectivity, authentication, and basic functionality tests.

.PARAMETER Detailed
    Switch to run detailed connection tests.

.PARAMETER SkipConnectivity
    Switch to skip basic connectivity tests.

.EXAMPLE
    .\Test-WebSocket-MCP.ps1

.EXAMPLE
    .\Test-WebSocket-MCP.ps1 -Detailed

.NOTES
    Author: WileyWidget Development Team
    Version: 1.0.0
    Requires: PowerShell 7.5.2+
#>

[CmdletBinding()]
param(
    [switch]$Detailed,
    [switch]$SkipConnectivity
)

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Test-EnvironmentConfiguration {
    Write-ColorOutput "🔍 Testing Environment Configuration..." -Color Yellow

    $envContent = Get-Content (Join-Path $PSScriptRoot "..\.env") -Raw
    $tests = @(
        @{ Name = "GITHUB_PERSONAL_ACCESS_TOKEN"; Pattern = 'GITHUB_PERSONAL_ACCESS_TOKEN=(.+)' },
        @{ Name = "MCP_WEBSOCKET_URL"; Pattern = 'MCP_WEBSOCKET_URL=(.+)' },
        @{ Name = "MCP_API_KEY"; Pattern = 'MCP_API_KEY=(.+)' }
    )

    $results = @{}
    foreach ($test in $tests) {
        if ($envContent -match $test.Pattern) {
            $value = $matches[1]
            if ($value -and $value -notmatch '^your_.*here$') {
                $results[$test.Name] = @{ Status = $true; Value = $value }
                Write-ColorOutput "✅ $($test.Name): Configured" -Color Green
            } else {
                $results[$test.Name] = @{ Status = $false; Value = $null }
                Write-ColorOutput "❌ $($test.Name): Not properly configured" -Color Red
            }
        } else {
            $results[$test.Name] = @{ Status = $false; Value = $null }
            Write-ColorOutput "❌ $($test.Name): Missing" -Color Red
        }
    }

    return $results
}

function Test-WebSocketConnectivity {
    param([string]$wsUrl, [string]$token, [string]$apiKey)

    if ($SkipConnectivity) {
        Write-ColorOutput "⏭️  Skipping connectivity tests..." -Color Yellow
        return $true
    }

    Write-ColorOutput "`n🌐 Testing WebSocket Server Connectivity..." -Color Yellow

    try {
        # Parse WebSocket URL
        $uri = [System.Uri]::new($wsUrl)

        # Test basic connectivity to the host
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $connectTask = $tcpClient.ConnectAsync($uri.Host, $uri.Port)
        $timeout = 5000  # 5 seconds

        if ($connectTask.Wait($timeout)) {
            $tcpClient.Close()
            Write-ColorOutput "✅ Basic connectivity: OK" -Color Green
        } else {
            Write-ColorOutput "❌ Basic connectivity: Timeout" -Color Red
            return $false
        }

        # Test HTTPS endpoint if available
        $httpsUrl = "https://$($uri.Host):$($uri.Port)/health"
        try {
            $headers = @{ "Authorization" = "Bearer $token" }
            if ($apiKey) { $headers["X-API-Key"] = $apiKey }

            $response = Invoke-WebRequest -Uri $httpsUrl -Method Head -Headers $headers -TimeoutSec 10 -ErrorAction Stop
            Write-ColorOutput "✅ HTTPS endpoint: OK" -Color Green
        }
        catch {
            Write-ColorOutput "⚠️  HTTPS endpoint: Not available (might be expected)" -Color Yellow
        }

        return $true
    }
    catch {
        Write-ColorOutput "❌ Connectivity test failed: $($_.Exception.Message)" -Color Red
        return $false
    }
}

function Test-VSCodeConfiguration {
    Write-ColorOutput "`n🔧 Testing VS Code Configuration..." -Color Yellow

    $settingsPath = Join-Path $PSScriptRoot "..\.vscode\settings.json"

    if (-not (Test-Path $settingsPath)) {
        Write-ColorOutput "❌ VS Code settings file not found" -Color Red
        return $false
    }

    try {
        $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json

        if (-not $settings.mcp) {
            Write-ColorOutput "❌ MCP configuration not found in settings" -Color Red
            return $false
        }

        if (-not $settings.mcp.servers.github) {
            Write-ColorOutput "❌ GitHub MCP server not configured" -Color Red
            return $false
        }

        $githubConfig = $settings.mcp.servers.github

        # Check if it's WebSocket configuration
        if ($githubConfig.url -and $githubConfig.url -match '^wss://') {
            Write-ColorOutput "✅ WebSocket configuration detected" -Color Green
        } elseif ($githubConfig.command -and $githubConfig.command -eq "npx") {
            Write-ColorOutput "⚠️  Local npx configuration detected (not WebSocket)" -Color Yellow
        } else {
            Write-ColorOutput "❌ Invalid MCP configuration" -Color Red
            return $false
        }

        # Check required properties
        $requiredProps = @("url", "headers", "reconnect", "reconnectInterval")
        foreach ($prop in $requiredProps) {
            if ($githubConfig.PSObject.Properties.Name -contains $prop) {
                Write-ColorOutput "✅ Property '$prop': OK" -Color Green
            } else {
                Write-ColorOutput "❌ Property '$prop': Missing" -Color Red
            }
        }

        return $true
    }
    catch {
        Write-ColorOutput "❌ Error reading VS Code settings: $($_.Exception.Message)" -Color Red
        return $false
    }
}

function Show-TestResults {
    param([hashtable]$envResults, [bool]$connectivityResult, [bool]$vscodeResult)

    Write-ColorOutput "`n📊 Test Results Summary:" -Color Cyan

    $overallStatus = $true

    # Environment tests
    Write-ColorOutput "`nEnvironment Configuration:" -Color White
    foreach ($key in $envResults.Keys) {
        $status = if ($envResults[$key].Status) { "✅" } else { "❌" }
        Write-ColorOutput "  $status $key" -Color (if ($envResults[$key].Status) { "Green" } else { "Red" })
        if (-not $envResults[$key].Status) { $overallStatus = $false }
    }

    # Connectivity test
    Write-ColorOutput "`nConnectivity:" -Color White
    $status = if ($connectivityResult) { "✅" } else { "❌" }
    Write-ColorOutput "  $status WebSocket Server" -Color (if ($connectivityResult) { "Green" } else { "Red" })
    if (-not $connectivityResult) { $overallStatus = $false }

    # VS Code test
    Write-ColorOutput "`nVS Code Configuration:" -Color White
    $status = if ($vscodeResult) { "✅" } else { "❌" }
    Write-ColorOutput "  $status MCP Settings" -Color (if ($vscodeResult) { "Green" } else { "Red" })
    if (-not $vscodeResult) { $overallStatus = $false }

    # Overall result
    Write-ColorOutput "`n🎯 Overall Status:" -Color Cyan
    if ($overallStatus) {
        Write-ColorOutput "✅ All tests passed! WebSocket MCP is ready." -Color Green
    } else {
        Write-ColorOutput "❌ Some tests failed. Please check the configuration." -Color Red
    }

    return $overallStatus
}

# Main execution
Write-ColorOutput "🚀 Testing WebSocket MCP Server Configuration for WileyWidget" -Color Cyan
Write-ColorOutput "=" * 60 -Color White

# Run tests
$envResults = Test-EnvironmentConfiguration
$vscodeResult = Test-VSCodeConfiguration

$connectivityResult = $true
if ($envResults["MCP_WEBSOCKET_URL"].Status -and $envResults["GITHUB_PERSONAL_ACCESS_TOKEN"].Status) {
    $wsUrl = $envResults["MCP_WEBSOCKET_URL"].Value
    $token = $envResults["GITHUB_PERSONAL_ACCESS_TOKEN"].Value
    $apiKey = $envResults["MCP_API_KEY"].Value
    $connectivityResult = Test-WebSocketConnectivity -wsUrl $wsUrl -token $token -apiKey $apiKey
}

# Show results
$overallResult = Show-TestResults -envResults $envResults -connectivityResult $connectivityResult -vscodeResult $vscodeResult

# Recommendations
Write-ColorOutput "`n💡 Recommendations:" -Color Yellow
if (-not $overallResult) {
    Write-ColorOutput "1. Run '.\Setup-WebSocket-MCP.ps1' to configure the server" -Color White
    Write-ColorOutput "2. Update your .env file with correct values" -Color White
    Write-ColorOutput "3. Ensure your MCP server is running and accessible" -Color White
    Write-ColorOutput "4. Restart VS Code after configuration changes" -Color White
} else {
    Write-ColorOutput "1. Restart VS Code to apply the new configuration" -Color White
    Write-ColorOutput "2. Check VS Code Output panel for MCP connection logs" -Color White
    Write-ColorOutput "3. Test MCP tools in your development workflow" -Color White
}

Write-ColorOutput "`n📚 Resources:" -Color Cyan
Write-ColorOutput "- Setup Guide: .\GitHub-MCP-Setup.md" -Color White
Write-ColorOutput "- Configuration Template: .\Remote-MCP-Config-Template.env" -Color White
Write-ColorOutput "- MCP Documentation: https://modelcontextprotocol.io" -Color White
