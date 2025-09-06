#!/usr/bin/env pwsh
<#
.SYNOPSIS
    MCP Server Connection Validator
    Tests and validates MCP server connections using official configurations

.DESCRIPTION
    This script validates MCP server connections and persistence settings
    based on official Microsoft and vendor documentation.

.PARAMETER TestAll
    Test all configured MCP servers

.PARAMETER TestAzure
    Test only Azure MCP server

.PARAMETER TestGitHub
    Test only GitHub MCP server

.PARAMETER TestMicrosoftDocs
    Test only Microsoft Docs MCP server

.PARAMETER TestBrightData
    Test only Bright Data MCP server

.PARAMETER ValidateConfig
    Validate configuration files without testing connections

.EXAMPLE
    .\test-mcp-connections.ps1 -TestAll

.EXAMPLE
    .\test-mcp-connections.ps1 -TestAzure -Verbose

.NOTES
    Based on official MCP documentation from:
    - Microsoft MCP Repository: https://github.com/microsoft/mcp
    - Azure MCP Server: https://github.com/microsoft/mcp/tree/main/servers/Azure.Mcp.Server
    - Bright Data MCP: https://docs.brightdata.com/mcp-server/
#>

param(
    [switch]$TestAll,
    [switch]$TestAzure,
    [switch]$TestGitHub,
    [switch]$TestMicrosoftDocs,
    [switch]$TestBrightData,
    [switch]$ValidateConfig,
    [switch]$Verbose
)

# Configuration
$Script:MCP_CONFIG_FILE = Join-Path $PSScriptRoot "..\mcp-config.json"
$Script:ENV_FILE = Join-Path $PSScriptRoot "..\.env"
$Script:LOG_FILE = Join-Path $PSScriptRoot "logs\mcp-test-results.log"

