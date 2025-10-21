#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Categorizes build errors from build-errors.log into actionable groups
.DESCRIPTION
    Parses MSBuild error log and creates categorized report with:
    - Error counts by category (CS errors, warnings, missing types)
    - Priority ranking for fixes
    - Quick reference to error types
    - Efficiency metrics for targeting fixes
#>

param(
    [string]$LogFile = "$PSScriptRoot/../build-errors.log",
    [string]$OutputFile = "$PSScriptRoot/../build-errors-categorized.log",
    [switch]$ShowReport
)

$ErrorActionPreference = "Continue"

# Check if log file exists
if (-not (Test-Path $LogFile)) {
    Write-Information "ℹ️  No build errors log found. Will be generated after build." -InformationAction Continue
    exit 0
}

# Initialize error categories
$categories = @{
    "MissingTypes"        = @()
    "PropertyMismatch"    = @()
    "MethodNotFound"      = @()
    "TypeConversion"      = @()
    "NullabilityWarnings" = @()
    "AsyncIssues"         = @()
    "VisualStylesIssues"  = @()
    "NavigationIssues"    = @()
    "ConstructorIssues"   = @()
    "Other"               = @()
}

$errorCounts = @{}
$warningCounts = @{}

# Parse log file
$lines = Get-Content $LogFile

foreach ($line in $lines) {
    # Extract error/warning code
    if ($line -match "(error|warning) (CS\d+|NETSDK\d+)") {
        $severity = $matches[1]
        $code = $matches[2]

        if ($severity -eq "error") {
            if (-not $errorCounts.ContainsKey($code)) {
                $errorCounts[$code] = 0
            }
            $errorCounts[$code]++
        }
        else {
            if (-not $warningCounts.ContainsKey($code)) {
                $warningCounts[$code] = 0
            }
            $warningCounts[$code]++
        }

        # Categorize by error pattern
        switch -Regex ($line) {
            "does not exist in the current context|could not be found" {
                $categories["MissingTypes"] += $line
            }
            "does not contain a definition for|is inaccessible" {
                $categories["PropertyMismatch"] += $line
            }
            "cannot convert from|cannot implicitly convert" {
                $categories["TypeConversion"] += $line
            }
            "CS8600|CS8601|CS8602|CS8603|CS8604|CS8618" {
                $categories["NullabilityWarnings"] += $line
            }
            "async|await|Task" {
                $categories["AsyncIssues"] += $line
            }
            "VisualStyles|VisualStyleState" {
                $categories["VisualStylesIssues"] += $line
            }
            "NavigationRequestEventArgs" {
                $categories["NavigationIssues"] += $line
            }
            "constructor" {
                $categories["ConstructorIssues"] += $line
            }
            default {
                $categories["Other"] += $line
            }
        }
    }
}

# Generate report
$report = @()
$report += "╔═══════════════════════════════════════════════════════════════════╗"
$report += "║          WILEY WIDGET BUILD ERROR ANALYSIS REPORT                 ║"
$report += "╚═══════════════════════════════════════════════════════════════════╝"
$report += ""
$report += "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$report += "Source: $LogFile"
$report += ""

# Summary statistics
$totalErrors = ($errorCounts.Values | Measure-Object -Sum).Sum
$totalWarnings = ($warningCounts.Values | Measure-Object -Sum).Sum

$report += "┌─────────────────────────────────────────────────────────────────┐"
$report += "│ SUMMARY STATISTICS                                              │"
$report += "├─────────────────────────────────────────────────────────────────┤"
$report += "│ Total Errors:   $($totalErrors.ToString().PadLeft(3))                                               │"
$report += "│ Total Warnings: $($totalWarnings.ToString().PadLeft(3))                                               │"
$report += "│ Unique Error Codes: $($errorCounts.Count.ToString().PadLeft(2))                                          │"
$report += "│ Unique Warning Codes: $($warningCounts.Count.ToString().PadLeft(2))                                        │"
$report += "└─────────────────────────────────────────────────────────────────┘"
$report += ""

# Top errors by frequency
if ($errorCounts.Count -gt 0) {
    $report += "┌─────────────────────────────────────────────────────────────────┐"
    $report += "│ TOP ERRORS BY FREQUENCY                                         │"
    $report += "├──────────┬──────────────────────────────────────────────────────┤"
    $report += "│ Code     │ Count │ Description                                   │"
    $report += "├──────────┼───────┼───────────────────────────────────────────────┤"

    $topErrors = $errorCounts.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 10
    foreach ($err in $topErrors) {
        $desc = switch ($err.Key) {
            "CS0103" { "Name does not exist" }
            "CS1061" { "Missing member/property" }
            "CS0246" { "Type not found" }
            "CS1503" { "Argument type mismatch" }
            "CS0117" { "Missing method" }
            "CS0029" { "Cannot convert type" }
            "CS8602" { "Possible null reference" }
            "CS8604" { "Possible null argument" }
            default { "See documentation" }
        }
        $report += "│ $($err.Key.PadRight(8)) │ $($err.Value.ToString().PadLeft(5)) │ $($desc.PadRight(45)) │"
    }
    $report += "└──────────┴───────┴───────────────────────────────────────────────┘"
    $report += ""
}

