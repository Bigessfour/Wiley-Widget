#!/usr/bin/env pwsh
#Requires -Version 7.5.4
<#
.SYNOPSIS
    Install Wiley-Widget PowerShell Profile
.DESCRIPTION
    Copies the Wiley-Widget development profile to the PowerShell profile location
    and shows setup instructions.
.EXAMPLE
    .\Install-WidgetProfile.ps1
#>

[CmdletBinding()]
param()

if ($PSVersionTable.PSVersion -lt [version]'7.5.4') {
    throw "PowerShell 7.5.4+ is required. Current: $($PSVersionTable.PSVersion)"
}

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Wiley-Widget PowerShell Profile Installation            ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Get profile paths
$workspaceRoot = Split-Path $PSScriptRoot -Parent
$sourceProfile = Join-Path $workspaceRoot ".vscode\profile.ps1"
$profileTargets = @(
    $PROFILE.CurrentUserCurrentHost,
    $PROFILE.CurrentUserAllHosts
) | Select-Object -Unique

Write-Host "📋 Profile Information:" -ForegroundColor Green
Write-Host "  Source: $sourceProfile"
Write-Host "  Targets:"
foreach ($target in $profileTargets) {
    Write-Host "    - $target"
}
Write-Host ""

# Check if source exists
if (-not (Test-Path $sourceProfile)) {
    Write-Host "❌ Source profile not found!" -ForegroundColor Red
    Write-Host "   Expected at: $sourceProfile"
    exit 1
}

foreach ($profilePath in $profileTargets) {
    $profileDir = Split-Path $profilePath -Parent

    if (-not (Test-Path $profileDir)) {
        Write-Host "📁 Creating profile directory: $profileDir" -ForegroundColor Yellow
        $null = New-Item -ItemType Directory -Path $profileDir -Force
        Write-Host "   ✅ Created: $profileDir" -ForegroundColor Green
    }

    if (Test-Path $profilePath) {
        $backupPath = "$profilePath.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
        Write-Host "💾 Backing up existing profile: $profilePath" -ForegroundColor Yellow
        Copy-Item -Path $profilePath -Destination $backupPath -Force
        Write-Host "   ✅ Backup saved: $backupPath" -ForegroundColor Green
    }

    Write-Host "📦 Installing profile to: $profilePath" -ForegroundColor Yellow
    Copy-Item -Path $sourceProfile -Destination $profilePath -Force
    Write-Host "   ✅ Profile installed successfully" -ForegroundColor Green
}

Write-Host ""
Write-Host "🔍 Testing profile installation..." -ForegroundColor Yellow
foreach ($profilePath in $profileTargets) {
    if (Test-Path $profilePath) {
        Write-Host "   ✅ Present: $profilePath" -ForegroundColor Green
    }
    else {
        Write-Host "   ⚠️  Missing: $profilePath" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║  Installation Complete!                                  ║" -ForegroundColor Green
Write-Host "╠════════════════════════════════════════════════════════════╣" -ForegroundColor Green
Write-Host "║  Next Steps:                                              ║" -ForegroundColor Green
Write-Host "║  1. Restart your PowerShell session                       ║" -ForegroundColor Green
Write-Host "║  2. The environment banner will display automatically     ║" -ForegroundColor Green
Write-Host "║  3. Use quick commands: w, b, t, r, clean, kill-tests    ║" -ForegroundColor Green
Write-Host "║  4. Run 'Get-Help -Name *Widget*' to see all functions   ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

# Show quick reference
Write-Host "⚡ Quick Reference:" -ForegroundColor Cyan
Write-Host "  w              → Go to workspace root"
Write-Host "  ws             → Go to src folder"
Write-Host "  wt             → Go to tests folder"
Write-Host "  b              → Build solution"
Write-Host "  bf             → Fast build (no analyzers)"
Write-Host "  t              → Run tests"
Write-Host "  r              → Run application"
Write-Host "  clean          → Clean build artifacts"
Write-Host "  kill-tests     → Kill hanging test processes"
Write-Host "  stats          → Show project statistics"
Write-Host "  docs           → Open docs in VS Code"
Write-Host "  sync           → Pull latest git changes"
Write-Host ""
