<#
.SYNOPSIS
    Validates programmatic layout implementation of WinForms panels per Syncfusion documentation.

.DESCRIPTION
    Analyzes panels for proper programmatic layout patterns:
    - Use of layout containers (TableLayoutPanel, FlowLayoutPanel) vs manual positioning
    - Margin/Padding property usage vs hardcoded coordinates
    - Presence of SuspendLayout/ResumeLayout blocks
    - Manual coordinate math (Location = new Point(), Size = new Size())

    Per Syncfusion best practices: "Programmatic layout requires explicit ordering, spacing
    calculations, and thorough DPI testing that the designer handles automatically. Use layout
    containers (TableLayoutPanel, FlowLayoutPanel) and Margin/Padding properties extensively
    to avoid manual coordinate math."

.PARAMETER ControlsPath
    Path to the Controls folder containing panel files.

.PARAMETER PanelFilter
    Optional filter to analyze specific panels only (e.g., 'ChatPanel', 'Dashboard*').

.PARAMETER OutputFormat
    Output format: 'Text' (default), 'Json', or 'Html'.

.EXAMPLE
    .\Validate-PanelLayoutImplementation.ps1
    Analyze all panels and generate text report.

.EXAMPLE
    .\Validate-PanelLayoutImplementation.ps1 -PanelFilter 'SettingsPanel.cs' -OutputFormat Json
    Analyze SettingsPanel only and output JSON.

.NOTES
    Author: Wiley Widget Development Team
    Date: 2026-01-11
    Requires: PowerShell 7+
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateScript({ Test-Path $_ -PathType Container })]
    [string]$ControlsPath = "$PSScriptRoot\..\src\WileyWidget.WinForms\Controls",

    [Parameter()]
    [string]$PanelFilter = '*Panel.cs',

    [Parameter()]
    [ValidateSet('Text', 'Json', 'Html')]
    [string]$OutputFormat = 'Text'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

#region Helper Functions

function Test-LayoutContainer {
    <#
    .SYNOPSIS
        Checks if panel uses layout containers (TableLayoutPanel, FlowLayoutPanel).
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Content
    )

    $tableLayoutCount = ([regex]::Matches($Content, '\bnew\s+TableLayoutPanel\b')).Count
    $flowLayoutCount = ([regex]::Matches($Content, '\bnew\s+FlowLayoutPanel\b')).Count
    $totalLayoutContainers = $tableLayoutCount + $flowLayoutCount

    return @{
        UsesLayoutContainers = $totalLayoutContainers -gt 0
        TableLayoutPanelCount = $tableLayoutCount
        FlowLayoutPanelCount = $flowLayoutCount
        TotalContainers = $totalLayoutContainers
    }
}

function Test-ManualPositioning {
    <#
    .SYNOPSIS
        Detects manual coordinate positioning (Location = new Point(), Size = new Size()).
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Content
    )

    # Match: Location = new Point(...), Size = new Size(...)
    $locationMatches = [regex]::Matches($Content, '\bLocation\s*=\s*new\s+Point\s*\(')
    $sizeMatches = [regex]::Matches($Content, '\bSize\s*=\s*new\s+Size\s*\(')

    # Get line numbers for violations
    $violations = @()

    foreach ($match in $locationMatches) {
        $lineNumber = ($Content.Substring(0, $match.Index) -split "`n").Count
        $violations += @{
            Type = 'Location'
            Line = $lineNumber
            Pattern = 'Location = new Point(...)'
        }
    }

    foreach ($match in $sizeMatches) {
        $lineNumber = ($Content.Substring(0, $match.Index) -split "`n").Count
        $violations += @{
            Type = 'Size'
            Line = $lineNumber
            Pattern = 'Size = new Size(...)'
        }
    }

    return @{
        HasManualPositioning = $violations.Count -gt 0
        LocationCount = $locationMatches.Count
        SizeCount = $sizeMatches.Count
        TotalViolations = $violations.Count
        Violations = $violations
    }
}

