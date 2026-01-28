<#
.SYNOPSIS
    Generates Visual Studio .vs/mcp.json from VS Code .vscode/mcp.json template

.DESCRIPTION
    Converts VS Code MCP configuration (with ${workspaceFolder} variables)
    to Visual Studio format (with absolute paths). Handles:
    - Variable substitution (${workspaceFolder} -> absolute path)
    - Removes trailing commas (strict JSON for VS)
    - Adds explicit "type": "stdio" to all servers
    - Escapes backslashes for Windows paths

.EXAMPLE
    .\scripts\generate-vs-mcp-config.ps1

.NOTES
    Author: Wiley-Widget Project
    Version: 1.0.0
    Last Updated: 2025-01-15
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Get workspace root (script location's parent directory)
$workspaceRoot = Split-Path -Parent $PSScriptRoot

Write-Host "🔧 Generating .vs/mcp.json from .vscode/mcp.json..." -ForegroundColor Cyan
Write-Host "   Workspace: $workspaceRoot" -ForegroundColor Gray

# Paths
$vscodeConfigPath = Join-Path $workspaceRoot '.vscode' 'mcp.json'
$vsConfigPath = Join-Path $workspaceRoot '.vs' 'mcp.json'

# Verify source file exists
if (-not (Test-Path $vscodeConfigPath)) {
    Write-Error ".vscode/mcp.json not found at: $vscodeConfigPath"
    exit 1
}

# Read VS Code config
Write-Host "📖 Reading .vscode/mcp.json..." -ForegroundColor Gray
$vscodeConfig = Get-Content $vscodeConfigPath -Raw

# Convert to JSON object for manipulation
try {
    # Remove trailing commas (VS Code allows them, but we need clean JSON for conversion)
    $cleanJson = $vscodeConfig -replace ',\s*}', '}' -replace ',\s*]', ']'
    $configObj = $cleanJson | ConvertFrom-Json
} catch {
    Write-Error "Failed to parse .vscode/mcp.json: $_"
    exit 1
}

Write-Host "✅ Parsed configuration with $($configObj.servers.PSObject.Properties.Count) MCP servers" -ForegroundColor Green

# Convert ${workspaceFolder} to absolute path (escaped for JSON)
$escapedPath = $workspaceRoot -replace '\\', '\\\\'

Write-Host "🔄 Transforming configuration..." -ForegroundColor Cyan
Write-Host "   Replacing: `${workspaceFolder}" -ForegroundColor Gray
Write-Host "   With:      $workspaceRoot" -ForegroundColor Gray

# Process each server
foreach ($serverName in $configObj.servers.PSObject.Properties.Name) {
    $server = $configObj.servers.$serverName

    # Ensure "type": "stdio" exists (required by Visual Studio)
    if (-not $server.type) {
        $server | Add-Member -MemberType NoteProperty -Name 'type' -Value 'stdio'
    }

    # Replace ${workspaceFolder} in args array
    if ($server.args) {
        $server.args = @($server.args | ForEach-Object {
            $_ -replace '\$\{workspaceFolder\}', $workspaceRoot
        })
    }

    Write-Host "   ✓ Processed: $serverName" -ForegroundColor Gray
}

# Convert back to JSON with proper formatting
$vsConfig = $configObj | ConvertTo-Json -Depth 10

# Ensure .vs directory exists
$vsDir = Split-Path -Parent $vsConfigPath
if (-not (Test-Path $vsDir)) {
    Write-Host "📁 Creating .vs directory..." -ForegroundColor Gray
    New-Item -ItemType Directory -Path $vsDir -Force | Out-Null
}

# Write Visual Studio config
Write-Host "💾 Writing .vs/mcp.json..." -ForegroundColor Cyan
$vsConfig | Set-Content $vsConfigPath -Encoding UTF8

Write-Host ""
Write-Host "✅ Successfully generated .vs/mcp.json" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Set Syncfusion API key environment variable:" -ForegroundColor Gray
Write-Host "      [System.Environment]::SetEnvironmentVariable('SYNCFUSION_MCP_API_KEY', 'your-key', 'User')" -ForegroundColor DarkGray
Write-Host ""
Write-Host "   2. Restart Visual Studio 2026" -ForegroundColor Gray
Write-Host ""
Write-Host "   3. Open GitHub Copilot Chat → Ask → Agent → Select MCP servers" -ForegroundColor Gray
Write-Host ""
Write-Host "   4. Test with: @SyncfusionWinFormsAssistant What packages do I need?" -ForegroundColor Gray
Write-Host ""

# Show diff summary
Write-Host "📊 Configuration Summary:" -ForegroundColor Cyan
Write-Host "   VS Code config:  $vscodeConfigPath" -ForegroundColor Gray
Write-Host "   Visual Studio:   $vsConfigPath" -ForegroundColor Gray
Write-Host "   Servers:         $($configObj.servers.PSObject.Properties.Count)" -ForegroundColor Gray
Write-Host ""

# Validate generated file
if (Test-Path $vsConfigPath) {
    try {
        $testParse = Get-Content $vsConfigPath -Raw | ConvertFrom-Json
        Write-Host "✅ Validation: Generated file is valid JSON" -ForegroundColor Green
    } catch {
        Write-Warning "Validation: Generated file may have JSON syntax errors: $_"
    }
} else {
    Write-Error "Failed to create .vs/mcp.json"
    exit 1
}

Write-Host "🎉 Setup complete! MCP servers are ready for Visual Studio 2026." -ForegroundColor Green
