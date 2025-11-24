<#
.SYNOPSIS
    PowerShell-based legacy scanner for Syncfusion and Prism remnants.

.DESCRIPTION
    Scans C#/XAML/.csproj files for legacy Syncfusion and Prism references.
    Provides console report and optional JSON export.
    Native Windows solution with no external dependencies.

.PARAMETER Root
    Root directory to scan. Default: current directory.

.PARAMETER Verbose
    Show all matches with full details.

.PARAMETER FailOnHits
    Exit with code 1 if any legacy code found (for CI).

.PARAMETER OutputJson
    Path to save JSON report. If not specified, console output only.

.PARAMETER IncludeComments
    Include commented-out code in scan (default: skip).

.EXAMPLE
    .\legacy_scanner.ps1
    Basic scan of current directory.

.EXAMPLE
    .\legacy_scanner.ps1 -Root "C:\Wiley-Widget\src" -Verbose
    Scan specific directory with full details.

.EXAMPLE
    .\legacy_scanner.ps1 -FailOnHits -OutputJson "scan_report.json"
    CI mode: fail if hits found, save report.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Root = ".",
    
    [Parameter(Mandatory = $false)]
    [switch]$Verbose,
    
    [Parameter(Mandatory = $false)]
    [switch]$FailOnHits,
    
    [Parameter(Mandatory = $false)]
    [string]$OutputJson = "",
    
    [Parameter(Mandatory = $false)]
    [switch]$IncludeComments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Legacy patterns to detect
$script:Patterns = @{
    Prism = @{
        Namespace_Using = @(
            "using\s+Prism(?:\.\w+)*\s*;",
            "global\s+using\s+Prism(?:\.\w+)*\s*;"
        )
        Classes_Methods = @(
            "\bBindableBase\b",
            "\bDelegateCommand\b",
            "\bIEventAggregator\b",
            "\bIRegionManager\b",
            "\bContainerLocator\b",
            "\bPrismApplication\b",
            "\bViewModelBase\b",
            "\bINavigationAware\b",
            "\bIConfirmNavigation\b"
        )
        XAML_Regions = @(
            "prism:RegionManager\.RegionName",
            "prism:ClearChildContent",
            "prism:ViewModelLocator"
        )
        Package_Reference = @(
            '<PackageReference\s+Include="Prism\.'
        )
        Suggestion = "Replace with CommunityToolkit.Mvvm (ObservableObject, RelayCommand) or manual DI."
    }
    Syncfusion = @{
        Namespace_Using = @(
            "using\s+Syncfusion(?:\.\w+)*\s*;",
            "global\s+using\s+Syncfusion(?:\.\w+)*\s*;"
        )
        Controls_Methods = @(
            "\bSfDataGrid\b",
            "\bSfChart\b",
            "\bSfTreeView\b",
            "\bSfBusyIndicator\b",
            "\bSfEditors\b",
            "\bSfProgressBar\b",
            "\bSfGauge\b",
            "\bSfDatePicker\b",
            "\bSfTextBox\b",
            "\bSfComboBox\b",
            "\bSfNumericTextBox\b"
        )
        XAML_Tags = @(
            "<(?:syncfusion:)?Sf(?:DataGrid|Chart|TreeView|BusyIndicator|Editors|ProgressBar|Gauge|DatePicker|TextBox|ComboBox|NumericTextBox)"
        )
        XAML_Namespace = @(
            'xmlns:syncfusion='
        )
        Licensing = @(
            "SyncfusionLicenseProvider\.RegisterLicense"
        )
        Package_Reference = @(
            '<PackageReference\s+Include="Syncfusion\.'
        )
        Suggestion = "Replace with native WinUI controls (DataGrid, ProgressRing) from Microsoft.UI.Xaml.Controls."
    }
}

# Directories to ignore
$script:IgnoreDirs = @(
    "bin", "obj", "packages", "node_modules", ".git", "__pycache__",
    ".vs", ".vscode", "TestResults", "coverage", "logs", "secrets",
    "temp", "temp_test", "ci-logs", "xaml-logs"
)

# File extensions to scan
$script:FileExtensions = @("*.cs", "*.xaml", "*.csproj", "*.config", "*.xml", "*.targets", "*.props")

