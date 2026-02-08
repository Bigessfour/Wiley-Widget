<#
.SYNOPSIS
    Interactive tool to evaluate and triage dead code findings
.DESCRIPTION
    Helps you decide whether to keep, implement, or delete each detected unused method
.PARAMETER ReportPath
    Path to the dead code report JSON file
#>

param(
    [string]$ReportPath = "tmp/dead-code-report.json"
)

if (-not (Test-Path $ReportPath)) {
    Write-Host "❌ Report not found: $ReportPath" -ForegroundColor Red
    Write-Host "Run 'Find-DeadCode.ps1' first to generate the report." -ForegroundColor Yellow
    exit 1
}

Write-Host "`n🔍 Dead Code Evaluator v1.0" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════`n" -ForegroundColor Gray

# Load report
$report = Get-Content $ReportPath -Raw | ConvertFrom-Json
$unusedMethods = $report.UnusedMethods

if ($unusedMethods.Count -eq 0) {
    Write-Host "✅ No unused methods found!" -ForegroundColor Green
    exit 0
}

Write-Host "📊 Found $($unusedMethods.Count) potentially unused methods" -ForegroundColor Yellow
Write-Host "Let's evaluate them systematically...`n" -ForegroundColor Gray

# Categories for triage
$decisions = @{
    Keep = @()           # False positive - actually used
    Implement = @()      # Should be connected/wired up
    Delete = @()         # True dead code - remove it
    Review = @()         # Needs manual review
    Skip = @()           # Skip for now
}

# Auto-categorization rules
function Get-AutoCategory {
    param($method)

    $fileName = [System.IO.Path]::GetFileName($method.RelativePath)
    $methodName = $method.MethodName

    # Event handler patterns
    if ($methodName -match '_Click$|_Load$|_Changed$|_Closing$|_Closed$|_Activated$|_KeyDown$|_KeyPress$') {
        # Check if it's in a Designer file or panel
        if ($fileName -match 'Panel\.cs$|Dialog\.cs$|Form\.cs$|Control\.cs$') {
            return "LIKELY_EVENT_HANDLER"
        }
    }

    # ViewModel command methods
    if ($methodName -match '^Can[A-Z]|Async$' -and $method.RelativePath -match 'ViewModel') {
        return "LIKELY_COMMAND_METHOD"
    }

    # Helper/utility methods
    if ($methodName -match '^Create|^Build|^Generate|^Calculate|^Update|^Apply') {
        return "HELPER_METHOD"
    }

    # Old/legacy patterns
    if ($methodName -match '^Old|^Legacy|^Deprecated|^Unused|^Test[0-9]') {
        return "LEGACY_CODE"
    }

    return "UNKNOWN"
}

