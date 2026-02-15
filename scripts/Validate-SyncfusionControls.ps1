<#
.SYNOPSIS
Validates Syncfusion control usage against factory pattern and complete property configuration.

.DESCRIPTION
Scans C# files for Syncfusion control instantiations and checks:
1. Are controls created via SyncfusionControlFactory?
2. If not, are ALL mandatory properties set?
3. Is theme integration present?

.PARAMETER Path
Path to scan (defaults to src/WileyWidget.WinForms)

.PARAMETER Fix
Attempt to suggest fixes (outputs recommendation only, doesn't modify files)

.EXAMPLE
.\Validate-SyncfusionControls.ps1
.\Validate-SyncfusionControls.ps1 -Path src/WileyWidget.WinForms/Forms -Fix
#>

[CmdletBinding()]
param(
    [string]$Path = "src/WileyWidget.WinForms",
    [switch]$Fix
)

$ErrorActionPreference = "Stop"

# Control patterns and their mandatory properties
$controlPatterns = @{
    'SfDataGrid' = @(
        'ThemeName',
        'ApplySyncfusionTheme',
        'PreventStringRelationalFilters',
        'AllowEditing',
        'AllowFiltering',
        'AllowSorting',
        'SelectionMode'
    )
    'SfButton' = @(
        'ThemeName',
        'ApplySyncfusionTheme',
        'Size',
        'Style'
    )
    'RadialGauge' = @(
        'ApplySyncfusionTheme',
        'MinimumValue',
        'MaximumValue',
        'Value',
        'FrameType'
    )
    'ChartControl' = @(
        'ApplySyncfusionTheme',
        'PrimaryXAxis',
        'PrimaryYAxis',
        'Legend'
    )
    'RibbonControlAdv' = @(
        'ThemeName',
        'ApplySyncfusionTheme',
        'Dock',
        'MenuButtonText',
        'RibbonStyle'
    )
    'TabControlAdv' = @(
        'ThemeName',
        'ApplySyncfusionTheme',
        'TabStyle',
        'Alignment'
    )
    'SfComboBox' = @(
        'ThemeName',
        'ApplySyncfusionTheme',
        'DropDownStyle'
    )
}

Write-Host "🔍 Scanning Syncfusion control usage in: $Path" -ForegroundColor Cyan
Write-Host ""

$files = Get-ChildItem -Path $Path -Filter "*.cs" -Recurse | Where-Object { 
    $_.FullName -notmatch '\\obj\\|\\bin\\|\.Designer\.cs|AssemblyInfo\.cs' 
}

$violations = @()
$factoryUsageCount = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $lineNumber = 0
    
    # Check for factory usage (good pattern)
    if ($content -match '_controlFactory\.Create|SyncfusionControlFactory\.Create') {
        $factoryUsageCount++
    }
    
    # Check for direct instantiation
    foreach ($controlType in $controlPatterns.Keys) {
        $pattern = "new\s+$controlType\s*\("
        
        if ($content -match $pattern) {
            # Found direct instantiation - check if all properties are set
            $lines = Get-Content $file.FullName
            
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match $pattern) {
                    $lineNumber = $i + 1
                    
                    # Get context (next ~30 lines for property check)
                    $contextEnd = [Math]::Min($i + 30, $lines.Count - 1)
                    $context = $lines[$i..$contextEnd] -join "`n"
                    
                    # Check for mandatory properties
                    $missingProperties = @()
                    foreach ($prop in $controlPatterns[$controlType]) {
                        if ($context -notmatch $prop) {
                            $missingProperties += $prop
                        }
                    }
                    
                    if ($missingProperties.Count -gt 0) {
                        $violations += [PSCustomObject]@{
                            File = $file.FullName -replace [regex]::Escape((Get-Location).Path + "\"), ""
                            Line = $lineNumber
                            ControlType = $controlType
                            MissingProperties = $missingProperties -join ", "
                            Severity = "High"
                        }
                    }
                }
            }
        }
    }
}

# Report results
Write-Host "📊 Validation Results" -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Yellow
Write-Host ""

if ($violations.Count -eq 0) {
    Write-Host "✅ No violations found!" -ForegroundColor Green
    Write-Host "   Factory usage count: $factoryUsageCount" -ForegroundColor Green
} else {
    Write-Host "❌ Found $($violations.Count) violations:" -ForegroundColor Red
    Write-Host ""
    
    foreach ($violation in $violations) {
        Write-Host "  📄 File: $($violation.File)" -ForegroundColor Yellow
        Write-Host "     Line: $($violation.Line)" -ForegroundColor Gray
        Write-Host "     Control: $($violation.ControlType)" -ForegroundColor Cyan
        Write-Host "     Missing: $($violation.MissingProperties)" -ForegroundColor Red
        
        if ($Fix) {
            Write-Host "     💡 Recommendation: Use _controlFactory.Create$($violation.ControlType)()" -ForegroundColor Magenta
        }
        
        Write-Host ""
    }
    
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Red
    Write-Host ""
    Write-Host "⚠️  Fix Required:" -ForegroundColor Yellow
    Write-Host "   1. Replace direct instantiation with SyncfusionControlFactory" -ForegroundColor White
    Write-Host "   2. OR ensure ALL mandatory properties are set" -ForegroundColor White
    Write-Host "   3. See: docs/SYNCFUSION_CONTROL_QUICK_REFERENCE.md" -ForegroundColor White
    Write-Host ""
    
    exit 1
}

Write-Host "✨ Validation complete!" -ForegroundColor Green
