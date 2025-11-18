# Wiley Widget - Icon Resources Simple Installer
# This script downloads essential icons via direct GitHub raw URLs

$icons = @(
    @{Name = 'dashboard'; Size = 20; Url = 'https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets/Grid/SVG/ic_fluent_grid_20_regular.svg' }
    @{Name = 'save'; Size = 20; Url = 'https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets/Save/SVG/ic_fluent_save_20_regular.svg' }
    @{Name = 'settings'; Size = 20; Url = 'https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets/Settings/SVG/ic_fluent_settings_20_regular.svg' }
    @{Name = 'search'; Size = 20; Url = 'https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets/Search/SVG/ic_fluent_search_20_regular.svg' }
    @{Name = 'add'; Size = 20; Url = 'https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets/Add/SVG/ic_fluent_add_20_regular.svg' }
    @{Name = 'delete'; Size = 20; Url = 'https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets/Delete/SVG/ic_fluent_delete_20_regular.svg' }
    @{Name = 'edit'; Size = 20; Url = 'https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets/Edit/SVG/ic_fluent_edit_20_regular.svg' }
    @{Name = 'refresh'; Size = 20; Url = 'https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets/Arrow%20Clockwise/SVG/ic_fluent_arrow_clockwise_20_regular.svg' }
)

$outputBase = "$PSScriptRoot\..\WileyWidget.UI\Resources\Icons"
New-Item -ItemType Directory -Path "$outputBase\20x20" -Force | Out-Null

Write-Output "Downloading 8 essential icons..."

foreach ($icon in $icons) {
    $dest = "$outputBase\20x20\$($icon.Name).svg"
    try {
        Invoke-WebRequest -Uri $icon.Url -OutFile $dest -UseBasicParsing
        Write-Output "✓ $($icon.Name)"
    } catch {
        Write-Output "✗ $($icon.Name) - $_"
    }
}

Write-Output "`nDone! Check: $outputBase\20x20\"
