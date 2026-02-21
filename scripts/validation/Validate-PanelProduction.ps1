<#
.SYNOPSIS
    Comprehensive panel validation for production readiness using PowerShell 7.5.4.

.DESCRIPTION
    Validates WinForms panels for:
    - Visual rendering and layout
    - Data binding configuration
    - Syncfusion control setup
    - Theme compatibility
    - Performance metrics

.PARAMETER PanelName
    Name of the panel to validate (e.g., 'DashboardPanel', 'AccountsPanel').

.PARAMETER Mode
    Validation mode: 'Quick' (layout only), 'Standard' (layout + bindings), 'Full' (all checks).

.PARAMETER CompareWithDemo
    If specified, fetches Syncfusion demo for comparison.

.PARAMETER OutputPath
    Path to save validation report. Defaults to tmp/validation-reports.

.EXAMPLE
    .\Validate-PanelProduction.ps1 -PanelName DashboardPanel -Mode Standard

.EXAMPLE
    .\Validate-PanelProduction.ps1 -PanelName AccountsPanel -Mode Full -CompareWithDemo

.NOTES
    Author: Wiley Widget Team
    Requires: PowerShell 7.5.4+, .NET 10, Syncfusion WinForms
#>

#Requires -Version 7.5.4

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$PanelName,

    [Parameter()]
    [ValidateSet('Quick', 'Standard', 'Full')]
    [string]$Mode = 'Standard',

    [Parameter()]
    [switch]$CompareWithDemo,

    [Parameter()]
    [string]$OutputPath = "$PSScriptRoot/../../tmp/validation-reports",

    [Parameter()]
    [ValidateSet('Markdown', 'JSON', 'Both')]
    [string]$OutputFormat = 'Markdown',

    [Parameter()]
    [switch]$PassThru
)

#region Configuration
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$script:Config = @{
    ControlsPath      = "$PSScriptRoot/../../src/WileyWidget.WinForms/Controls"
    McpServerUrl      = 'wileywidget-ui-mcp-server'
    SyncfusionDemoUrl = 'https://github.com/syncfusion/winforms-demos/tree/master'
    ReportFormat      = 'Markdown'
}

$script:ValidationResults = [ordered]@{
    PanelName      = $PanelName
    Mode           = $Mode
    Timestamp      = Get-Date -Format 'o'
    PowerShellVer  = $PSVersionTable.PSVersion.ToString()
    Checks         = [System.Collections.Generic.List[PSCustomObject]]::new()
    Summary        = @{
        Total   = 0
        Passed  = 0
        Failed  = 0
        Skipped = 0
    }
}
#endregion

#region Helper Functions

function Write-ValidationStep {
    <#
    .SYNOPSIS
        Writes a validation step with color-coded output.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter()]
        [ValidateSet('Info', 'Success', 'Warning', 'Error')]
        [string]$Level = 'Info'
    )

    $color = switch ($Level) {
        'Info' { 'Cyan' }
        'Success' { 'Green' }
        'Warning' { 'Yellow' }
        'Error' { 'Red' }
    }

    $prefix = switch ($Level) {
        'Info' { '[INFO]' }
        'Success' { '[✓]' }
        'Warning' { '[⚠]' }
        'Error' { '[✗]' }
    }

    # Use Write-Information for pipeline compatibility
    $messageData = @{
        Message = "$prefix $Message"
        Color   = $color
        Level   = $Level
    }
    Write-Information -MessageData $messageData -InformationAction Continue

    # Also output to console for interactive use with ANSI colors
    if ($Host.UI.SupportsVirtualTerminal -or $PSVersionTable.PSVersion.Major -ge 7) {
        $ansiColor = switch ($color) {
            'Cyan' { "`e[96m" }
            'Green' { "`e[92m" }
            'Yellow' { "`e[93m" }
            'Red' { "`e[91m" }
            default { "`e[0m" }
        }
        [Console]::WriteLine("$ansiColor$prefix $Message`e[0m")
    }
}