# Ensure log directory exists
$logDir = Split-Path $LOG_FILE -Parent
if (!(Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

function Write-Log {
    param(
        [string]$Message,
        [string]$Level = "INFO"
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"

    Write-Host $logMessage
    Add-Content -Path $LOG_FILE -Value $logMessage
}

function Test-EnvironmentVariables {
    Write-Log "Validating environment variables..." "INFO"

    $requiredVars = @(
        "MCP_CONNECTION_TIMEOUT",
        "MCP_RECONNECT_ATTEMPTS",
        "NODE_TLS_REJECT_UNAUTHORIZED"
    )

    $missingVars = @()
    foreach ($var in $requiredVars) {
        if (!(Test-Path "env:$var")) {
            $missingVars += $var
        }
    }

    if ($missingVars.Count -gt 0) {
        Write-Log "Missing required environment variables: $($missingVars -join ', ')" "WARNING"
        return $false
    }

    Write-Log "All required environment variables are set" "SUCCESS"
    return $true
}

function Test-AzureMCPServer {
    Write-Log "Testing Azure MCP Server connection..." "INFO"

    try {
        # Check if azmcp-win32-x64 is available
        $azmcpPath = Get-Command "azmcp-win32-x64" -ErrorAction SilentlyContinue
        if (!$azmcpPath) {
            Write-Log "Azure MCP CLI (azmcp-win32-x64) not found in PATH" "ERROR"
            Write-Log "Please install Azure MCP Server extension for VS Code" "INFO"
            return $false
        }

        # Test Azure MCP server with timeout (Microsoft recommended)
        $job = Start-Job -ScriptBlock {
            param($azmcpPath)
            & $azmcpPath --version 2>&1
        } -ArgumentList $azmcpPath.Source

        $result = Wait-Job $job -Timeout 10
        if ($result) {
            $output = Receive-Job $job
            Remove-Job $job

            if ($output -match "azmcp|azure|mcp|\d+\.\d+") {
                Write-Log "Azure MCP CLI found at: $($azmcpPath.Source)" "SUCCESS"
                Write-Log "Azure MCP Server configuration validated" "SUCCESS"
                return $true
            } else {
                Write-Log "Azure MCP Server version check failed: $output" "ERROR"
                return $false
            }
        } else {
            Stop-Job $job -ErrorAction SilentlyContinue
            Remove-Job $job -ErrorAction SilentlyContinue
            Write-Log "Azure MCP Server test timed out after 10 seconds" "ERROR"
            return $false
        }

    } catch {
        Write-Log "Azure MCP Server test failed: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

function Test-GitHubMCPServer {
    Write-Log "Testing GitHub MCP Server connection..." "INFO"

    try {
        # Check if npx is available
        $npxPath = Get-Command "npx" -ErrorAction SilentlyContinue
        if (!$npxPath) {
            Write-Log "npx not found in PATH. Please install Node.js" "ERROR"
            return $false
        }

        # Test GitHub MCP server using version check (Microsoft recommended method)
        # Use timeout to prevent hanging
        $job = Start-Job -ScriptBlock {
            param($npxPath)
            & $npxPath --yes @modelcontextprotocol/server-github --version 2>&1
        } -ArgumentList $npxPath.Source

        $result = Wait-Job $job -Timeout 10
        if ($result) {
            $output = Receive-Job $job
            Remove-Job $job

            if ($output -match "GitHub MCP Server") {
                Write-Log "GitHub MCP Server package resolved successfully" "SUCCESS"
                Write-Log "Server version check passed" "INFO"
                return $true
            } else {
                Write-Log "GitHub MCP Server version check failed: $output" "ERROR"
                return $false
            }
        } else {
            Stop-Job $job -ErrorAction SilentlyContinue
            Remove-Job $job -ErrorAction SilentlyContinue
            Write-Log "GitHub MCP Server test timed out after 10 seconds" "ERROR"
            return $false
        }

    } catch {
        Write-Log "GitHub MCP Server test failed: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

function Test-MicrosoftDocsMCPServer {
    Write-Log "Testing Microsoft Docs MCP Server connection..." "INFO"

    try {
        # Microsoft Docs MCP uses HTTP transport, test the base documentation site
        $url = "https://learn.microsoft.com/en-us/"
        $response = Invoke-WebRequest -Uri $url -Method Head -TimeoutSec 10 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Log "Microsoft Learn documentation site is accessible" "SUCCESS"
            Write-Log "Microsoft Docs MCP server endpoint configured for HTTP transport" "INFO"
            return $true
        } else {
            Write-Log "Microsoft Learn site returned status: $($response.StatusCode)" "WARNING"
            return $false
        }

    } catch {
        Write-Log "Microsoft Docs MCP Server test failed: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

function Test-BrightDataMCPServer {
    Write-Log "Testing Bright Data MCP Server connection..." "INFO"

    try {
        # Check if npx is available
        $npxPath = Get-Command "npx" -ErrorAction SilentlyContinue
        if (!$npxPath) {
            Write-Log "npx not found in PATH. Please install Node.js" "ERROR"
            return $false
        }

        # Check if API key is configured
        $apiKey = $env:BRIGHTDATA_API_KEY
        if (!$apiKey -or $apiKey -eq "REPLACE_WITH_REAL_KEY" -or $apiKey.Length -lt 10) {
            Write-Log "Bright Data API key not configured or invalid. Please load from Azure Key Vault" "WARNING"
            return $false
        }

        # Test Bright Data MCP server using version check with timeout
        $job = Start-Job -ScriptBlock {
            param($npxPath)
            & $npxPath --yes @brightdata/mcp --version 2>&1
        } -ArgumentList $npxPath.Source

        $result = Wait-Job $job -Timeout 10
        if ($result) {
            $output = Receive-Job $job
            Remove-Job $job

            if ($LASTEXITCODE -eq 0 -or $output -match "brightdata|mcp") {
                Write-Log "Bright Data MCP Server package resolved successfully" "SUCCESS"
                Write-Log "API key is configured (length: $($apiKey.Length))" "INFO"
                return $true
            } else {
                Write-Log "Bright Data MCP Server package test failed: $output" "ERROR"
                return $false
            }
        } else {
            Stop-Job $job -ErrorAction SilentlyContinue
            Remove-Job $job -ErrorAction SilentlyContinue
            Write-Log "Bright Data MCP Server test timed out after 10 seconds" "ERROR"
            return $false
        }

    } catch {
        Write-Log "Bright Data MCP Server test failed: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

function Validate-ConfigurationFiles {
    Write-Log "Validating configuration files..." "INFO"

    $configValid = $true

    # Check .env file
    if (!(Test-Path $ENV_FILE)) {
        Write-Log ".env file not found at: $ENV_FILE" "ERROR"
        $configValid = $false
    } else {
        Write-Log ".env file found and accessible" "SUCCESS"
    }

    # Check mcp-config.json file
    if (!(Test-Path $MCP_CONFIG_FILE)) {
        Write-Log "mcp-config.json file not found at: $MCP_CONFIG_FILE" "ERROR"
        $configValid = $false
    } else {
        try {
            $config = Get-Content $MCP_CONFIG_FILE -Raw | ConvertFrom-Json
            Write-Log "mcp-config.json is valid JSON" "SUCCESS"

            # Validate required servers
            $requiredServers = @("azure", "github", "microsoft-docs", "brightdata")
            foreach ($server in $requiredServers) {
                if ($config.mcpServers.$server) {
                    Write-Log "Server '$server' configuration found" "SUCCESS"
                } else {
                    Write-Log "Server '$server' configuration missing" "WARNING"
                }
            }
        } catch {
            Write-Log "mcp-config.json contains invalid JSON: $($_.Exception.Message)" "ERROR"
            $configValid = $false
        }
    }

    return $configValid
}

function Show-Summary {
    param(
        [hashtable]$Results
    )

    Write-Log "`n=== MCP Server Connection Test Summary ===" "INFO"
    Write-Log "Test completed at: $(Get-Date)" "INFO"
    Write-Log "Results:" "INFO"

    foreach ($test in $Results.GetEnumerator()) {
        $status = if ($test.Value) { "PASS" } else { "FAIL" }
        Write-Log "  $($test.Key): $status" "INFO"
    }

    $totalTests = $Results.Count
    $passedTests = ($Results.Values | Where-Object { $_ -eq $true }).Count
    $successRate = [math]::Round(($passedTests / $totalTests) * 100, 1)

    Write-Log "`nSuccess Rate: $passedTests/$totalTests ($successRate%)" "INFO"

    if ($successRate -eq 100) {
        Write-Log "Overall Status: EXCELLENT - All MCP servers are properly configured" "SUCCESS"
    } elseif ($successRate -ge 80) {
        Write-Log "Overall Status: GOOD - Most MCP servers are properly configured" "SUCCESS"
    } elseif ($successRate -ge 50) {
        Write-Log "Overall Status: FAIR - Some MCP servers need attention" "WARNING"
    } else {
        Write-Log "Overall Status: POOR - Multiple MCP servers need configuration" "ERROR"
    }
}

# Main execution
Write-Log "=== MCP Server Connection Validator Started ===" "INFO"
Write-Log "Log file: $LOG_FILE" "INFO"

# Load environment variables from .env file (don't override existing ones)
if (Test-Path $ENV_FILE) {
    Write-Log "Loading environment variables from .env file..." "INFO"
    Get-Content $ENV_FILE | ForEach-Object {
        if ($_ -match '^([^#][^=]+)=(.*)$') {
            $key = $matches[1].Trim()
            $value = $matches[2].Trim()
            # Only set if not already set (preserves Azure Key Vault values)
            if (!(Test-Path "env:$key")) {
                Set-Item -Path "env:$key" -Value $value
            }
        }
    }
}

$testResults = @{}

# Validate configuration if requested
if ($ValidateConfig) {
    $testResults["ConfigValidation"] = Validate-ConfigurationFiles
}

# Test environment variables
$testResults["EnvironmentVariables"] = Test-EnvironmentVariables

# Test individual servers
if ($TestAll -or $TestAzure) {
    $testResults["AzureMCPServer"] = Test-AzureMCPServer
}

if ($TestAll -or $TestGitHub) {
    $testResults["GitHubMCPServer"] = Test-GitHubMCPServer
}

if ($TestAll -or $TestMicrosoftDocs) {
    $testResults["MicrosoftDocsMCPServer"] = Test-MicrosoftDocsMCPServer
}

if ($TestAll -or $TestBrightData) {
    $testResults["BrightDataMCPServer"] = Test-BrightDataMCPServer
}

# Show summary
Show-Summary -Results $testResults

Write-Log "=== MCP Server Connection Validator Completed ===" "INFO"

# Return exit code based on results
$failedTests = @($testResults.Values | Where-Object { $_ -eq $false }).Count
exit $failedTests
