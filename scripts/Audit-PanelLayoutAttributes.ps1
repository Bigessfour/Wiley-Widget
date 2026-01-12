<#
.SYNOPSIS
    Audits WinForms panels for proper programmatic layout attributes per Syncfusion documentation.

.DESCRIPTION
    Analyzes all *Panel.cs files in the Controls folder to ensure they follow
    Syncfusion's best practices for programmatic UI creation without designer files:
    - SuspendLayout/ResumeLayout pattern
    - Proper Margin/Padding settings
    - AutoScaleMode.Dpi
    - Anchor/Dock properties
    - MinimumSize/Size constraints

.PARAMETER ControlsPath
    Path to the Controls folder containing panel files.

.PARAMETER DryRun
    If specified, performs analysis without making any changes.

.PARAMETER OutputFormat
    Output format: 'Text' (default), 'Json', or 'Html'.

.EXAMPLE
    .\Audit-PanelLayoutAttributes.ps1 -DryRun

.EXAMPLE
    .\Audit-PanelLayoutAttributes.ps1 -OutputFormat Json | Out-File audit-results.json

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
        [switch]$DryRun,

    [Parameter()]
    [ValidateSet('Text', 'Json', 'Html')]
    [string]$OutputFormat = 'Text'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

#region Helper Functions

function Test-LayoutAttribute {
    <#
    .SYNOPSIS
        Tests if a panel file contains a specific layout attribute or pattern.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Content,

        [Parameter(Mandatory)]
        [string]$Pattern,

        [Parameter()]
        [System.Text.RegularExpressions.RegexOptions]$Options = 'IgnoreCase'
    )

    try {
        return [regex]::IsMatch($Content, $Pattern, $Options)
    }
    catch {
        Write-Warning "Pattern matching failed for: $Pattern"
        return $false
    }
}

