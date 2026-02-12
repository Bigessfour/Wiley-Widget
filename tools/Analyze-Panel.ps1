#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Enhanced WinForms Panel Analyzer for Syncfusion projects
.DESCRIPTION
    Comprehensive readiness check with reduced false positives, line numbers,
    accessibility checks, and actionable fix suggestions.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FilePath,

    [Parameter()]
    [switch]$EnableDebug,

    [Parameter()]
    [switch]$JsonReport
)

$ErrorActionPreference = 'Stop'

function Write-DebugLog {
    param([string]$Message, [string]$Color = 'Gray')
    if ($EnableDebug) { Write-Host "[DEBUG] $Message" -ForegroundColor $Color }
}

# Load file
if (-not (Test-Path $FilePath)) { Write-Error "File not found: $FilePath"; exit 1 }
$lines = Get-Content $FilePath
$content = $lines -join "`n"
$lineCount = $lines.Count

Write-DebugLog "Loaded $FilePath ($lineCount lines)"

# Extract class name
$panelName = if ($content -match 'class\s+(\w+Panel)\b') { $matches[1] } else { "UnknownPanel" }

Write-Host "`n═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🔍 Enhanced Panel Analysis: $(Split-Path $FilePath -Leaf)" -ForegroundColor Cyan
Write-Host "   Class: $panelName" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════`n"

$issues = @()
$warnings = @()
$info = @()

# Helper: Add issue with line number when possible
function Add-Issue {
    param($Type, $Severity, $Name, $Details, $Line = $null)
    $entry = @{ Type = $Type; Severity = $Severity; Name = $Name; Details = $Details }
    if ($Line) { $entry.Line = $Line }
    switch ($Severity) {
        "High"   { $issues += $entry }
        "Medium" { $warnings += $entry }
        "Low"    { $warnings += $entry }
        "Info"   { $info += $entry }
    }
}

# === Private Field Analysis ===
function Analyze-PrivateFields {
    Write-Host "🔎 Analyzing private fields..." -ForegroundColor Yellow
    $fieldMatches = [regex]::Matches($content, '(?m)^\s*private\s+(?:readonly\s+)?[\w<>\[\], ]+\s+(_\w+)\s*[;=]')
    foreach ($m in $fieldMatches) {
        $field = $m.Groups[1].Value
        # Count usages - simple pattern to find field references
        $usages = [regex]::Matches($content, "\b$field\b").Count
        if ($usages -le 1) {  # declaration + 0 or 1 use
            $line = ($lines | Select-String -Pattern [regex]::Escape($field) -SimpleMatch | Select-Object -First 1).LineNumber
            if ($usages -eq 0) {
                Add-Issue "Unused Field" "High" $field "Private field never used - consider removing" $line
            } else {
                Add-Issue "Barely Used Field" "Medium" $field "Field only initialized - possible dead code" $line
            }
        }
    }
}

# === Button Icon Analysis ===
function Analyze-ButtonIcons {
    Write-Host "🎨 Analyzing button icons..." -ForegroundColor Yellow
    $buttons = [regex]::Matches($content, '(?m)^\s*private\s+(?:readonly\s+)?(?:SfButton|Button)\s+(_\w+)')
    foreach ($m in $buttons) {
        $btn = $m.Groups[1].Value
        $hasIcon = $content -match "$btn\.(?:Image|ImageList|BackgroundImage)\s*=" -or
                   $content -match "$btn.*LoadIcon\(" -or
                   $content -match "Properties\.Resources\..*->$btn"
        if (-not $hasIcon) {
            Add-Issue "Missing Icon" "Low" $btn "Consider adding an icon for better visual distinction"
        }
    }
}

# === Event Handler Analysis ===
function Analyze-EventHandlers {
    Write-Host "🔌 Analyzing event handlers..." -ForegroundColor Yellow
    $registrations = [regex]::Matches($content, '(\w+)\s*\+=\s*([.\w]+);')
    foreach ($reg in $registrations) {
        $handler = $reg.Groups[2].Value -replace '\.$',''
        $bodyMatch = [regex]::Match($content, "(?ms)private\s+(?:async\s+)?(?:void|Task)\s+$handler\s*\([^)]*\)\s*\{(.*?)\}")
        if ($bodyMatch.Success) {
            $body = $bodyMatch.Groups[1].Value.Trim()
            if ([string]::IsNullOrWhiteSpace($body) -or $body -eq '}' -or $body -match '^base\.') {
                Add-Issue "Empty/Trivial Handler" "High" $handler "Handler registered but does nothing"
            } elseif ($body -match '\b(TODO|FIXME|HACK)\b') {
                Add-Issue "Incomplete Handler" "Medium" $handler "Contains TODO/FIXME - implement or remove"
            }
        }
    }
}

# === InitializeComponent Extraction (robust) ===
function Get-InitializeComponentBody {
    $start = $lines | Select-String -Pattern 'private\s+void\s+InitializeComponent\s*\(\s*\)\s*\{' | Select-Object -First 1
    if (-not $start) { return $null }
    $braceCount = 1
    $bodyLines = @()
    for ($i = $start.LineNumber; $i -lt $lines.Count; $i++) {
        $bodyLines += $lines[$i]
        $braceCount += ($lines[$i] | Select-String -Pattern '{|}' -AllMatches).Matches.Count
        if ($lines[$i] -match '}' ) { $braceCount -= 2 }
        if ($braceCount -eq 0) { break }
    }
    return ($bodyLines -join "`n")
}

