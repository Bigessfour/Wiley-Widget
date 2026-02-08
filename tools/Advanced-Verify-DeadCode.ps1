#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Advanced verification detecting nameof(), string references, and other patterns
#>

$ErrorActionPreference = 'Stop'
$methodsPath = "tmp/methods-to-implement.json"

if (-not (Test-Path $methodsPath)) {
    Write-Error "Run Verify-AllDeadCode.ps1 first"
    exit 1
}

$methods = Get-Content $methodsPath | ConvertFrom-Json
$total = $methods.Count
$truelyUnused = @()
$falsePositives = @()
$progress = 0

Write-Host "`n🔍 Advanced verification (nameof, strings, any reference)..." -ForegroundColor Cyan
Write-Host ("━" * 70)

foreach ($method in $methods) {
    $progress++
    $pct = [math]::Round(($progress / $total) * 100)
    Write-Progress -Activity "Advanced scan" -Status "$progress/$total ($pct%)" -PercentComplete $pct

    $name = $method.Method
    $file = $method.File
    $fullPath = Join-Path "src\WileyWidget.WinForms" $file

    if (-not (Test-Path $fullPath)) {
        Write-Warning "Skipping missing file: $fullPath"
        continue
    }

    $content = Get-Content $fullPath -Raw

    # Check for ANY reference (very broad)
    $patterns = @(
        "nameof\(\s*$name\s*\)",# nameof(Method)
        "\""$name\""",                      # "Method" in strings
        "'$name'",                          # 'Method' in strings
        "(\+=|=)\s*$name\s*;",             # += Method; or = Method;
        "new\s+\w+\(\s*$name\s*\)",        # new Handler(Method)
        "\.$name\s*[\(;]",                 # .Method( or .Method;
        "\s+$name\s*\(",                   # Direct call Method(
        "CanExecute.*$name"                 # CanExecute patterns
    )

    $foundAny = $false
    $foundPattern = ""

    foreach ($pattern in $patterns) {
        if ($content -match $pattern) {
            $foundAny = $true
            $foundPattern = $pattern -replace '\s+', ' '
            break
        }
    }

    if ($foundAny) {
        $falsePositives += [PSCustomObject]@{
            Method = $name
            File = $file
            DetectedVia = $foundPattern
        }
    } else {
        $truelyUnused += [PSCustomObject]@{
            Method = $name
            File = $file
            Line = $method.Line
            Declaration = $method.Declaration
        }
    }
}

Write-Progress -Activity "Advanced scan" -Completed

# Results
Write-Host "`n" + ("━" * 70)
Write-Host "📊 ADVANCED VERIFICATION RESULTS" -ForegroundColor Cyan
Write-Host ("━" * 70)

Write-Host "`n✅ False Positives (actually used): " -NoNewline -ForegroundColor Green
Write-Host $falsePositives.Count -ForegroundColor White

Write-Host "❌ TRULY UNUSED (genuinely need action): " -NoNewline -ForegroundColor Red
Write-Host $truelyUnused.Count -ForegroundColor White

if ($truelyUnused.Count -gt 0) {
    Write-Host "`n🚨 METHODS GENUINELY NEEDING ACTION:" -ForegroundColor Red
    Write-Host ("━" * 70)
    $truelyUnused | Format-Table Method, File, Line -AutoSize

    $outputPath = "tmp/truly-unused-methods.json"
    $truelyUnused | ConvertTo-Json -Depth 3 | Out-File $outputPath
    Write-Host "`n💾 Saved to: $outputPath" -ForegroundColor Yellow
} else {
    Write-Host "`n🎉 ALL METHODS ARE ACTUALLY USED!" -ForegroundColor Green
    Write-Host "   Every method is referenced somewhere (events, commands, nameof, etc.)" -ForegroundColor Gray
}

if ($falsePositives.Count -gt 10) {
    Write-Host "`n📋 Sample false positives (first 10):" -ForegroundColor Cyan
    $falsePositives | Select-Object -First 10 | Format-Table Method, File, DetectedVia -AutoSize
} elseif ($falsePositives.Count -gt 0) {
    Write-Host "`n📋 All false positives:" -ForegroundColor Cyan
    $falsePositives | Format-Table Method, File, DetectedVia -AutoSize
}

# Summary
Write-Host "`n" + ("━" * 70)
Write-Host "📊 FINAL SUMMARY" -ForegroundColor Cyan
Write-Host ("━" * 70)
Write-Host "Total methods scanned: $total"
Write-Host "  - False Positives: $($falsePositives.Count) (scanner limitations)"
Write-Host "  - Truly Unused: $($truelyUnused.Count) (need implementation/deletion)"
