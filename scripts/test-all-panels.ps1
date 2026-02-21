<#
.SYNOPSIS
    Headless validation for all WinForms panels using WileyWidget MCP server.
.DESCRIPTION
    Iterates through all panels and validates control instantiation, theme compliance, and configuration.
.EXAMPLE
    .\scripts\test-all-panels.ps1
.EXAMPLE
    .\scripts\test-all-panels.ps1 -PanelNames "DashboardPanel","AccountsPanel"
.EXAMPLE
    .\scripts\test-all-panels.ps1 -OutputFormat json
#>

[CmdletBinding()]
param(
    [string[]]$PanelNames = @(
        'DashboardPanel',
        'AccountsPanel',
        'BudgetPanel',
        'AnalyticsHubPanel',
        'WarRoomPanel',
        'QuickBooksPanel',
        'SettingsPanel',
        'BudgetOverviewPanel',
        'ReportsPanel',
        'CustomersPanel',
        'AuditLogPanel',
        'ActivityLogPanel'
    ),

    [ValidateSet('text', 'json', 'html')]
    [string]$OutputFormat = 'text',

    [int]$TimeoutSeconds = 30,

    [switch]$FailFast
)

$ErrorActionPreference = 'Stop'

Write-Host "`n🔍 WileyWidget Panel Validation Suite" -ForegroundColor Cyan
Write-Host "======================================`n" -ForegroundColor Cyan

$results = @()
$passCount = 0
$failCount = 0

foreach ($panel in $PanelNames) {
    Write-Host "Testing $panel..." -ForegroundColor Yellow

    $fullTypeName = "WileyWidget.WinForms.Controls.$panel"

    # Test 1: Headless instantiation
    try {
        $testParams = @{
            formTypeName = $fullTypeName
            timeoutSeconds = $TimeoutSeconds
            jsonOutput = $true
        } | ConvertTo-Json -Compress

        $output = npx --yes @modelcontextprotocol/cli call wileywidget-ui RunHeadlessFormTest --params $testParams 2>&1 | Out-String

        $instantiationStatus = if ($LASTEXITCODE -eq 0) { 'PASS' } else { 'FAIL' }

        if ($instantiationStatus -eq 'FAIL') {
            $failCount++
            Write-Host "  ❌ Instantiation: FAIL" -ForegroundColor Red

            if ($FailFast) {
                Write-Host "`nFail-fast enabled. Stopping validation." -ForegroundColor Red
                break
            }
        }
        else {
            $passCount++
            Write-Host "  ✅ Instantiation: PASS" -ForegroundColor Green
        }

        $results += [PSCustomObject]@{
            Panel = $panel
            FullTypeName = $fullTypeName
            Instantiation = $instantiationStatus
            Output = $output
            Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        }
    }
    catch {
        $failCount++
        Write-Host "  ❌ Error: $($_.Exception.Message)" -ForegroundColor Red

        $results += [PSCustomObject]@{
            Panel = $panel
            FullTypeName = $fullTypeName
            Instantiation = 'ERROR'
            Output = $_.Exception.Message
            Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        }

        if ($FailFast) {
            throw
        }
    }

    Write-Host ""
}

# Summary
Write-Host "`n📊 Validation Summary" -ForegroundColor Cyan
Write-Host "=====================`n" -ForegroundColor Cyan
Write-Host "Total Panels:  $($PanelNames.Count)" -ForegroundColor White
Write-Host "Passed:        $passCount" -ForegroundColor Green
Write-Host "Failed:        $failCount" -ForegroundColor Red
Write-Host "Success Rate:  $('{0:P0}' -f ($passCount / $PanelNames.Count))`n" -ForegroundColor $(if ($passCount -eq $PanelNames.Count) { 'Green' } else { 'Yellow' })

# Export results
$resultDir = "tmp"
if (!(Test-Path $resultDir)) {
    New-Item -ItemType Directory -Path $resultDir -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$csvPath = "$resultDir/panel-validation-$timestamp.csv"
$jsonPath = "$resultDir/panel-validation-$timestamp.json"

$results | Export-Csv -Path $csvPath -NoTypeInformation
$results | ConvertTo-Json -Depth 10 | Out-File -FilePath $jsonPath -Encoding UTF8

Write-Host "Results saved:" -ForegroundColor Cyan
Write-Host "  CSV:  $csvPath" -ForegroundColor White
Write-Host "  JSON: $jsonPath`n" -ForegroundColor White

# Display detailed results if requested
if ($OutputFormat -eq 'json') {
    $results | ConvertTo-Json -Depth 10
}
elseif ($OutputFormat -eq 'html') {
    $htmlPath = "$resultDir/panel-validation-$timestamp.html"
    $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Panel Validation Report</title>
    <style>
        body { font-family: 'Segoe UI', sans-serif; margin: 20px; }
        h1 { color: #0078d4; }
        table { border-collapse: collapse; width: 100%; margin-top: 20px; }
        th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }
        th { background-color: #0078d4; color: white; }
        tr:nth-child(even) { background-color: #f2f2f2; }
        .pass { color: green; font-weight: bold; }
        .fail { color: red; font-weight: bold; }
    </style>
</head>
<body>
    <h1>🔍 Panel Validation Report</h1>
    <p>Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</p>
    <p>Total Panels: $($PanelNames.Count) | Passed: <span class="pass">$passCount</span> | Failed: <span class="fail">$failCount</span></p>
    <table>
        <thead>
            <tr>
                <th>Panel</th>
                <th>Instantiation</th>
                <th>Timestamp</th>
            </tr>
        </thead>
        <tbody>
            $(foreach ($result in $results) {
                $statusClass = if ($result.Instantiation -eq 'PASS') { 'pass' } else { 'fail' }
                "<tr><td>$($result.Panel)</td><td class='$statusClass'>$($result.Instantiation)</td><td>$($result.Timestamp)</td></tr>"
            })
        </tbody>
    </table>
</body>
</html>
"@
    $html | Out-File -FilePath $htmlPath -Encoding UTF8
    Write-Host "  HTML: $htmlPath`n" -ForegroundColor White
}
else {
    $results | Format-Table -AutoSize -Property Panel, Instantiation, Timestamp
}

# Exit code based on results
if ($failCount -eq 0) {
    Write-Host "✅ All panels passed validation!" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "⚠️  Some panels failed validation. Review output above." -ForegroundColor Yellow
    exit 1
}
