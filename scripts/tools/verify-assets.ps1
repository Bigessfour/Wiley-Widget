<#
.SYNOPSIS
    Verifies WinUI asset configuration and resolves common issues.

.DESCRIPTION
    This script checks:
    - Assets directory structure
    - .csproj configuration
    - Build output verification
    - Asset file integrity

.PARAMETER ProjectPath
    Path to the WinUI project (default: src\WileyWidget.WinUI)

.PARAMETER Fix
    Automatically fix common issues

.EXAMPLE
    .\verify-assets.ps1
    
.EXAMPLE
    .\verify-assets.ps1 -Fix
#>

param(
    [string]$ProjectPath = "src\WileyWidget.WinUI",
    [switch]$Fix
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot | Split-Path -Parent | Split-Path -Parent

Write-Host "🔍 Verifying WinUI Asset Configuration" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

# Resolve full path
$fullProjectPath = Join-Path $repoRoot $ProjectPath
if (-not (Test-Path $fullProjectPath)) {
    Write-Host "❌ Project path not found: $fullProjectPath" -ForegroundColor Red
    exit 1
}

$issues = @()
$fixes = @()

# ============================================================================
# 1. Check Assets Directory
# ============================================================================
Write-Host "📁 Checking Assets Directory..." -ForegroundColor Yellow

$assetsPath = Join-Path $fullProjectPath "Assets"
if (Test-Path $assetsPath) {
    Write-Host "   ✅ Assets directory found: $assetsPath" -ForegroundColor Green
    
    # List all asset files
    $assetFiles = Get-ChildItem $assetsPath -Recurse -File
    Write-Host "   📊 Total asset files: $($assetFiles.Count)`n" -ForegroundColor Gray
    
    # Group by extension
    $assetFiles | Group-Object Extension | Sort-Object Count -Descending | ForEach-Object {
        $ext = if ($_.Name) { $_.Name } else { "(no extension)" }
        Write-Host "      $ext : $($_.Count) file(s)" -ForegroundColor Gray
    }
    
    # Check for SVG files
    $svgFiles = $assetFiles | Where-Object { $_.Extension -eq ".svg" }
    if ($svgFiles.Count -gt 0) {
        Write-Host "`n   🎨 SVG Files Found:" -ForegroundColor Cyan
        $svgFiles | ForEach-Object {
            $relativePath = $_.FullName.Replace("$assetsPath\", "Assets\")
            $size = "{0:N2} KB" -f ($_.Length / 1KB)
            Write-Host "      • $relativePath ($size)" -ForegroundColor Gray
            
            # Validate SVG format
            $content = Get-Content $_.FullName -Raw
            if ($content -notmatch '<svg') {
                Write-Host "        ⚠️  May not be valid SVG" -ForegroundColor Yellow
                $issues += "Invalid SVG: $relativePath"
            }
        }
    } else {
        Write-Host "   ⚠️  No SVG files found" -ForegroundColor Yellow
        $issues += "No SVG assets found in Assets directory"
        
        # Check if SVGs exist in Uno project
        $unoAssetsPath = Join-Path $repoRoot "src\WileyWidget.Uno\Assets"
        if (Test-Path $unoAssetsPath) {
            $unoSvgs = Get-ChildItem $unoAssetsPath -Recurse -Filter "*.svg"
            if ($unoSvgs.Count -gt 0) {
                Write-Host "   💡 Found $($unoSvgs.Count) SVG(s) in Uno project" -ForegroundColor Cyan
                $fixes += "Copy SVG files from Uno project to WinUI project"
            }
        }
    }
} else {
    Write-Host "   ❌ Assets directory not found: $assetsPath" -ForegroundColor Red
    $issues += "Assets directory missing"
    $fixes += "Create Assets directory structure"
}

# ============================================================================
# 2. Check .csproj Configuration
# ============================================================================
Write-Host "`n📄 Checking .csproj Configuration..." -ForegroundColor Yellow

$csprojPath = Join-Path $fullProjectPath "WileyWidget.WinUI.csproj"
if (Test-Path $csprojPath) {
    $csprojContent = Get-Content $csprojPath -Raw
    
    # Check for Content Include
    if ($csprojContent -match '<Content Include="Assets\\\*\*\\\*">') {
        Write-Host "   ✅ .csproj includes Assets with wildcard pattern" -ForegroundColor Green
    } else {
        Write-Host "   ❌ .csproj missing Assets wildcard inclusion" -ForegroundColor Red
        $issues += ".csproj does not include <Content Include='Assets\**\*'>"
        $fixes += "Add Assets Content Include to .csproj"
    }
    
    # Check for CopyToOutputDirectory
    if ($csprojContent -match 'CopyToOutputDirectory.*PreserveNewest') {
        Write-Host "   ✅ CopyToOutputDirectory set to PreserveNewest" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  CopyToOutputDirectory not configured" -ForegroundColor Yellow
        $issues += "CopyToOutputDirectory not set to PreserveNewest"
        $fixes += "Set CopyToOutputDirectory to PreserveNewest in .csproj"
    }
    
    # Check Windows App SDK version
    if ($csprojContent -match 'Microsoft\.WindowsAppSDK.*Version="([^"]+)"') {
        $version = $matches[1]
        Write-Host "   📦 Windows App SDK Version: $version" -ForegroundColor Gray
        
        # SVG support requires 1.8+
        if ([version]$version -lt [version]"1.8.0") {
            Write-Host "   ⚠️  SVG support may be limited (requires 1.8+)" -ForegroundColor Yellow
            $issues += "Windows App SDK version $version may not fully support SVG"
        }
    }
} else {
    Write-Host "   ❌ .csproj file not found: $csprojPath" -ForegroundColor Red
    $issues += "Project file not found"
}

# ============================================================================
# 3. Check Build Output
# ============================================================================
Write-Host "`n🏗️  Checking Build Output..." -ForegroundColor Yellow

$binPatterns = @(
    "bin\Debug\net9.0-windows10.0.26100.0\win-x64\Assets",
    "bin\Debug\net9.0-windows10.0.19041.0\win-x64\Assets",
    "bin\Release\net9.0-windows10.0.26100.0\win-x64\Assets"
)

$foundOutput = $false
foreach ($pattern in $binPatterns) {
    $binPath = Join-Path $fullProjectPath $pattern
    if (Test-Path $binPath) {
        $foundOutput = $true
        Write-Host "   ✅ Assets found in build output: $pattern" -ForegroundColor Green
        
        $outputFiles = Get-ChildItem $binPath -Recurse -File
        Write-Host "   📊 Files in output: $($outputFiles.Count)" -ForegroundColor Gray
        
        # Compare with source
        if (Test-Path $assetsPath) {
            $sourceFiles = Get-ChildItem $assetsPath -Recurse -File
            if ($outputFiles.Count -lt $sourceFiles.Count) {
                Write-Host "   ⚠️  Not all assets copied to output ($($outputFiles.Count)/$($sourceFiles.Count))" -ForegroundColor Yellow
                $issues += "Not all assets copied to build output"
            }
        }
        break
    }
}

if (-not $foundOutput) {
    Write-Host "   ⚠️  Build output not found (project may need building)" -ForegroundColor Yellow
    $issues += "No build output found"
    $fixes += "Build the project: dotnet build"
}

# ============================================================================
# 4. Apply Fixes (if -Fix switch used)
# ============================================================================
if ($Fix -and $fixes.Count -gt 0) {
    Write-Host "`n🔧 Applying Fixes..." -ForegroundColor Cyan
    
    # Fix 1: Copy SVGs from Uno project
    if ($fixes -like "*Copy SVG files*") {
        $unoAssetsPath = Join-Path $repoRoot "src\WileyWidget.Uno\Assets"
        if (Test-Path $unoAssetsPath) {
            Write-Host "   📋 Copying SVG files from Uno project..." -ForegroundColor Yellow
            
            # Create subdirectories
            $iconsPath = Join-Path $assetsPath "Icons"
            $splashPath = Join-Path $assetsPath "Splash"
            New-Item -ItemType Directory -Force -Path $iconsPath | Out-Null
            New-Item -ItemType Directory -Force -Path $splashPath | Out-Null
            
            # Copy files
            Get-ChildItem "$unoAssetsPath\Icons\*.svg" -ErrorAction SilentlyContinue | ForEach-Object {
                Copy-Item $_.FullName -Destination $iconsPath -Force
                Write-Host "      ✅ Copied $($_.Name)" -ForegroundColor Green
            }
            
            Get-ChildItem "$unoAssetsPath\Splash\*.svg" -ErrorAction SilentlyContinue | ForEach-Object {
                Copy-Item $_.FullName -Destination $splashPath -Force
                Write-Host "      ✅ Copied $($_.Name)" -ForegroundColor Green
            }
        }
    }
    
    # Fix 2: Build project
    if ($fixes -like "*Build the project*") {
        Write-Host "`n   🏗️  Building project..." -ForegroundColor Yellow
        Push-Location $fullProjectPath
        try {
            dotnet build --no-restore --verbosity quiet
            Write-Host "      ✅ Build completed" -ForegroundColor Green
        } catch {
            Write-Host "      ❌ Build failed: $_" -ForegroundColor Red
        } finally {
            Pop-Location
        }
    }
}

# ============================================================================
# 5. Summary
# ============================================================================
Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "📊 Summary" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

if ($issues.Count -eq 0) {
    Write-Host "✅ All checks passed! Assets are properly configured." -ForegroundColor Green
} else {
    Write-Host "⚠️  Found $($issues.Count) issue(s):`n" -ForegroundColor Yellow
    $issues | ForEach-Object { Write-Host "   • $_" -ForegroundColor Yellow }
}

if ($fixes.Count -gt 0) {
    Write-Host "`n💡 Suggested Fixes:" -ForegroundColor Cyan
    $fixes | ForEach-Object { Write-Host "   • $_" -ForegroundColor Cyan }
    
    if (-not $Fix) {
        Write-Host "`n   Run with -Fix switch to automatically apply fixes:" -ForegroundColor Gray
        Write-Host "   .\verify-assets.ps1 -Fix`n" -ForegroundColor White
    }
}

# ============================================================================
# 6. Asset Path Examples
# ============================================================================
if (Test-Path $assetsPath) {
    Write-Host "`n📝 Asset Path Examples (XAML):" -ForegroundColor Cyan
    Write-Host "================================================`n" -ForegroundColor Cyan
    
    Get-ChildItem $assetsPath -Recurse -File | Select-Object -First 5 | ForEach-Object {
        $relativePath = $_.FullName.Replace("$assetsPath\", "Assets/").Replace("\", "/")
        Write-Host "   <Image Source=`"ms-appx:///$relativePath`" Width=`"32`" Height=`"32`" />" -ForegroundColor Gray
    }
}

Write-Host "`n✅ Asset verification complete`n" -ForegroundColor Green

# Return exit code based on issues
if ($issues.Count -gt 0 -and -not $Fix) {
    exit 1
} else {
    exit 0
}