function Test-MarginPaddingUsage {
    <#
    .SYNOPSIS
        Checks if panel uses Margin/Padding properties for spacing.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Content
    )

    $marginMatches = ([regex]::Matches($Content, '\bMargin\s*=\s*new\s+Padding\s*\(')).Count
    $paddingMatches = ([regex]::Matches($Content, '\bPadding\s*=\s*new\s+Padding\s*\(')).Count
    $totalSpacingProps = $marginMatches + $paddingMatches

    return @{
        UsesMarginPadding = $totalSpacingProps -gt 0
        MarginCount = $marginMatches
        PaddingCount = $paddingMatches
        TotalSpacingProperties = $totalSpacingProps
    }
}

function Test-SuspendResumeLayout {
    <#
    .SYNOPSIS
        Checks if panel uses SuspendLayout/ResumeLayout blocks.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Content
    )

    $hasSuspendLayout = $Content -match '\bSuspendLayout\s*\(\s*\)'
    $hasResumeLayout = $Content -match '\bResumeLayout\s*\('

    return @{
        HasSuspendResume = $hasSuspendLayout -and $hasResumeLayout
        HasSuspendLayout = $hasSuspendLayout
        HasResumeLayout = $hasResumeLayout
    }
}

function Get-LayoutScore {
    <#
    .SYNOPSIS
        Calculates layout implementation quality score (0-100).
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory)]
        [hashtable]$LayoutContainers,

        [Parameter(Mandatory)]
        [hashtable]$ManualPositioning,

        [Parameter(Mandatory)]
        [hashtable]$MarginPadding,

        [Parameter(Mandatory)]
        [hashtable]$SuspendResume
    )

    $score = 0

    # Layout containers usage (40 points)
    if ($LayoutContainers.UsesLayoutContainers) {
        $score += 40
    }

    # Margin/Padding usage (20 points)
    if ($MarginPadding.UsesMarginPadding) {
        $score += 20
    }

    # SuspendLayout/ResumeLayout (20 points)
    if ($SuspendResume.HasSuspendResume) {
        $score += 20
    }

    # Penalty for manual positioning (up to -40 points)
    if ($ManualPositioning.HasManualPositioning) {
        # Deduct points based on violation count (capped at -40)
        $penalty = [Math]::Min(40, $ManualPositioning.TotalViolations * 2)
        $score -= $penalty
    }
    else {
        # No manual positioning = bonus 20 points
        $score += 20
    }

    return [Math]::Max(0, [Math]::Min(100, $score))
}

function Get-Recommendation {
    <#
    .SYNOPSIS
        Generates recommendations based on validation results.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)]
        [hashtable]$LayoutContainers,

        [Parameter(Mandatory)]
        [hashtable]$ManualPositioning,

        [Parameter(Mandatory)]
        [hashtable]$MarginPadding,

        [Parameter(Mandatory)]
        [hashtable]$SuspendResume,

        [Parameter(Mandatory)]
        [int]$Score
    )

    $recommendations = @()

    if (-not $LayoutContainers.UsesLayoutContainers) {
        $recommendations += "CRITICAL: Replace manual positioning with TableLayoutPanel or FlowLayoutPanel"
    }

    if ($ManualPositioning.HasManualPositioning) {
        $recommendations += "HIGH: Remove manual Location/Size assignments (found $($ManualPositioning.TotalViolations) instances) - use layout containers instead"
    }

    if (-not $MarginPadding.UsesMarginPadding) {
        $recommendations += "MEDIUM: Add Margin/Padding properties for spacing instead of coordinate math"
    }

    if (-not $SuspendResume.HasSuspendResume) {
        $recommendations += "MEDIUM: Wrap multi-control additions in SuspendLayout/ResumeLayout blocks"
    }

    if ($Score -ge 80) {
        $recommendations += "PASS: Panel follows Syncfusion best practices for programmatic layout"
    }
    elseif ($Score -ge 60) {
        $recommendations += "WARNING: Panel needs minor improvements to fully comply with best practices"
    }
    else {
        $recommendations += "FAIL: Panel requires significant refactoring to use layout containers"
    }

    return $recommendations
}

