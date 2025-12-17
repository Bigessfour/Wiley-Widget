#!/usr/bin/env pwsh
# VS Code Debugging Configuration Test Script
# This script validates the optimized VS Code debugging setup for WileyWidget

[CmdletBinding()]
param(
    [switch]$TestBuild,
    [switch]$TestEnv,
    [switch]$TestTasks,
    [switch]$All
)

$ErrorActionPreference = "Continue"
$workspaceFolder = $PSScriptRoot

function Test-Environment {
    Write-Host "=== Environment Variable Validation ===" -ForegroundColor Green
    
    $envFile = Join-Path $workspaceFolder ".env"
    if (Test-Path $envFile) {
        Write-Host "✅ .env file found: $envFile" -ForegroundColor Green
        
        # Check key environment variables
        $envContent = Get-Content $envFile -Raw
        $requiredVars = @("SYNCFUSION_LICENSE_KEY", "XAI_API_KEY", "QBO_CLIENT_ID")
        
        foreach ($var in $requiredVars) {
            if ($envContent -match "$var=(.+)") {
                Write-Host "✅ $var is configured" -ForegroundColor Green
            } else {
                Write-Host "⚠️  $var not found in .env" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "❌ .env file not found" -ForegroundColor Red
    }
}

function Test-BuildPaths {
    Write-Host "=== Build Path Validation ===" -ForegroundColor Green
    
    $projectPath = Join-Path $workspaceFolder "src\WileyWidget\WileyWidget.csproj"
    $binPath = Join-Path $workspaceFolder "src\WileyWidget\bin\Debug\net9.0-windows10.0.19041.0\win-x64"
    $exePath = Join-Path $binPath "WileyWidget.exe"
    
    if (Test-Path $projectPath) {
        Write-Host "✅ Project file found: WileyWidget.csproj" -ForegroundColor Green
    } else {
        Write-Host "❌ Project file not found: $projectPath" -ForegroundColor Red
    }
    
    if (Test-Path $binPath) {
        Write-Host "✅ Build output directory exists" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Build output directory not found (run build first): $binPath" -ForegroundColor Yellow
    }
    
    if (Test-Path $exePath) {
        Write-Host "✅ Executable found: WileyWidget.exe" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Executable not found (run build first): $exePath" -ForegroundColor Yellow
    }
}

function Test-VSCodeConfig {
    Write-Host "=== VS Code Configuration Validation ===" -ForegroundColor Green
    
    $launchPath = Join-Path $workspaceFolder ".vscode\launch.json"
    $tasksPath = Join-Path $workspaceFolder ".vscode\tasks.json"
    
    if (Test-Path $launchPath) {
        $launchContent = Get-Content $launchPath -Raw
        if ($launchContent -match "WileyWidget: Launch \(Debug Startup\) - OPTIMIZED") {
            Write-Host "✅ Optimized launch configuration found" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Optimized launch configuration not found" -ForegroundColor Yellow
        }
    }
    
    if (Test-Path $tasksPath) {
        $tasksContent = Get-Content $tasksPath -Raw
        if ($tasksContent -match "WileyWidget: Clean & Build") {
            Write-Host "✅ Optimized task configuration found" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Optimized task configuration not found" -ForegroundColor Yellow
        }
    }
}

function Test-PowerShellCompatibility {
    Write-Host "=== PowerShell Compatibility Check ===" -ForegroundColor Green
    
    Write-Host "PowerShell Version: $($PSVersionTable.PSVersion)" -ForegroundColor Cyan
    
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        Write-Host "✅ PowerShell 7+ detected (compatible)" -ForegroundColor Green
    } else {
        Write-Host "⚠️  PowerShell 5.x detected (may have issues)" -ForegroundColor Yellow
    }
    
    # Test the kill process command syntax
    try {
        $testCmd = "Get-Process -Name 'notepad' -ErrorAction SilentlyContinue | Where-Object { `$_.ProcessName -eq 'notepad' }"
        $null = Invoke-Expression $testCmd
        Write-Host "✅ Process filtering syntax validated" -ForegroundColor Green
    } catch {
        Write-Host "❌ Process filtering syntax error: $_" -ForegroundColor Red
    }
}

function Test-QuickBuild {
    Write-Host "=== Quick Build Test ===" -ForegroundColor Green
    
    try {
        Write-Host "Running: dotnet build --verbosity minimal" -ForegroundColor Cyan
        $buildOutput = dotnet build "$workspaceFolder\WileyWidget.sln" --verbosity minimal --no-restore 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Build successful" -ForegroundColor Green
        } else {
            Write-Host "❌ Build failed" -ForegroundColor Red
            Write-Host $buildOutput -ForegroundColor Red
        }
    } catch {
        Write-Host "❌ Build error: $_" -ForegroundColor Red
    }
}

# Main execution
Write-Host "WileyWidget VS Code Debugging Configuration Validator" -ForegroundColor Magenta
Write-Host "=======================================================" -ForegroundColor Magenta
Write-Host ""

if ($All -or $TestEnv) {
    Test-Environment
    Write-Host ""
}

if ($All -or $TestTasks) {
    Test-VSCodeConfig
    Write-Host ""
}

if ($All -or (!$TestBuild -and !$TestEnv -and !$TestTasks)) {
    Test-BuildPaths
    Write-Host ""
    Test-PowerShellCompatibility
    Write-Host ""
}

if ($All -or $TestBuild) {
    Test-QuickBuild
    Write-Host ""
}

Write-Host "=== Testing Complete ===" -ForegroundColor Magenta
Write-Host ""
Write-Host "To test the optimized debugging configuration:" -ForegroundColor Cyan
Write-Host "1. Open VS Code in this workspace" -ForegroundColor White
Write-Host "2. Press F5 and select 'WileyWidget: Launch (Debug Startup) - OPTIMIZED'" -ForegroundColor White
Write-Host "3. The application should launch with proper process cleanup" -ForegroundColor White
Write-Host ""
Write-Host "To run specific tests:" -ForegroundColor Cyan
Write-Host "./test-vscode-config.ps1 -TestBuild    # Test build process" -ForegroundColor White
Write-Host "./test-vscode-config.ps1 -TestEnv      # Test environment variables" -ForegroundColor White  
Write-Host "./test-vscode-config.ps1 -TestTasks    # Test VS Code configuration" -ForegroundColor White
Write-Host "./test-vscode-config.ps1 -All          # Run all tests" -ForegroundColor White