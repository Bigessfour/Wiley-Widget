#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Enhanced dead code scanner with false positive detection
.DESCRIPTION
    Improved scanner that understands RelayCommand patterns, nameof(),
    event handlers, and other patterns that cause false positives
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

Write-Host "`n🔍 Enhanced Dead Code Scanner" -ForegroundColor Cyan
Write-Host "=" * 70

# Step 1: Find all method declarations
Write-Host "`n📂 Scanning for method declarations..." -ForegroundColor Yellow
$methods = @()

foreach ($basePath in $searchPaths) {
    Get-ChildItem -Path $basePath -Filter "*.cs" -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        $file = $_.FullName
        $relativePath = $file -replace [regex]::Escape($PWD.Path + "\"), ""

        # Skip excluded patterns
        $shouldSkip = $false
        foreach ($exclude in $excludePatterns) {
            if ($relativePath -like $exclude) {
                $shouldSkip = $true
                break
            }
        }
        if ($shouldSkip) { return }

        $lineNumber = 0
        Get-Content $file | ForEach-Object {
            $lineNumber++
            $line = $_

            # Match method declarations
            if ($line -match '^\s*(public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:partial\s+)?(?:virtual\s+)?(?:override\s+)?[\w<>]+\s+(\w+)\s*\(') {
                $visibility = $matches[1]
                $methodName = $matches[2]

                # Skip if public and not including public methods
                if ($visibility -eq 'public' -and -not $IncludePublic) {
                    return
                }

                # Skip constructors, properties, special methods
                $fileName = [System.IO.Path]::GetFileNameWithoutExtension($file)
                if ($methodName -eq $fileName -or
                    $methodName -match '^(get_|set_|add_|remove_|op_)' -or
                    $methodName -in @('Dispose', 'ToString', 'GetHashCode', 'Equals', 'Main', 'OnModelCreating')) {
                    return
                }

                $methods += [PSCustomObject]@{
                    MethodName = $methodName
                    Visibility = $visibility
                    File = $file
                    RelativePath = $relativePath
                    LineNumber = $lineNumber
                    Declaration = $line.Trim()
                }
            }
        }
    }
}

Write-Host "Found $($methods.Count) method declarations" -ForegroundColor Green

# Step 2: Check for usage with ENHANCED patterns
Write-Host "`n🔎 Checking for usage (enhanced patterns)..." -ForegroundColor Yellow
$totalMethods = $methods.Count
$progress = 0
$unusedMethods = @()
$usedMethods = @()

foreach ($method in $methods) {
    $progress++
    if ($progress % 10 -eq 0) {
        Write-Progress -Activity "Checking usage" -Status "$progress/$totalMethods" -PercentComplete (($progress / $totalMethods) * 100)
    }

    $methodName = $method.MethodName
    $file = $method.File
    $content = Get-Content $file -Raw

    # Enhanced usage detection patterns
    $usagePatterns = @(
        # Direct call
        "\s+$methodName\s*\(",
        # Event subscription
        "(\+=|=)\s*$methodName\s*;",
        # nameof() reference
        "nameof\(\s*$methodName\s*\)",
        # String reference
        "[\""']$methodName[\""']",
        # RelayCommand with CanExecute
        "\[RelayCommand\([^\]]*$methodName",
        # Event handler in constructor/method
        "\.$methodName\s*[\(;]",
        # new EventHandler(methodName)
        "new\s+\w+Handler\(\s*$methodName\s*\)",
        # Lambda or delegate
        "=>\s*$methodName\s*\("
    )

    $isUsed = $false
    $detectionMethod = ""

    # Check each pattern
    foreach ($pattern in $usagePatterns) {
        # Search in same file first
        if ($content -match $pattern) {
            $isUsed = $true
            $detectionMethod = "Same file: $($pattern -replace '\s+', ' ')"
            break
        }

        # Search across all C# files for cross-file references
        $searchResult = Get-ChildItem -Path "src/**/*.cs" -Recurse -File -ErrorAction SilentlyContinue |
            Select-String -Pattern $pattern -SimpleMatch:$false -Quiet -ErrorAction SilentlyContinue

        if ($searchResult) {
            $isUsed = $true
            $detectionMethod = "Cross-file: $($pattern -replace '\s+', ' ')"
            break
        }
    }

    if ($isUsed) {
        $usedMethods += [PSCustomObject]@{
            MethodName = $methodName
            File = $method.RelativePath
            DetectedVia = $detectionMethod
        }
    } else {
        $unusedMethods += [PSCustomObject]@{
            MethodName = $methodName
            Visibility = $method.Visibility
            File = $method.File
            RelativePath = $method.RelativePath
            LineNumber = $method.LineNumber
            Declaration = $method.Declaration
        }
    }
}

Write-Progress -Activity "Checking usage" -Completed

# Step 3: Report results
Write-Host "`n" + ("=" * 70)
Write-Host "📊 ENHANCED SCAN RESULTS" -ForegroundColor Cyan
Write-Host ("=" * 70)

Write-Host "`n✅ Used methods: $($usedMethods.Count)" -ForegroundColor Green
Write-Host "⚠️  Potentially unused: $($unusedMethods.Count)" -ForegroundColor Yellow

if ($unusedMethods.Count -eq 0) {
    Write-Host "`n🎉 No unused methods found! All methods are properly referenced." -ForegroundColor Green
} else {
    Write-Host "`n⚠️  Potentially unused methods:" -ForegroundColor Yellow
    $unusedMethods | Select-Object MethodName, Visibility, RelativePath, LineNumber |
        Format-Table -AutoSize

    # Save results
    $outputPath = "tmp/dead-code-report.json"
    $report = @{
        ScanDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        TotalMethods = $totalMethods
        UsedMethods = $usedMethods.Count
        UnusedMethods = $unusedMethods
    }

    $report | ConvertTo-Json -Depth 3 | Out-File $outputPath
    Write-Host "`n💾 Full report saved to: $outputPath" -ForegroundColor Cyan
}

Write-Host "`n" + ("=" * 70)
Write-Host "Scan complete!" -ForegroundColor Green