function Test-ShouldIgnorePath {
    param([System.IO.FileInfo]$File)
    
    foreach ($ignore in $script:IgnoreDirs) {
        if ($File.FullName -like "*\$ignore\*") {
            return $true
        }
    }
    return $false
}

function Get-FilesToScan {
    param([string]$RootPath)
    
    $files = @()
    foreach ($ext in $script:FileExtensions) {
        $files += Get-ChildItem -Path $RootPath -Filter $ext -Recurse -File -ErrorAction SilentlyContinue
    }
    
    # Filter out ignored paths
    $filtered = $files | Where-Object { -not (Test-ShouldIgnorePath $_) }
    
    return $filtered
}

function Invoke-FileScan {
    param(
        [System.IO.FileInfo]$File,
        [bool]$IncludeComments
    )
    
    $hits = @()
    
    # Skip large files (>5MB likely binary)
    if ($File.Length -gt (5 * 1024 * 1024)) {
        return $hits
    }
    
    try {
        $lines = Get-Content -Path $File.FullName -Encoding UTF8 -ErrorAction Stop
    }
    catch {
        return $hits  # Skip unreadable files
    }
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i].Trim()
        
        # Skip empty lines
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        
        # Skip full-line comments unless IncludeComments
        if (-not $IncludeComments -and ($line -match "^\s*//")) {
            continue
        }
        
        # Check each pattern category
        foreach ($category in $script:Patterns.Keys) {
            $categoryPatterns = $script:Patterns[$category]
            $suggestion = $categoryPatterns.Suggestion
            
            foreach ($patternType in $categoryPatterns.Keys) {
                if ($patternType -eq "Suggestion") {
                    continue
                }
                
                $patterns = $categoryPatterns[$patternType]
                foreach ($pattern in $patterns) {
                    if ($line -match $pattern) {
                        $hits += [PSCustomObject]@{
                            FilePath = $File.FullName
                            LineNum = $i + 1
                            LineContent = $line.Substring(0, [Math]::Min($line.Length, 200))
                            PatternType = "$category`_$patternType"
                            Suggestion = $suggestion
                        }
                        break  # Only record one hit per line
                    }
                }
            }
        }
    }
    
    return $hits
}

function New-ScanReport {
    param(
        [array]$AllHits,
        [string]$RootPath,
        [bool]$IsVerbose
    )
    
    $prismHits = $AllHits | Where-Object { $_.PatternType -like "Prism*" }
    $syncfusionHits = $AllHits | Where-Object { $_.PatternType -like "Syncfusion*" }
    $affectedFiles = ($AllHits | Select-Object -ExpandProperty FilePath -Unique).Count
    
    $report = @{
        Summary = @{
            TotalHits = $AllHits.Count
            PrismHits = $prismHits.Count
            SyncfusionHits = $syncfusionHits.Count
            AffectedFiles = $affectedFiles
            ScanDate = (Get-Date).ToString("o")
            RootDir = (Resolve-Path $RootPath).Path
        }
    }
    
    if ($IsVerbose) {
        $report.Hits = $AllHits
    }
    else {
        # Group by file
        $fileGroups = $AllHits | Group-Object -Property FilePath
        $report.FileSummary = @{}
        foreach ($group in $fileGroups) {
            $report.FileSummary[$group.Name] = $group.Group
        }
    }
    
    return $report
}