function Get-PanelLayoutAudit {
    <#
    .SYNOPSIS
        Performs comprehensive layout audit on a single panel file.
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [System.IO.FileInfo]$File
    )

    $content = Get-Content -Path $File.FullName -Raw -ErrorAction Stop

    # Define audit checks per Syncfusion documentation
    $checks = @{
        # Critical patterns
        'SuspendLayout'         = @{
            Pattern     = 'SuspendLayout\s*\(\s*\)'
            Required    = $true
            Description = 'SuspendLayout() call for batched layout operations'
        }
        'ResumeLayout'          = @{
            Pattern     = 'ResumeLayout\s*\('
            Required    = $true
            Description = 'ResumeLayout() call to complete layout batch'
        }
        'AutoScaleMode'         = @{
            Pattern     = 'AutoScaleMode\s*=\s*AutoScaleMode\.Dpi'
            Required    = $true
            Description = 'AutoScaleMode.Dpi for high-DPI support'
        }

        # Important patterns
        'Padding'               = @{
            Pattern     = 'Padding\s*=\s*new\s+Padding\s*\('
            Required    = $false
            Description = 'Padding property for container spacing'
        }
        'Margin'                = @{
            Pattern     = 'Margin\s*=\s*new\s+Padding\s*\('
            Required    = $false
            Description = 'Margin property for control spacing'
        }
        'MinimumSize'           = @{
            Pattern     = 'MinimumSize\s*='
            Required    = $false
            Description = 'MinimumSize to prevent layout collapse'
        }
        'Size'                  = @{
            Pattern     = 'Size\s*=\s*new\s+Size\s*\('
            Required    = $true
            Description = 'Explicit Size property'
        }
        'Dock'                  = @{
            Pattern     = 'Dock\s*=\s*DockStyle\.'
            Required    = $false
            Description = 'Dock property for edge alignment'
        }
        'Anchor'                = @{
            Pattern     = 'Anchor\s*=\s*AnchorStyles\.'
            Required    = $false
            Description = 'Anchor property for responsive resizing'
        }

        # Layout containers (recommended)
        'TableLayoutPanel'      = @{
            Pattern     = 'new\s+TableLayoutPanel\s*\('
            Required    = $false
            Description = 'TableLayoutPanel for grid-based layout'
        }
        'FlowLayoutPanel'       = @{
            Pattern     = 'new\s+FlowLayoutPanel\s*\('
            Required    = $false
            Description = 'FlowLayoutPanel for sequential layout'
        }

        # Syncfusion-specific
        'LogicalToDeviceUnits'  = @{
            Pattern     = 'LogicalToDeviceUnits\s*\('
            Required    = $false
            Description = 'DPI-aware unit conversion'
        }
        'GradientPanelExt'      = @{
            Pattern     = 'new\s+.*GradientPanelExt\s*\('
            Required    = $false
            Description = 'Syncfusion GradientPanelExt container'
        }
        'SfSkinManager'         = @{
            Pattern     = 'SfSkinManager\.SetVisualStyle\s*\('
            Required    = $true
            Description = 'Theme application via SfSkinManager'
        }
    }

    $results = @{}
    $passed = 0
    $failed = 0
    $warnings = 0

    foreach ($checkName in $checks.Keys) {
        $check = $checks[$checkName]
        $found = Test-LayoutAttribute -Content $content -Pattern $check.Pattern

        $status = if ($found) {
            $passed++
            'Pass'
        }
        elseif ($check.Required) {
            $failed++
            'Fail'
        }
        else {
            $warnings++
            'Warning'
        }

        $results[$checkName] = @{
            Status      = $status
            Found       = $found
            Required    = $check.Required
            Description = $check.Description
        }
    }

    # Calculate compliance score
    $totalRequired = ($checks.Values | Where-Object { $_.Required }).Count
    $passedRequired = ($results.Values | Where-Object { $_.Status -eq 'Pass' -and $_.Required }).Count
    $complianceScore = if ($totalRequired -gt 0) {
        [math]::Round(($passedRequired / $totalRequired) * 100, 1)
    }
    else {
        100.0
    }

    return [PSCustomObject]@{
        FileName         = $File.Name
        FilePath         = $File.FullName
        Passed           = $passed
        Failed           = $failed
        Warnings         = $warnings
        ComplianceScore  = $complianceScore
        Checks           = $results
        LinesOfCode      = ($content -split "`n").Count
        HasInitComponent = Test-LayoutAttribute -Content $content -Pattern 'private\s+void\s+InitializeComponent\s*\('
    }
}

