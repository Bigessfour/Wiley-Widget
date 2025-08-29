# Trunk CI/CD Maintenance and Setup Script
# This script diagnoses, fixes, and optimizes Trunk configuration

param(
    [switch]$Diagnose,
    [switch]$Fix,
    [switch]$Optimize,
    [switch]$Update,
    [switch]$Reset,
    [switch]$Help
)

$ErrorActionPreference = "Stop"

# Configuration
$TrunkConfigPath = ".trunk\trunk.yaml"
$ToolsPath       = "$env:LOCALAPPDATA\trunk\tools"
$LogsPath        = ".trunk\logs"

function Show-Help {
    Write-Host "Trunk CI/CD Maintenance Script" -ForegroundColor Cyan
    Write-Host "==============================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: .\trunk-maintenance.ps1 [options]" -ForegroundColor White
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  -Diagnose    Run comprehensive diagnostics" -ForegroundColor White
    Write-Host "  -Fix         Fix common configuration issues" -ForegroundColor White
    Write-Host "  -Optimize    Optimize Trunk configuration for CI/CD" -ForegroundColor White
    Write-Host "  -Update      Update all Trunk tools and plugins" -ForegroundColor White
    Write-Host "  -Reset       Reset Trunk configuration to defaults" -ForegroundColor White
    Write-Host "  -Help        Show this help message" -ForegroundColor White
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Green
    Write-Host "  .\trunk-maintenance.ps1 -Diagnose -Fix" -ForegroundColor White
    Write-Host "  .\trunk-maintenance.ps1 -Optimize" -ForegroundColor White
    Write-Host "  .\trunk-maintenance.ps1 -Update" -ForegroundColor White
}

