<#
.SYNOPSIS
    Downloads UI icons for the Wiley Widget application from Fluent UI System Icons
.DESCRIPTION
    Batch downloads 48+ icons in 16x16 and 32x32 PNG formats from Microsoft's Fluent UI System Icons.
    Falls back to Material Design Icons if primary source fails.
    Converts SVG to PNG using ImageMagick if available, otherwise downloads pre-rendered PNGs.
.PARAMETER OutputPath
    Root output directory (default: src/Resources/Icons)
.PARAMETER UseImageMagick
    Convert SVG to PNG using ImageMagick (requires ImageMagick installed)
.EXAMPLE
    .\Download-UIIcons.ps1
    .\Download-UIIcons.ps1 -OutputPath "C:\MyProject\Icons" -UseImageMagick
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$OutputPath = "$PSScriptRoot\..\WileyWidget.UI\Resources\Icons",

    [Parameter()]
    [switch]$UseImageMagick,

    [Parameter()]
    [switch]$SkipExisting
)

$ErrorActionPreference = 'Stop'

# Icon mapping: name -> Fluent UI icon name
$iconMapping = @{
    # Navigation (5)
    'dashboard'       = 'ic_fluent_grid_24_regular'
    'enterprises'     = 'ic_fluent_building_24_regular'
    'accounts'        = 'ic_fluent_book_24_regular'
    'budget'          = 'ic_fluent_money_24_regular'
    'departments'     = 'ic_fluent_people_team_24_regular'

    # Actions (7)
    'save'            = 'ic_fluent_save_24_regular'
    'refresh'         = 'ic_fluent_arrow_clockwise_24_regular'
    'undo'            = 'ic_fluent_arrow_undo_24_regular'
    'redo'            = 'ic_fluent_arrow_redo_24_regular'
    'search'          = 'ic_fluent_search_24_regular'
    'help'            = 'ic_fluent_question_circle_24_regular'
    'settings'        = 'ic_fluent_settings_24_regular'

    # Data Operations (5)
    'import'          = 'ic_fluent_arrow_import_24_regular'
    'export'          = 'ic_fluent_arrow_export_24_regular'
    'sync'            = 'ic_fluent_arrow_sync_24_regular'
    'backup'          = 'ic_fluent_database_arrow_up_24_regular'
    'restore'         = 'ic_fluent_database_arrow_down_24_regular'

    # CRUD Operations (5)
    'add'             = 'ic_fluent_add_24_regular'
    'edit'            = 'ic_fluent_edit_24_regular'
    'delete'          = 'ic_fluent_delete_24_regular'
    'view'            = 'ic_fluent_eye_24_regular'
    'copy'            = 'ic_fluent_copy_24_regular'

    # Reports (4)
    'financial'       = 'ic_fluent_money_calculator_24_regular'
    'budget-analysis' = 'ic_fluent_data_trending_24_regular'
    'performance'     = 'ic_fluent_chart_multiple_24_regular'
    'custom'          = 'ic_fluent_document_table_24_regular'

    # Tools (4)
    'calculator'      = 'ic_fluent_calculator_24_regular'
    'ai-assist'       = 'ic_fluent_bot_24_regular'
    'analytics'       = 'ic_fluent_data_bar_vertical_24_regular'
    'chart'           = 'ic_fluent_chart_multiple_24_regular'

    # Status (5)
    'success'         = 'ic_fluent_checkmark_circle_24_regular'
    'warning'         = 'ic_fluent_warning_24_regular'
    'error'           = 'ic_fluent_error_circle_24_regular'
    'info'            = 'ic_fluent_info_24_regular'
    'loading'         = 'ic_fluent_spinner_ios_24_regular'

    # Communication (3)
    'mail'            = 'ic_fluent_mail_24_regular'
    'notification'    = 'ic_fluent_alert_24_regular'
    'message'         = 'ic_fluent_chat_24_regular'

    # Files (3)
    'folder'          = 'ic_fluent_folder_24_regular'
    'document'        = 'ic_fluent_document_24_regular'
    'file'            = 'ic_fluent_document_one_page_24_regular'

    # System (3)
    'power'           = 'ic_fluent_power_24_regular'
    'lock'            = 'ic_fluent_lock_closed_24_regular'
    'user'            = 'ic_fluent_person_24_regular'

    # Additional useful icons
    'back'            = 'ic_fluent_arrow_left_24_regular'
    'forward'         = 'ic_fluent_arrow_right_24_regular'
    'up'              = 'ic_fluent_arrow_up_24_regular'
    'down'            = 'ic_fluent_arrow_down_24_regular'
    'close'           = 'ic_fluent_dismiss_24_regular'
    'minimize'        = 'ic_fluent_subtract_24_regular'
    'maximize'        = 'ic_fluent_maximize_24_regular'
    'filter'          = 'ic_fluent_filter_24_regular'
    'sort'            = 'ic_fluent_arrow_sort_24_regular'
    'print'           = 'ic_fluent_print_24_regular'
    'home'            = 'ic_fluent_home_24_regular'
}

