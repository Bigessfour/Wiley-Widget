#Requires -Version 7.5
<#
.SYNOPSIS
    Migrates WileyWidget MCP configuration from local to remote server setup.

.DESCRIPTION
    This script helps migrate the GitHub MCP server configuration from a local
    npx-based setup to a remote server configuration with additional settings.

.PARAMETER ServerUrl
    The URL of the remote MCP server.

.PARAMETER UseWebSocket
    Switch to use WebSocket connection instead of HTTP.

.PARAMETER LoadBalanced
    Switch to configure load balancing across multiple servers.

.PARAMETER BackupCurrent
    Switch to backup current configuration before migration.

.EXAMPLE
    .\Migrate-MCP-To-Remote.ps1 -ServerUrl "https://mcp.example.com/github"

.EXAMPLE
    .\Migrate-MCP-To-Remote.ps1 -UseWebSocket -ServerUrl "wss://mcp.example.com/github"

.NOTES
    Author: WileyWidget Development Team
    Version: 1.0.0
    Requires: PowerShell 7.5.2+
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServerUrl,

    [switch]$UseWebSocket,
    [switch]$LoadBalanced,
    [switch]$BackupCurrent
)

# Configuration paths
$settingsPath = Join-Path $PSScriptRoot "..\.vscode\settings.json"
$envPath = Join-Path $PSScriptRoot "..\.env"
$backupDir = Join-Path $PSScriptRoot "..\.vscode\backups"

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Backup-CurrentConfiguration {
    if (-not (Test-Path $backupDir)) {
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    }

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupPath = Join-Path $backupDir "settings_$timestamp.json"

    Copy-Item $settingsPath $backupPath -Force
    Write-ColorOutput "✓ Configuration backed up to: $backupPath" -Color Green
}

function Update-VSCodeSettings {
    param([string]$configType)

    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json

    # Remove existing MCP configuration
    if ($settings.PSObject.Properties.Name -contains "mcp") {
        $settings.PSObject.Properties.Remove("mcp")
    }

    # Add new MCP configuration based on type
    $mcpConfig = [PSCustomObject]@{
        servers = [PSCustomObject]@{
            github = [PSCustomObject]@{}
        }
    }

    switch ($configType) {
        "http" {
            $mcpConfig.servers.github = [PSCustomObject]@{
                url = $ServerUrl
                headers = [PSCustomObject]@{
                    Authorization = "Bearer `$(env:GITHUB_PERSONAL_ACCESS_TOKEN)"
                    "X-API-Key" = "`$(env:MCP_API_KEY)"
                }
                timeout = 30000
            }
        }
        "websocket" {
            $mcpConfig.servers.github = [PSCustomObject]@{
                url = $ServerUrl
                headers = [PSCustomObject]@{
                    Authorization = "Bearer `$(env:GITHUB_PERSONAL_ACCESS_TOKEN)"
                }
                reconnect = $true
                reconnectInterval = 5000
            }
        }
        "loadbalanced" {
            $mcpConfig.servers.github = [PSCustomObject]@{
                urls = @($ServerUrl -split ",")
                loadBalancing = "round-robin"
                headers = [PSCustomObject]@{
                    Authorization = "Bearer `$(env:GITHUB_PERSONAL_ACCESS_TOKEN)"
                }
            }
        }
    }

    $settings | Add-Member -MemberType NoteProperty -Name "mcp" -Value $mcpConfig -Force

    # Convert back to JSON with proper formatting
    $jsonContent = $settings | ConvertTo-Json -Depth 10

    # Fix the environment variable references
    $jsonContent = $jsonContent -replace '"`$(env:', '"${env:'

    $jsonContent | Set-Content $settingsPath -Encoding UTF8

    Write-ColorOutput "✓ VS Code settings updated for $configType configuration" -Color Green
}

function Update-EnvironmentVariables {
    $envContent = Get-Content $envPath -Raw

    # Add remote server configuration variables if they don't exist
    $newVars = @(
        "# Remote MCP Server Configuration",
        "MCP_SERVER_URL=$ServerUrl",
        "MCP_API_KEY=your_mcp_api_key_here",
        "MCP_CONNECTION_TIMEOUT=30000",
        "GITHUB_REQUEST_TIMEOUT=30000",
        "GITHUB_MAX_RETRIES=3",
        "GITHUB_RETRY_DELAY=1000"
    )

    foreach ($var in $newVars) {
        if ($envContent -notmatch [regex]::Escape($var.Split('=')[0])) {
            $envContent += "`n$var"
        }
    }

    $envContent | Set-Content $envPath -Encoding UTF8
    Write-ColorOutput "✓ Environment variables updated" -Color Green
}

function Test-Configuration {
    Write-ColorOutput "`n🔍 Testing configuration..." -Color Yellow

    # Check if environment variables are set
    $requiredVars = @("GITHUB_PERSONAL_ACCESS_TOKEN")
    foreach ($var in $requiredVars) {
        if (-not (Test-Path "Env:$var")) {
            Write-ColorOutput "⚠️  Warning: Environment variable '$var' is not set" -Color Yellow
        }
    }

    # Validate server URL
    try {
        $uri = [System.Uri]::new($ServerUrl)
        Write-ColorOutput "✓ Server URL is valid: $($uri.Scheme)://$($uri.Host)" -Color Green
    }
    catch {
        Write-ColorOutput "❌ Invalid server URL: $ServerUrl" -Color Red
        return $false
    }

    return $true
}

# Main execution
Write-ColorOutput "🚀 Starting MCP Server Migration" -Color Cyan
Write-ColorOutput "Server URL: $ServerUrl" -Color White

if ($BackupCurrent) {
    Backup-CurrentConfiguration
}

# Determine configuration type
$configType = "http"
if ($UseWebSocket) { $configType = "websocket" }
if ($LoadBalanced) { $configType = "loadbalanced" }

Write-ColorOutput "Configuration Type: $configType" -Color White

# Update configurations
Update-VSCodeSettings -configType $configType
Update-EnvironmentVariables

# Test configuration
if (Test-Configuration) {
    Write-ColorOutput "`n✅ Migration completed successfully!" -Color Green
    Write-ColorOutput "`nNext steps:" -Color White
    Write-ColorOutput "1. Restart VS Code to apply new configuration" -Color White
    Write-ColorOutput "2. Update your environment variables in .env file" -Color White
    Write-ColorOutput "3. Test MCP server connection in VS Code" -Color White
} else {
    Write-ColorOutput "`n❌ Migration completed with warnings. Please review the configuration." -Color Red
}
