<#
.SYNOPSIS
    Clears saved ribbon layout cache to force ribbon structure refresh.

.DESCRIPTION
    When ribbon structure changes in code (buttons, groups, tabs), saved layout
    files may cache old state. This script removes those cache files to ensure
    the new ribbon structure is loaded.

.EXAMPLE
    .\scripts\clear-ribbon-cache.ps1
#>

$ErrorActionPreference = 'Stop'

Write-Host "`n🧹 Clearing Ribbon Layout Cache`n" -ForegroundColor Cyan

$workspaceRoot = Split-Path $PSScriptRoot -Parent

# Files and directories to clear
$layoutsDir = Join-Path $workspaceRoot "Layouts"
$cdockFile = Join-Path $workspaceRoot "CDock.xml"
$cdockBinFile = Join-Path $workspaceRoot "CDock.bin"

$cleared = 0

# Remove Layouts directory
if (Test-Path $layoutsDir) {
    Write-Host "  Removing Layouts directory..." -ForegroundColor Yellow
    Remove-Item $layoutsDir -Recurse -Force
    $cleared++
}

# Remove CDock files
if (Test-Path $cdockFile) {
    Write-Host "  Removing CDock.xml..." -ForegroundColor Yellow
    Remove-Item $cdockFile -Force
    $cleared++
}

if (Test-Path $cdockBinFile) {
    Write-Host "  Removing CDock.bin..." -ForegroundColor Yellow
    Remove-Item $cdockBinFile -Force
    $cleared++
}

if ($cleared -eq 0) {
    Write-Host "  ✅ No layout cache files found (already clean)" -ForegroundColor Green
} else {
    Write-Host "`n  ✅ Cleared $cleared cache file(s)" -ForegroundColor Green
    Write-Host "  ℹ️  Next app launch will use default ribbon layout" -ForegroundColor Cyan
}

Write-Host ""