# GitHub raw content base URL for Fluent UI System Icons
$fluentBaseUrl = 'https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets'

function Test-ImageMagick {
    try {
        $null = magick --version 2>$null
        return $true
    } catch {
        return $false
    }
}

function Download-IconFromFluentUI {
    param(
        [string]$IconName,
        [string]$FluentName,
        [int]$Size,
        [string]$OutputDir
    )

    $outputFile = Join-Path $OutputDir "$IconName.png"

    if ($SkipExisting -and (Test-Path $outputFile)) {
        Write-Host "  ✓ Skipping existing: $outputFile" -ForegroundColor Green
        return $true
    }

    # Extract folder name from fluent icon name (e.g., ic_fluent_grid_24_regular -> Grid)
    # Convert fluent name to folder structure
    if ($FluentName -match 'ic_fluent_(.+)_\d+_(regular|filled)') {
        $iconFolder = $matches[1]
        # Convert snake_case to PascalCase for folder name
        $folderName = ($iconFolder -split '_' | ForEach-Object {
                $_.Substring(0, 1).ToUpper() + $_.Substring(1)
            }) -join ' '
    } else {
        Write-Warning "  ✗ Invalid Fluent icon name format: $FluentName"
        return $false
    }

    # Try PNG first (pre-rendered)
    $pngUrl = "$fluentBaseUrl/$folderName/PNG/$FluentName.png"

    try {
        Write-Host "  → Downloading from Fluent UI: $IconName ($Size px)..." -NoNewline
        $response = Invoke-WebRequest -Uri $pngUrl -UseBasicParsing -TimeoutSec 10
        [System.IO.File]::WriteAllBytes($outputFile, $response.Content)

        # Resize if needed using ImageMagick
        if ($UseImageMagick -and (Test-ImageMagick)) {
            magick convert $outputFile -resize "${Size}x${Size}" $outputFile 2>$null
        }

        Write-Host " ✓" -ForegroundColor Green
        return $true
    } catch {
        Write-Host " ✗" -ForegroundColor Red

        # Try SVG as fallback
        $svgUrl = "$fluentBaseUrl/$folderName/SVG/$FluentName.svg"
        try {
            Write-Host "    → Trying SVG..." -NoNewline
            $svgResponse = Invoke-WebRequest -Uri $svgUrl -UseBasicParsing -TimeoutSec 10
            $svgFile = Join-Path $OutputDir "$IconName.svg"
            [System.IO.File]::WriteAllBytes($svgFile, $svgResponse.Content)

            if ($UseImageMagick -and (Test-ImageMagick)) {
                magick convert -background none -density 300 $svgFile -resize "${Size}x${Size}" $outputFile 2>$null
                Remove-Item $svgFile -Force
                Write-Host " ✓ (converted)" -ForegroundColor Green
                return $true
            } else {
                Write-Host " ✓ (SVG saved, manual conversion needed)" -ForegroundColor Yellow
                return $true
            }
        } catch {
            Write-Host " ✗" -ForegroundColor Red
            return $false
        }
    }
}