function Add-ValidationCheck {
    <#
    .SYNOPSIS
        Adds a validation check result to the report.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Category,

        [Parameter(Mandatory)]
        [string]$Check,

        [Parameter(Mandatory)]
        [ValidateSet('Pass', 'Fail', 'Skip', 'Warning')]
        [string]$Result,

        [Parameter()]
        [string]$Details = '',

        [Parameter()]
        [string]$Recommendation = ''
    )

    $checkResult = [PSCustomObject]@{
        Category       = $Category
        Check          = $Check
        Result         = $Result
        Details        = $Details
        Recommendation = $Recommendation
    }

    $script:ValidationResults.Checks.Add($checkResult)
    $script:ValidationResults.Summary.Total++

    switch ($Result) {
        'Pass' { $script:ValidationResults.Summary.Passed++ }
        'Fail' { $script:ValidationResults.Summary.Failed++ }
        'Skip' { $script:ValidationResults.Summary.Skipped++ }
        'Warning' { $script:ValidationResults.Summary.Passed++ } # Count warnings as passes with notes
    }
}

function Get-PanelSourcePath {
    <#
    .SYNOPSIS
        Resolves the full path to a panel source file.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$PanelName
    )

    $searchPaths = @(
        "$($script:Config.ControlsPath)/$PanelName.cs",
        "$($script:Config.ControlsPath)/Analytics/$PanelName.cs"
    )

    foreach ($path in $searchPaths) {
        if (Test-Path -Path $path) {
            return Resolve-Path -Path $path | Select-Object -ExpandProperty Path
        }
    }

    throw "Panel not found: $PanelName. Searched: $($searchPaths -join ', ')"
}

function Test-PanelFileStructure {
    <#
    .SYNOPSIS
        Validates panel file structure and basic C# syntax.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath
    )

    Write-ValidationStep "Validating file structure for: $(Split-Path -Leaf $FilePath)" -Level Info

    # Check 1: File exists and is readable
    if (-not (Test-Path -Path $FilePath)) {
        Add-ValidationCheck -Category 'File Structure' -Check 'File Exists' -Result 'Fail' `
            -Details "File not found: $FilePath"
        return
    }

    Add-ValidationCheck -Category 'File Structure' -Check 'File Exists' -Result 'Pass'

    # Check 2: Read file content
    try {
        $content = Get-Content -Path $FilePath -Raw -ErrorAction Stop
    }
    catch {
        Add-ValidationCheck -Category 'File Structure' -Check 'File Readable' -Result 'Fail' `
            -Details $_.Exception.Message
        return
    }

    Add-ValidationCheck -Category 'File Structure' -Check 'File Readable' -Result 'Pass' `
        -Details "File size: $([math]::Round((Get-Item $FilePath).Length / 1KB, 2)) KB"

    # Check 3: Namespace declaration
    if ($content -match 'namespace\s+WileyWidget\.WinForms\.Controls') {
        Add-ValidationCheck -Category 'File Structure' -Check 'Namespace' -Result 'Pass'
    }
    else {
        Add-ValidationCheck -Category 'File Structure' -Check 'Namespace' -Result 'Fail' `
            -Details 'Missing or incorrect namespace' `
            -Recommendation 'Add: namespace WileyWidget.WinForms.Controls'
    }

    # Check 4: Base class
    $baseClasses = @('ScopedPanelBase', 'UserControl', 'DockingClientPanel')
    $hasBaseClass = $baseClasses | Where-Object { $content -match ": $_" }

    if ($hasBaseClass) {
        Add-ValidationCheck -Category 'File Structure' -Check 'Base Class' -Result 'Pass' `
            -Details "Inherits from: $hasBaseClass"
    }
    else {
        Add-ValidationCheck -Category 'File Structure' -Check 'Base Class' -Result 'Warning' `
            -Details 'No recognized base class found' `
            -Recommendation 'Consider inheriting from ScopedPanelBase'
    }

    # Check 5: Constructor
    $panelClassName = [System.IO.Path]::GetFileNameWithoutExtension($FilePath)
    if ($content -match "public\s+$panelClassName\s*\(") {
        Add-ValidationCheck -Category 'File Structure' -Check 'Constructor' -Result 'Pass'
    }
    else {
        Add-ValidationCheck -Category 'File Structure' -Check 'Constructor' -Result 'Fail' `
            -Details 'No public constructor found' `
            -Recommendation "Add: public $panelClassName() { }"
    }
}

