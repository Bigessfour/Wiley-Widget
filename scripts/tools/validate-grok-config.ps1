#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validate Grok 4.1 tool calling configuration

.DESCRIPTION
    Checks VS Code settings, continue.json, and grok-config.yaml
    to ensure all tool calling improvements are properly configured.

.EXAMPLE
    .\validate-grok-config.ps1

.NOTES
    Requires: PowerShell 7.5+
#>

#Requires -Version 7.5

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

# Colors
$script:ColorGreen = $PSStyle.Foreground.Green
$script:ColorRed = $PSStyle.Foreground.Red
$script:ColorYellow = $PSStyle.Foreground.Yellow
$script:ColorCyan = $PSStyle.Foreground.Cyan
$script:ColorReset = $PSStyle.Reset

function Test-JsonPath {
    param(
        [string]$FilePath,
        [string]$JsonPath,
        [string]$ExpectedValue,
        [string]$Description
    )
    
    try {
        $content = Get-Content $FilePath -Raw | ConvertFrom-Json
        $value = $content
        
        foreach ($segment in $JsonPath.Split('.')) {
            $value = $value.$segment
        }
        
        if ($null -eq $ExpectedValue) {
            # Just check existence
            if ($null -ne $value) {
                Write-Information "${script:ColorGreen}✓${script:ColorReset} $Description"
                return $true
            }
        } else {
            if ($value -eq $ExpectedValue) {
                Write-Information "${script:ColorGreen}✓${script:ColorReset} $Description"
                return $true
            }
        }
        
        Write-Warning "✗ $Description - Expected: $ExpectedValue, Got: $value"
        return $false
    } catch {
        Write-Warning "✗ $Description - Error: $_"
        return $false
    }
}

function Test-ToolSchema {
    param(
        [string]$FilePath,
        [string]$ToolName
    )
    
    try {
        $content = Get-Content $FilePath -Raw | ConvertFrom-Json
        $tool = $content.tools | Where-Object { $_.name -eq $ToolName }
        
        if ($tool) {
            Write-Information "${script:ColorGreen}✓${script:ColorReset} Tool schema: $ToolName"
            return $true
        } else {
            Write-Warning "✗ Tool schema missing: $ToolName"
            return $false
        }
    } catch {
        Write-Warning "✗ Error checking tool $ToolName`: $_"
        return $false
    }
}

Write-Information "`n${script:ColorCyan}╔═══════════════════════════════════════════════════════════╗${script:ColorReset}"
Write-Information "${script:ColorCyan}║  Grok 4.1 Tool Calling Configuration Validation          ║${script:ColorReset}"
Write-Information "${script:ColorCyan}╚═══════════════════════════════════════════════════════════╝${script:ColorReset}`n"

$results = @{
    Passed = 0
    Failed = 0
}

# Check VS Code settings
Write-Information "${script:ColorYellow}Checking .vscode/settings.json...${script:ColorReset}"
$checks = @(
    @{ Path = 'github.copilot.chat.experimental.toolEnabled'; Value = $true; Desc = 'Copilot tool calling enabled' }
    @{ Path = 'github.copilot.chat.streaming'; Value = $true; Desc = 'Copilot streaming enabled' }
    @{ Path = 'continue.streaming'; Value = $true; Desc = 'Continue streaming enabled' }
    @{ Path = 'continue.observability.enabled'; Value = $true; Desc = 'Observability enabled' }
    @{ Path = 'continue.observability.showToolCalls'; Value = $true; Desc = 'Show tool calls enabled' }
    @{ Path = 'continue.observability.showReasoningTokens'; Value = $true; Desc = 'Show reasoning tokens enabled' }
)

foreach ($check in $checks) {
    if (Test-JsonPath -FilePath '.vscode/settings.json' -JsonPath $check.Path -ExpectedValue $check.Value -Description $check.Desc) {
        $results.Passed++
    } else {
        $results.Failed++
    }
}

# Check continue.json
Write-Information "`n${script:ColorYellow}Checking continue.json...${script:ColorReset}"
$checks = @(
    @{ Path = 'experimental.enableAgentMode'; Value = $true; Desc = 'Agent mode enabled' }
    @{ Path = 'experimental.streaming'; Value = $true; Desc = 'Streaming enabled' }
    @{ Path = 'experimental.parallelToolCalls'; Value = $true; Desc = 'Parallel tool calls enabled' }
    @{ Path = 'experimental.showToolCallProgress'; Value = $true; Desc = 'Tool call progress enabled' }
    @{ Path = 'experimental.cachePrompts'; Value = $true; Desc = 'Prompt caching enabled' }
)

foreach ($check in $checks) {
    if (Test-JsonPath -FilePath 'continue.json' -JsonPath $check.Path -ExpectedValue $check.Value -Description $check.Desc) {
        $results.Passed++
    } else {
        $results.Failed++
    }
}

# Check tool schemas
Write-Information "`n${script:ColorYellow}Checking MCP tool schemas...${script:ColorReset}"
$tools = @(
    'mcp_filesystem_read_text_file',
    'mcp_filesystem_read_multiple_files',
    'mcp_filesystem_edit_file',
    'mcp_filesystem_write_file',
    'mcp_filesystem_search_files',
    'mcp_csharp-mcp_eval_c_sharp',
    'mcp_sequential_th_sequentialthinking'
)

foreach ($tool in $tools) {
    if (Test-ToolSchema -FilePath 'continue.json' -ToolName $tool) {
        $results.Passed++
    } else {
        $results.Failed++
    }
}

# Check grok-config.yaml
Write-Information "`n${script:ColorYellow}Checking config/grok-config.yaml...${script:ColorReset}"
$yamlContent = Get-Content 'config/grok-config.yaml' -Raw
$checks = @(
    @{ Pattern = 'version: 1.2.0'; Desc = 'Config version 1.2.0' }
    @{ Pattern = 'streaming: true'; Desc = 'Streaming enabled' }
    @{ Pattern = 'caching: true'; Desc = 'Caching enabled' }
    @{ Pattern = 'agenticMode: true'; Desc = 'Agentic mode enabled' }
    @{ Pattern = 'showToolCalls: true'; Desc = 'Show tool calls in observability' }
    @{ Pattern = 'showReasoningTokens: true'; Desc = 'Show reasoning tokens in observability' }
)

foreach ($check in $checks) {
    if ($yamlContent -match $check.Pattern) {
        Write-Information "${script:ColorGreen}✓${script:ColorReset} $($check.Desc)"
        $results.Passed++
    } else {
        Write-Warning "✗ $($check.Desc)"
        $results.Failed++
    }
}

# Summary
Write-Information "`n${script:ColorCyan}═══════════════════════════════════════════════════════════${script:ColorReset}"
Write-Information "${script:ColorCyan}Summary:${script:ColorReset}"
Write-Information "${script:ColorGreen}Passed: $($results.Passed)${script:ColorReset}"

if ($results.Failed -gt 0) {
    Write-Information "${script:ColorRed}Failed: $($results.Failed)${script:ColorReset}"
    Write-Warning "`nSome checks failed. Review the output above for details."
    exit 1
} else {
    Write-Information "${script:ColorGreen}Failed: 0${script:ColorReset}"
    Write-Information "`n${script:ColorGreen}✓ All configuration checks passed!${script:ColorReset}"
    Write-Information "${script:ColorCyan}Next: Reload VS Code (Ctrl+Shift+P > 'Developer: Reload Window')${script:ColorReset}"
}