function Analyze-PanelLayout {
    <#
    .SYNOPSIS
        Analyzes a single panel file for layout implementation quality.
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [System.IO.FileInfo]$File
    )

    Write-Verbose "Analyzing: $($File.Name)"

    try {
        $content = Get-Content -Path $File.FullName -Raw -ErrorAction Stop

        # Run all checks
        $layoutContainers = Test-LayoutContainer -Content $content
        $manualPositioning = Test-ManualPositioning -Content $content
        $marginPadding = Test-MarginPaddingUsage -Content $content
        $suspendResume = Test-SuspendResumeLayout -Content $content

        # Calculate score
        $score = Get-LayoutScore -LayoutContainers $layoutContainers `
                                  -ManualPositioning $manualPositioning `
                                  -MarginPadding $marginPadding `
                                  -SuspendResume $suspendResume

        # Generate recommendations
        $recommendations = Get-Recommendation -LayoutContainers $layoutContainers `
                                               -ManualPositioning $manualPositioning `
                                               -MarginPadding $marginPadding `
                                               -SuspendResume $suspendResume `
                                               -Score $score

        return [PSCustomObject]@{
            FileName                = $File.Name
            FilePath                = $File.FullName
            Score                   = $score
            Status                  = if ($score -ge 80) { 'PASS' } elseif ($score -ge 60) { 'WARNING' } else { 'FAIL' }
            UsesLayoutContainers    = $layoutContainers.UsesLayoutContainers
            TableLayoutPanelCount   = $layoutContainers.TableLayoutPanelCount
            FlowLayoutPanelCount    = $layoutContainers.FlowLayoutPanelCount
            HasManualPositioning    = $manualPositioning.HasManualPositioning
            ManualPositionCount     = $manualPositioning.TotalViolations
            ManualPositionDetails   = $manualPositioning.Violations
            UsesMarginPadding       = $marginPadding.UsesMarginPadding
            MarginCount             = $marginPadding.MarginCount
            PaddingCount            = $marginPadding.PaddingCount
            HasSuspendResume        = $suspendResume.HasSuspendResume
            Recommendations         = $recommendations
            Success                 = $true
        }
    }
    catch {
        Write-Warning "Failed to analyze $($File.Name): $_"
        return [PSCustomObject]@{
            FileName    = $File.Name
            FilePath    = $File.FullName
            Score       = 0
            Status      = 'ERROR'
            Success     = $false
            Error       = $_.Exception.Message
        }
    }
}

#endregion

#region Output Formatting

function Format-TextReport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject[]]$Results
    )

    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "PANEL LAYOUT VALIDATION REPORT" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan

    foreach ($result in $Results) {
        if (-not $result.Success) {
            Write-Host "[$($result.FileName)] ERROR: $($result.Error)" -ForegroundColor Red
            continue
        }

        # Header with status color
        $statusColor = switch ($result.Status) {
            'PASS' { 'Green' }
            'WARNING' { 'Yellow' }
            'FAIL' { 'Red' }
            default { 'Gray' }
        }

        Write-Host "[$($result.FileName)]" -NoNewline
        Write-Host " Score: $($result.Score)/100" -NoNewline
        Write-Host " [$($result.Status)]" -ForegroundColor $statusColor

        # Metrics
        Write-Host "  Layout Containers: " -NoNewline
        if ($result.UsesLayoutContainers) {
            Write-Host "✓ YES" -ForegroundColor Green -NoNewline
            Write-Host " (TableLayout: $($result.TableLayoutPanelCount), FlowLayout: $($result.FlowLayoutPanelCount))"
        }
        else {
            Write-Host "✗ NO" -ForegroundColor Red
        }

        Write-Host "  Manual Positioning: " -NoNewline
        if ($result.HasManualPositioning) {
            Write-Host "✗ YES" -ForegroundColor Red -NoNewline
            Write-Host " ($($result.ManualPositionCount) violations)"
        }
        else {
            Write-Host "✓ NO" -ForegroundColor Green
        }

        Write-Host "  Margin/Padding: " -NoNewline
        if ($result.UsesMarginPadding) {
            Write-Host "✓ YES" -ForegroundColor Green -NoNewline
            Write-Host " (Margin: $($result.MarginCount), Padding: $($result.PaddingCount))"
        }
        else {
            Write-Host "✗ NO" -ForegroundColor Red
        }

        Write-Host "  SuspendLayout/ResumeLayout: " -NoNewline
        if ($result.HasSuspendResume) {
            Write-Host "✓ YES" -ForegroundColor Green
        }
        else {
            Write-Host "✗ NO" -ForegroundColor Red
        }

        # Show violations if any
        if ($result.ManualPositionCount -gt 0 -and $result.ManualPositionDetails) {
            Write-Host "  Violations:" -ForegroundColor Yellow
            $result.ManualPositionDetails | Select-Object -First 5 | ForEach-Object {
                Write-Host "    Line $($_.Line): $($_.Pattern)" -ForegroundColor Gray
            }
            if ($result.ManualPositionCount -gt 5) {
                Write-Host "    ... and $($result.ManualPositionCount - 5) more" -ForegroundColor Gray
            }
        }

        # Recommendations
        Write-Host "  Recommendations:" -ForegroundColor Cyan
        foreach ($rec in $result.Recommendations) {
            $recColor = if ($rec -like 'CRITICAL:*') { 'Red' }
                       elseif ($rec -like 'HIGH:*') { 'Yellow' }
                       elseif ($rec -like 'PASS:*') { 'Green' }
                       else { 'Gray' }
            Write-Host "    • $rec" -ForegroundColor $recColor
        }

        Write-Host ""
    }

    # Summary
    $passCount = @($Results | Where-Object { $_.Status -eq 'PASS' }).Count
    $warningCount = @($Results | Where-Object { $_.Status -eq 'WARNING' }).Count
    $failCount = @($Results | Where-Object { $_.Status -eq 'FAIL' }).Count
    $errorCount = @($Results | Where-Object { $_.Status -eq 'ERROR' }).Count
    $avgScore = [Math]::Round(($Results | Where-Object { $_.Success } | Measure-Object -Property Score -Average).Average, 1)

    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "SUMMARY" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Total Panels: $($Results.Count)"
    Write-Host "PASS: $passCount" -ForegroundColor Green
    Write-Host "WARNING: $warningCount" -ForegroundColor Yellow
    Write-Host "FAIL: $failCount" -ForegroundColor Red
    Write-Host "ERROR: $errorCount" -ForegroundColor Red
    Write-Host "Average Score: $avgScore/100`n"
}

