#Requires -Version 7.0
<#
.SYNOPSIS
    Deep diagnostic for RibbonControlAdv navigation and DockingManager panel visibility issues
.DESCRIPTION
    Analyzes the navigation flow, DockingManager configuration, and panel visibility state
#>

Write-Host "`n🔍 DEEP DIAGNOSTIC: Navigation & Panel Visibility`n" -ForegroundColor Cyan

# Read latest log file
$latestLog = Get-ChildItem "logs\wiley-widget-*.log" -ErrorAction SilentlyContinue | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1

if (-not $latestLog) {
    Write-Host "❌ No log files found in logs\ directory" -ForegroundColor Red
    Write-Host "   Run the application first to generate logs" -ForegroundColor Yellow
    exit 1
}

Write-Host "📄 Analyzing log: $($latestLog.Name)" -ForegroundColor Gray
Write-Host "   Last modified: $($latestLog.LastWriteTime)`n" -ForegroundColor Gray

$logContent = Get-Content $latestLog.FullName -Raw

# 1. Check if navigation was attempted
Write-Host "1️⃣  Navigation Attempts:" -ForegroundColor Yellow
$navAttempts = [regex]::Matches($logContent, '\[RIBBON_NAV\] Navigating to panel (.+?) \(Type=(.+?), Dock="(.+?)"\)')

if ($navAttempts.Count -eq 0) {
    Write-Host "   ❌ NO navigation attempts found - ribbon buttons may not be wired up" -ForegroundColor Red
} else {
    Write-Host "   ✅ Found $($navAttempts.Count) navigation attempts:" -ForegroundColor Green
    foreach ($match in $navAttempts | Select-Object -First 5) {
        $panelName = $match.Groups[1].Value
        $panelType = $match.Groups[2].Value
        $dockStyle = $match.Groups[3].Value
        Write-Host "      - $panelName ($panelType, Dock=$dockStyle)" -ForegroundColor Gray
    }
}

# 2. Check if panels were docked successfully
Write-Host "`n2️⃣  Panel Docking Results:" -ForegroundColor Yellow
$dockedPanels = [regex]::Matches($logContent, 'Panel (.+?) docked successfully')

if ($dockedPanels.Count -eq 0) {
    Write-Host "   ❌ NO panels docked successfully - docking failure" -ForegroundColor Red
} else {
    Write-Host "   ✅ $($dockedPanels.Count) panels docked successfully:" -ForegroundColor Green
    foreach ($match in $dockedPanels | Select-Object -First 5) {
        Write-Host "      - $($match.Groups[1].Value)" -ForegroundColor Gray
    }
}

# 3. Check if visibility was applied
Write-Host "`n3️⃣  Panel Visibility Configuration:" -ForegroundColor Yellow
$visibilityLogs = @(
    @{ Pattern = 'Left dock panel set to visible'; Name = 'Left Panel' },
    @{ Pattern = 'Right dock panel set to visible'; Name = 'Right Panel' },
    @{ Pattern = 'Central document panel set to visible'; Name = 'Central Panel' }
)

foreach ($check in $visibilityLogs) {
    if ($logContent -match $check.Pattern) {
        Write-Host "   ✅ $($check.Name) visibility applied" -ForegroundColor Green
    } else {
        Write-Host "   ❌ $($check.Name) visibility NOT logged (may still be hidden!)" -ForegroundColor Red
    }
}

# 4. Check for navigation errors
Write-Host "`n4️⃣  Navigation Errors:" -ForegroundColor Yellow
$errors = [regex]::Matches($logContent, '\[ERR\].*(?:navigation|panel|docking).*', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

if ($errors.Count -eq 0) {
    Write-Host "   ✅ No navigation-related errors found" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Found $($errors.Count) errors:" -ForegroundColor Red
    foreach ($err in $errors | Select-Object -First 3) {
        Write-Host "      - $($err.Value)" -ForegroundColor Gray
    }
}

# 5. Check readiness gate status
Write-Host "`n5️⃣  Docking Readiness:" -ForegroundColor Yellow
if ($logContent -match 'readiness=true') {
    Write-Host "   ✅ DockingManager marked as ready for mutations" -ForegroundColor Green
} else {
    Write-Host "   ❌ DockingManager never reached ready state" -ForegroundColor Red
}

if ($logContent -match 'Force-marked docking as ready') {
    Write-Host "   ✅ Force-mark was triggered (bypassed readiness gate)" -ForegroundColor Green
}

# 6. Check if panels have valid dimensions
Write-Host "`n6️⃣  Checking for size/dimension issues..." -ForegroundColor Yellow
if ($logContent -match 'Width=0|Height=0|Size=\{Width=0') {
    Write-Host "   ⚠️  Found zero-size panels - THIS COULD BE THE ISSUE!" -ForegroundColor Red
    $zeroSizeMatches = [regex]::Matches($logContent, '(?:Width|Height|Size)=.*?0')
    foreach ($match in $zeroSizeMatches | Select-Object -First 3) {
        Write-Host "      - $($match.Value)" -ForegroundColor Gray
    }
} else {
    Write-Host "   ✅ No obvious zero-size issues found in logs" -ForegroundColor Green
}

# 7. Summary
Write-Host "`n📊 DIAGNOSTIC SUMMARY:`n" -ForegroundColor Cyan

$issues = @()

if ($navAttempts.Count -eq 0) { $issues += "No navigation attempts - ribbon buttons not wired" }
if ($dockedPanels.Count -eq 0) { $issues += "No panels docked - docking mechanism broken" }
if ($logContent -notmatch 'Left dock panel set to visible') { $issues += "Left panel visibility NOT applied" }
if ($logContent -notmatch 'Right dock panel set to visible') { $issues += "Right panel visibility NOT applied" }

if ($issues.Count -eq 0) {
    Write-Host "✅ Navigation appears to be working correctly in logs!" -ForegroundColor Green
    Write-Host "`nPossible causes of invisible panels:" -ForegroundColor Yellow
    Write-Host "   1. Z-Order: Panels docked but behind other controls" -ForegroundColor White
    Write-Host "   2. Size: Panels have zero width/height" -ForegroundColor White
    Write-Host "   3. Parent: Parent container is hidden or has zero size" -ForegroundColor White
    Write-Host "   4. Timing: Panels created before form is fully shown" -ForegroundColor White
    
    Write-Host "`n🔧 Recommended Next Steps:" -ForegroundColor Cyan
    Write-Host "   1. Run app and click a navigation button" -ForegroundColor White
    Write-Host "   2. Use Snoop or Inspect.exe to check control tree" -ForegroundColor White
    Write-Host "   3. Check if panels have valid Bounds (X, Y, Width, Height)" -ForegroundColor White
    Write-Host "   4. Verify parent container hierarchy visibility" -ForegroundColor White
} else {
    Write-Host "❌ Found issues in navigation flow:" -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host "   - $issue" -ForegroundColor Yellow
    }
}

Write-Host "`nDone! 🎉`n" -ForegroundColor Green
