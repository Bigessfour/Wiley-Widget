#!/usr/bin/env pwsh
# Configure MSSQL MCP Server Connection String
# Usage: .\scripts\setup-mssql-mcp.ps1
# Purpose: Set MSSQL_CONNECTION_STRING environment variable for VS Code MSSQL MCP server

param(
    [ValidateSet('windows', 'sql')]
    [string]$AuthType = 'windows',

    [string]$Server = 'localhost',

    [string]$Database = 'WileyWidget',

    [string]$UserId = '',

    [string]$Password = '',

    [ValidateSet('User', 'Machine')]
    [string]$Scope = 'User',

    [switch]$Test,

    [switch]$Help
)

function Show-Help {
    @"
Setup MSSQL MCP Server Connection String

SYNTAX
    .\setup-mssql-mcp.ps1 [OPTIONS]

OPTIONS
    -AuthType <string>
        Authentication method: 'windows' (default) or 'sql'
        Windows: Uses integrated security (current Windows user)
        SQL: Uses SQL Server login with -UserId and -Password

    -Server <string>
        SQL Server name or IP address (default: localhost)
        Examples: localhost, 127.0.0.1, server.example.com

    -Database <string>
        Database name (default: WileyWidget)

    -UserId <string>
        SQL Server login username (required if -AuthType is 'sql')
        Example: sa

    -Password <string>
        SQL Server login password (required if -AuthType is 'sql')
        ⚠️  Avoid using special characters that need escaping

    -Scope <string>
        Environment variable scope: 'User' (default) or 'Machine'
        User: Current Windows user (recommended for development)
        Machine: All users (requires admin privileges)

    -Test
        Test the connection after setting environment variable

    -Help
        Show this help message

EXAMPLES
    # Windows Authentication (recommended)
    .\setup-mssql-mcp.ps1

    # Windows Authentication with custom server
    .\setup-mssql-mcp.ps1 -Server myserver.example.com

    # SQL Authentication with 'sa' user
    .\setup-mssql-mcp.ps1 -AuthType sql -UserId sa -Password MyPassword123

    # Set for all users (Machine scope, requires admin)
    .\setup-mssql-mcp.ps1 -Scope Machine

    # Test the connection
    .\setup-mssql-mcp.ps1 -Test

NOTES
    After running this script:
    1. Restart VS Code for environment variable to take effect
    2. Run 'MCP: Start Server' from Command Palette (Ctrl+Shift+P)
    3. Verify with 'MCP: List Servers' command
    4. Test via Copilot Chat: @mssql Show databases

SEE ALSO
    - docs/MSSQL_MCP_SETUP_GUIDE.md
    - https://code.visualstudio.com/docs/copilot/customization/mcp-servers
"@
}

# Show help if requested
if ($Help) {
    Show-Help
    exit 0
}

# Validate parameters
if ($AuthType -eq 'sql' -and (-not $UserId -or -not $Password)) {
    Write-Host '❌ Error: -UserId and -Password are required when -AuthType is "sql"' -ForegroundColor Red
    exit 1
}

# Build connection string
$connectionString = "Server=$Server;Database=$Database;"

if ($AuthType -eq 'windows') {
    $connectionString += "Integrated Security=true;"
    $authMethod = "Windows Authentication"
}
else {
    $connectionString += "User Id=$UserId;Password=$Password;"
    $authMethod = "SQL Authentication"
}

Write-Host "┌─ MSSQL MCP Setup" -ForegroundColor Cyan
Write-Host "├─ Server: $Server" -ForegroundColor Cyan
Write-Host "├─ Database: $Database" -ForegroundColor Cyan
Write-Host "├─ Auth Method: $authMethod" -ForegroundColor Cyan
Write-Host "├─ Scope: $Scope-level" -ForegroundColor Cyan
Write-Host "└─ Status: Configuring..." -ForegroundColor Cyan
Write-Host ""

# Check if running as admin for Machine scope
if ($Scope -eq 'Machine') {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        Write-Host "❌ Error: Machine scope requires administrator privileges" -ForegroundColor Red
        Write-Host "   Please run this script as Administrator or use -Scope User" -ForegroundColor Yellow
        exit 1
    }
}

# Set the environment variable
try {
    # Sanitize connection string for logging to avoid exposing credentials
    $sanitizedConnectionString = $connectionString
    $sanitizedConnectionString = $sanitizedConnectionString -replace '(?i)(Password\s*=\s*)([^;]+)', '$1********'
    $sanitizedConnectionString = $sanitizedConnectionString -replace '(?i)(Pwd\s*=\s*)([^;]+)', '$1********'
    
    [System.Environment]::SetEnvironmentVariable(
        'MSSQL_CONNECTION_STRING',
        $connectionString,
        [System.EnvironmentVariableTarget]::$Scope
    )
    Write-Host "✅ Environment variable set: MSSQL_CONNECTION_STRING ($Scope scope)" -ForegroundColor Green
    Write-Host "   Value: $sanitizedConnectionString" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Error setting environment variable: $_" -ForegroundColor Red
    exit 1
}

# Also set for current process (immediate use)
$env:MSSQL_CONNECTION_STRING = $connectionString
Write-Host "✅ Current process environment variable updated (session only)" -ForegroundColor Green

Write-Host ""
Write-Host "┌─ Next Steps" -ForegroundColor Cyan
Write-Host "├─ 1. Restart VS Code (environment variable takes effect)" -ForegroundColor Cyan
Write-Host "├─ 2. Open Command Palette: Ctrl+Shift+P" -ForegroundColor Cyan
Write-Host "├─ 3. Run: MCP: Start Server" -ForegroundColor Cyan
Write-Host "├─ 4. Select: mssql" -ForegroundColor Cyan
Write-Host "├─ 5. Verify: MCP: List Servers (should show ✓ mssql)" -ForegroundColor Cyan
Write-Host "└─ 6. Test: Copilot Chat: @mssql Show databases" -ForegroundColor Cyan
Write-Host ""

# Optional: Test the connection
if ($Test) {
    Write-Host "Testing connection..." -ForegroundColor Yellow

    # Try to import SqlServer module (if available)
    try {
        # Import the module, suppress warnings if not installed
        $SqlServerModule = Get-Module -Name SqlServer -ErrorAction SilentlyContinue
        if (-not $SqlServerModule) {
            Write-Host "⚠️  SqlServer module not installed, skipping connection test" -ForegroundColor Yellow
            Write-Host "   To test connections, install: Install-Module -Name SqlServer" -ForegroundColor Gray
        }
        else {
            # Build invoke SQL string based on auth type
            if ($AuthType -eq 'windows') {
                $testQuery = "SELECT DB_NAME() AS CurrentDatabase, @@VERSION AS SqlVersion"
                $result = Invoke-Sqlcmd -ServerInstance $Server -Database $Database -Query $testQuery
            }
            else {
                $credential = New-Object System.Management.Automation.PSCredential($UserId, (ConvertTo-SecureString $Password -AsPlainText -Force))
                $testQuery = "SELECT DB_NAME() AS CurrentDatabase, @@VERSION AS SqlVersion"
                $result = Invoke-Sqlcmd -ServerInstance $Server -Database $Database -Query $testQuery -Credential $credential
            }

            Write-Host "✅ Connection successful!" -ForegroundColor Green
            Write-Host "   Database: $($result.CurrentDatabase)" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "⚠️  Connection test failed: $_" -ForegroundColor Yellow
        Write-Host "   Verify your SQL Server is running and connection string is correct" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "ℹ️  For more information, see: docs/MSSQL_MCP_SETUP_GUIDE.md" -ForegroundColor Cyan
Write-Host ""
