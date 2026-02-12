<#
.SYNOPSIS
    Batch validation script for all WinForms panels.

.DESCRIPTION
    Validates all panels in the Controls directory and aggregates results.
    Generates consolidated report showing pass rates across all panels.

.PARAMETER Mode
    Validation mode: 'Quick', 'Standard', or 'Full'.

.PARAMETER OutputFormat
    Report format: 'Markdown', 'JSON', or 'Both'.

.PARAMETER FailFast
    Stop on first failure.

.EXAMPLE
    .\Validate-AllPanels.ps1 -Mode Standard

.EXAMPLE
    .\Validate-AllPanels.ps1 -Mode Full -OutputFormat Both

.NOTES
    Author: Wiley Widget Team
    Requires: PowerShell 7.5.4+
#>

#Requires -Version 7.5.4

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Quick', 'Standard', 'Full')]
    [string]$Mode = 'Standard',

    [Parameter()]
    [ValidateSet('Markdown', 'JSON', 'Both')]
    [string]$OutputFormat = 'Both',

    [Parameter()]
    [switch]$FailFast,

    [Parameter()]
    [string]$OutputPath = "$PSScriptRoot/../../tmp/validation-reports"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

#region Helper Functions

function Write-BatchStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter()]
        [ValidateSet('Info', 'Success', 'Warning', 'Error')]
        [string]$Level = 'Info'
    )

    $color = switch ($Level) {
        'Info' { "`e[96m" }
        'Success' { "`e[92m" }
        'Warning' { "`e[93m" }
        'Error' { "`e[91m" }
    }

    if ($Host.UI.SupportsVirtualTerminal -or $PSVersionTable.PSVersion.Major -ge 7) {
        [Console]::WriteLine("$color$Message`e[0m")
    }
    else {
        Write-Information $Message -InformationAction Continue
    }
}

function Export-ConsolidatedReport {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Results,

        [Parameter(Mandatory)]
        [string]$OutputPath,

        [Parameter()]
        [string]$Format = 'Both'
    )

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $reportDir = $OutputPath
    if (-not (Test-Path -Path $reportDir)) {
        New-Item -Path $reportDir -ItemType Directory -Force | Out-Null
    }

    $consolidatedFile = Join-Path -Path $reportDir -ChildPath "consolidated-$timestamp.md"

    # Build markdown report
    $report = [System.Text.StringBuilder]::new()
    [void]$report.AppendLine("# Consolidated Panel Validation Report")
    [void]$report.AppendLine()
    [void]$report.AppendLine("**Date:** $(Get-Date -Format 'o')")
    [void]$report.AppendLine("**Mode:** $Mode")
    [void]$report.AppendLine("**Panels Tested:** $($Results.Count)")
    [void]$report.AppendLine()

    # Summary statistics
    $totalChecks = ($Results | Measure-Object -Property TotalChecks -Sum).Sum
    $totalPassed = ($Results | Measure-Object -Property Passed -Sum).Sum
    $totalFailed = ($Results | Measure-Object -Property Failed -Sum).Sum
    $totalSkipped = ($Results | Measure-Object -Property Skipped -Sum).Sum

    $overallPassRate = if ($totalChecks -gt 0) {
        [math]::Round(($totalPassed / $totalChecks) * 100, 1)
    }
    else {
        0
    }

    [void]$report.AppendLine("## Overall Summary")
    [void]$report.AppendLine()
    [void]$report.AppendLine("- **Total Checks:** $totalChecks")
    [void]$report.AppendLine("- **Passed:** ✓ $totalPassed")
    [void]$report.AppendLine("- **Failed:** ✗ $totalFailed")
    [void]$report.AppendLine("- **Skipped:** ⊘ $totalSkipped")
    [void]$report.AppendLine("- **Overall Pass Rate:** $overallPassRate%")
    [void]$report.AppendLine()

    # Per-panel results
    [void]$report.AppendLine("## Per-Panel Results")
    [void]$report.AppendLine()
    [void]$report.AppendLine("| Panel | Total | Passed | Failed | Skipped | Pass Rate | Status |")
    [void]$report.AppendLine("|-------|-------|--------|--------|---------|-----------|--------|")

    foreach ($result in $Results | Sort-Object -Property PassRate -Descending) {
        $status = if ($result.Failed -eq 0) { '✓ Pass' } else { '✗ Fail' }
        [void]$report.AppendLine("| $($result.Panel) | $($result.TotalChecks) | $($result.Passed) | $($result.Failed) | $($result.Skipped) | $($result.PassRate)% | $status |")
    }

    [void]$report.AppendLine()

    # Save markdown
    if ($Format -in @('Markdown', 'Both')) {
        $report.ToString() | Set-Content -Path $consolidatedFile -Encoding UTF8
        Write-BatchStatus "Consolidated report saved: $consolidatedFile" -Level Success
    }

    # Save JSON
    if ($Format -in @('JSON', 'Both')) {
        $jsonFile = $consolidatedFile -replace '\.md$', '.json'
        @{
            Timestamp       = Get-Date -Format 'o'
            Mode            = $Mode
            PanelsTested    = $Results.Count
            TotalChecks     = $totalChecks
            TotalPassed     = $totalPassed
            TotalFailed     = $totalFailed
            TotalSkipped    = $totalSkipped
            OverallPassRate = $overallPassRate
            Results         = $Results
        } | ConvertTo-Json -Depth 10 | Set-Content -Path $jsonFile -Encoding UTF8
        Write-BatchStatus "JSON report saved: $jsonFile" -Level Success
    }

    return $consolidatedFile
}