function Run-Diagnostic {
    Write-Host "🔍 Running Trunk Diagnostics..." -ForegroundColor Cyan

    # Check Trunk installation
    Write-Host "  Checking Trunk installation..." -ForegroundColor Gray
    try {
        trunk --version | Out-Null
        Write-Host "  ✅ Trunk is installed" -ForegroundColor Green
    }
    catch {
        Write-Host "  ❌ Trunk not found in PATH" -ForegroundColor Red
        return $false
    }

    # Check authentication
    Write-Host "  Checking authentication..." -ForegroundColor Gray
    try {
        trunk whoami | Out-Null
        Write-Host "  ✅ Authentication successful" -ForegroundColor Green
    }
    catch {
        Write-Host "  ❌ Authentication failed" -ForegroundColor Red
        return $false
    }

    # Check daemon status
    Write-Host "  Checking daemon status..." -ForegroundColor Gray
    try {
        $daemonOutput = trunk daemon status
        if ($daemonOutput -match "running") {
            Write-Host "  ✅ Daemon running" -ForegroundColor Green
        }
        else {
            Write-Host "  ⚠️ Daemon status: $daemonOutput" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "  ❌ Daemon check failed" -ForegroundColor Red
    }

    # Check tool installations
    Write-Host "  Checking tool installations..." -ForegroundColor Gray
    if (Test-Path $ToolsPath) {
        $installedTools = Get-ChildItem $ToolsPath -Directory
        Write-Host "  📦 $($installedTools.Count) tools installed" -ForegroundColor White

        # Check for problematic tools
        $problematicTools = @("gitleaks", "dotnet-format", "semgrep")
        foreach ($tool in $problematicTools) {
            $toolPath = Join-Path $ToolsPath $tool
            if (Test-Path $toolPath) {
                Write-Host "  ✅ $tool installed" -ForegroundColor Green
            }
            else {
                Write-Host "  ⚠️ $tool not installed or failed" -ForegroundColor Yellow
            }
        }
    }
    else {
        Write-Host "  ❌ Tools directory not found" -ForegroundColor Red
    }

    # Check configuration
    Write-Host "  Checking configuration..." -ForegroundColor Gray
    if (Test-Path $TrunkConfigPath) {
        Write-Host "  ✅ Configuration file found" -ForegroundColor Green

        # Check for Windows-incompatible tools
        $config = Get-Content $TrunkConfigPath -Raw | ConvertFrom-Json
        $windowsIncompatible = @("semgrep")
        foreach ($tool in $windowsIncompatible) {
            if ($config.lint.enabled -contains $tool) {
                Write-Host "  ⚠️ $tool may not be compatible with Windows" -ForegroundColor Yellow
            }
        }
    }
    else {
        Write-Host "  ❌ Configuration file not found" -ForegroundColor Red
    }

    # Check recent logs for errors
    Write-Host "  Checking recent logs..." -ForegroundColor Gray
    if (Test-Path $LogsPath) {
        $recentLogs = Get-ChildItem $LogsPath -File |
                      Where-Object LastWriteTime -GT (Get-Date).AddHours(-24)
        if ($recentLogs) {
            Write-Host "  📝 Found $($recentLogs.Count) recent log files" -ForegroundColor White

            # Check for error patterns
            foreach ($log in $recentLogs) {
                $content = Get-Content $log.FullName -Raw
                if ($content -match "error|failed|PATH not found") {
                    Write-Host "  ⚠️ Errors found in $($log.Name)" -ForegroundColor Yellow
                }
            }
        }
    }

    Write-Host "🎉 Diagnostics completed" -ForegroundColor Green
    return $true
}

function Fix-CommonIssue {
    Write-Host "🔧 Fixing common Trunk issues..." -ForegroundColor Cyan

    # Fix PATH issues for Python
    Write-Host "  Checking Python PATH..." -ForegroundColor Gray
    $pythonPaths = $env:PATH -split ';' |
                   Where-Object { $_ -like "*python*" -and (Test-Path $_) }
    if ($pythonPaths.Count -eq 0) {
        Write-Host "  ⚠️ No valid Python paths found in PATH" -ForegroundColor Yellow
    }
    else {
        Write-Host "  ✅ Found $($pythonPaths.Count) Python paths" -ForegroundColor Green
    }

    # Fix dotnet-format compatibility
    Write-Host "  Checking dotnet-format compatibility..." -ForegroundColor Gray
    if (Test-Path $TrunkConfigPath) {
        $config = Get-Content $TrunkConfigPath -Raw
        if ($config -match "dotnet-format") {
            Write-Host "  ⚠️ dotnet-format may not be compatible with current platform" -ForegroundColor Yellow
            Write-Host "  💡 Consider using 'dotnet format' command directly in CI/CD" -ForegroundColor Cyan
        }
    }

    # Fix semgrep Windows compatibility
    Write-Host "  Checking semgrep compatibility..." -ForegroundColor Gray
    if (Test-Path $TrunkConfigPath) {
        $config = Get-Content $TrunkConfigPath -Raw
        if ($config -match "semgrep") {
            Write-Host "  ⚠️ semgrep is not supported on Windows" -ForegroundColor Yellow
            Write-Host "  💡 Consider removing semgrep from trunk.yaml on Windows" -ForegroundColor Cyan
        }
    }

    # Restart daemon if needed
    Write-Host "  Checking daemon health..." -ForegroundColor Gray
    try {
        trunk daemon status | Out-Null
        Write-Host "  ✅ Daemon is healthy" -ForegroundColor Green
    }
    catch {
        Write-Host "  🔄 Restarting daemon..." -ForegroundColor Gray
        try {
            trunk daemon restart
            Write-Host "  ✅ Daemon restarted" -ForegroundColor Green
        }
        catch {
            Write-Host "  ❌ Failed to restart daemon" -ForegroundColor Red
        }
    }

    # Clear cache if issues persist
    Write-Host "  Clearing Trunk cache..." -ForegroundColor Gray
    try {
        trunk cache clean
        Write-Host "  ✅ Cache cleared" -ForegroundColor Green
    }
    catch {
        Write-Host "  ⚠️ Could not clear cache" -ForegroundColor Yellow
    }

    Write-Host "🎉 Common issues fixed" -ForegroundColor Green
}

function Optimize-Configuration {
    Write-Host "⚡ Optimizing Trunk configuration for CI/CD..." -ForegroundColor Cyan

    # Create optimized configuration
    $optimizedConfig = @"
# Optimized Trunk Configuration for Wiley Widget
version: 0.1
cli:
  version: 1.25.0

plugins:
  sources:
    - id: trunk
      ref: v1.7.2
      uri: https://github.com/trunk-io/plugins

runtimes:
  enabled:
    - go@1.21.0
    - node@22.16.0
    - python@3.10.8

lint:
  enabled:
    # Core security scanning
    - trufflehog@3.90.5
    - osv-scanner@2.2.2
    - gitleaks@8.28.0

    # Code quality
    - psscriptanalyzer@1.24.0
    - prettier@3.6.2

    # Skip Windows-incompatible tools
    # - semgrep@1.133.0  # Not supported on Windows
    # - dotnet-format@8.0.0  # Not supported on current platform

  disabled:
    - markdownlint
    - git-diff-check
    - actionlint
    - yamllint
    - checkov

  ignore:
    - linters: [ALL]
      paths:
        - docs/**/*.md
        - "*.md"
        - README.md
        - CHANGELOG.md
        - CONTRIBUTING.md
    - linters: [prettier]
      paths:
        - .github/**/*.yml
        - .github/**/*.yaml
    - linters: [ALL]
      paths:
        - bin/
        - obj/
        - TestResults/
        - coverage/
        - "*.binlog"
        - "*.exe"
        - "*.dll"

actions:
  enabled:
    - trunk-announce
    - trunk-check-pre-push
    - trunk-fmt-pre-commit
    - trunk-upgrade-available

tools:
  enabled:
    - pwsh@7.4.1
    - converttosarif@1.0.0
"@

    # Backup current configuration
    if (Test-Path $TrunkConfigPath) {
        Copy-Item $TrunkConfigPath "$TrunkConfigPath.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')" -Force
        Write-Host "  📋 Configuration backed up" -ForegroundColor Gray
    }

    # Apply optimized configuration
    $optimizedConfig | Out-File $TrunkConfigPath -Encoding UTF8 -Force
    Write-Host "  ✅ Optimized configuration applied" -ForegroundColor Green

    # Sync configuration
    Write-Host "  🔄 Syncing configuration..." -ForegroundColor Gray
    try {
        trunk git-hooks sync
        Write-Host "  ✅ Git hooks synced" -ForegroundColor Green
    }
    catch {
        Write-Host "  ⚠️ Could not sync git hooks" -ForegroundColor Yellow
    }

    Write-Host "🎉 Configuration optimized" -ForegroundColor Green
}

function Update-Tool {
    Write-Host "⬆️ Updating Trunk tools and plugins..." -ForegroundColor Cyan

    # Update Trunk CLI
    Write-Host "  Updating Trunk CLI..." -ForegroundColor Gray
    try {
        trunk upgrade
        Write-Host "  ✅ Trunk CLI updated" -ForegroundColor Green
    }
    catch {
        Write-Host "  ⚠️ Could not update Trunk CLI" -ForegroundColor Yellow
    }

    # Update tools
    Write-Host "  Updating tools..." -ForegroundColor Gray
    try {
        trunk upgrade tools
        Write-Host "  ✅ Tools updated" -ForegroundColor Green
    }
    catch {
        Write-Host "  ⚠️ Could not update tools" -ForegroundColor Yellow
    }

    # Update plugins
    Write-Host "  Updating plugins..." -ForegroundColor Gray
    try {
        trunk plugins upgrade
        Write-Host "  ✅ Plugins updated" -ForegroundColor Green
    }
    catch {
        Write-Host "  ⚠️ Could not update plugins" -ForegroundColor Yellow
    }

    Write-Host "🎉 Updates completed" -ForegroundColor Green
}

function Reset-Configuration {
    Write-Host "🔄 Resetting Trunk configuration..." -ForegroundColor Cyan

    # Create backup
    if (Test-Path $TrunkConfigPath) {
        Copy-Item $TrunkConfigPath "$TrunkConfigPath.reset-backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')" -Force
        Write-Host "  📋 Configuration backed up" -ForegroundColor Gray
    }

    # Reset to defaults
    Write-Host "  Resetting to defaults..." -ForegroundColor Gray
    try {
        trunk deinit
        trunk init
        Write-Host "  ✅ Configuration reset" -ForegroundColor Green
    }
    catch {
        Write-Host "  ❌ Failed to reset configuration" -ForegroundColor Red
        return
    }

    Write-Host "🎉 Configuration reset completed" -ForegroundColor Green
    Write-Host "💡 Run '.\trunk-maintenance.ps1 -Optimize' to apply optimized settings" -ForegroundColor Cyan
}

# Main execution logic
if ($Help) {
    Show-Help
    exit 0
}

$actions = @()

if ($Diagnose) { $actions += "Diagnose" }
if ($Fix)      { $actions += "Fix" }
if ($Optimize) { $actions += "Optimize" }
if ($Update)   { $actions += "Update" }
if ($Reset)    { $actions += "Reset" }

if ($actions.Count -eq 0) {
    Write-Host "No actions specified. Use -Help for usage information." -ForegroundColor Yellow
    exit 1
}

Write-Host "🚀 Trunk CI/CD Maintenance for Wiley Widget" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan

foreach ($action in $actions) {
    try {
        switch ($action) {
            "Diagnose" { Run-Diagnostic }
            "Fix"      { Fix-CommonIssues }
            "Optimize" { Optimize-Configuration }
            "Update"   { Update-Tool }
            "Reset"    { Reset-Configuration }
        }
    }
    catch {
        Write-Host "❌ Failed to execute $action : $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "🎉 Trunk maintenance completed!" -ForegroundColor Green
Write-Host "📚 See docs\trunk-cicd-integration-guide.md for detailed documentation" -ForegroundColor White
