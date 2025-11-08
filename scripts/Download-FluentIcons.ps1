<#=====================================================================
  Download-FluentIcons.ps1
  --------------------------------------------------------------
  Downloads Fluent UI System Icons (regular) in 16×16 and 32×32 SVG
  and (optionally) converts them to PNG.
=====================================================================#>

# -------------------------- CONFIG --------------------------
$BaseUrl = "https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets"
$Sizes = @(16, 32)                     # we need both
$Style = "regular"                     # line icons
$OutputDir = "$PSScriptRoot\..\WileyWidget.UI\Resources\Icons"  # absolute path
$ConvertToPng = $false                     # set $true if ImageMagick installed
# -----------------------------------------------------------

# Mapping: our internal name → Fluent folder name
$IconMap = [ordered]@{
    # Navigation
    "dashboard"       = "grid"
    "enterprises"     = "building"
    "accounts"        = "book"
    "budget"          = "money"
    "departments"     = "people_team"

    # Actions
    "save"            = "save"
    "refresh"         = "arrow_sync"
    "undo"            = "arrow_undo"
    "redo"            = "arrow_redo"
    "search"          = "search"
    "help"            = "question_circle"
    "settings"        = "settings"

    # Data Ops
    "import"          = "arrow_import"
    "export"          = "arrow_export"
    "sync"            = "arrow_sync"
    "backup"          = "arrow_upload"
    "restore"         = "arrow_download"

    # CRUD
    "add"             = "add"
    "edit"            = "edit"
    "delete"          = "delete"
    "view"            = "eye"
    "copy"            = "copy"

    # Reports
    "financial"       = "money_calculator"
    "budget-analysis" = "data_trending"
    "performance"     = "chart_multiple"
    "custom"          = "document_table"

    # Tools
    "calculator"      = "calculator"
    "ai-assist"       = "bot"
    "analytics"       = "data_bar_vertical"
    "chart"           = "chart_multiple"

    # Status
    "success"         = "checkmark_circle"
    "warning"         = "warning"
    "error"           = "error_circle"
    "info"            = "info"
    "loading"         = "spinner_ios"

    # Communication
    "mail"            = "mail"
    "notification"    = "alert"
    "message"         = "chat"

    # Files
    "folder"          = "folder"
    "document"        = "document"
    "file"            = "document_one_page"

    # System
    "power"           = "power"
    "lock"            = "lock_closed"
    "user"            = "person"

    # Additional navigation
    "back"            = "arrow_left"
    "forward"         = "arrow_right"
    "up"              = "arrow_up"
    "down"            = "arrow_down"
    "close"           = "dismiss"
    "minimize"        = "subtract"
    "maximize"        = "maximize"
    "filter"          = "filter"
    "sort"            = "arrow_sort"
    "print"           = "print"
    "home"            = "home"
}

# --------------------- CREATE FOLDERS ----------------------
Write-Host "`nCreating output directories..." -ForegroundColor Cyan
foreach ($sz in $Sizes) {
    $dir = Join-Path $OutputDir "${sz}x${sz}"
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "  ✓ Created ${sz}x${sz} directory" -ForegroundColor Green
    }
}

# ------------------- DOWNLOAD FUNCTION --------------------
function Download-Icon {
    param(
        [string]$OurName,
        [string]$FluentFolder,
        [int]$Size
    )

    $fileName = "$OurName.svg"
    $destPath = Join-Path $OutputDir "${Size}x${Size}" $fileName

    # Fluent naming: ic_fluent_<folder>_<size>_regular.svg
    $fluentLower = $FluentFolder.ToLower()
    $url = "$BaseUrl/$FluentFolder/SVG/ic_fluent_${fluentLower}_${Size}_$Style.svg"

    try {
        Write-Host "  Downloading $OurName (${Size}×${Size}) ..." -NoNewline
        Invoke-WebRequest -Uri $url -OutFile $destPath -UseBasicParsing -ErrorAction Stop -TimeoutSec 10
        Write-Host " ✓" -ForegroundColor Green

        # ---- optional PNG conversion ----
        if ($ConvertToPng) {
            $pngPath = [System.IO.Path]::ChangeExtension($destPath, ".png")
            $result = magick convert $destPath -background none $pngPath 2>&1
            if ($LASTEXITCODE -eq 0) {
                Remove-Item $destPath   # keep only PNG
                Write-Host "    → PNG created" -ForegroundColor Gray
            } else {
                Write-Warning "    → PNG conversion failed (ImageMagick missing?)"
            }
        }

        return $true
    } catch {
        Write-Host " ✗" -ForegroundColor Red
        Write-Host "    URL: $url" -ForegroundColor DarkGray
        return $false
    }
}

# --------------------- MAIN LOOP -------------------------
Write-Host "`nDownloading icons ($($IconMap.Count) icons × $($Sizes.Count) sizes)...`n" -ForegroundColor Cyan

$successCount = 0
$failedIcons = @()

foreach ($kv in $IconMap.GetEnumerator()) {
    $our = $kv.Key
    $fluent = $kv.Value

    Write-Host "[$($successCount + 1)/$($IconMap.Count)] $our" -ForegroundColor White

    $allSuccess = $true
    foreach ($sz in $Sizes) {
        $result = Download-Icon -OurName $our -FluentFolder $fluent -Size $sz
        if (-not $result) { $allSuccess = $false }
    }

    if ($allSuccess) {
        $successCount++
    } else {
        $failedIcons += $our
    }

    Write-Host ""
}

# --------------------- SUMMARY -------------------------
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "DOWNLOAD SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════`n" -ForegroundColor Cyan

Write-Host "✓ Success: $successCount/$($IconMap.Count) icons" -ForegroundColor Green
Write-Host "✓ Location: $OutputDir`n" -ForegroundColor Green

if ($failedIcons.Count -gt 0) {
    Write-Host "✗ Failed: $($failedIcons.Count) icons" -ForegroundColor Red
    foreach ($icon in $failedIcons) {
        Write-Host "  - $icon" -ForegroundColor Yellow
    }
    Write-Host ""
}

if ($ConvertToPng) {
    Write-Host "PNGs are ready for direct use in XAML (SmallIcon/LargeIcon)." -ForegroundColor White
} else {
    Write-Host "SVGs are ready – WPF can use them directly or convert to PathGeometry." -ForegroundColor White
}

Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Add icons to WileyWidget.UI.csproj as <Resource> items" -ForegroundColor White
Write-Host "2. Update Shell.xaml RibbonButtons with SmallIcon/LargeIcon paths" -ForegroundColor White
Write-Host "3. Test rendering in FluentDark theme`n" -ForegroundColor White