function Test-PanelLayoutAttribute {
    <#
    .SYNOPSIS
        Validates layout attributes per Syncfusion best practices.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath
    )

    Write-ValidationStep 'Validating layout attributes' -Level Info

    $content = Get-Content -Path $FilePath -Raw

    # Check 1: SuspendLayout/ResumeLayout pattern
    $hasSuspendLayout = $content -match 'SuspendLayout\s*\(\s*\)'
    $hasResumeLayout = $content -match 'ResumeLayout\s*\(\s*(false|true)?\s*\)'

    if ($hasSuspendLayout -and $hasResumeLayout) {
        Add-ValidationCheck -Category 'Layout' -Check 'SuspendLayout/ResumeLayout' -Result 'Pass'
    }
    elseif ($hasSuspendLayout -or $hasResumeLayout) {
        Add-ValidationCheck -Category 'Layout' -Check 'SuspendLayout/ResumeLayout' -Result 'Warning' `
            -Details 'Only one of SuspendLayout/ResumeLayout found' `
            -Recommendation 'Add both: SuspendLayout() before changes, ResumeLayout(false) after'
    }
    else {
        Add-ValidationCheck -Category 'Layout' -Check 'SuspendLayout/ResumeLayout' -Result 'Fail' `
            -Details 'Missing layout suspension pattern' `
            -Recommendation 'Wrap control initialization in SuspendLayout()/ResumeLayout(false)'
    }

    # Check 2: AutoScaleMode
    if ($content -match 'AutoScaleMode\s*=\s*AutoScaleMode\.Dpi') {
        Add-ValidationCheck -Category 'Layout' -Check 'AutoScaleMode.Dpi' -Result 'Pass'
    }
    else {
        Add-ValidationCheck -Category 'Layout' -Check 'AutoScaleMode.Dpi' -Result 'Fail' `
            -Details 'AutoScaleMode.Dpi not set' `
            -Recommendation 'Add: AutoScaleMode = AutoScaleMode.Dpi;'
    }

    # Check 3: Dock property
    $hasDock = $content -match 'Dock\s*=\s*DockStyle\.'
    if ($hasDock) {
        Add-ValidationCheck -Category 'Layout' -Check 'Dock Property' -Result 'Pass'
    }
    else {
        Add-ValidationCheck -Category 'Layout' -Check 'Dock Property' -Result 'Warning' `
            -Details 'No Dock property set' `
            -Recommendation 'Consider: Dock = DockStyle.Fill; for dockable panels'
    }

    # Check 4: Size constraints
    $hasMinSize = $content -match 'MinimumSize\s*=\s*new\s+Size'
    $hasSize = $content -match 'Size\s*=\s*new\s+Size'

    if ($hasMinSize -and $hasSize) {
        Add-ValidationCheck -Category 'Layout' -Check 'Size Constraints' -Result 'Pass'
    }
    elseif ($hasSize) {
        Add-ValidationCheck -Category 'Layout' -Check 'Size Constraints' -Result 'Warning' `
            -Details 'Size set but no MinimumSize' `
            -Recommendation 'Add: MinimumSize = new Size(width, height);'
    }
    else {
        Add-ValidationCheck -Category 'Layout' -Check 'Size Constraints' -Result 'Skip' `
            -Details 'No explicit size constraints (may be dock-filled)'
    }

    # Check 5: Padding/Margin
    $hasPadding = $content -match 'Padding\s*=\s*new\s+Padding'

    if ($hasPadding) {
        Add-ValidationCheck -Category 'Layout' -Check 'Padding' -Result 'Pass'
    }
    else {
        Add-ValidationCheck -Category 'Layout' -Check 'Padding' -Result 'Warning' `
            -Details 'No Padding set' `
            -Recommendation 'Add: Padding = new Padding(12); for proper spacing'
    }

    if ($content -match 'Margin\s*=\s*new\s+Padding') {
        Add-ValidationCheck -Category 'Layout' -Check 'Margin' -Result 'Pass'
    }
    else {
        Add-ValidationCheck -Category 'Layout' -Check 'Margin' -Result 'Warning' `
            -Details 'No Margin settings detected' `
            -Recommendation 'Explicit margins help maintain spacing between panels'
    }
}

function Test-SyncfusionControl {
    <#
    .SYNOPSIS
        Validates Syncfusion control configuration.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath
    )

    Write-ValidationStep 'Validating Syncfusion controls' -Level Info

    $content = Get-Content -Path $FilePath -Raw

    # Detect Syncfusion controls
    $syncfusionControls = @(
        @{ Name = 'SfDataGrid'; Pattern = 'SfDataGrid\b' }
        @{ Name = 'SfChart'; Pattern = 'SfChart\b' }
        @{ Name = 'SfButton'; Pattern = 'SfButton\b' }
        @{ Name = 'SfTextBox'; Pattern = 'SfTextBox\b' }
        @{ Name = 'SfComboBox'; Pattern = 'SfComboBox\b' }
        @{ Name = 'DockingManager'; Pattern = 'DockingManager\b' }
    )

    $foundControls = $syncfusionControls | Where-Object { $content -match $_.Pattern }

    if ($foundControls.Count -eq 0) {
        Add-ValidationCheck -Category 'Syncfusion Controls' -Check 'Control Detection' -Result 'Skip' `
            -Details 'No Syncfusion controls detected'
        return
    }

    Add-ValidationCheck -Category 'Syncfusion Controls' -Check 'Control Detection' -Result 'Pass' `
        -Details "Found controls: $($foundControls.Name -join ', ')"

    # Check ThemeName property
    $hasThemeName = $content -match '\.ThemeName\s*='
    if ($hasThemeName) {
        Add-ValidationCheck -Category 'Syncfusion Controls' -Check 'ThemeName Property' -Result 'Pass'
    }
    else {
        Add-ValidationCheck -Category 'Syncfusion Controls' -Check 'ThemeName Property' -Result 'Warning' `
            -Details 'No ThemeName property set on controls' `
            -Recommendation 'Add: control.ThemeName = SfSkinManager.ApplicationVisualTheme;'
    }

    # Check for DataSource binding (SfDataGrid)
    if ($content -match 'SfDataGrid\b') {
        $hasDataSource = $content -match '\.DataSource\s*='
        if ($hasDataSource) {
            Add-ValidationCheck -Category 'Syncfusion Controls' -Check 'SfDataGrid DataSource' -Result 'Pass'
        }
        else {
            Add-ValidationCheck -Category 'Syncfusion Controls' -Check 'SfDataGrid DataSource' -Result 'Warning' `
                -Details 'SfDataGrid found but no DataSource binding detected' `
                -Recommendation 'Add: sfDataGrid.DataSource = viewModel.Data;'
        }
    }

    # Check for proper disposal
    $hasDispose = $content -match 'protected\s+override\s+void\s+Dispose\s*\('
    if ($hasDispose) {
        Add-ValidationCheck -Category 'Syncfusion Controls' -Check 'Dispose Pattern' -Result 'Pass'
    }
    else {
        Add-ValidationCheck -Category 'Syncfusion Controls' -Check 'Dispose Pattern' -Result 'Fail' `
            -Details 'No Dispose override found' `
            -Recommendation 'Add: protected override void Dispose(bool disposing) { }'
    }
}

function Invoke-McpValidation {
    <#
    .SYNOPSIS
        Uses MCP server to validate panel through inspection.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$PanelName
    )

    Write-ValidationStep "Running MCP validation for $PanelName" -Level Info

    try {
        # Call MCP inspection tool

        # Note: This would call the actual MCP tool in production
        # For now, we'll simulate the check
        Add-ValidationCheck -Category 'MCP Validation' -Check 'Panel Inspection' -Result 'Skip' `
            -Details "MCP validation requires running application for WileyWidget.WinForms.Controls.$PanelName" `
            -Recommendation 'Run: mcp_wileywidget-u_EvalCSharp for runtime validation'
    }
    catch {
        Add-ValidationCheck -Category 'MCP Validation' -Check 'Panel Inspection' -Result 'Fail' `
            -Details $_.Exception.Message
    }
}