# Category breakdown with priority
$report += "┌─────────────────────────────────────────────────────────────────┐"
$report += "│ ERROR CATEGORIES (Prioritized for Fix Efficiency)              │"
$report += "├──────────────────────────────────┬───────┬──────────────────────┤"
$report += "│ Category                         │ Count │ Priority             │"
$report += "├──────────────────────────────────┼───────┼──────────────────────┤"

$categoryPriority = @{
    "MissingTypes"        = "HIGH"
    "MethodNotFound"      = "HIGH"
    "PropertyMismatch"    = "HIGH"
    "ConstructorIssues"   = "MEDIUM"
    "TypeConversion"      = "MEDIUM"
    "VisualStylesIssues"  = "MEDIUM"
    "NavigationIssues"    = "MEDIUM"
    "AsyncIssues"         = "LOW"
    "NullabilityWarnings" = "LOW"
    "Other"               = "REVIEW"
}

foreach ($cat in $categories.Keys | Sort-Object { $categoryPriority[$_] }) {
    $count = $categories[$cat].Count
    if ($count -gt 0) {
        $priority = $categoryPriority[$cat]
        $priorityColor = switch ($priority) {
            "HIGH" { "🔴" }
            "MEDIUM" { "🟡" }
            "LOW" { "🟢" }
            default { "⚪" }
        }
        $report += "│ $($cat.PadRight(32)) │ $($count.ToString().PadLeft(5)) │ $priorityColor $($priority.PadRight(15)) │"
    }
}
$report += "└──────────────────────────────────┴───────┴──────────────────────┘"
$report += ""

# Actionable recommendations
$report += "┌─────────────────────────────────────────────────────────────────┐"
$report += "│ RECOMMENDED FIX STRATEGY                                        │"
$report += "└─────────────────────────────────────────────────────────────────┘"
$report += ""

if ($categories["MissingTypes"].Count -gt 0) {
    $report += "🔴 HIGH PRIORITY: Missing Types ($($categories['MissingTypes'].Count) errors)"
    $report += "   → Check 'using' statements and namespace references"
    $report += "   → Verify NuGet packages are restored"
    $report += "   → Run: dotnet restore"
    $report += ""
}

if ($categories["PropertyMismatch"].Count -gt 0) {
    $report += "🔴 HIGH PRIORITY: Property/Method Mismatches ($($categories['PropertyMismatch'].Count) errors)"
    $report += "   → Review model property names vs. usage"
    $report += "   → Check for typos or renamed properties"
    $report += "   → Use IDE's 'Find All References' feature"
    $report += ""
}

if ($categories["MethodNotFound"].Count -gt 0) {
    $report += "🔴 HIGH PRIORITY: Missing Methods ($($categories['MethodNotFound'].Count) errors)"
    $report += "   → Implement missing service/repository methods"
    $report += "   → Check interface implementations"
    $report += "   → Review method signatures"
    $report += ""
}

if ($categories["TypeConversion"].Count -gt 0) {
    $report += "🟡 MEDIUM PRIORITY: Type Conversion Issues ($($categories['TypeConversion'].Count) errors)"
    $report += "   → Add explicit casts where needed"
    $report += "   → Review enum/int conversions"
    $report += "   → Check VisualStyles type mappings"
    $report += ""
}

if ($categories["NullabilityWarnings"].Count -gt 0) {
    $report += "🟢 LOW PRIORITY: Nullability Warnings ($($categories['NullabilityWarnings'].Count) warnings)"
    $report += "   → Add null checks (if statements)"
    $report += "   → Use null-coalescing operators (??)"
    $report += "   → Consider nullable reference types"
    $report += ""
}

$report += "┌─────────────────────────────────────────────────────────────────┐"
$report += "│ QUICK COMMANDS                                                  │"
$report += "└─────────────────────────────────────────────────────────────────┘"
$report += ""
$report += "View full error log:"
$report += "  cat build-errors.log"
$report += ""
$report += "Fix specific category:"
$report += "  pwsh scripts/fix-all-build-errors.ps1"
$report += ""
$report += "Clean and rebuild:"
$report += "  dotnet clean && dotnet build"
$report += ""

# Write report
$report | Out-File $OutputFile -Encoding utf8
Write-Information "✅ Build error analysis complete: $OutputFile" -InformationAction Continue

if ($ShowReport) {
    $report | Write-Output
}

# Return categorized data for other scripts
return @{
    TotalErrors   = $totalErrors
    TotalWarnings = $totalWarnings
    Categories    = $categories
    ErrorCounts   = $errorCounts
    WarningCounts = $warningCounts
}
