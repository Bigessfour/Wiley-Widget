#Requires -Version 7.0
<#
.SYNOPSIS
    Converts StaticResource to DynamicResource in XAML files for Fluent theme compatibility.

.DESCRIPTION
    This script scans XAML files and converts {StaticResource Key} to {DynamicResource Key}
    for resources that are Fluent theme-related or Syncfusion styles. This fixes XamlParseException
    errors when using FluentDark and other Fluent themes.

.PARAMETER Path
    The root path to scan for XAML files. Default is "src" directory.

.PARAMETER DryRun
    If specified, shows what would be changed without making actual modifications.

.PARAMETER Backup
    If specified, creates .bak files before modifying originals.

.PARAMETER Report
    Path to save a detailed report of changes made.

.EXAMPLE
    .\Convert-XamlStaticToDynamic.ps1 -DryRun
    Shows what would be changed without modifying files.

.EXAMPLE
    .\Convert-XamlStaticToDynamic.ps1 -Backup -Report "conversion-report.txt"
    Converts resources and creates backups with a detailed report.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Path = "src",

    [Parameter()]
    [switch]$DryRun,

    [Parameter()]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '', Justification = 'Used inside Convert-XamlFile nested function')]
    [switch]$Backup,

    [Parameter()]
    [string]$Report
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

# Resources that should remain as StaticResource (local converters, local styles)
$KEEP_STATIC = @(
    # Converters defined in Generic.xaml
    'StringEqualsConverter',
    'GreaterThanConverter',
    'NullToBoolConverter',
    'InverseBooleanToVisibilityConverter',
    'BooleanToVisibilityConverter',
    'BoolToVis',
    'CountToVisibilityConverter',
    'EmptyStringToVisibilityConverter',
    'StringToVisibilityConverter',
    'ZeroToVisibleConverter',
    'BalanceColorConverter',
    'StatusColorConverter',
    'StatusToColorConverter',
    'MessageAlignmentConverter',
    'MessageBackgroundConverter',
    'UniqueDepartmentsConverter',
    'ComparisonConverter',

    # Local styles that don't reference Fluent resources
    'ErrorTemplate',
    'ValidationErrorBorder',

    # Enum values
    'AccountTypeValues',
    'FundTypeValues'
)

# Resources that MUST be converted to DynamicResource (Fluent theme / Syncfusion styles)
$CONVERT_TO_DYNAMIC = @(
    # Fluent theme text styles
    'BodyStrongTextBlockStyle',
    'BodyTextBlockStyle',
    'CaptionTextBlockStyle',
    'SubtitleTextBlockStyle',
    'TitleTextBlockStyle',
    'BaseTextBlockStyle',
    'HeaderTextBlockStyle',
    'SubHeaderTextBlockStyle',
    'MetricTextBlockStyle',
    'ValueTextBlockStyle',
    'NormalTextStyle',
    'WileyHeaderStyle',
    'WileySubHeaderStyle',
    'WileyBodyStyle',
    'StatusTextBlockStyle',

    # Fluent theme brushes
    'SystemControlBackgroundChromeMediumLowBrush',
    'SystemControlForegroundBaseHighBrush',
    'ApplicationPageBackgroundThemeBrush',
    'SystemAccentColor',
    'PrimaryBrush',
    'SecondaryBrush',
    'AccentBrush',
    'BackgroundBrush',
    'ForegroundBrush',
    'CaptionTextBrush',
    'EmphasisBrush',
    'NegativeBrush',
    'PositiveBrush',
    'PanelBackgroundBrush',
    'PanelBorderBrush',
    'SectionHeaderBrush',
    'TreeGridHeaderBrush',

    # Syncfusion Grid theme brushes
    'GridFilterRowBackgroundBrush',
    'GridFilterRowForegroundBrush',
    'GridGroupDropAreaBackgroundBrush',
    'GridGroupDropAreaForegroundBrush',
    'GridSummaryBackgroundBrush',
    'GridSummaryForegroundBrush',

    # Syncfusion control styles
    'ButtonAdv',
    'SfButton',
    'SfDataGrid',
    'ActionButtonStyle',

    # Cell templates and styles
    'OverdueCellStyle',
    'OverBudgetCellStyle',
    'BalanceCellTemplate',
    'StatusCellTemplate',
    'StatusIndicatorStyle',

    # Chart styles
    'TrackBallLineStyle',
    'PieChartLabelTemplate',

    # Toolbar styles
    'BudgetToolBarStyle',
    'BudgetTreeGridHeaderStyle',
    'HighVisibilityFocusStyle',

    # Custom styles that may reference Fluent resources
    'ValidationTextBox',
    'ValidationIntegerTextBox',
    'ValidationSfMaskedEdit',
    'ValidationTextStyle',

    # Theme-specific
    'CustomBackground'
)

