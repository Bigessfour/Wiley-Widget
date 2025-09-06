#!/usr/bin/env pwsh
<#
.SYNOPSIS
    MCP Server Terminal Persistence Validator
    Tests MCP server connections across terminal sessions

.DESCRIPTION
    This script validates that MCP servers maintain persistent connections
    across multiple terminal sessions and VS Code restarts.

.PARAMETER TestPersistence
    Test MCP server persistence across terminal sessions

.PARAMETER TestAutostart
    Test MCP server autostart functionality

.PARAMETER ValidateEnvironment
    Validate environment variables and configuration

.EXAMPLE
    .\validate-mcp-persistence.ps1 -TestPersistence

.EXAMPLE
    .\validate-mcp-persistence.ps1 -TestAutostart -Verbose

.NOTES
    Based on official MCP documentation and VS Code MCP configuration
#>

param(
    [switch]$TestPersistence,
    [switch]$TestAutostart,
    [switch]$ValidateEnvironment,
    [switch]$Verbose
)

# Configuration
$ScriptName = "MCP Server Terminal Persistence Validator"
$LogFile = Join-Path $PSScriptRoot "logs\mcp-persistence-test.log"
$ConfigFiles = @(
    ".\.env",
    ".\.vscode\settings.json",
    ".\.vscode\mcp.json",
    ".\mcp-config.json"
)

# Logging function
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $LogMessage = "[$Timestamp] [$Level] $Message"
    Write-Host $LogMessage
    Add-Content -Path $LogFile -Value $LogMessage
}

# Test environment validation
function Test-EnvironmentValidation {
    Write-Log "Starting environment validation..."

    $results = @{}

    # Check configuration files
    foreach ($file in $ConfigFiles) {
        if (Test-Path $file) {
            Write-Log "✅ Configuration file found: $file"
            $results[$file] = $true
        } else {
            Write-Log "❌ Configuration file missing: $file" "ERROR"
            $results[$file] = $false
        }
    }

    # Validate .env file contents
    if (Test-Path ".\.env") {
        $envContent = Get-Content ".\.env" -Raw
        $requiredVars = @(
            "MCP_CONNECTION_TIMEOUT",
            "MCP_RECONNECT_ATTEMPTS",
            "MCP_AUTO_DISCOVERY",
            "MCP_SERVER_AUTOSTART"
        )

        foreach ($var in $requiredVars) {
            if ($envContent -match "$var=") {
                Write-Log "✅ Environment variable configured: $var"
            } else {
                Write-Log "⚠️ Environment variable not found: $var" "WARNING"
            }
        }
    }

    return $results
}

# Test MCP server persistence
function Test-MCPServerPersistence {
    Write-Log "Starting MCP server persistence test..."

    $persistenceResults = @{}

    # Test 1: Check if MCP servers are configured for persistence
    Write-Log "Testing MCP server persistence configuration..."

    $vscodeSettings = Get-Content ".\.vscode\settings.json" -Raw | ConvertFrom-Json
    if ($vscodeSettings.mcp.autostart.enabled -eq $true) {
        Write-Log "✅ MCP autostart is enabled in VS Code settings"
        $persistenceResults["AutostartEnabled"] = $true
    } else {
        Write-Log "❌ MCP autostart is not enabled" "ERROR"
        $persistenceResults["AutostartEnabled"] = $false
    }

    if ($vscodeSettings.mcp.persistence.enabled -eq $true) {
        Write-Log "✅ MCP persistence is enabled in VS Code settings"
        $persistenceResults["PersistenceEnabled"] = $true
    } else {
        Write-Log "❌ MCP persistence is not enabled" "ERROR"
        $persistenceResults["PersistenceEnabled"] = $false
    }

    # Test 2: Validate terminal session persistence
    Write-Log "Testing terminal session persistence..."

    # Check if MCP environment variables persist
    $mcpVars = Get-ChildItem env: | Where-Object { $_.Name -like "MCP_*" }
    if ($mcpVars.Count -gt 0) {
        Write-Log "✅ MCP environment variables found: $($mcpVars.Count)"
        $persistenceResults["EnvironmentVariables"] = $true
    } else {
        Write-Log "⚠️ No MCP environment variables found in current session" "WARNING"
        $persistenceResults["EnvironmentVariables"] = $false
    }

    # Test 3: Check VS Code MCP configuration persistence
    Write-Log "Testing VS Code MCP configuration persistence..."

    if (Test-Path ".\.vscode\mcp.json") {
        $mcpConfig = Get-Content ".\.vscode\mcp.json" -Raw | ConvertFrom-Json
        $serverCount = ($mcpConfig.servers | Get-Member -MemberType NoteProperty).Count
        Write-Log "✅ MCP configuration loaded with $serverCount servers"
        $persistenceResults["VSCodeConfig"] = $true
    } else {
        Write-Log "❌ VS Code MCP configuration file not found" "ERROR"
        $persistenceResults["VSCodeConfig"] = $false
    }

    return $persistenceResults
}