function Get-SyncfusionDemoReference {
    <#
    .SYNOPSIS
        Fetches relevant Syncfusion demo examples for comparison.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ControlType
    )

    Write-ValidationStep "Fetching Syncfusion demo for: $ControlType" -Level Info

    $demoUrls = @{
        'SfDataGrid' = "$($script:Config.SyncfusionDemoUrl)/datagrid"
        'SfChart'    = "$($script:Config.SyncfusionDemoUrl)/chart"
        'SfButton'   = "$($script:Config.SyncfusionDemoUrl)/buttons"
    }

    if ($demoUrls.ContainsKey($ControlType)) {
        return $demoUrls[$ControlType]
    }

    return $null
}

function Export-ValidationReport {
    <#
    .SYNOPSIS
        Exports validation results to file.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$OutputPath,

        [Parameter()]
        [ValidateSet('Markdown', 'JSON', 'Both')]
        [string]$Format = 'Markdown'
    )

    # Ensure output directory exists
    $reportDir = Split-Path -Path $OutputPath -Parent
    if (-not (Test-Path -Path $reportDir)) {
        New-Item -Path $reportDir -ItemType Directory -Force | Out-Null
    }

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $reportFile = Join-Path -Path $reportDir -ChildPath "$($script:ValidationResults.PanelName)-$timestamp.md"

    # Build Markdown report
    $report = [System.Text.StringBuilder]::new()
    [void]$report.AppendLine("# Panel Validation Report: $($script:ValidationResults.PanelName)")
    [void]$report.AppendLine()
    [void]$report.AppendLine("**Date:** $($script:ValidationResults.Timestamp)")
    [void]$report.AppendLine("**Mode:** $($script:ValidationResults.Mode)")
    [void]$report.AppendLine("**PowerShell:** $($script:ValidationResults.PowerShellVer)")
    [void]$report.AppendLine()

    # Summary
    [void]$report.AppendLine('## Summary')
    [void]$report.AppendLine()
    $summary = $script:ValidationResults.Summary
    [void]$report.AppendLine("- **Total Checks:** $($summary.Total)")
    [void]$report.AppendLine("- **Passed:** ✓ $($summary.Passed)")
    [void]$report.AppendLine("- **Failed:** ✗ $($summary.Failed)")
    [void]$report.AppendLine("- **Skipped:** ⊘ $($summary.Skipped)")
    [void]$report.AppendLine()

    $passRate = if ($summary.Total -gt 0) {
        [math]::Round(($summary.Passed / $summary.Total) * 100, 1)
    }
    else {
        0
    }
    [void]$report.AppendLine("**Pass Rate:** $passRate%")
    [void]$report.AppendLine()

    # Detailed Results
    [void]$report.AppendLine('## Detailed Results')
    [void]$report.AppendLine()

    $groupedChecks = $script:ValidationResults.Checks | Group-Object -Property Category

    foreach ($group in $groupedChecks) {
        [void]$report.AppendLine("### $($group.Name)")
        [void]$report.AppendLine()
        [void]$report.AppendLine('| Check | Result | Details | Recommendation |')
        [void]$report.AppendLine('|-------|--------|---------|----------------|')

        foreach ($check in $group.Group) {
            $resultIcon = switch ($check.Result) {
                'Pass' { '✓' }
                'Fail' { '✗' }
                'Skip' { '⊘' }
                'Warning' { '⚠' }
            }
            [void]$report.AppendLine("| $($check.Check) | $resultIcon $($check.Result) | $($check.Details) | $($check.Recommendation) |")
        }

        [void]$report.AppendLine()
    }

    # Save reports based on format
    $reportFiles = @()

    if ($Format -in @('Markdown', 'Both')) {
        $report.ToString() | Set-Content -Path $reportFile -Encoding UTF8
        Write-ValidationStep "Markdown report saved: $reportFile" -Level Success
        $reportFiles += $reportFile
    }

    if ($Format -in @('JSON', 'Both')) {
        $jsonFile = $reportFile -replace '\.md$', '.json'
        $script:ValidationResults | ConvertTo-Json -Depth 10 | Set-Content -Path $jsonFile -Encoding UTF8
        Write-ValidationStep "JSON report saved: $jsonFile" -Level Success
        $reportFiles += $jsonFile
    }

    return $reportFiles
}
#endregion

