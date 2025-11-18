# Remove Duplicate Converter Definitions
# Converters defined in Generic.xaml should not be redefined in views

$files = @(
    'src\WileyWidget.UI\Views\Windows\Shell.xaml',
    'src\WileyWidget.UI\Views\Main\UtilityCustomerView.xaml',
    'src\WileyWidget.UI\Views\Main\SettingsView.xaml',
    'src\WileyWidget.UI\Views\Main\QuickBooksView.xaml',
    'src\WileyWidget.UI\Views\Main\ProgressView.xaml',
    'src\WileyWidget.UI\Views\Main\MunicipalAccountView.xaml',
    'src\WileyWidget.UI\Views\Main\ExcelImportView.xaml',
    'src\WileyWidget.UI\Views\Main\EnterpriseView.xaml',
    'src\WileyWidget.UI\Views\Main\DepartmentView.xaml',
    'src\WileyWidget.UI\Views\Main\DashboardView.xaml',
    'src\WileyWidget.UI\Views\Main\BudgetView.xaml',
    'src\WileyWidget.UI\Views\Main\BudgetAnalysisView.xaml',
    'src\WileyWidget.UI\Views\Main\AIAssistView.xaml',
    'src\WileyWidget.UI\Views\Dialogs\MunicipalAccountEditDialogView.xaml',
    'src\WileyWidget.UI\Views\Dialogs\CustomerEditDialogView.xaml'
)

$duplicateConverters = @(
    'BoolToVis',
    'BooleanToVisibilityConverter',
    'InverseBooleanToVisibilityConverter',
    'NullToVis',
    'NullToVisibilityConverter',
    'NullToBool',
    'NullToBoolConverter',
    'StringToVis',
    'StringToVisibilityConverter',
    'ZeroToVisible',
    'ZeroToVisibleConverter',
    'CountToVisibilityConverter',
    'EmptyStringToVisibilityConverter',
    'BalanceColorConverter',
    'CurrencyFormatConverter',
    'ComparisonConverter',
    'GreaterThanConverter',
    'StringEqualsConverter',
    'BooleanToBrushConverter',
    'StatusColorConverter',
    'StatusToColorConverter',
    'UniqueDepartmentsConverter',
    'MessageAlignmentConverter',
    'BudgetProgressConverter'
)

$count = 0

foreach ($file in $files) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw -Encoding UTF8
        $originalContent = $content

        foreach ($converter in $duplicateConverters) {
            # Remove declarations with various namespace prefixes
            $patterns = @(
                "<local:$converter\s+x:Key=`"$converter`"\s*/>",
                "<converters:$converter\s+x:Key=`"$converter`"\s*/>",
                "<root:$converter\s+x:Key=`"$converter`"\s*/>"
            )

            foreach ($pattern in $patterns) {
                $content = $content -replace $pattern, ''
            }
        }

        # Clean up empty lines
        $content = $content -replace '(\r?\n\s*){3,}', "`r`n`r`n"

        if ($content -ne $originalContent) {
            $content | Set-Content $file -Encoding UTF8 -NoNewline
            $count++
            Write-Host "✓ Cleaned: $file" -ForegroundColor Green
        }
    }
}

Write-Host "`n🎉 Cleaned $count files" -ForegroundColor Cyan
