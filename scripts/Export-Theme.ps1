<#
.SYNOPSIS
    Exports Wiley Widget theme resources and merges into App.xaml
.DESCRIPTION
    Simulates Theme Studio export by generating Brushes.xaml from SfSkinManager keys
    and merges the WileyTheme.xaml into App.xaml for the Wiley Widget project.
.PARAMETER ThemeName
    The theme to export (FluentLight, FluentDark, Office2019HighContrast)
.PARAMETER OutputPath
    Path to output the generated files
.EXAMPLE
    .\Export-Theme.ps1 -ThemeName FluentLight -OutputPath "C:\Projects\Wiley-Widget"
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("FluentLight", "FluentDark", "Office2019HighContrast")]
    [string]$ThemeName,

    [Parameter(Mandatory = $false)]
    [string]$OutputPath = $PSScriptRoot,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun
)

# Enable information messages
$InformationPreference = 'Continue'

# Theme color mappings (simulating Theme Studio export)
$themeColors = @{
    "FluentLight"            = @{
        "ContentBackground"     = "#FFFFFF"
        "ContentForeground"     = "#000000"
        "ContentForegroundAlt1" = "#6B6B6B"
        "ContentForegroundAlt2" = "#9B9B9B"
        "ContentForegroundAlt3" = "#CDCDCD"
        "DisabledForeground"    = "#CDCDCD"
        "MutedForeground"       = "#757575"
        "BorderAlt"             = "#E0E0E0"
        "PrimaryColor"          = "#0078D4"
        "HoverBackground"       = "#F3F3F3"
        "SelectedBackground"    = "#E3F2FD"
        "SuccessBackground"     = "#E8F5E8"
        "ErrorBackground"       = "#FFEBEE"
        "WarningBackground"     = "#FFF8E1"
        "InfoBackground"        = "#E3F2FD"
    }
    "FluentDark"             = @{
        "ContentBackground"     = "#1F1F1F"
        "ContentForeground"     = "#FFFFFF"
        "ContentForegroundAlt1" = "#B3B3B3"
        "ContentForegroundAlt2" = "#D1D1D1"
        "ContentForegroundAlt3" = "#5D5D5D"
        "DisabledForeground"    = "#5D5D5D"
        "MutedForeground"       = "#B3B3B3"
        "BorderAlt"             = "#3F3F3F"
        "PrimaryColor"          = "#0078D4"
        "HoverBackground"       = "#2D2D2D"
        "SelectedBackground"    = "#1A1A1A"
        "SuccessBackground"     = "#1B5E20"
        "ErrorBackground"       = "#B71C1C"
        "WarningBackground"     = "#F57F17"
        "InfoBackground"        = "#0D47A1"
    }
    "Office2019HighContrast" = @{
        "ContentBackground"     = "#000000"
        "ContentForeground"     = "#FFFFFF"
        "ContentForegroundAlt1" = "#FFFFFF"
        "ContentForegroundAlt2" = "#FFFFFF"
        "ContentForegroundAlt3" = "#FFFFFF"
        "DisabledForeground"    = "#808080"
        "MutedForeground"       = "#FFFFFF"
        "BorderAlt"             = "#FFFFFF"
        "PrimaryColor"          = "#FFFF00"
        "HoverBackground"       = "#FFFFFF"
        "SelectedBackground"    = "#FFFF00"
        "SuccessBackground"     = "#00FF00"
        "ErrorBackground"       = "#FF0000"
        "WarningBackground"     = "#FFFF00"
        "InfoBackground"        = "#00FFFF"
    }
}

function Export-BrushesXaml {
    param([string]$theme, [string]$path)

    $colors = $themeColors[$theme]
    $brushesXaml = @"
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Exported from Theme Studio - $theme Theme -->

"@

    foreach ($key in $colors.Keys) {
        $color = $colors[$key]
        $brushesXaml += "    <SolidColorBrush x:Key=`"$key`" Color=`"$color`" />`n"
    }

    $brushesXaml += "</ResourceDictionary>"

    $outputFile = Join-Path $path "Brushes_$theme.xaml"
    if ($DryRun) {
        Write-Information "Dry run: Would export Brushes_$theme.xaml to $path"
    }
    else {
        $brushesXaml | Out-File -FilePath $outputFile -Encoding UTF8
        Write-Information "Exported Brushes_$theme.xaml to $path"
    }
}

function Merge-ThemeToApp {
    param([string]$themePath, [string]$appPath)

    if (!(Test-Path $appPath)) {
        Write-Warning "App.xaml not found at $appPath"
        return
    }

    $appContent = Get-Content $appPath -Raw

    # Check if theme is already merged
    if ($appContent -match "WileyTheme\.xaml") {
        Write-Information "Theme already merged in App.xaml"
        return
    }

    # Insert merged dictionary
    $mergeDict = @"
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="$themePath" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
"@

    if ($appContent -match "<.*Application\.Resources>") {
        Write-Information "App.xaml already has Resources section"
    }
    else {
        # Insert before </Application> or </prism:PrismApplication>
        if ($DryRun) {
            Write-Information "Dry run: Would merge theme into App.xaml"
        }
        else {
            $appContent = $appContent -replace "</.*Application>", "$mergeDict</.*Application>"
            $appContent | Out-File -FilePath $appPath -Encoding UTF8
            Write-Information "Merged theme into App.xaml"
        }
    }
}

# Main execution
$themeDir = Join-Path $OutputPath "src\Themes"
$appXaml = Join-Path $OutputPath "src\App.xaml"
$themeXaml = Join-Path $themeDir "WileyTheme.xaml"

# Ensure directories exist
if (!$DryRun -and !(Test-Path $themeDir)) {
    New-Item -ItemType Directory -Path $themeDir -Force
}

# Export brushes
Export-BrushesXaml -theme $ThemeName -path $themeDir

# Copy WileyTheme.txt to .xaml if needed
$themeTxt = Join-Path $themeDir "WileyTheme.txt"
if (Test-Path $themeTxt) {
    if ($DryRun) {
        Write-Information "Dry run: Would copy WileyTheme.txt to WileyTheme.xaml"
    }
    else {
        Copy-Item $themeTxt $themeXaml -Force
        Write-Information "Copied WileyTheme.txt to WileyTheme.xaml"
    }
}

# Merge into App.xaml
Merge-ThemeToApp -themePath "Themes/WileyTheme.xaml" -appPath $appXaml

Write-Information "Theme export and merge completed for $ThemeName"