class ConversionResult {
    [string]$File
    [int]$LineNumber
    [string]$ResourceKey
    [string]$OldText
    [string]$NewText
    [string]$Status  # 'Converted', 'Skipped', 'Error'
    [string]$Reason
}

$results = [System.Collections.Generic.List[ConversionResult]]::new()

# Note: $Backup parameter is used in Convert-XamlFile function below
# This explicit reference satisfies PSScriptAnalyzer's unused parameter check
$null = $Backup

function Test-ShouldConvert {
    param([string]$ResourceKey)

    # Remove any extra whitespace
    $ResourceKey = $ResourceKey.Trim()

    # If it's in the keep-static list, don't convert
    if ($KEEP_STATIC -contains $ResourceKey) {
        return $false, "In keep-static list"
    }

    # If it's explicitly in convert list, convert it
    if ($CONVERT_TO_DYNAMIC -contains $ResourceKey) {
        return $true, "In convert-to-dynamic list"
    }

    # Convert any style that ends with 'Style' or 'Template' or 'Brush'
    if ($ResourceKey -match '(Style|Template|Brush|Color)$') {
        return $true, "Matches pattern for theme resources"
    }

    # Default: keep as static (converters, local resources)
    return $false, "Default: keep as static"
}

function Convert-XamlFile {
    param(
        [Parameter(Mandatory)]
        [System.IO.FileInfo]$File
    )

    Write-Information "Processing: $($File.FullName)" -Tags "Cyan"

    try {
        # Read file
        $content = Get-Content -Path $File.FullName -Raw -Encoding UTF8
        $lineNumber = 0
        $fileModified = $false

        # Process line by line for accurate reporting
        $lines = $content -split "`r?`n"
        $modifiedLines = foreach ($line in $lines) {
            $lineNumber++
            $modifiedLine = $line

            # Find all StaticResource references in this line
            $regexMatches = [regex]::Matches($line, '\{StaticResource\s+([^}]+)\}')

            foreach ($match in $regexMatches) {
                $resourceKey = $match.Groups[1].Value.Trim()
                $shouldConvert, $reason = Test-ShouldConvert -ResourceKey $resourceKey

                if ($shouldConvert) {
                    $oldText = $match.Value
                    $newText = "{DynamicResource $resourceKey}"
                    $modifiedLine = $modifiedLine -replace [regex]::Escape($oldText), $newText
                    $fileModified = $true

                    $result = [ConversionResult]@{
                        File        = $File.FullName
                        LineNumber  = $lineNumber
                        ResourceKey = $resourceKey
                        OldText     = $oldText
                        NewText     = $newText
                        Status      = 'Converted'
                        Reason      = $reason
                    }
                    $results.Add($result)

                    Write-Information "  Line $lineNumber : $resourceKey" -Tags "Green"
                    Write-Information "    $oldText → $newText" -Tags "Gray"
                }
                else {
                    Write-Verbose "  Line $lineNumber : Keeping $resourceKey as static ($reason)"
                }
            }

            $modifiedLine
        }

        if ($fileModified) {
            $newContent = $modifiedLines -join "`r`n"

            if (-not $DryRun) {
                # Create backup if requested
                if ($Backup) {
                    $backupPath = "$($File.FullName).bak"
                    Copy-Item -Path $File.FullName -Destination $backupPath -Force
                    Write-Information "  ✓ Backup created: $backupPath" -Tags "Yellow"
                }

                # Write modified content
                Set-Content -Path $File.FullName -Value $newContent -Encoding UTF8 -NoNewline
                Write-Information "  ✓ File updated" -Tags "Green"
            }
            else {
                Write-Information "  [DRY RUN] Would update file" -Tags "Yellow"
            }
        }
        else {
            Write-Information "  No changes needed" -Tags "Gray"
        }

    }
    catch {
        Write-Error "Failed to process $($File.FullName): $_"
        $result = [ConversionResult]@{
            File        = $File.FullName
            LineNumber  = 0
            ResourceKey = ''
            OldText     = ''
            NewText     = ''
            Status      = 'Error'
            Reason      = $_.Exception.Message
        }
        $results.Add($result)
    }
}

