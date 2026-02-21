#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Comprehensive verification of all "dead code" methods
.DESCRIPTION
    Cross-references each reported method with its actual usage patterns
#>

$ErrorActionPreference = 'Stop'
$jsonPath = "tmp/dead-code-report.json"

if (-not (Test-Path $jsonPath)) {
    Write-Error "Run Find-DeadCode.ps1 first to generate $jsonPath"
    exit 1
}

$json = Get-Content $jsonPath | ConvertFrom-Json
$totalMethods = $json.UnusedMethods.Count
$verified = @()
$genuinelyUnused = @()
$progress = 0

Write-Host "`n🔍 Verifying $totalMethods methods..." -ForegroundColor Cyan
Write-Host "━" * 60

foreach ($method in $json.UnusedMethods) {
    $progress++
    $pct = [math]::Round(($progress / $totalMethods) * 100)
    Write-Progress -Activity "Verifying methods" -Status "$progress/$totalMethods ($pct%)" -PercentComplete $pct

    $methodName = $method.MethodName
    $file = $method.File

    if (-not (Test-Path $file)) {
        Write-Warning "File not found: $file"
        continue
    }

    $content = Get-Content $file -Raw

    # Pattern 1: Direct event subscription (+=)
    if ($content -match "(\+=\s*$methodName|\.$methodName\s*\+=)") {
        $verified += [PSCustomObject]@{
            Method = $methodName
            File = ($method.RelativePath -replace '^src\\WileyWidget\.WinForms\\', '')
            Pattern = "Event subscription (+=)"
        }
        continue
    }

    # Pattern 2: Handler assignment
    if ($content -match "=\s*$methodName;") {
        $verified += [PSCustomObject]@{
            Method = $methodName
            File = ($method.RelativePath -replace '^src\\WileyWidget\.WinForms\\', '')
            Pattern = "Handler assignment (=)"
        }
        continue
    }

    # Pattern 3: Event handler in one line
    if ($content -match "\+=\s*\w+\s*=\s*$methodName") {
        $verified += [PSCustomObject]@{
            Method = $methodName
            File = ($method.RelativePath -replace '^src\\WileyWidget\.WinForms\\', '')
            Pattern = "Inline handler assignment"
        }
        continue
    }

    # Pattern 4: new EventHandler(methodName)
    if ($content -match "new\s+\w+EventHandler\(\s*$methodName") {
        $verified += [PSCustomObject]@{
            Method = $methodName
            File = ($method.RelativePath -replace '^src\\WileyWidget\.WinForms\\', '')
            Pattern = "EventHandler constructor"
        }
        continue
    }

    # Pattern 5: RelayCommand attribute
    if ($content -match "\[RelayCommand\][\s\S]{0,50}$methodName") {
        $verified += [PSCustomObject]@{
            Method = $methodName
            File = ($method.RelativePath -replace '^src\\WileyWidget\.WinForms\\', '')
            Pattern = "[RelayCommand] attribute"
        }
        continue
    }

    # Pattern 6: Check Designer file
    $designerFile = $file -replace '\.cs$', '.Designer.cs'
    if (Test-Path $designerFile) {
        $designerContent = Get-Content $designerFile -Raw
        if ($designerContent -match $methodName) {
            $verified += [PSCustomObject]@{
                Method = $methodName
                File = ($method.RelativePath -replace '^src\\WileyWidget\.WinForms\\', '')
                Pattern = "Designer.cs registration"
            }
            continue
        }
    }

    # If we get here, method is genuinely unused
    $genuinelyUnused += [PSCustomObject]@{
        Method = $methodName
        File = ($method.RelativePath -replace '^src\\WileyWidget\.WinForms\\', '')
        Line = $method.LineNumber
        Declaration = $method.Declaration
    }
}

Write-Progress -Activity "Verifying methods" -Completed

# Results
Write-Host "`n" + ("━" * 60)
Write-Host "📊 VERIFICATION RESULTS" -ForegroundColor Cyan
Write-Host ("━" * 60)

Write-Host "`n✅ Verified (already wired up): " -NoNewline -ForegroundColor Green
Write-Host $verified.Count -ForegroundColor White

Write-Host "❌ Genuinely unused (need wiring): " -NoNewline -ForegroundColor Red
Write-Host $genuinelyUnused.Count -ForegroundColor White

if ($genuinelyUnused.Count -gt 0) {
    Write-Host "`n🚨 METHODS NEEDING IMPLEMENTATION:" -ForegroundColor Red
    Write-Host ("━" * 60)
    $genuinelyUnused | Format-Table -AutoSize

    # Save to file
    $outputPath = "tmp/methods-to-implement.json"
    $genuinelyUnused | ConvertTo-Json -Depth 3 | Out-File $outputPath
    Write-Host "`n💾 Saved to: $outputPath" -ForegroundColor Yellow
} else {
    Write-Host "`n🎉 All methods are properly wired up!" -ForegroundColor Green
    Write-Host "   No implementation needed." -ForegroundColor Gray
}

# Show sample of verified methods
if ($verified.Count -gt 0) {
    Write-Host "`n📋 Sample verified methods (first 10):" -ForegroundColor Cyan
    $verified | Select-Object -First 10 | Format-Table -AutoSize
}