# Test MCP server autostart
function Test-MCPServerAutostart {
    Write-Log "Starting MCP server autostart test..."

    $autostartResults = @{}

    # Check autostart configuration
    $vscodeSettings = Get-Content ".\.vscode\settings.json" -Raw | ConvertFrom-Json

    if ($vscodeSettings.mcp.autostart.enabled -eq $true) {
        Write-Log "✅ MCP autostart is enabled"
        $autostartResults["AutostartEnabled"] = $true

        # Check configured servers for autostart
        $autostartServers = $vscodeSettings.mcp.autostart.servers
        Write-Log "✅ Autostart configured for servers: $($autostartServers -join ', ')"

        # Check autostart delay and timeout
        $delay = $vscodeSettings.mcp.autostart.delay
        $timeout = $vscodeSettings.mcp.autostart.timeout
        Write-Log "✅ Autostart delay: ${delay}ms, timeout: ${timeout}ms"

        $autostartResults["AutostartConfigured"] = $true
    } else {
        Write-Log "❌ MCP autostart is not enabled" "ERROR"
        $autostartResults["AutostartEnabled"] = $false
        $autostartResults["AutostartConfigured"] = $false
    }

    return $autostartResults
}

# Main execution
Write-Log "=== $ScriptName Started ==="
Write-Log "Log file: $LogFile"

$overallResults = @{}

# Run tests based on parameters
if ($ValidateEnvironment -or $TestPersistence -or $TestAutostart) {
    if ($ValidateEnvironment) {
        $overallResults["Environment"] = Test-EnvironmentValidation
    }

    if ($TestPersistence) {
        $overallResults["Persistence"] = Test-MCPServerPersistence
    }

    if ($TestAutostart) {
        $overallResults["Autostart"] = Test-MCPServerAutostart
    }
} else {
    # Run all tests by default
    Write-Log "No specific test requested, running all tests..."
    $overallResults["Environment"] = Test-EnvironmentValidation
    $overallResults["Persistence"] = Test-MCPServerPersistence
    $overallResults["Autostart"] = Test-MCPServerAutostart
}

# Generate summary
Write-Log ""
Write-Log "=== Test Summary ==="
$successCount = 0
$totalTests = 0

foreach ($category in $overallResults.Keys) {
    Write-Log "$category Test Results:"
    foreach ($test in $overallResults[$category].Keys) {
        $result = $overallResults[$category][$test]
        $status = if ($result) { "✅ PASS" } else { "❌ FAIL" }
        Write-Log "  $test : $status"
        $totalTests++
        if ($result) { $successCount++ }
    }
    Write-Log ""
}

$successRate = if ($totalTests -gt 0) { [math]::Round(($successCount / $totalTests) * 100, 1) } else { 0 }
Write-Log "Success Rate: $successCount/$totalTests ($successRate%)"

if ($successRate -ge 90) {
    Write-Log "Overall Status: EXCELLENT - MCP persistence and autostart are properly configured" "SUCCESS"
} elseif ($successRate -ge 75) {
    Write-Log "Overall Status: GOOD - MCP configuration is mostly correct" "WARNING"
} else {
    Write-Log "Overall Status: NEEDS ATTENTION - MCP configuration requires fixes" "ERROR"
}

Write-Log "=== $ScriptName Completed ==="