$initBody = Get-InitializeComponentBody
$hasInitComponent = $initBody -ne $null

if (-not $hasInitComponent) {
    Add-Issue "Missing InitializeComponent" "High" "Constructor" "No InitializeComponent() method found"
}

# === Modern UI Guidelines (using robust body) ===
function Analyze-ModernUI {
    if (-not $hasInitComponent) { return }
    Write-Host "🎯 Analyzing modern UI guidelines..." -ForegroundColor Yellow

    # Input heights
    $inputs = [regex]::Matches($initBody, 'new\s+(TextBoxExt|SfNumericTextBox|SfComboBox|ComboBoxAdv|DateTimePickerAdv)\s*\{[^}]*Height\s*=\s*(\d+)[^}]*\}')
    foreach ($m in $inputs) {
        $h = [int]$m.Groups[2].Value
        if ($h -lt 40 -and -not ($m.Value -match 'Multiline\s*=\s*true')) {
            Add-Issue "Input Height Too Small" "Medium" "$($m.Groups[1].Value) ($h`px)" "Recommend ≥40px for touch-friendly UX"
        }
    }

    # Button heights
    $buttons = [regex]::Matches($initBody, 'new\s+(SfButton|Button)\s*\{[^}]*Height\s*=\s*(\d+)[^}]*\}')
    foreach ($m in $buttons) {
        $h = [int]$m.Groups[2].Value
        if ($h -lt 36 -or $h -gt 48) {
            Add-Issue "Non-Standard Button Height" "Medium" "Button ($h`px)" "Ideal range: 36–48px (preferably 40px)"
        }
    }

    # Label column width (first absolute column)
    if ($initBody -match 'ColumnStyles\.Add\s*\(\s*new\s+ColumnStyle\s*\(\s*SizeType\.Absolute,\s*(\d+)') {
        $w = [int]$matches[1]
        if ($w -lt 140) {
            Add-Issue "Label Column Narrow" "Medium" "Column ($w`px)" "Recommend 160–180px for alignment"
        }
    }

    # Font sizes
    $fonts = [regex]::Matches($initBody, 'new\s+Font\([^,]+,\s*(\d+(?:\.\d+)?)')
    foreach ($m in $fonts) {
        $size = [float]$m.Groups[1].Value
        if ($size -lt 11) {
            Add-Issue "Small Font" "Medium" "$size`pt" "Minimum 11pt recommended for readability"
        }
    }
}

# === Architecture & Pattern Checks ===
function Analyze-Architecture {
    Write-Host "🏗️  Validating architecture..." -ForegroundColor Yellow

    if ($content -notmatch ':\s*ScopedPanelBase') {
        Add-Issue "Missing Base Class" "High" "Inheritance" "Should inherit from ScopedPanelBase"
    }

    if ($content -notmatch 'Dock\s*=\s*DockStyle\.Fill' -and $content -notmatch 'this\.Size\s*=') {
        Add-Issue "Missing Layout Config" "High" "Dock/Size" "Set Dock = Fill or explicit Size"
    }

    if ($content -notmatch 'AutoScaleMode\s*=\s*AutoScaleMode\.Dpi') {
        Add-Issue "Missing DPI Awareness" "Medium" "Scaling" "Set AutoScaleMode = Dpi for high-DPI support"
    }
}

# === Run Analyses ===
Analyze-PrivateFields
Analyze-ButtonIcons
Analyze-EventHandlers
Analyze-ModernUI
Analyze-Architecture

# === Results ===
$allFindings = $issues + $warnings + $info | Sort-Object Severity, Type

if ($allFindings.Count -eq 0) {
    Write-Host "✅ No issues detected! Panel is clean and follows best practices." -ForegroundColor Green
} else {
    if ($issues.Count -gt 0) {
        Write-Host "`n🔴 CRITICAL ISSUES ($($issues.Count))" -ForegroundColor Red
        $issues | ForEach-Object { Write-Host "   • $($_.Type) - $($_.Name)" -ForegroundColor Red; Write-Host "     $($_.Details)" -ForegroundColor Gray }
    }
    if ($warnings.Count -gt 0) {
        Write-Host "`n⚠️  WARNINGS ($($warnings.Count))" -ForegroundColor Yellow
        $warnings | ForEach-Object { Write-Host "   • $($_.Type) - $($_.Name)" -ForegroundColor Yellow; Write-Host "     $($_.Details)" -ForegroundColor Gray }
    }
    if ($info.Count -gt 0) {
        Write-Host "`nℹ️  SUGGESTIONS ($($info.Count))" -ForegroundColor Cyan
        $info | Select-Object -First 15 | ForEach-Object { Write-Host "   • $($_.Type) - $($_.Name)" -ForegroundColor Cyan; Write-Host "     $($_.Details)" -ForegroundColor Gray }
    }
}

Write-Host "`n📊 Summary: $($issues.Count) critical | $($warnings.Count) warnings | $($info.Count) suggestions" -ForegroundColor Cyan

if ($JsonReport) {
    $report = @{
        Panel = $panelName
        ScanDate = Get-Date
        Issues = $issues
        Warnings = $warnings
        Suggestions = $info
    }
    $report | ConvertTo-Json -Depth 5 | Out-File "tmp/panel-analysis-$panelName.json"
    Write-Host "`n💾 JSON report saved to tmp/panel-analysis-$panelName.json" -ForegroundColor Cyan
}

if ($issues.Count -gt 0) { exit 1 } else { exit 0 }
