#Requires -Version 7.0

<#
.SYNOPSIS
    Sets up SQL Server MCP integration for database testing.

.DESCRIPTION
    Configures the MSSQL MCP server for database testing integration with Wiley Widget.
    Handles connection profiles, server discovery, and validation.

.PARAMETER TestConnection
    Tests the SQL Server connection after setup.

.PARAMETER CreateProfile
    Creates a saved connection profile for reuse.

.PARAMETER ProfileName
    Name for the connection profile (default: "WileyWidget-Dev").

.EXAMPLE
    .\setup-sql-mcp.ps1 -TestConnection
    
.EXAMPLE
    .\setup-sql-mcp.ps1 -CreateProfile -ProfileName "WileyWidget-Test"
#>
[CmdletBinding()]
param(
    [Parameter()]
    [switch]$TestConnection,
    
    [Parameter()]
    [switch]$CreateProfile,
    
    [Parameter()]
    [string]$ProfileName = "WileyWidget-Dev"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Configuration
$script:Config = @{
    McpSettingsPath = "$env:APPDATA\Code\User\globalStorage\github.copilot-mcp\settings.json"
    DefaultServer = "localhost"
    DefaultDatabase = "WileyWidget"
    ConnectionTimeout = 30
}

function Write-ColorOutput {
    param(
        [string]$Message,
        [ValidateSet("Success", "Error", "Warning", "Info")]
        [string]$Type = "Info"
    )
    
    $color = switch ($Type) {
        "Success" { "Green" }
        "Error" { "Red" }
        "Warning" { "Yellow" }
        "Info" { "Cyan" }
    }
    
    Write-Host $Message -ForegroundColor $color
}

function Test-SqlServerAvailable {
    <#
    .SYNOPSIS
        Tests if SQL Server is available and accessible.
    #>
    try {
        $service = Get-Service -Name "MSSQLSERVER" -ErrorAction SilentlyContinue
        if ($service -and $service.Status -eq "Running") {
            Write-ColorOutput "✓ SQL Server service is running" -Type Success
            return $true
        }
        
        Write-ColorOutput "⚠ SQL Server service not found or not running" -Type Warning
        Write-ColorOutput "  Checking for SQL Express..." -Type Info
        
        $sqlExpress = Get-Service -Name "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
        if ($sqlExpress -and $sqlExpress.Status -eq "Running") {
            Write-ColorOutput "✓ SQL Server Express is running" -Type Success
            $script:Config.DefaultServer = "localhost\SQLEXPRESS"
            return $true
        }
        
        Write-ColorOutput "✗ No SQL Server instance found running" -Type Error
        return $false
    }
    catch {
        Write-ColorOutput "✗ Error checking SQL Server status: $_" -Type Error
        return $false
    }
}

function Get-McpSettings {
    <#
    .SYNOPSIS
        Retrieves current MCP settings.
    #>
    if (-not (Test-Path $script:Config.McpSettingsPath)) {
        Write-ColorOutput "⚠ MCP settings file not found at: $($script:Config.McpSettingsPath)" -Type Warning
        return $null
    }
    
    try {
        $content = Get-Content $script:Config.McpSettingsPath -Raw
        return $content | ConvertFrom-Json
    }
    catch {
        Write-ColorOutput "✗ Failed to read MCP settings: $_" -Type Error
        return $null
    }
}

function Add-SqlMcpServer {
    <#
    .SYNOPSIS
        Adds SQL Server MCP to Copilot settings.
    #>
    param([object]$CurrentSettings)
    
    $mcpServers = if ($CurrentSettings.mcpServers) {
        $CurrentSettings.mcpServers
    }
    else {
        @{}
    }
    
    # Check if MSSQL MCP already exists
    if ($mcpServers.PSObject.Properties.Name -contains "mssql") {
        Write-ColorOutput "✓ MSSQL MCP server already configured" -Type Success
        return $CurrentSettings
    }
    
    # Add MSSQL MCP configuration
    $mssqlConfig = @{
        command = "npx"
        args = @(
            "-y"
            "@modelcontextprotocol/server-mssql"
        )
        env = @{
            MSSQL_CONNECTION_TIMEOUT = "30"
        }
    }
    
    $mcpServers | Add-Member -NotePropertyName "mssql" -NotePropertyValue $mssqlConfig -Force
    $CurrentSettings.mcpServers = $mcpServers
    
    Write-ColorOutput "✓ Added MSSQL MCP server configuration" -Type Success
    return $CurrentSettings
}

function Save-McpSettings {
    <#
    .SYNOPSIS
        Saves updated MCP settings.
    #>
    param([object]$Settings)
    
    try {
        $settingsDir = Split-Path $script:Config.McpSettingsPath -Parent
        if (-not (Test-Path $settingsDir)) {
            New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
        }
        
        $json = $Settings | ConvertTo-Json -Depth 10
        Set-Content -Path $script:Config.McpSettingsPath -Value $json -Force
        
        Write-ColorOutput "✓ Saved MCP settings" -Type Success
        return $true
    }
    catch {
        Write-ColorOutput "✗ Failed to save MCP settings: $_" -Type Error
        return $false
    }
}

function Test-SqlConnection {
    <#
    .SYNOPSIS
        Tests SQL Server connection using .NET SqlClient.
    #>
    param(
        [string]$Server = $script:Config.DefaultServer,
        [string]$Database = "master"
    )
    
    Write-ColorOutput "`nTesting SQL Server connection..." -Type Info
    Write-ColorOutput "  Server: $Server" -Type Info
    Write-ColorOutput "  Database: $Database" -Type Info
    
    try {
        Add-Type -AssemblyName System.Data
        
        $connectionString = "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=$($script:Config.ConnectionTimeout)"
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        
        $connection.Open()
        
        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT @@VERSION AS Version, DB_NAME() AS CurrentDatabase"
        
        $reader = $command.ExecuteReader()
        if ($reader.Read()) {
            Write-ColorOutput "`n✓ Connection successful!" -Type Success
            Write-ColorOutput "  SQL Server Version: $($reader["Version"])" -Type Info
            Write-ColorOutput "  Current Database: $($reader["CurrentDatabase"])" -Type Info
        }
        $reader.Close()
        $connection.Close()
        
        return $true
    }
    catch {
        Write-ColorOutput "`n✗ Connection failed: $_" -Type Error
        Write-ColorOutput "`nTroubleshooting tips:" -Type Warning
        Write-ColorOutput "  1. Ensure SQL Server service is running" -Type Info
        Write-ColorOutput "  2. Verify TCP/IP is enabled in SQL Server Configuration Manager" -Type Info
        Write-ColorOutput "  3. Check Windows Firewall allows SQL Server connections" -Type Info
        Write-ColorOutput "  4. Verify your Windows account has SQL Server access" -Type Info
        return $false
    }
}

function New-ConnectionProfile {
    <#
    .SYNOPSIS
        Creates a connection profile for MCP SQL Server usage.
    #>
    param(
        [string]$Name = $ProfileName,
        [string]$Server = $script:Config.DefaultServer,
        [string]$Database = $script:Config.DefaultDatabase
    )
    
    $profilesDir = "$PSScriptRoot/../config/sql-profiles"
    if (-not (Test-Path $profilesDir)) {
        New-Item -ItemType Directory -Path $profilesDir -Force | Out-Null
    }
    
    $profile = @{
        name = $Name
        server = $Server
        database = $Database
        authenticationType = "Integrated"
        created = (Get-Date -Format "o")
        connectionTimeout = $script:Config.ConnectionTimeout
    }
    
    $profilePath = Join-Path $profilesDir "$Name.json"
    $profile | ConvertTo-Json -Depth 5 | Set-Content -Path $profilePath -Force
    
    Write-ColorOutput "`n✓ Created connection profile: $Name" -Type Success
    Write-ColorOutput "  Path: $profilePath" -Type Info
    
    return $profile
}

function Show-SetupSummary {
    <#
    .SYNOPSIS
        Displays setup summary and usage instructions.
    #>
    Write-ColorOutput "`n" + ("=" * 70) -Type Info
    Write-ColorOutput "SQL Server MCP Setup Complete!" -Type Success
    Write-ColorOutput ("=" * 70) -Type Info
    
    Write-ColorOutput "`nConfiguration:" -Type Info
    Write-ColorOutput "  • MCP Settings: $($script:Config.McpSettingsPath)" -Type Info
    Write-ColorOutput "  • Default Server: $($script:Config.DefaultServer)" -Type Info
    Write-ColorOutput "  • Default Database: $($script:Config.DefaultDatabase)" -Type Info
    
    Write-ColorOutput "`nAvailable MCP Tools (via GitHub Copilot):" -Type Info
    Write-ColorOutput "  • mssql_connect - Connect to SQL Server" -Type Info
    Write-ColorOutput "  • mssql_list_servers - List available servers" -Type Info
    Write-ColorOutput "  • mssql_list_databases - List databases" -Type Info
    Write-ColorOutput "  • mssql_list_tables - List tables in database" -Type Info
    Write-ColorOutput "  • mssql_query - Execute SQL queries" -Type Info
    
    Write-ColorOutput "`nNext Steps:" -Type Warning
    Write-ColorOutput "  1. Restart VS Code to load MCP configuration" -Type Info
    Write-ColorOutput "  2. Use '@mssql' in Copilot Chat to invoke SQL Server tools" -Type Info
    Write-ColorOutput "  3. Test with: 'List all databases on localhost'" -Type Info
    
    Write-ColorOutput "`nDocumentation:" -Type Info
    Write-ColorOutput "  • MCP SQL Docs: https://modelcontextprotocol.io/servers/mssql" -Type Info
    Write-ColorOutput "  • Wiley Widget Docs: docs/integration/sql-server-mcp.md" -Type Info
    Write-ColorOutput ("=" * 70) + "`n" -Type Info
}

# Main execution
function Main {
    Write-ColorOutput "`n╔══════════════════════════════════════════════════════════╗" -Type Info
    Write-ColorOutput "║  SQL Server MCP Setup for Wiley Widget                  ║" -Type Info
    Write-ColorOutput "╚══════════════════════════════════════════════════════════╝`n" -Type Info
    
    # Step 1: Check SQL Server availability
    Write-ColorOutput "[1/4] Checking SQL Server availability..." -Type Info
    if (-not (Test-SqlServerAvailable)) {
        Write-ColorOutput "`n✗ Setup aborted: SQL Server not available" -Type Error
        exit 1
    }
    
    # Step 2: Load current MCP settings
    Write-ColorOutput "`n[2/4] Loading MCP settings..." -Type Info
    $settings = Get-McpSettings
    if (-not $settings) {
        $settings = @{ mcpServers = @{} }
        Write-ColorOutput "  Created new settings structure" -Type Info
    }
    
    # Step 3: Add SQL MCP server
    Write-ColorOutput "`n[3/4] Configuring MSSQL MCP server..." -Type Info
    $settings = Add-SqlMcpServer -CurrentSettings $settings
    
    if (-not (Save-McpSettings -Settings $settings)) {
        Write-ColorOutput "`n✗ Setup failed: Could not save settings" -Type Error
        exit 1
    }
    
    # Step 4: Optional tests
    Write-ColorOutput "`n[4/4] Running optional validations..." -Type Info
    
    if ($TestConnection) {
        Test-SqlConnection | Out-Null
    }
    
    if ($CreateProfile) {
        New-ConnectionProfile | Out-Null
    }
    
    # Summary
    Show-SetupSummary
    
    Write-ColorOutput "✓ Setup completed successfully!" -Type Success
}

# Run main
try {
    Main
}
catch {
    Write-ColorOutput "`n✗ Setup failed with error: $_" -Type Error
    Write-ColorOutput $_.ScriptStackTrace -Type Error
    exit 1
}
