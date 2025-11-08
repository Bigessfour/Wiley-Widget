# Wiley Widget - Working Icon Downloader
# Uses confirmed working URLs from Fluent UI System Icons

$icons = @(
    @{Name = 'dashboard'; Folder = 'Grid' }
    @{Name = 'save'; Folder = 'Save' }
    @{Name = 'settings'; Folder = 'Settings' }
    @{Name = 'search'; Folder = 'Search' }
    @{Name = 'add'; Folder = 'Add' }
    @{Name = 'delete'; Folder = 'Delete' }
    @{Name = 'edit'; Folder = 'Edit' }
    @{Name = 'refresh'; Folder = 'Arrow Clockwise' }
    @{Name = 'undo'; Folder = 'Arrow Undo' }
    @{Name = 'redo'; Folder = 'Arrow Redo' }
    @{Name = 'help'; Folder = 'Question Circle' }
    @{Name = 'import'; Folder = 'Arrow Import' }
    @{Name = 'export'; Folder = 'Arrow Export' }
    @{Name = 'enterprises'; Folder = 'Building' }
    @{Name = 'accounts'; Folder = 'Book' }
    @{Name = 'budget'; Folder = 'Money' }
    @{Name = 'departments'; Folder = 'People Team' }
    @{Name = 'sync'; Folder = 'Arrow Sync' }
    @{Name = 'back'; Folder = 'Arrow Left' }
    @{Name = 'forward'; Folder = 'Arrow Right' }
    @{Name = 'close'; Folder = 'Dismiss' }
    @{Name = 'folder'; Folder = 'Folder' }
    @{Name = 'document'; Folder = 'Document' }
    @{Name = 'user'; Folder = 'Person' }
    @{Name = 'calculator'; Folder = 'Calculator' }
    @{Name = 'chart'; Folder = 'Chart Multiple' }
    @{Name = 'success'; Folder = 'Checkmark Circle' }
    @{Name = 'warning'; Folder = 'Warning' }
    @{Name = 'error'; Folder = 'Error Circle' }
    @{Name = 'info'; Folder = 'Info' }
    @{Name = 'mail'; Folder = 'Mail' }
    @{Name = 'lock'; Folder = 'Lock Closed' }
    @{Name = 'power'; Folder = 'Power' }
    @{Name = 'view'; Folder = 'Eye' }
    @{Name = 'copy'; Folder = 'Copy' }
    @{Name = 'filter'; Folder = 'Filter' }
    @{Name = 'sort'; Folder = 'Arrow Sort' }
    @{Name = 'print'; Folder = 'Print' }
    @{Name = 'home'; Folder = 'Home' }
    @{Name = 'up'; Folder = 'Arrow Up' }
    @{Name = 'down'; Folder = 'Arrow Down' }
)

$outputBase = "$PSScriptRoot\..\WileyWidget.UI\Resources\Icons"
$sizes = @(16, 20, 32)

Write-Host "`nWiley Widget Icon Downloader" -ForegroundColor Cyan
Write-Host "============================`n" -ForegroundColor Cyan

# Create directories
foreach ($size in $sizes) {
    $dir = "$outputBase\${size}x${size}"
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

$successCount = 0
$totalAttempts = $icons.Count * $sizes.Count

Write-Host "Downloading $($icons.Count) icons in $($sizes.Count) sizes...`n"

foreach ($icon in $icons) {
    Write-Host "[$($successCount + 1)/$($icons.Count)] $($icon.Name)" -ForegroundColor White

    foreach ($size in $sizes) {
        $folderName = $icon.Folder
        $iconLower = ($icon.Folder -replace ' ', '_').ToLower()
        $url = "https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets/$folderName/SVG/ic_fluent_${iconLower}_${size}_regular.svg"
        $dest = "$outputBase\${size}x${size}\$($icon.Name).svg"

        try {
            Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
            Write-Host "  ✓ ${size}x${size}" -ForegroundColor Green
        } catch {
            Write-Host "  ✗ ${size}x${size}" -ForegroundColor Red
        }
    }

    $successCount++
    Write-Host ""
}

# Count actual downloaded files
$downloaded16 = (Get-ChildItem "$outputBase\16x16" -File).Count
$downloaded20 = (Get-ChildItem "$outputBase\20x20" -File).Count
$downloaded32 = (Get-ChildItem "$outputBase\32x32" -File).Count

Write-Host "=============================" -ForegroundColor Cyan
Write-Host "Results:" -ForegroundColor Cyan
Write-Host "  16x16: $downloaded16 icons" -ForegroundColor White
Write-Host "  20x20: $downloaded20 icons" -ForegroundColor White
Write-Host "  32x32: $downloaded32 icons" -ForegroundColor White
Write-Host "`nLocation: $outputBase`n" -ForegroundColor White