function Download-IconFromMaterialDesign {
    param(
        [string]$IconName,
        [int]$Size,
        [string]$OutputDir
    )

    $outputFile = Join-Path $OutputDir "$IconName.png"

    if ($SkipExisting -and (Test-Path $outputFile)) {
        return $true
    }

    # Material Design Icons base URL (using Google Fonts API)
    $materialName = $IconName -replace '-', '_'
    $mdUrl = "https://fonts.gstatic.com/s/i/materialicons/$materialName/v1/24dp.png"

    try {
        Write-Host "  → Fallback to Material Design: $IconName..." -NoNewline
        $response = Invoke-WebRequest -Uri $mdUrl -UseBasicParsing -TimeoutSec 10
        [System.IO.File]::WriteAllBytes($outputFile, $response.Content)

        if ($UseImageMagick -and (Test-ImageMagick)) {
            magick convert $outputFile -resize "${Size}x${Size}" $outputFile 2>$null
        }

        Write-Host " ✓" -ForegroundColor Green
        return $true
    } catch {
        Write-Host " ✗" -ForegroundColor Red
        return $false
    }
}

# Main execution
Write-Host "`n╔════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Wiley Widget Icon Downloader                     ║" -ForegroundColor Cyan
Write-Host "║  Fluent UI System Icons → WPF Resources            ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

# Check ImageMagick
if ($UseImageMagick) {
    if (Test-ImageMagick) {
        Write-Host "✓ ImageMagick detected - will convert/resize images" -ForegroundColor Green
    } else {
        Write-Host "✗ ImageMagick not found - will download pre-rendered sizes only" -ForegroundColor Yellow
        Write-Host "  Install from: https://imagemagick.org/script/download.php`n" -ForegroundColor Yellow
        $UseImageMagick = $false
    }
}

# Create output directories
$sizes = @(16, 32)
foreach ($size in $sizes) {
    $dir = Join-Path $OutputPath "${size}x${size}"
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "✓ Created directory: $dir" -ForegroundColor Green
    }
}

Write-Host "`nDownloading $($iconMapping.Count) icons in $($sizes.Count) sizes...`n" -ForegroundColor Cyan

$successCount = 0
$failedIcons = @()

foreach ($icon in $iconMapping.GetEnumerator()) {
    $iconName = $icon.Key
    $fluentName = $icon.Value

    Write-Host "[$($successCount + 1)/$($iconMapping.Count)] $iconName" -ForegroundColor Cyan

    $allSuccess = $true
    foreach ($size in $sizes) {
        $outputDir = Join-Path $OutputPath "${size}x${size}"

        # Try Fluent UI first
        $success = Download-IconFromFluentUI -IconName $iconName -FluentName $fluentName -Size $size -OutputDir $outputDir

        # Fallback to Material Design if Fluent UI fails
        if (-not $success) {
            $success = Download-IconFromMaterialDesign -IconName $iconName -Size $size -OutputDir $outputDir
        }

        if (-not $success) {
            $allSuccess = $false
        }
    }

    if ($allSuccess) {
        $successCount++
    } else {
        $failedIcons += $iconName
    }

    Write-Host ""
}

# Summary
Write-Host "╔════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Download Summary                                  ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

Write-Host "✓ Successfully downloaded: $successCount/$($iconMapping.Count) icons" -ForegroundColor Green

if ($failedIcons.Count -gt 0) {
    Write-Host "✗ Failed downloads: $($failedIcons.Count)" -ForegroundColor Red
    Write-Host "`nFailed icons:" -ForegroundColor Yellow
    $failedIcons | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host "`nThese icons may need manual download or alternative sources.`n" -ForegroundColor Yellow
}

Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "1. Add icons to WileyWidget.UI.csproj as <Resource> items" -ForegroundColor White
Write-Host "2. Update Shell.xaml and DashboardView.xaml with SmallIcon/LargeIcon properties" -ForegroundColor White
Write-Host "3. Test icon rendering in FluentDark theme`n" -ForegroundColor White

# Generate manifest file
$manifestPath = Join-Path $OutputPath "icon-manifest.json"
$manifest = @{
    generated           = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    totalIcons          = $iconMapping.Count
    successfulDownloads = $successCount
    failedDownloads     = $failedIcons.Count
    sizes               = $sizes
    icons               = $iconMapping
    failedIcons         = $failedIcons
}

$manifest | ConvertTo-Json -Depth 5 | Out-File -FilePath $manifestPath -Encoding UTF8
Write-Host "✓ Manifest saved to: $manifestPath`n" -ForegroundColor Green