function Format-JsonReport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject[]]$Results
    )

    $report = @{
        GeneratedAt = (Get-Date -Format 'o')
        TotalPanels = $Results.Count
        Summary = @{
            Pass = @($Results | Where-Object { $_.Status -eq 'PASS' }).Count
            Warning = @($Results | Where-Object { $_.Status -eq 'WARNING' }).Count
            Fail = @($Results | Where-Object { $_.Status -eq 'FAIL' }).Count
            Error = @($Results | Where-Object { $_.Status -eq 'ERROR' }).Count
            AverageScore = [Math]::Round(($Results | Where-Object { $_.Success } | Measure-Object -Property Score -Average).Average, 1)
        }
        Panels = $Results
    }

    $report | ConvertTo-Json -Depth 10
}

function Format-HtmlReport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject[]]$Results
    )

    $passCount = @($Results | Where-Object { $_.Status -eq 'PASS' }).Count
    $warningCount = @($Results | Where-Object { $_.Status -eq 'WARNING' }).Count
    $failCount = @($Results | Where-Object { $_.Status -eq 'FAIL' }).Count
    $avgScore = [Math]::Round(($Results | Where-Object { $_.Success } | Measure-Object -Property Score -Average).Average, 1)

    $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Panel Layout Validation Report</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; background: #f5f5f5; }
        h1 { color: #333; border-bottom: 3px solid #0078d4; padding-bottom: 10px; }
        .summary { background: white; padding: 20px; margin: 20px 0; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .summary-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 15px; margin-top: 15px; }
        .metric { text-align: center; padding: 15px; border-radius: 6px; background: #f9f9f9; }
        .metric-value { font-size: 32px; font-weight: bold; display: block; margin-bottom: 5px; }
        .metric-label { font-size: 14px; color: #666; }
        .pass { color: #28a745; }
        .warning { color: #ffc107; }
        .fail { color: #dc3545; }
        table { width: 100%; border-collapse: collapse; background: white; margin: 20px 0; box-shadow: 0 2px 4px rgba(0,0,0,0.1); border-radius: 8px; overflow: hidden; }
        th { background: #0078d4; color: white; padding: 12px; text-align: left; font-weight: 600; }
        td { padding: 12px; border-bottom: 1px solid #eee; }
        tr:hover { background: #f9f9f9; }
        .status-badge { padding: 4px 12px; border-radius: 12px; font-size: 12px; font-weight: bold; display: inline-block; }
        .status-pass { background: #d4edda; color: #155724; }
        .status-warning { background: #fff3cd; color: #856404; }
        .status-fail { background: #f8d7da; color: #721c24; }
        .score { font-size: 18px; font-weight: bold; }
    </style>
</head>
<body>
    <h1>Panel Layout Validation Report</h1>
    <p>Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</p>

    <div class="summary">
        <h2>Summary</h2>
        <div class="summary-grid">
            <div class="metric">
                <span class="metric-value pass">$passCount</span>
                <span class="metric-label">PASS</span>
            </div>
            <div class="metric">
                <span class="metric-value warning">$warningCount</span>
                <span class="metric-label">WARNING</span>
            </div>
            <div class="metric">
                <span class="metric-value fail">$failCount</span>
                <span class="metric-label">FAIL</span>
            </div>
            <div class="metric">
                <span class="metric-value">$avgScore</span>
                <span class="metric-label">Avg Score</span>
            </div>
        </div>
    </div>

    <table>
        <thead>
            <tr>
                <th>Panel</th>
                <th>Score</th>
                <th>Status</th>
                <th>Layout Containers</th>
                <th>Manual Positioning</th>
                <th>Margin/Padding</th>
                <th>Suspend/Resume</th>
            </tr>
        </thead>
        <tbody>
"@

    foreach ($result in $Results) {
        $statusClass = switch ($result.Status) {
            'PASS' { 'status-pass' }
            'WARNING' { 'status-warning' }
            'FAIL' { 'status-fail' }
            default { '' }
        }

        $html += @"
            <tr>
                <td>$($result.FileName)</td>
                <td><span class="score">$($result.Score)/100</span></td>
                <td><span class="status-badge $statusClass">$($result.Status)</span></td>
                <td>$(if ($result.UsesLayoutContainers) { '✓ YES' } else { '✗ NO' })</td>
                <td>$(if ($result.HasManualPositioning) { "✗ $($result.ManualPositionCount)" } else { '✓ NO' })</td>
                <td>$(if ($result.UsesMarginPadding) { '✓ YES' } else { '✗ NO' })</td>
                <td>$(if ($result.HasSuspendResume) { '✓ YES' } else { '✗ NO' })</td>
            </tr>
"@
    }

    $html += @"
        </tbody>
    </table>
</body>
</html>
"@

    return $html
}

#endregion

#region Main Script

try {
    Write-Verbose "Controls path: $ControlsPath"
    Write-Verbose "Panel filter: $PanelFilter"

    # Find panel files
    $panelFiles = @(Get-ChildItem -Path $ControlsPath -Filter $PanelFilter -File -ErrorAction Stop |
        Where-Object { $_.Name -notmatch '\.(Designer|fixed|New|bak)\.cs$' })

    if ($panelFiles.Count -eq 0) {
        Write-Warning "No panel files found matching filter: $PanelFilter"
        exit 0
    }

    Write-Verbose "Found $($panelFiles.Count) panel file(s)"

    # Analyze each panel
    [System.Collections.ArrayList]$results = @()
    foreach ($file in $panelFiles) {
        $result = Analyze-PanelLayout -File $file
        [void]$results.Add($result)
    }

    # Output report
    switch ($OutputFormat) {
        'Json' {
            Format-JsonReport -Results $results
        }
        'Html' {
            $html = Format-HtmlReport -Results $results
            $reportPath = Join-Path $PSScriptRoot "panel-layout-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').html"
            $html | Out-File -FilePath $reportPath -Encoding utf8
            Write-Host "HTML report saved: $reportPath" -ForegroundColor Green
        }
        default {
            Format-TextReport -Results $results
        }
    }

    # Exit code based on results
    $failCount = @($results | Where-Object { $_.Status -eq 'FAIL' }).Count
    if ($failCount -gt 0) {
        exit 1
    }
    else {
        exit 0
    }
}
catch {
    Write-Error "Fatal error: $_"
    Write-Error $_.ScriptStackTrace
    exit 2
}

#endregion
