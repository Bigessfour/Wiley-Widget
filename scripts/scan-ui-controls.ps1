# Wiley Widget UI Control Scanner
# Scans XAML files for non-Syncfusion controls to ensure compliance with Syncfusion-only UI policy

param(
    [switch]$Detailed,
    [switch]$Export
)

$ErrorActionPreference = "Stop"

# Configuration
$projectRoot = Split-Path -Parent $PSScriptRoot
$scanResultsPath = Join-Path $projectRoot "ui-control-scan-results.json"

# Standard WPF controls (flagged for review)
$standardWpfControls = @(
    'Button',
    'TextBlock',
    'TextBox',
    'ComboBox',
    'ListBox',
    'DataGrid',
    'Grid',
    'StackPanel',
    'DockPanel',
    'WrapPanel',
    'Border',
    'Label',
    'CheckBox',
    'RadioButton',
    'ProgressBar',
    'StatusBar',
    'Menu',
    'MenuItem',
    'ToolBar',
    'TreeView',
    'TabControl',
    'TabItem',
    'GroupBox',
    'ScrollViewer',
    'Expander',
    'Slider',
    'DatePicker',
    'Calendar'
)

function Write-ScanLog {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        "ERROR" { "Red" }
        "WARNING" { "Yellow" }
        "SUCCESS" { "Green" }
        "INFO" { "Cyan" }
        default { "White" }
    }
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

function Get-XamlFile {
    param([string]$Path)
    
    Write-ScanLog "🔍 Scanning for XAML files in: $Path" "INFO"

    if (-not (Test-Path $Path)) {
        Write-ScanLog "❌ Path does not exist: $Path" "ERROR"
        return @()
    }

    # Exclude common build directories
    $excludePatterns = @(
        'bin',
        'obj', 
        'Debug',
        'Release',
        '.vs',
        'packages',
        'node_modules'
    )
    
    # Get all directories excluding build artifacts
    $allowedDirs = Get-ChildItem -Path $Path -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object {
        $dirPath = $_.FullName
        $exclude = $false
        foreach ($pattern in $excludePatterns) {
                if ($dirPath -match "[\\/]$([regex]::Escape($pattern))[\\/]") {
                    $exclude = $true
                    break
                }
        }
        -not $exclude
    }
    $allowedDirs += $Path # Include root directory

    # Search for XAML files only in allowed directories
    $xamlFiles = @()
    foreach ($dir in $allowedDirs) {
        $xamlFiles += Get-ChildItem -Path $dir -Filter "*.xaml" -File -ErrorAction SilentlyContinue
    }

    Write-ScanLog "Found $($xamlFiles.Count) XAML files (excluding build artifacts)" "INFO"
    return $xamlFiles
}

function Test-XamlFile {
    param([string]$FilePath)

    $results = @{
        FilePath = $FilePath
        SyncfusionControls = @()
        StandardWpfControls = @()
        UnknownControls = @()
        Issues = @()
        Recommendations = @()
    }

    try {
        if (-not (Test-Path $FilePath)) {
            $results.Issues += "File not found: $FilePath"
            return $results
        }

        $content = Get-Content $FilePath -Raw -ErrorAction Stop

        if ([string]::IsNullOrWhiteSpace($content)) {
            $results.Issues += "File is empty or contains no content"
            return $results
        }

        # Find all control declarations
        $controlPattern = '<(?:(\w+):)?(\w+(?:\.\w+)*)(?:\s[^>]*)?>'
        $controlMatches = [regex]::Matches($content, $controlPattern)

        foreach ($match in $controlMatches) {
            $fullControl = $match.Groups[0].Value.Trim('<', ' ')
            $namespace = $match.Groups[1].Value
            $controlName = $match.Groups[2].Value

            if ($namespace -eq 'syncfusion') {
                if ($controlName -notin $results.SyncfusionControls) {
                    $results.SyncfusionControls += $controlName
                }
            }
            elseif ($controlName -in $standardWpfControls) {
                if ($controlName -notin $results.StandardWpfControls) {
                    $results.StandardWpfControls += $controlName
                }
                $results.Issues += "Found standard WPF control: $controlName"
                $results.Recommendations += "Consider replacing $controlName with Syncfusion equivalent"
            }
            else {
                if ($fullControl -notin $results.UnknownControls) {
                    $results.UnknownControls += $fullControl
                }
            }
        }

        # Check for inline style definitions (potential static styling)
        if ($content -match 'Style\s*=\s*"(?:[^"]|\r?\n)*"') {
            $results.Issues += "Found inline Style attribute - should use StaticResource"
            $results.Recommendations += "Replace inline styles with StaticResource references to Theme resources"
        }

        # Check for hardcoded colors (but allow in theme files)
        $isThemeFile = $FilePath -like "*\Themes\*" -or $FilePath -like "*\Resources\*"
        if (-not $isThemeFile) {
            $colorPattern = '#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})'
            if ($content -match $colorPattern) {
                $results.Issues += "Found hardcoded color values"
                $results.Recommendations += "Replace hardcoded colors with dynamic theme resources"
            }
        }

    }
    catch {
        Write-ScanLog "Error analyzing $FilePath : $($_.Exception.Message)" "ERROR"
        $results.Issues += "Error analyzing $FilePath : $($_.Exception.Message)"
    }

    return $results
}

