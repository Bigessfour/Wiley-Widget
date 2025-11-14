#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test script for Syncfusion MCP Server
.DESCRIPTION
    Tests all MCP tools with sample data from the Wiley Widget project
.PARAMETER ToolName
    Specific tool to test (or "all" for all tools)
#>
param(
    [string]$ToolName = "all"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ProjectPath = Join-Path $RepoRoot "src\SyncfusionMcpServer\SyncfusionMcpServer.csproj"

Write-Host "🧪 Testing Syncfusion MCP Server" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

function Invoke-McpTool {
    param(
        [string]$Tool,
        [hashtable]$Arguments
    )
    
    $request = @{
        jsonrpc = "2.0"
        id = 1
        method = "tools/call"
        params = @{
            name = $Tool
            arguments = $Arguments
        }
    } | ConvertTo-Json -Depth 10 -Compress
    
    Write-Host "Testing $Tool..." -ForegroundColor Yellow
    Write-Host "  Arguments: $($Arguments | ConvertTo-Json -Compress)" -ForegroundColor Gray
    
    try {
        $env:WW_REPO_ROOT = $RepoRoot
        $result = $request | dotnet run --project $ProjectPath 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ $Tool passed" -ForegroundColor Green
            $result | Write-Host -ForegroundColor Gray
            return $true
        } else {
            Write-Host "❌ $Tool failed" -ForegroundColor Red
            $result | Write-Host -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "❌ $Tool error: $_" -ForegroundColor Red
        return $false
    }
}

$results = @{}

# Test initialize
Write-Host "Testing initialize..." -ForegroundColor Yellow
$initRequest = @{
    jsonrpc = "2.0"
    id = 0
    method = "initialize"
    params = @{
        protocolVersion = "2024-11-05"
    }
} | ConvertTo-Json -Compress

$initResult = $initRequest | dotnet run --project $ProjectPath 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Initialize successful" -ForegroundColor Green
} else {
    Write-Host "❌ Initialize failed" -ForegroundColor Red
    Write-Host $initResult -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test license check
if ($ToolName -eq "all" -or $ToolName -eq "license") {
    $results["license"] = Invoke-McpTool -Tool "syncfusion_check_license" -Arguments @{
        expectedVersion = "27.2.5"
    }
    Write-Host ""
}

# Test theme validation
if ($ToolName -eq "all" -or $ToolName -eq "theme") {
    $appXamlPath = Join-Path $RepoRoot "src\WileyWidget.WinUI\App.xaml.cs"
    if (Test-Path $appXamlPath) {
        $results["theme"] = Invoke-McpTool -Tool "syncfusion_validate_theme" -Arguments @{
            themeName = "FluentDark"
            appXamlPath = $appXamlPath
        }
    } else {
        Write-Host "⚠ App.xaml.cs not found, skipping theme test" -ForegroundColor Yellow
    }
    Write-Host ""
}

# Test XAML parsing
if ($ToolName -eq "all" -or $ToolName -eq "xaml") {
    $xamlFiles = Get-ChildItem -Path (Join-Path $RepoRoot "src\WileyWidget") -Filter "*.xaml" -Recurse |
        Where-Object { $_.FullName -notmatch "\\obj\\|\\bin\\" } |
        Select-Object -First 1
    
    if ($xamlFiles) {
        $results["xaml"] = Invoke-McpTool -Tool "syncfusion_parse_xaml" -Arguments @{
            xamlPath = $xamlFiles[0].FullName
            validateBindings = $true
            checkNamespaces = $true
        }
    } else {
        Write-Host "⚠ No XAML files found, skipping XAML test" -ForegroundColor Yellow
    }
    Write-Host ""
}

# Test DataGrid analysis
if ($ToolName -eq "all" -or $ToolName -eq "datagrid") {
    $xamlFiles = Get-ChildItem -Path (Join-Path $RepoRoot "src\WileyWidget") -Filter "*View.xaml" -Recurse |
        Where-Object { $_.FullName -notmatch "\\obj\\|\\bin\\" } |
        Select-Object -First 1
    
    if ($xamlFiles) {
        $results["datagrid"] = Invoke-McpTool -Tool "syncfusion_analyze_datagrid" -Arguments @{
            xamlPath = $xamlFiles[0].FullName
            checkBinding = $true
            checkPerformance = $true
        }
    } else {
        Write-Host "⚠ No View XAML files found, skipping DataGrid test" -ForegroundColor Yellow
    }
    Write-Host ""
}

# Test report generation
if ($ToolName -eq "all" -or $ToolName -eq "report") {
    $results["report"] = Invoke-McpTool -Tool "syncfusion_generate_report" -Arguments @{
        projectPath = Join-Path $RepoRoot "src\WileyWidget"
        includeThemes = $true
        includeComponents = $true
        outputFormat = "json"
    }
    Write-Host ""
}

# Summary
Write-Host "================================" -ForegroundColor Cyan
Write-Host "Test Summary:" -ForegroundColor Cyan
$passed = ($results.Values | Where-Object { $_ -eq $true }).Count
$total = $results.Count
Write-Host "  Passed: $passed / $total" -ForegroundColor $(if ($passed -eq $total) { "Green" } else { "Yellow" })

if ($passed -eq $total) {
    Write-Host ""
    Write-Host "✅ All tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "⚠ Some tests failed" -ForegroundColor Yellow
    exit 1
}