# Check if method is likely used
function Test-MethodUsage {
    param($method)

    $file = $method.File
    $methodName = $method.MethodName

    # Check Designer file for event subscriptions
    $designerFile = $file -replace '\.cs$', '.Designer.cs'
    if (Test-Path $designerFile) {
        $designerContent = Get-Content $designerFile -Raw
        if ($designerContent -match "\+= new.*\(\s*this\.$methodName\s*\)") {
            return "WIRED_IN_DESIGNER"
        }
    }

    # Check for [RelayCommand] attribute nearby
    $content = Get-Content $file -Raw
    $pattern = "\[RelayCommand[^\]]*\]\s*(?:private|public)\s+\w+\s+$methodName"
    if ($content -match $pattern) {
        return "HAS_RELAY_COMMAND"
    }

    # Check for reflection usage
    $allFiles = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse
    $reflectionPattern = "GetMethod\(`"$methodName`"|nameof\($methodName\)"
    $reflectionUsage = $allFiles | Select-String -Pattern $reflectionPattern -Quiet
    if ($reflectionUsage) {
        return "CALLED_VIA_REFLECTION"
    }

    return $null
}

Write-Host "📝 Auto-categorizing methods...`n" -ForegroundColor Cyan

$analyzed = 0
foreach ($method in $unusedMethods) {
    $analyzed++
    Write-Progress -Activity "Analyzing methods" -Status "$analyzed of $($unusedMethods.Count)" -PercentComplete (($analyzed / $unusedMethods.Count) * 100)

    $category = Get-AutoCategory -method $method
    $usage = Test-MethodUsage -method $method

    $method | Add-Member -NotePropertyName "Category" -NotePropertyValue $category -Force
    $method | Add-Member -NotePropertyName "UsageCheck" -NotePropertyValue $usage -Force

    # Auto-decide based on usage check
    if ($usage -in @("WIRED_IN_DESIGNER", "HAS_RELAY_COMMAND", "CALLED_VIA_REFLECTION")) {
        $decisions.Keep += $method
    }
    elseif ($category -eq "LEGACY_CODE") {
        $decisions.Delete += $method
    }
    else {
        $decisions.Review += $method
    }
}

Write-Progress -Activity "Analyzing methods" -Completed

# Display results
Write-Host "`n📊 Auto-Categorization Results" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════`n" -ForegroundColor Gray

Write-Host "✅ Keep (False Positives): $($decisions.Keep.Count)" -ForegroundColor Green
foreach ($m in $decisions.Keep | Select-Object -First 5) {
    Write-Host "   $($m.RelativePath):$($m.LineNumber) - $($m.MethodName) [$($m.UsageCheck)]" -ForegroundColor Gray
}
if ($decisions.Keep.Count -gt 5) {
    Write-Host "   ... and $($decisions.Keep.Count - 5) more" -ForegroundColor DarkGray
}

Write-Host "`n🗑️  Delete (Legacy Code): $($decisions.Delete.Count)" -ForegroundColor Red
foreach ($m in $decisions.Delete | Select-Object -First 5) {
    Write-Host "   $($m.RelativePath):$($m.LineNumber) - $($m.MethodName)" -ForegroundColor Gray
}
if ($decisions.Delete.Count -gt 5) {
    Write-Host "   ... and $($decisions.Delete.Count - 5) more" -ForegroundColor DarkGray
}

Write-Host "`n👀 Needs Review: $($decisions.Review.Count)" -ForegroundColor Yellow
foreach ($m in $decisions.Review | Select-Object -First 10) {
    Write-Host "   $($m.RelativePath):$($m.LineNumber) - $($m.MethodName) [$($m.Category)]" -ForegroundColor Gray
}
if ($decisions.Review.Count -gt 10) {
    Write-Host "   ... and $($decisions.Review.Count - 10) more" -ForegroundColor DarkGray
}

# Interactive review for uncertain cases
if ($decisions.Review.Count -gt 0) {
    Write-Host "`n🔍 Interactive Review" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════`n" -ForegroundColor Gray

    $response = Read-Host "Do you want to review uncertain methods interactively? (y/N)"

    if ($response -eq 'y' -or $response -eq 'Y') {
        foreach ($method in $decisions.Review) {
            Write-Host "`n📄 $($method.RelativePath):$($method.LineNumber)" -ForegroundColor Cyan
            Write-Host "   Method: $($method.MethodName)" -ForegroundColor White
            Write-Host "   Category: $($method.Category)" -ForegroundColor Gray
            Write-Host "   Declaration: $($method.Declaration)" -ForegroundColor DarkGray

            Write-Host "`n   Options:" -ForegroundColor Yellow
            Write-Host "   [K]eep (false positive)" -ForegroundColor Green
            Write-Host "   [I]mplement (needs to be wired up)" -ForegroundColor Blue
            Write-Host "   [D]elete (true dead code)" -ForegroundColor Red
            Write-Host "   [S]kip (decide later)" -ForegroundColor Gray
            Write-Host "   [Q]uit review" -ForegroundColor DarkGray

            $decision = Read-Host "`n   Your decision"

            switch ($decision.ToLower()) {
                'k' { $decisions.Keep += $method }
                'i' { $decisions.Implement += $method }
                'd' { $decisions.Delete += $method }
                's' { $decisions.Skip += $method }
                'q' {
                    Write-Host "`n   Stopping review..." -ForegroundColor Yellow
                    break
                }
                default {
                    Write-Host "   Invalid choice, skipping..." -ForegroundColor Red
                    $decisions.Skip += $method
                }
            }
        }
    }
}

# Generate action report
$actionReportPath = "tmp/dead-code-actions.json"
$actionReport = @{
    EvaluatedDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    TotalMethods = $unusedMethods.Count
    Keep = $decisions.Keep
    Implement = $decisions.Implement
    Delete = $decisions.Delete
    Review = $decisions.Review
    Skip = $decisions.Skip
}
$actionReport | ConvertTo-Json -Depth 5 | Out-File -FilePath $actionReportPath -Encoding UTF8

Write-Host "`n📊 Final Summary" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Gray
Write-Host "✅ Keep (False Positives):   $($decisions.Keep.Count)" -ForegroundColor Green
Write-Host "🔧 Implement (Wire Up):      $($decisions.Implement.Count)" -ForegroundColor Blue
Write-Host "🗑️  Delete (True Dead Code): $($decisions.Delete.Count)" -ForegroundColor Red
Write-Host "👀 Still Under Review:       $($decisions.Review.Count + $decisions.Skip.Count)" -ForegroundColor Yellow

Write-Host "`n💾 Action report saved: $actionReportPath" -ForegroundColor Green

# Generate deletion script if there are methods to delete
if ($decisions.Delete.Count -gt 0) {
    Write-Host "`n⚠️  Deletion Script" -ForegroundColor Yellow
    $response = Read-Host "Generate script to delete confirmed dead code? (y/N)"

    if ($response -eq 'y' -or $response -eq 'Y') {
        $deleteScriptPath = "tmp/delete-dead-code.ps1"
        $deleteScript = @"
# Auto-generated script to delete confirmed dead code
# Review carefully before executing!

Write-Host "Deleting $($decisions.Delete.Count) confirmed dead code methods..." -ForegroundColor Yellow

"@

        foreach ($method in $decisions.Delete) {
            $deleteScript += @"

# $($method.RelativePath):$($method.LineNumber) - $($method.MethodName)
# Manual deletion required - use VS Code to remove method body

"@
        }

        $deleteScript | Out-File -FilePath $deleteScriptPath -Encoding UTF8
        Write-Host "   Deletion guide saved: $deleteScriptPath" -ForegroundColor Gray
        Write-Host "   (Manual review and deletion recommended)" -ForegroundColor DarkGray
    }
}

Write-Host "`n✅ Evaluation Complete!" -ForegroundColor Green
Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "  1. Review the action report: $actionReportPath" -ForegroundColor Gray
Write-Host "  2. Implement methods marked for wiring up" -ForegroundColor Gray
Write-Host "  3. Delete confirmed dead code" -ForegroundColor Gray
Write-Host "  4. Re-run Find-DeadCode.ps1 to verify cleanup`n" -ForegroundColor Gray