function Format-AuditResults {
    <#
    .SYNOPSIS
        Formats audit results based on output format.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [PSCustomObject[]]$Results,

        [Parameter()]
        [ValidateSet('Text', 'Json', 'Html')]
        [string]$Format = 'Text'
    )

    begin {
        $allResults = @()
    }

    process {
        $allResults += $Results
    }

    end {
        switch ($Format) {
            'Json' {
                return $allResults | ConvertTo-Json -Depth 10
            }

            'Html' {
                $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Panel Layout Audit Results</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, sans-serif; margin: 20px; }
        h1 { color: #0078d4; }
        table { border-collapse: collapse; width: 100%; margin: 20px 0; }
        th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }
        th { background-color: #0078d4; color: white; }
        .pass { color: green; font-weight: bold; }
        .fail { color: red; font-weight: bold; }
        .warning { color: orange; font-weight: bold; }
        .score-high { background-color: #d4edda; }
        .score-med { background-color: #fff3cd; }
        .score-low { background-color: #f8d7da; }
    </style>
</head>
<body>
    <h1>Panel Layout Audit Results</h1>
    <p>Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</p>
    <table>
        <tr>
            <th>Panel</th>
            <th>Compliance Score</th>
            <th>Passed</th>
            <th>Failed</th>
            <th>Warnings</th>
        </tr>
"@
                foreach ($result in $allResults) {
                    $scoreClass = if ($result.ComplianceScore -ge 80) { 'score-high' }
                    elseif ($result.ComplianceScore -ge 60) { 'score-med' }
                    else { 'score-low' }

                    $html += @"
        <tr class="$scoreClass">
            <td>$($result.FileName)</td>
            <td>$($result.ComplianceScore)%</td>
            <td class="pass">$($result.Passed)</td>
            <td class="fail">$($result.Failed)</td>
            <td class="warning">$($result.Warnings)</td>
        </tr>
"@
                }

                $html += @"
    </table>
</body>
</html>
"@
                return $html
            }

            default {
                # Text format
                $output = @"

===========================================
PANEL LAYOUT AUDIT RESULTS
===========================================
Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Mode: $(if ($DryRun) { 'DRY RUN' } else { 'LIVE' })

"@
                foreach ($result in $allResults) {
                    $statusIcon = if ($result.Failed -eq 0) { '✓' } else { '✗' }

                    $output += @"

$statusIcon $($result.FileName)
   Compliance Score: $($result.ComplianceScore)%
   Passed: $($result.Passed) | Failed: $($result.Failed) | Warnings: $($result.Warnings)
   Lines of Code: $($result.LinesOfCode)
   Has InitializeComponent: $($result.HasInitComponent)

"@

                    # Show failed checks
                    $failedChecks = $result.Checks.GetEnumerator() | Where-Object { $_.Value.Status -eq 'Fail' }
                    if ($failedChecks) {
                        $output += "   FAILED CHECKS:`n"
                        foreach ($check in $failedChecks) {
                            $output += "      ✗ $($check.Key): $($check.Value.Description)`n"
                        }
                    }

                    # Show warnings
                    $warningChecks = $result.Checks.GetEnumerator() | Where-Object { $_.Value.Status -eq 'Warning' }
                    if ($warningChecks) {
                        $output += "   WARNINGS:`n"
                        foreach ($check in $warningChecks) {
                            $output += "      ⚠ $($check.Key): $($check.Value.Description)`n"
                        }
                    }
                }

                # Summary
                $totalPanels = $allResults.Count
                $avgCompliance = ($allResults | Measure-Object -Property ComplianceScore -Average).Average
                $fullCompliance = ($allResults | Where-Object { $_.Failed -eq 0 }).Count

                $output += @"

===========================================
SUMMARY
===========================================
Total Panels: $totalPanels
Average Compliance: $([math]::Round($avgCompliance, 1))%
Fully Compliant: $fullCompliance/$totalPanels

"@
                return $output
            }
        }
    }
}

#endregion

#region Main Script

try {
    Write-Verbose "Starting panel layout audit..."
    Write-Verbose "Controls path: $ControlsPath"
    Write-Verbose "Dry run: $DryRun"
    Write-Verbose "Output format: $OutputFormat"

    # Find all panel files
    $panelFiles = Get-ChildItem -Path $ControlsPath -Filter '*Panel.cs' -File -ErrorAction Stop |
        Where-Object { $_.Name -notmatch '\.(Designer|fixed|New)\.cs$' }

    if ($panelFiles.Count -eq 0) {
        Write-Warning "No panel files found in $ControlsPath"
        exit 0
    }

    Write-Verbose "Found $($panelFiles.Count) panel files"

    # Audit each panel
    $results = @()
    foreach ($file in $panelFiles) {
        Write-Verbose "Auditing: $($file.Name)"
        try {
            $audit = Get-PanelLayoutAudit -File $file
            $results += $audit
        }
        catch {
            Write-Warning "Failed to audit $($file.Name): $_"
        }
    }

    # Format and output results
    $output = $results | Format-AuditResults -Format $OutputFormat
    Write-Output $output

    # Exit code based on compliance
    $criticalFailures = ($results | Where-Object { $_.Failed -gt 0 }).Count
    if ($criticalFailures -gt 0) {
        Write-Warning "$criticalFailures panel(s) have critical compliance failures"
        exit 1
    }
    else {
        Write-Verbose "All panels passed critical checks"
        exit 0
    }
}
catch {
    Write-Error "Audit failed: $_"
    Write-Error $_.ScriptStackTrace
    exit 2
}

#endregion