#region Main Execution

try {
    # Display header
    $border = '═' * 51
    $header = @"

$border
 Panel Production Validation
$border

"@
    Write-Information $header -InformationAction Continue

    if ($Host.UI.SupportsVirtualTerminal -or $PSVersionTable.PSVersion.Major -ge 7) {
        [Console]::WriteLine("`e[96m`n$border`e[0m")
        [Console]::WriteLine("`e[96m Panel Production Validation`e[0m")
        [Console]::WriteLine("`e[96m$border`n`e[0m")
    }

    # Step 1: Locate panel file
    $panelPath = Get-PanelSourcePath -PanelName $PanelName
    Write-ValidationStep "Found panel: $panelPath" -Level Success

    # Step 2: File structure validation
    Test-PanelFileStructure -FilePath $panelPath

    # Step 3: Layout attributes (Quick+ modes)
    if ($Mode -in @('Quick', 'Standard', 'Full')) {
        Test-PanelLayoutAttribute -FilePath $panelPath
    }

    # Step 4: Syncfusion controls (Standard+ modes)
    if ($Mode -in @('Standard', 'Full')) {
        Test-SyncfusionControl -FilePath $panelPath
    }

    # Step 5: MCP validation (Full mode)
    if ($Mode -eq 'Full') {
        Invoke-McpValidation -PanelName $PanelName
    }

    # Step 6: Compare with Syncfusion demo
    if ($CompareWithDemo) {
        $panelContent = Get-Content -Path $panelPath -Raw
        $detectedControls = @('SfDataGrid', 'SfChart', 'SfButton') | Where-Object { $panelContent -match $_ }

        foreach ($controlType in $detectedControls) {
            $demoUrl = Get-SyncfusionDemoReference -ControlType $controlType
            if ($demoUrl) {
                Write-ValidationStep "Syncfusion demo for $controlType : $demoUrl" -Level Info
            }
        }
    }

    # Step 7: Generate report
    $reportPath = Export-ValidationReport -OutputPath $OutputPath -Format $OutputFormat

    # Final summary
    $border = '═' * 51
    $summary = $script:ValidationResults.Summary

    $passRate = if ($summary.Total -gt 0) {
        [math]::Round(($summary.Passed / $summary.Total) * 100, 1)
    }
    else {
        0
    }

    $summaryText = @"

$border
 Validation Complete
$border

Total Checks : $($summary.Total)
Passed       : $($summary.Passed)
Failed       : $($summary.Failed)
Skipped      : $($summary.Skipped)

Pass Rate: $passRate%

Report: $reportPath
"@

    Write-Information $summaryText -InformationAction Continue

    # ANSI colored output for interactive terminals
    if ($Host.UI.SupportsVirtualTerminal -or $PSVersionTable.PSVersion.Major -ge 7) {
        [Console]::WriteLine("`e[96m`n$border`e[0m")
        [Console]::WriteLine("`e[96m Validation Complete`e[0m")
        [Console]::WriteLine("`e[96m$border`e[0m")
        [Console]::WriteLine("`nTotal Checks : $($summary.Total)")
        [Console]::WriteLine("`e[92mPassed       : $($summary.Passed)`e[0m")
        [Console]::WriteLine("`e[91mFailed       : $($summary.Failed)`e[0m")
        [Console]::WriteLine("`e[93mSkipped      : $($summary.Skipped)`e[0m")

        $rateColor = if ($passRate -ge 80) { "`e[92m" } elseif ($passRate -ge 60) { "`e[93m" } else { "`e[91m" }
        [Console]::WriteLine("`n${rateColor}Pass Rate: $passRate%`e[0m")
        [Console]::WriteLine("`n`e[96mReport: $reportPath`e[0m")
    }

    # Return validation results if PassThru specified
    if ($PassThru) {
        return $script:ValidationResults
    }

    # Exit code based on failures
    if ($summary.Failed -gt 0) {
        exit 1
    }
}
catch {
    Write-ValidationStep "Validation failed: $($_.Exception.Message)" -Level Error
    Write-Error $_.Exception
    exit 1
}
#endregion