function New-Report {
    param([array]$ScanResults, [switch]$Detailed)
    
    if ($Detailed) {
        Write-ScanLog "Debug: ScanResults type: $($ScanResults.GetType())" "INFO"
        Write-ScanLog "Debug: ScanResults count: $($ScanResults.Count)" "INFO"
    }
    
    $totalFiles = $ScanResults.Count
    $filesWithIssues = ($ScanResults | Where-Object { $_.Issues -and $_.Issues.Count -gt 0 }).Count
    
    $report = @{
        ScanDate = (Get-Date)
        TotalFiles = $totalFiles
        FilesWithIssues = $filesWithIssues
        TotalSyncfusionControls = ($ScanResults | ForEach-Object {
            if ($_.SyncfusionControls) { $_.SyncfusionControls.Count } else { 0 }
        } | Measure-Object -Sum).Sum
        TotalStandardWpfControls = ($ScanResults | ForEach-Object {
            if ($_.StandardWpfControls) { $_.StandardWpfControls.Count } else { 0 }
        } | Measure-Object -Sum).Sum
        Files = $ScanResults
        Summary = @{
            ComplianceStatus = "FULLY_COMPLIANT"
            Recommendations = @()
        }
    }

    # Check compliance and build dynamic recommendations
    $filesWithStandardControls = 0
    foreach ($result in $ScanResults) {
        if ($result.StandardWpfControls -and $result.StandardWpfControls.Count -gt 0) {
            $filesWithStandardControls++
            $report.Summary.ComplianceStatus = "NEEDS_REVIEW"
        }
    }
    
    # Add the count to the report
    $report.FilesWithStandardControls = $filesWithStandardControls
    
    # Dynamic recommendations based on findings
    if ($filesWithStandardControls -gt 0) {
        $report.Summary.Recommendations += "Replace all standard WPF controls with Syncfusion equivalents"
    }
    $report.Summary.Recommendations += @(
        "Use only dynamic theme resources from SyncfusionResources.xaml",
        "Avoid inline styles and hardcoded colors",
        "Ensure all controls inherit from Syncfusion base styles"
    )

    return $report
}

function Export-Result {
    param([object]$Report, [string]$OutputPath)

    $jsonReport = $Report | ConvertTo-Json -Depth 5
    $jsonReport | Out-File -FilePath $OutputPath -Encoding UTF8 -Force
    Write-ScanLog "Results exported to: $OutputPath (JSON format)" "SUCCESS"
}

# Main execution
Write-ScanLog "🚀 Wiley Widget UI Control Scanner" "INFO"
Write-ScanLog "===================================" "INFO"

$xamlFiles = Get-XamlFile -Path $projectRoot
$scanResultsArray = @()

foreach ($file in $xamlFiles) {
    Write-ScanLog "Analyzing: $($file.FullName)" "INFO"
    $result = Test-XamlFile -FilePath $file.FullName
    $scanResultsArray += $result

    if ($Detailed) {
        if ($result.SyncfusionControls.Count -gt 0) {
            Write-ScanLog "  ✅ Syncfusion Controls: $($result.SyncfusionControls -join ', ')" "SUCCESS"
        }
        if ($result.StandardWpfControls.Count -gt 0) {
            Write-ScanLog "  ⚠️  Standard WPF Controls: $($result.StandardWpfControls -join ', ')" "WARNING"
        }
        if ($result.Issues.Count -gt 0) {
            foreach ($issue in $result.Issues) {
                Write-ScanLog "  ❌ $issue" "ERROR"
            }
        }
    }
}

# Generate and display report
$report = New-Report -ScanResults $scanResultsArray

Write-ScanLog "`n📊 SCAN SUMMARY" "INFO"
Write-ScanLog "===============" "INFO"
Write-ScanLog "Total XAML files scanned: $($report.TotalFiles)" "INFO"
Write-ScanLog "Files with issues: $($report.FilesWithIssues)" "INFO"
Write-ScanLog "Syncfusion controls found: $($report.TotalSyncfusionControls)" "SUCCESS"
Write-ScanLog "Standard WPF controls found: $($report.TotalStandardWpfControls)" "WARNING"
$complianceLogLevel = if ($report.Summary.ComplianceStatus -eq "FULLY_COMPLIANT") { "SUCCESS" } else { "WARNING" }
Write-ScanLog "Compliance Status: $($report.Summary.ComplianceStatus)" $complianceLogLevel
if ($report.Summary.ComplianceStatus -ne "FULLY_COMPLIANT") {
    Write-ScanLog "`n💡 RECOMMENDATIONS" "INFO"
    foreach ($rec in $report.Summary.Recommendations) {
        Write-ScanLog "  • $rec" "INFO"
    }
} else {
    Write-ScanLog "No recommendations needed. All scanned files are fully compliant." "SUCCESS"
}

# Export results if requested
if ($Export) {
    Export-Result -Report $report -OutputPath $scanResultsPath
}

Write-ScanLog "`n✅ UI Control scan completed!" "SUCCESS"
Write-ScanLog "`n✅ UI Control scan completed!" "SUCCESS"