function Write-ScanReport {
    param(
        [hashtable]$Report,
        [string]$OutputJsonPath
    )
    
    $summary = $Report.Summary
    
    Write-Host ""
    Write-Host ("="*60) -ForegroundColor Cyan
    Write-Host "   LEGACY SCAN REPORT - Wiley Widget" -ForegroundColor Yellow
    Write-Host ("="*60) -ForegroundColor Cyan
    Write-Host "Total Hits: $($summary.TotalHits) (Prism: $($summary.PrismHits), Syncfusion: $($summary.SyncfusionHits))"
    Write-Host "Affected Files: $($summary.AffectedFiles)"
    Write-Host "Scanned: $($summary.RootDir)"
    Write-Host "Date: $($summary.ScanDate)"
    Write-Host ("="*60) -ForegroundColor Cyan
    
    if ($summary.TotalHits -eq 0) {
        Write-Host ""
        Write-Host "✅ SUCCESS: No legacy remnants found! Codebase is clean." -ForegroundColor Green
        return
    }
    
    Write-Host ""
    Write-Host "⚠️  FILES WITH LEGACY CODE DETECTED:" -ForegroundColor Yellow
    Write-Host ""
    
    if ($Report.FileSummary) {
        $sortedFiles = $Report.FileSummary.GetEnumerator() | Sort-Object { $_.Value.Count } -Descending
        
        foreach ($entry in $sortedFiles) {
            $filePath = $entry.Key
            $hits = $entry.Value
            
            # Get relative path for readability
            $relativePath = $filePath -replace [regex]::Escape($summary.RootDir), ""
            $relativePath = $relativePath.TrimStart("\")
            
            Write-Host "📄 $relativePath ($($hits.Count) hits):" -ForegroundColor Cyan
            
            # Show top 3 hits per file
            $displayHits = $hits | Select-Object -First 3
            $suggestionShown = $false
            
            foreach ($hit in $displayHits) {
                Write-Host ("  Line {0,4}: {1}" -f $hit.LineNum, $hit.LineContent.Substring(0, [Math]::Min($hit.LineContent.Length, 100)))
                Write-Host ("           Type: {0}" -f $hit.PatternType) -ForegroundColor DarkGray
                
                if (-not $suggestionShown -and $hit.Suggestion) {
                    Write-Host ("           💡 {0}" -f $hit.Suggestion) -ForegroundColor Magenta
                    $suggestionShown = $true
                }
            }
            
            if ($hits.Count -gt 3) {
                Write-Host ("  ... and {0} more hits. Run with -Verbose for full list." -f ($hits.Count - 3)) -ForegroundColor DarkGray
            }
            Write-Host ""
        }
    }
    
    # Save JSON report if requested
    if ($OutputJsonPath) {
        try {
            $Report | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputJsonPath -Encoding UTF8
            Write-Host "📊 Full report saved to: $(Resolve-Path $OutputJsonPath)" -ForegroundColor Green
        }
        catch {
            Write-Warning "Failed to save JSON report: $_"
        }
    }
    
    Write-Host ""
    Write-Host ("="*60) -ForegroundColor Cyan
    Write-Host "Next Steps:"
    Write-Host "  1. Review affected files listed above"
    Write-Host "  2. Refactor using suggested replacements"
    Write-Host "  3. Re-run scanner to verify cleanup"
    Write-Host "  4. Consider adding to CI with -FailOnHits"
    Write-Host ("="*60) -ForegroundColor Cyan
    Write-Host ""
}

# MAIN EXECUTION
try {
    $rootPath = Resolve-Path $Root -ErrorAction Stop
    
    Write-Host "Scanning from root: $rootPath" -ForegroundColor Cyan
    Write-Host "Looking for extensions: $($script:FileExtensions -join ', ')" -ForegroundColor Cyan
    
    # Get files to scan
    $filesToScan = Get-FilesToScan -RootPath $rootPath
    Write-Host "Scanning $($filesToScan.Count) files..." -ForegroundColor Cyan
    Write-Host ""
    
    # Scan files
    $allHits = @()
    $progress = 0
    foreach ($file in $filesToScan) {
        $progress++
        Write-Progress -Activity "Scanning files" -Status "$progress of $($filesToScan.Count)" -PercentComplete (($progress / $filesToScan.Count) * 100)
        
        $fileHits = Invoke-FileScan -File $file -IncludeComments $IncludeComments
        $allHits += $fileHits
    }
    Write-Progress -Activity "Scanning files" -Completed
    
    # Generate report
    $report = New-ScanReport -AllHits $allHits -RootPath $rootPath -IsVerbose $Verbose
    
    # Print report
    Write-ScanReport -Report $report -OutputJsonPath $OutputJson
    
    # Exit with appropriate code
    if ($FailOnHits -and $report.Summary.TotalHits -gt 0) {
        exit 1
    }
    
    exit 0
}
catch {
    Write-Error "Fatal error during scan: $_"
    exit 2
}