#endregion

#region Main Execution

try {
    $border = '═' * 60
    Write-BatchStatus "`n$border" -Level Info
    Write-BatchStatus " Batch Panel Validation - $Mode Mode" -Level Info
    Write-BatchStatus "$border`n" -Level Info

    # Find all panels
    $controlsPath = "$PSScriptRoot/../../src/WileyWidget.WinForms/Controls"
    $panelFiles = Get-ChildItem -Path $controlsPath -Filter "*Panel.cs" -File

    Write-BatchStatus "Found $($panelFiles.Count) panels to validate`n" -Level Info

    # Validate each panel
    $results = [System.Collections.Generic.List[PSCustomObject]]::new()
    $failureCount = 0

    foreach ($panelFile in $panelFiles) {
        $panelName = $panelFile.BaseName
        Write-BatchStatus "Validating $panelName..." -Level Info

        try {
            # Run validation
            $validationScript = Join-Path -Path $PSScriptRoot -ChildPath 'Validate-PanelProduction.ps1'
            $output = & $validationScript -PanelName $panelName -Mode $Mode -OutputFormat 'JSON' -InformationAction SilentlyContinue 2>&1

            # Load JSON result
            $jsonFile = Get-ChildItem -Path "$PSScriptRoot/../../tmp" -Filter "$panelName-*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

            if ($jsonFile) {
                $validationResult = Get-Content -Path $jsonFile.FullName | ConvertFrom-Json

                $passRate = if ($validationResult.Summary.Total -gt 0) {
                    [math]::Round(($validationResult.Summary.Passed / $validationResult.Summary.Total) * 100, 1)
                }
                else {
                    0
                }

                $result = [PSCustomObject]@{
                    Panel       = $panelName
                    TotalChecks = $validationResult.Summary.Total
                    Passed      = $validationResult.Summary.Passed
                    Failed      = $validationResult.Summary.Failed
                    Skipped     = $validationResult.Summary.Skipped
                    PassRate    = $passRate
                    ReportPath  = $jsonFile.FullName
                }

                $results.Add($result)

                $status = if ($result.Failed -eq 0) { 'PASS' } else { 'FAIL' }
                $statusColor = if ($result.Failed -eq 0) { 'Success' } else { 'Error' }
                Write-BatchStatus "  [$status] $panelName - Pass Rate: $passRate%" -Level $statusColor

                if ($result.Failed -gt 0) {
                    $failureCount++
                    if ($FailFast) {
                        Write-BatchStatus "Stopping due to -FailFast" -Level Error
                        break
                    }
                }
            }
            else {
                Write-BatchStatus "  [SKIP] No validation result found for $panelName" -Level Warning
            }
        }
        catch {
            Write-BatchStatus "  [ERROR] Failed to validate $panelName : $($_.Exception.Message)" -Level Error
            $failureCount++
            if ($FailFast) {
                throw
            }
        }
    }

    Write-BatchStatus "`n$border" -Level Info

    # Generate consolidated report
    if ($results.Count -gt 0) {
        $consolidatedReport = Export-ConsolidatedReport -Results $results -OutputPath $OutputPath -Format $OutputFormat

        # Display summary
        $totalChecks = ($results | Measure-Object -Property TotalChecks -Sum).Sum
        $totalPassed = ($results | Measure-Object -Property Passed -Sum).Sum
        $overallPassRate = if ($totalChecks -gt 0) {
            [math]::Round(($totalPassed / $totalChecks) * 100, 1)
        }
        else {
            0
        }

        Write-BatchStatus "`nBatch Validation Complete" -Level Success
        Write-BatchStatus "Panels Validated: $($results.Count)" -Level Info
        Write-BatchStatus "Overall Pass Rate: $overallPassRate%" -Level $(if ($overallPassRate -ge 80) { 'Success' } else { 'Warning' })
        Write-BatchStatus "Failures: $failureCount" -Level $(if ($failureCount -eq 0) { 'Success' } else { 'Error' })
    }
    else {
        Write-BatchStatus "No validation results to report" -Level Warning
    }

    # Exit with error code if failures
    if ($failureCount -gt 0) {
        exit 1
    }
}
catch {
    Write-BatchStatus "Batch validation failed: $($_.Exception.Message)" -Level Error
    Write-Error $_.Exception
    exit 1
}

#endregion