# Main execution
Write-Information "=== XAML StaticResource to DynamicResource Converter ===" -Tags "Magenta"
Write-Information ""

if ($DryRun) {
    Write-Information "[DRY RUN MODE] No files will be modified" -Tags "Yellow"
    Write-Information ""
}

# Find all XAML files
$xamlFiles = Get-ChildItem -Path $Path -Filter "*.xaml" -Recurse -File
Write-Information "Found $($xamlFiles.Count) XAML files in '$Path'" -Tags "Cyan"
Write-Information ""

# Process each file
foreach ($file in $xamlFiles) {
    Convert-XamlFile -File $file
    Write-Information ""
}

# Summary
Write-Information "=== Conversion Summary ===" -Tags "Magenta"
$converted = @($results | Where-Object { $_.Status -eq 'Converted' })
$errors = @($results | Where-Object { $_.Status -eq 'Error' })

Write-Information "Total files processed: $($xamlFiles.Count)" -Tags "Cyan"
Write-Information "Total conversions: $($converted.Count)" -Tags "Green"
Write-Information "Total errors: $($errors.Count)" -Tags "Red"

if ($converted.Count -gt 0) {
    Write-Information ""
    Write-Information "Converted resource keys:" -Tags "Cyan"
    $converted | Group-Object ResourceKey | Sort-Object Count -Descending | ForEach-Object {
        Write-Information "  $($_.Name): $($_.Count)x" -Tags "Gray"
    }
}

if ($errors.Count -gt 0) {
    Write-Information ""
    Write-Information "Errors encountered:" -Tags "Red"
    $errors | ForEach-Object {
        Write-Information "  $($_.File): $($_.Reason)" -Tags "Red"
    }
}

# Generate report if requested
if ($Report) {
    $reportContent = @"
XAML StaticResource to DynamicResource Conversion Report
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Mode: $(if ($DryRun) { 'DRY RUN' } else { 'LIVE' })

Summary:
--------
Total files processed: $($xamlFiles.Count)
Total conversions: $($converted.Count)
Total errors: $($errors.Count)

Detailed Changes:
-----------------
"@

    foreach ($result in $results | Sort-Object File, LineNumber) {
        $reportContent += @"

File: $($result.File)
Line: $($result.LineNumber)
Resource: $($result.ResourceKey)
Status: $($result.Status)
Reason: $($result.Reason)
Change: $($result.OldText) → $($result.NewText)

"@
    }

    Set-Content -Path $Report -Value $reportContent -Encoding UTF8
    Write-Information ""
    Write-Information "Report saved to: $Report" -Tags "Green"
}

Write-Information ""
if (-not $DryRun) {
    Write-Information "✓ Conversion complete!" -Tags "Green"
}
else {
    Write-Information "Run without -DryRun to apply changes" -Tags "Yellow"
}
