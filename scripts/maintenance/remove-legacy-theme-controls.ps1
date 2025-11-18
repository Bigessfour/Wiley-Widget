# Remove legacy theme controls from XAML files
# This script removes xmlns:darkTheme declarations and <darkTheme:SfAcrylicPanel> usages

$files = @(
    'C:\Users\biges\Desktop\Wiley_Widget\src\WileyWidget.UI\Views\Dialogs\MunicipalAccountEditDialogView.xaml',
    'C:\Users\biges\Desktop\Wiley_Widget\src\WileyWidget.UI\Views\Main\AIAssistView.xaml',
    'C:\Users\biges\Desktop\Wiley_Widget\src\WileyWidget.UI\Views\Main\DepartmentView.xaml',
    'C:\Users\biges\Desktop\Wiley_Widget\src\WileyWidget.UI\Views\Main\EnterpriseView.xaml',
    'C:\Users\biges\Desktop\Wiley_Widget\src\WileyWidget.UI\Views\Main\MunicipalAccountView.xaml',
    'C:\Users\biges\Desktop\Wiley_Widget\src\WileyWidget.UI\Views\Main\ReportsView.xaml',
    'C:\Users\biges\Desktop\Wiley_Widget\src\WileyWidget.UI\Views\Panels\SettingsPanelView.xaml'
)

foreach ($file in $files) {
    if (-not (Test-Path $file)) {
        Write-Warning "File not found: $file"
        continue
    }

    $content = Get-Content $file -Raw
    $original = $content

    # Remove xmlns:darkTheme declaration
    $content = $content -replace '\s+xmlns:darkTheme="[^"]+"', ''

    # Replace <darkTheme:SfAcrylicPanel...> with <Grid Grid.RowSpan="3">
    $content = $content -replace '<darkTheme:SfAcrylicPanel[^>]*Grid\.RowSpan="(\d+)"[^>]*>\s*<!--[^-]*-->\s*<Grid>', '<Grid Grid.RowSpan="$1">'
    $content = $content -replace '<darkTheme:SfAcrylicPanel[^>]*>\s*<Grid>', '<Grid>'

    # Remove closing tag </darkTheme:SfAcrylicPanel>
    $content = $content -replace '\s*</darkTheme:SfAcrylicPanel>\s*', "`n"

    # Remove any remaining dark Theme: attributes
    $content = $content -replace '\s+darkTheme:[^\s/>]+="[^"]*"', ''

    if ($content -ne $original) {
        Set-Content -Path $file -Value $content -NoNewline
        Write-Host "✓ Fixed: $(Split-Path $file -Leaf)" -ForegroundColor Green
    } else {
        Write-Host "- No changes needed: $(Split-Path $file -Leaf)" -ForegroundColor Gray
    }
}

Write-Host "`nDone! All files processed." -ForegroundColor Cyan
