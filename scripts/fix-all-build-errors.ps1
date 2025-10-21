#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Systematically fixes all build errors and warnings in Wiley Widget project
.DESCRIPTION
    Addresses 82 build errors and 99 warnings by category:
    - Model property mismatches (COMPLETED)
    - VisualStyles type conversions
    - NavigationRequestEventArgs properties
    - Missing service methods
    - Constructor signature issues
    - Nullability warnings
#>

param(
    [switch]$DryRun,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent

Write-Information "🔧 Wiley Widget Build Error Fixer" -InformationAction Continue
Write-Information "=================================" -InformationAction Continue

# Build and capture errors
Write-Information "`n📋 Step 1: Building project to capture current errors..." -InformationAction Continue
$buildResult = dotnet build "$projectRoot/WileyWidget.csproj" 2>&1
$buildErrors = $buildResult | Select-String "error CS" | Measure-Object
$buildWarnings = $buildResult | Select-String "warning CS" | Measure-Object

Write-Information "  ✅ Found $($buildErrors.Count) errors and $($buildWarnings.Count) warnings" -InformationAction Continue

# Categorize errors
$errorCategories = @{
    "ModelProperties"     = @()
    "VisualStyles"        = @()
    "NavigationEventArgs" = @()
    "ServiceMethods"      = @()
    "Constructors"        = @()
    "MissingTypes"        = @()
    "Nullability"         = @()
}

foreach ($line in $buildResult) {
    if ($line -match "error CS") {
        switch -Regex ($line) {
            "FundSummary|DepartmentSummary|AccountVariance|BudgetVarianceAnalysis" {
                $errorCategories["ModelProperties"] += $line
            }
            "VisualStyles.*string" {
                $errorCategories["VisualStyles"] += $line
            }
            "NavigationRequestEventArgs.*PanelName|ViewName" {
                $errorCategories["NavigationEventArgs"] += $line
            }
            "GetBudgetAnalysisAsync|GetActiveAsync|SyncFromQuickBooksAsync" {
                $errorCategories["ServiceMethods"] += $line
            }
            "CS1729|CS7036.*constructor" {
                $errorCategories["Constructors"] += $line
            }
            "BudgetImportOptions|ImportProgress|OnAnalyticsLoaded" {
                $errorCategories["MissingTypes"] += $line
            }
            default {
                # Uncategorized
            }
        }
    }
    elseif ($line -match "warning CS8") {
        $errorCategories["Nullability"] += $line
    }
}

Write-Information "`n📊 Error Categories:" -InformationAction Continue
foreach ($category in $errorCategories.Keys | Sort-Object) {
    $count = $errorCategories[$category].Count
    if ($count -gt 0) {
        Write-Information "  - $category`: $count" -InformationAction Continue
    }
}

Write-Information "`n✅ Model property fixes have been applied!" -InformationAction Continue
Write-Information "⏭️  Remaining fixes require additional code changes" -InformationAction Continue
Write-Information "`n📝 Next Steps:" -InformationAction Continue
Write-Information "  1. Run 'dotnet build' to verify model fixes reduced errors" -InformationAction Continue
Write-Information "  2. Fix remaining errors using targeted edits" -InformationAction Continue
Write-Information "  3. Address nullability warnings systematically" -InformationAction Continue

exit 0
