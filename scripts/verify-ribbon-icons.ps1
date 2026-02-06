#Requires -Version 7
<#
.SYNOPSIS
    Verifies ribbon icon loading from embedded resources.
.DESCRIPTION
    Tests that all ribbon button icons can be loaded from embedded resources
    in the compiled assembly.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$assemblyPath = "$PSScriptRoot\..\src\WileyWidget.WinForms\bin\Debug\net10.0-windows\WileyWidget.WinForms.dll"

if (-not (Test-Path $assemblyPath)) {
    Write-Error "Assembly not found: $assemblyPath. Run 'dotnet build' first."
    exit 1
}

Write-Host "🔍 Verifying Ribbon Icon Resources..." -ForegroundColor Cyan
Write-Host ""

# Load assembly
$assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)
$allResources = $assembly.GetManifestResourceNames()
$iconResources = $allResources | Where-Object { $_ -like '*FlatIcons*.png' }

Write-Host "📦 Total embedded resources: $($allResources.Count)" -ForegroundColor Gray
Write-Host "🎨 FlatIcon resources: $($iconResources.Count)" -ForegroundColor Cyan
Write-Host ""

# Critical ribbon button icons that MUST be present
$criticalIcons = @(
    'dashboard', 'accounts', 'budget', 'budgetoverview',
    'analytics', 'reports', 'deptsummary', 'insightfeed',
    'settings', 'quickbooks', 'jarvis',
    'customers', 'utilitybill', 'revenuetrends', 'recommendedcharge',
    'activitylog', 'auditlog',
    'warroom', 'lock', 'reset', 'save'
)

$missingIcons = @()
$foundIcons = @()

Write-Host "Checking critical ribbon button icons:" -ForegroundColor Yellow
foreach ($iconName in $criticalIcons) {
    $resourceName = "WileyWidget.WinForms.Resources.FlatIcons.${iconName}flat.png"

    if ($iconResources -contains $resourceName) {
        Write-Host "  ✓ $iconName" -ForegroundColor Green
        $foundIcons += $iconName

        # Try to actually load the resource
        try {
            $stream = $assembly.GetManifestResourceStream($resourceName)
            if ($stream) {
                $bytes = New-Object byte[] $stream.Length
                $null = $stream.Read($bytes, 0, $stream.Length)
                $stream.Close()

                if ($bytes.Length -gt 0) {
                    Write-Host "    → Loaded ($($bytes.Length) bytes)" -ForegroundColor Gray
                } else {
                    Write-Host "    ⚠ Empty resource!" -ForegroundColor Yellow
                }
            } else {
                Write-Host "    ⚠ Failed to open stream!" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "    ⚠ Load error: $_" -ForegroundColor Red
        }
    } else {
        Write-Host "  ✗ $iconName (MISSING)" -ForegroundColor Red
        $missingIcons += $iconName
    }
}

Write-Host ""
Write-Host "📊 Verification Summary:" -ForegroundColor Cyan
Write-Host "  Found: $($foundIcons.Count) / $($criticalIcons.Count)" -ForegroundColor Green
Write-Host "  Missing: $($missingIcons.Count)" -ForegroundColor $(if ($missingIcons.Count -eq 0) { 'Green' } else { 'Red' })

if ($missingIcons.Count -gt 0) {
    Write-Host ""
    Write-Host "❌ Missing icons:" -ForegroundColor Red
    foreach ($icon in $missingIcons) {
        Write-Host "  - $icon" -ForegroundColor Red
    }
    exit 1
} else {
    Write-Host ""
    Write-Host "✅ All critical ribbon icons verified!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Run the application: dotnet run --project src\WileyWidget.WinForms"
    Write-Host "  2. Check ribbon buttons display icons correctly"
    Write-Host "  3. Verify icon sizing matches Syncfusion reference (70x80px buttons, 32x32 icons)"
}
