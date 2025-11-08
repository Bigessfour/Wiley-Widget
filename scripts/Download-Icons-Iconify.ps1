<#
.SYNOPSIS
    Downloads reliable PNG icons from iconify.design API
.DESCRIPTION
    Uses Iconify's API to download Fluent System icons as PNG.
    Iconify is a reliable CDN with consistent URLs.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$OutputPath = "$PSScriptRoot\..\WileyWidget.UI\Resources\Icons"
)

$ErrorActionPreference = 'Continue'

Write-Output "Wiley Widget Icon Installer - Iconify CDN"
Write-Output "=========================================="

# Icon mapping - using iconify collection names
$icons = @(
    'dashboard', 'building', 'book', 'money', 'people-team',
    'save', 'arrow-clockwise', 'arrow-undo', 'arrow-redo', 'search',
    'question-circle', 'settings', 'arrow-import', 'arrow-export', 'arrow-sync',
    'arrow-upload', 'arrow-download', 'add', 'edit', 'delete',
    'eye', 'copy', 'calculator', 'bot', 'chart-multiple',
    'checkmark-circle', 'warning', 'error-circle', 'info', 'alert',
    'mail', 'chat', 'folder', 'document', 'person',
    'power', 'lock-closed', 'arrow-left', 'arrow-right', 'arrow-up',
    'arrow-down', 'dismiss', 'subtract', 'maximize', 'filter',
    'arrow-sort', 'print', 'home', 'grid', 'data-trending',
    'data-bar-vertical', 'document-table', 'money-calculator', 'document-one-page', 'spinner-ios'
)

$sizes = @(16, 32)
$baseUrl = "https://api.iconify.design/fluent"

# Create directories
foreach ($size in $sizes) {
    $dir = Join-Path $OutputPath "${size}x${size}"
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

$successCount = 0
$total = $icons.Count

foreach ($icon in $icons) {
    $num = $successCount + 1
    Write-Output "[$num/$total] Processing: $icon"
    
    $allSuccess = $true
    foreach ($size in $sizes) {
        $url = "$baseUrl`:$icon.svg?download=true&height=$size&width=$size"
        $outputDir = Join-Path $OutputPath "${size}x${size}"
        $outputFile = Join-Path $outputDir "$icon.svg"
        
        try {
            Invoke-WebRequest -Uri $url -OutFile $outputFile -TimeoutSec 10 -UseBasicParsing
            Write-Output "  ✓ ${size}x${size} downloaded"
        } catch {
            Write-Output "  ✗ ${size}x${size} failed: $_"
            $allSuccess = $false
        }
    }
    
    if ($allSuccess) { $successCount++ }
}

Write-Output "`n=========================================="
Write-Output "Success: $successCount/$total icons downloaded"
Write-Output "Location: $OutputPath"
Write-Output "`nNote: Icons are in SVG format. WPF can use them directly."
