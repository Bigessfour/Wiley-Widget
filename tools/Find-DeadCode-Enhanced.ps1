#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Enhanced dead code scanner with false positive reduction and performance improvements
.DESCRIPTION
    Detects potentially unused methods while handling RelayCommand, nameof(), event handlers,
    implicit private methods, and qualified calls. Optimized for larger projects.
#>

param(
    [switch]$IncludePublic,
    [switch]$AllSources,
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'

# Configuration
$searchPaths = if ($AllSources) {
    @("src", "tests", "scripts")
} else {
    @("src")
}

$excludePatterns = @(
    "*/obj/*", "*/bin/*", "*.Designer.cs", "*.g.cs", "*.xaml.cs"
)

Write-Host "`n🔍 Enhanced Dead Code Scanner (Improved)" -ForegroundColor Cyan
Write-Host "=" * 80

# Step 1: Collect all valid .cs files
Write-Host "`n📂 Collecting C# source files..." -ForegroundColor Yellow
$allCodeFiles = @()
foreach ($basePath in $searchPaths) {
    if (-not (Test-Path $basePath)) { continue }
    Get-ChildItem -Path $basePath -Filter "*.cs" -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        $relativePath = $_.FullName -replace [regex]::Escape("$((Get-Location).Path)$( [System.IO.Path]::DirectorySeparatorChar )"), ""
        $skip = $excludePatterns | Where-Object { $relativePath -like $_ }
        if (-not $skip) {
            $allCodeFiles += $_
        }
    }
}
Write-Host "Found $($allCodeFiles.Count) source files to scan" -ForegroundColor Green

# Step 2: Find method declarations
Write-Host "`n🔍 Scanning for method declarations..." -ForegroundColor Yellow
$methods = @()
foreach ($csFile in $allCodeFiles) {
    $file = $csFile.FullName
    $relativePath = $csFile.FullName -replace [regex]::Escape("$((Get-Location).Path)$( [System.IO.Path]::DirectorySeparatorChar )"), ""
    $lines = Get-Content $file
    $lineNumber = 0

    foreach ($line in $lines) {
        $lineNumber++
        # Updated regex: visibility optional → defaults to private
        if ($line -match '^\s*(?:(public|private|protected|internal)\s+)?(?:static\s+)?(?:async\s+)?(?:partial\s+)?(?:virtual\s+)?(?:override\s+)?(?:abstract\s+)?(?:new\s+)?[\w<>, \.]+\s+(\w+)\s*\(') {
            $visibility = if ($matches[1]) { $matches[1] } else { 'private' }
            $methodName = $matches[2]

            # Skip if public and not requested
            if ($visibility -eq 'public' -and -not $IncludePublic) { continue }

            # Skip constructors, properties, event add/remove, special methods
            $fileName = [System.IO.Path]::GetFileNameWithoutExtension($file)
            if ($methodName -eq $fileName -or
                $methodName -match '^(get_|set_|add_|remove_|op_)' -or
                $methodName -in @('Dispose', 'ToString', 'GetHashCode', 'Equals', 'Main', 'OnModelCreating')) {
                continue
            }

            $methods += [PSCustomObject]@{
                MethodName    = $methodName
                Visibility    = $visibility
                File          = $file
                RelativePath  = $relativePath
                LineNumber    = $lineNumber
                Declaration   = $line.Trim()
            }
        }
    }
}
Write-Host "Found $($methods.Count) method declarations" -ForegroundColor Green

# Step 3: Check usage with enhanced patterns
Write-Host "`n🔎 Analyzing usage (optimized)..." -ForegroundColor Yellow
$progress = 0
$total = $methods.Count
$usedMethods = @()
$unusedMethods = @()

foreach ($method in $methods) {
    $progress++
    if ($progress % 20 -eq 0 -or $progress -eq $total) {
        Write-Progress -Activity "Analyzing methods" -Status "$progress/$total" -PercentComplete ($progress / $total * 100)
    }

    $methodName = $method.MethodName
    $content = Get-Content $method.File -Raw

    # Enhanced usage patterns
    $usagePatterns = @(
        "$methodName\s*\("                  # Direct or qualified call: Method( or obj.Method(
        "\.$methodName\s*\("                # Explicitly qualified: .Method(
        "\+=\s*$methodName"                 # Event subscription
        "=\s*$methodName\s*;"               # Delegate assignment
        "nameof\(\s*$methodName\s*\)"       # nameof(Method)
        "`"$methodName`""                   # String literal "Method"
        "'$methodName'"                     # String literal 'Method'
        "new\s+\w+Handler\(\s*$methodName\s*\)"   # EventHandler
        "new\s+RelayCommand\(\s*$methodName\s*\)" # RelayCommand
    )

    $isUsed = $false
    $detectedVia = ""

    # Check same file first
    foreach ($pattern in $usagePatterns) {
        if ($content -match $pattern) {
            $isUsed = $true
            $detectedVia = "Same file ($pattern)"
            break
        }
    }

    if ($isUsed) {
        $usedMethods += [PSCustomObject]@{ MethodName = $methodName; File = $method.RelativePath; DetectedVia = $detectedVia }
        continue
    }

    # Heuristic: private method not found in own file → almost certainly unused
    if ($method.Visibility -eq 'private') {
        $unusedMethods += $method
        continue
    }

    # Cross-file search only for non-private methods
    foreach ($pattern in $usagePatterns) {
        if (Select-String -Path $allCodeFiles.FullName -Pattern $pattern -Quiet) {
            $isUsed = $true
            $detectedVia = "Cross-file ($pattern)"
            break
        }
    }

    if ($isUsed) {
        $usedMethods += [PSCustomObject]@{ MethodName = $methodName; File = $method.RelativePath; DetectedVia = $detectedVia }
    } else {
        $unusedMethods += $method
    }
}

Write-Progress -Activity "Analyzing methods" -Completed

# Step 4: Results
Write-Host "`n" + ("=" * 80)
Write-Host "📊 SCAN COMPLETE" -ForegroundColor Cyan
Write-Host ("=" * 80)
Write-Host "`n✅ Used methods     : $($usedMethods.Count)" -ForegroundColor Green
Write-Host "⚠️  Potentially unused: $($unusedMethods.Count)" -ForegroundColor Yellow

if ($unusedMethods.Count -eq 0) {
    Write-Host "`n🎉 No unused methods detected!" -ForegroundColor Green
} else {
    Write-Host "`n⚠️  Potentially unused methods:" -ForegroundColor Yellow
    $unusedMethods | Select-Object MethodName, Visibility, RelativePath, LineNumber | Format-Table -AutoSize
}

# Save detailed report
$reportPath = "tmp/dead-code-report.json"
if (-not (Test-Path "tmp")) { New-Item -ItemType Directory -Path "tmp" | Out-Null }
@{
    ScanDate       = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    TotalMethods   = $methods.Count
    Used           = $usedMethods.Count
    PotentiallyUnused = $unusedMethods
} | ConvertTo-Json -Depth 4 | Out-File $reportPath -Encoding utf8

Write-Host "`n💾 Detailed report saved to: $reportPath" -ForegroundColor Cyan
Write-Host "`nScan finished!" -ForegroundColor Green
