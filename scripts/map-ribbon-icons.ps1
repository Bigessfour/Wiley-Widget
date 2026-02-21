#Requires -Version 7
<#
.SYNOPSIS
    Maps existing FlatIcons to standardized ribbon button icon names.
.DESCRIPTION
    Creates icon aliases for Wiley Widget ribbon buttons by copying/linking
    existing Syncfusion flat icons to button-specific names. Generates
    placeholder icons for missing resources.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$flatIconsPath = "$PSScriptRoot\..\src\WileyWidget.WinForms\Resources\FlatIcons"

# Ensure FlatIcons directory exists
if (-not (Test-Path $flatIconsPath)) {
    Write-Error "FlatIcons directory not found: $flatIconsPath"
    exit 1
}

# Icon mapping: RibbonButton -> SourceFlatIcon (without "flat" suffix)
$iconMappings = @{
    # Navigation
    'dashboard' = 'base_business_contacts'  # Will try to find base_business_contactsflat.png
    'accounts' = 'Table_32'
    'budget' = 'base_paperclip_32'
    'budgetoverview' = 'base_charts'

    # Analytics
    'analytics' = 'base_charts'
    'reports' = 'New32'  # Use New32 as document icon
    'deptsummary' = 'Table_32'
    'insightfeed' = '0356_NewComment_32'

    # Tools
    'settings' = 'base_charts'  # Temporary until we get gear icon
    'quickbooks' = 'base_business_contacts'
    'jarvis' = '0356_NewComment_32'  # Chat icon

    # Customers & Billing
    'customers' = 'base_business_contacts'
    'utilitybill' = 'New32'
    'revenuetrends' = 'base_charts'
    'recommendedcharge' = 'base_paperclip_32'

    # Logs
    'activitylog' = 'previouspage'
    'auditlog' = 'previouspage'

    # Layout
    'warroom' = '0205_WebInsertHyperlink_32'
    'lock' = 'PrintArea'
    'reset' = 'previouspage'
    'save' = 'Save16'  # Will upscale to 32x32
}

Write-Host "🎨 Mapping Ribbon Icons..." -ForegroundColor Cyan
Write-Host ""

$mapped = 0
$created = 0
$errors = 0

foreach ($iconName in $iconMappings.Keys) {
    $sourceBaseName = $iconMappings[$iconName]
    $targetFileName = "${iconName}flat.png"
    $targetPath = Join-Path $flatIconsPath $targetFileName

    # Try to find source file with "flat" suffix first
    $sourceFlatName = "${sourceBaseName}flat.png"
    $sourcePath = Join-Path $flatIconsPath $sourceFlatName

    if (Test-Path $sourcePath) {
        if (Test-Path $targetPath) {
            Write-Host "  ✓ $iconName -> $sourceFlatName (already exists)" -ForegroundColor Green
        } else {
            Copy-Item -Path $sourcePath -Destination $targetPath -Force
            Write-Host "  ✓ $iconName -> $sourceFlatName (copied)" -ForegroundColor Green
            $mapped++
        }
    } else {
        # Try without "flat" suffix
        $sourceNoSuffix = "${sourceBaseName}.png"
        $sourcePathNoSuffix = Join-Path $flatIconsPath $sourceNoSuffix

        if (Test-Path $sourcePathNoSuffix) {
            if (Test-Path $targetPath) {
                Write-Host "  ✓ $iconName -> $sourceNoSuffix (already exists)" -ForegroundColor Green
            } else {
                Copy-Item -Path $sourcePathNoSuffix -Destination $targetPath -Force
                Write-Host "  ✓ $iconName -> $sourceNoSuffix (copied)" -ForegroundColor Green
                $mapped++
            }
        } else {
            Write-Host "  ! WARNING: ${iconName} source not found ($sourceFlatName or $sourceNoSuffix)" -ForegroundColor Yellow

            # Create a simple placeholder 32x32 icon
            if (-not (Test-Path $targetPath)) {
                # Use PowerShell + .NET to create a basic placeholder
                Add-Type -AssemblyName System.Drawing
                $bitmap = New-Object System.Drawing.Bitmap(32, 32)
                $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
                $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::LightGray)
                $graphics.FillRectangle($brush, 0, 0, 32, 32)
                $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::DarkGray, 2)
                $graphics.DrawRectangle($pen, 4, 4, 24, 24)

                # Draw text initials
                $font = New-Object System.Drawing.Font("Arial", 10, [System.Drawing.FontStyle]::Bold)
                $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::DarkGray)
                $initials = ($iconName.Substring(0, [Math]::Min(2, $iconName.Length))).ToUpper()
                $graphics.DrawString($initials, $font, $textBrush, 8, 10)

                $bitmap.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)
                $bitmap.Dispose()
                $graphics.Dispose()

                Write-Host "    Created placeholder: $targetFileName" -ForegroundColor Cyan
                $created++
            }
        }
    }
}

Write-Host ""
Write-Host "📊 Summary:" -ForegroundColor Cyan
Write-Host "  Mapped: $mapped"
Write-Host "  Created placeholders: $created"
Write-Host "  Errors: $errors"
Write-Host ""
Write-Host "✅ Icon mapping complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Review placeholders in: $flatIconsPath"
Write-Host "  2. Replace placeholders with proper Syncfusion icons if available"
Write-Host "  3. Rebuild project to embed resources"
