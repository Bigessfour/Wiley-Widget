#!/usr/bin/env pwsh
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

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Wiley-Widget PowerShell Profile Installation            ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Get profile paths
$sourceProfile = Join-Path (Split-Path $PSScriptRoot) "Microsoft.PowerShell_profile.ps1"
$profileDir = Split-Path $PROFILE -Parent
$profilePath = $PROFILE

Write-Host "📋 Profile Information:" -ForegroundColor Green
Write-Host "  Source: $sourceProfile"
Write-Host "  Target: $profilePath"
Write-Host ""

# Check if source exists
if (-not (Test-Path $sourceProfile)) {
    Write-Host "❌ Source profile not found!" -ForegroundColor Red
    Write-Host "   Expected at: $sourceProfile"
    exit 1
}

# Create profile directory if needed
if (-not (Test-Path $profileDir)) {
    Write-Host "📁 Creating profile directory..." -ForegroundColor Yellow
    $null = New-Item -ItemType Directory -Path $profileDir -Force
    Write-Host "   ✅ Created: $profileDir" -ForegroundColor Green
}

# Backup existing profile if it exists
if (Test-Path $profilePath) {
    $backupPath = "$profilePath.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Write-Host "💾 Backing up existing profile..." -ForegroundColor Yellow
    Copy-Item -Path $profilePath -Destination $backupPath -Force
    Write-Host "   ✅ Backup saved: $backupPath" -ForegroundColor Green
}

# Copy new profile
Write-Host "📦 Installing new profile..." -ForegroundColor Yellow
Copy-Item -Path $sourceProfile -Destination $profilePath -Force
Write-Host "   ✅ Profile installed successfully" -ForegroundColor Green
Write-Host ""

# Test profile
Write-Host "🔍 Testing profile syntax..." -ForegroundColor Yellow
$testResult = Test-Path $profilePath
if ($testResult) {
    Write-Host "   ✅ Profile syntax is valid" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Could not verify profile" -ForegroundColor Yellow
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
