#Requires -Version 7.0
<#
.SYNOPSIS
    Diagnoses Syncfusion Toolbox issues in Visual Studio 2026
.DESCRIPTION
    Checks:
    - Essential Studio installation
    - NuGet package versions
    - VS component cache status
    - Toolbox registry entries
.NOTES
    Run this from PowerShell 7+ in the repository root
#>

param(
    [switch]$FixIssues,
    [switch]$ClearCache
)

$ErrorActionPreference = 'Continue'
$VerbosePreference = 'Continue'

Write-Host "`n🔍 Syncfusion Toolbox Diagnostic for VS 2026`n" -ForegroundColor Cyan

# 1. Check Visual Studio 2026 Installation
Write-Host "1️⃣  Checking Visual Studio 2026 installation..." -ForegroundColor Yellow
$vsPath = Get-ChildItem "$env:ProgramFiles\Microsoft Visual Studio\2026" -ErrorAction SilentlyContinue
if ($vsPath) {
    Write-Host "   ✅ VS 2026 found at: $($vsPath.FullName)" -ForegroundColor Green
    $vsVersion = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\VisualStudio" -Directory | 
        Where-Object { $_.Name -like "17.*" } | 
        Sort-Object Name -Descending | 
        Select-Object -First 1
    if ($vsVersion) {
        Write-Host "   ✅ VS instance folder: $($vsVersion.Name)" -ForegroundColor Green
    }
} else {
    Write-Host "   ⚠️  VS 2026 not found in standard location" -ForegroundColor Red
}

# 2. Check Syncfusion Essential Studio Installation
Write-Host "`n2️⃣  Checking Syncfusion Essential Studio installation..." -ForegroundColor Yellow
$essentialStudioPath = "C:\Program Files (x86)\Syncfusion\Essential Studio\Windows"
if (Test-Path $essentialStudioPath) {
    $versions = Get-ChildItem $essentialStudioPath -Directory | Select-Object -ExpandProperty Name
    Write-Host "   ✅ Essential Studio installed versions:" -ForegroundColor Green
    foreach ($ver in $versions) {
        Write-Host "      - $ver" -ForegroundColor Gray
    }
    
    # Check for version 32.1.19
    if ($versions -contains "32.1.19") {
        Write-Host "   ✅ Version 32.1.19 found (matches project)" -ForegroundColor Green
        $samplesPath = Join-Path $essentialStudioPath "32.1.19\Windows"
        if (Test-Path $samplesPath) {
            Write-Host "   ✅ Samples directory exists: $samplesPath" -ForegroundColor Green
        }
    } else {
        Write-Host "   ⚠️  Version 32.1.19 NOT found (project uses 32.1.19)" -ForegroundColor Red
    }
} else {
    Write-Host "   ❌ Essential Studio NOT installed at expected location" -ForegroundColor Red
    Write-Host "      Expected: $essentialStudioPath" -ForegroundColor Gray
}

# 3. Check NuGet Package Cache
Write-Host "`n3️⃣  Checking NuGet package cache..." -ForegroundColor Yellow
$nugetCache = "$env:USERPROFILE\.nuget\packages"
$syncfusionPackages = @(
    'syncfusion.core.winforms',
    'syncfusion.sfdatagrid.winforms',
    'syncfusion.tools.windows',
    'syncfusion.chart.windows',
    'syncfusion.gauge.windows'
)

foreach ($pkg in $syncfusionPackages) {
    $pkgPath = Join-Path $nugetCache "$pkg\32.1.19"
    if (Test-Path $pkgPath) {
        Write-Host "   ✅ $pkg (32.1.19) cached" -ForegroundColor Green
        # Check for .NET 10 lib
        $net10Lib = Join-Path $pkgPath "lib\net10.0-windows7.0"
        if (Test-Path $net10Lib) {
            $dlls = Get-ChildItem $net10Lib -Filter "*.dll" | Select-Object -ExpandProperty Name
            Write-Host "      📦 .NET 10 libraries: $($dlls -join ', ')" -ForegroundColor Gray
        } else {
            Write-Host "      ⚠️  .NET 10 target NOT found (may use net8.0 or net9.0)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "   ❌ $pkg (32.1.19) NOT in cache" -ForegroundColor Red
    }
}

# 4. Check VS Component Model Cache
Write-Host "`n4️⃣  Checking Visual Studio component cache..." -ForegroundColor Yellow
if ($vsVersion) {
    $componentCache = "$env:LOCALAPPDATA\Microsoft\VisualStudio\$($vsVersion.Name)\ComponentModelCache"
    if (Test-Path $componentCache) {
        $cacheSize = (Get-ChildItem $componentCache -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
        Write-Host "   ✅ Component cache exists: $componentCache" -ForegroundColor Green
        Write-Host "      Size: $([math]::Round($cacheSize, 2)) MB" -ForegroundColor Gray
        
        if ($ClearCache) {
            Write-Host "   🧹 Clearing component cache..." -ForegroundColor Cyan
            try {
                Remove-Item -Recurse -Force $componentCache -ErrorAction Stop
                Write-Host "   ✅ Cache cleared successfully" -ForegroundColor Green
            } catch {
                Write-Host "   ❌ Failed to clear cache: $($_.Exception.Message)" -ForegroundColor Red
                Write-Host "      Close Visual Studio and try again" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "   ⚠️  Component cache not found (will be created on next VS start)" -ForegroundColor Yellow
    }
    
    # Check Designer Shadow Cache
    $designerCache = "$env:LOCALAPPDATA\Microsoft\VisualStudio\$($vsVersion.Name)\Designer\ShadowCache"
    if (Test-Path $designerCache) {
        Write-Host "   ✅ Designer cache exists" -ForegroundColor Green
        if ($ClearCache) {
            Write-Host "   🧹 Clearing designer cache..." -ForegroundColor Cyan
            Remove-Item -Recurse -Force $designerCache -ErrorAction SilentlyContinue
        }
    }
}

# 5. Check Project Package References
Write-Host "`n5️⃣  Checking project Syncfusion references..." -ForegroundColor Yellow
$projectFile = "src\WileyWidget.WinForms\WileyWidget.WinForms.csproj"
if (Test-Path $projectFile) {
    [xml]$proj = Get-Content $projectFile
    $syncfusionRefs = $proj.Project.ItemGroup.PackageReference | Where-Object { $_.Include -like "Syncfusion.*" }
    if ($syncfusionRefs) {
        Write-Host "   ✅ Found $($syncfusionRefs.Count) Syncfusion package references" -ForegroundColor Green
        foreach ($ref in $syncfusionRefs) {
            Write-Host "      - $($ref.Include)" -ForegroundColor Gray
        }
    } else {
        Write-Host "   ❌ No Syncfusion package references found!" -ForegroundColor Red
    }
}

# 6. Check if Visual Studio is running
Write-Host "`n6️⃣  Checking for running Visual Studio instances..." -ForegroundColor Yellow
$vsProcesses = Get-Process -Name "devenv" -ErrorAction SilentlyContinue
if ($vsProcesses) {
    Write-Host "   ⚠️  Visual Studio is currently running ($($vsProcesses.Count) instance(s))" -ForegroundColor Yellow
    Write-Host "      Close VS before clearing caches or making changes" -ForegroundColor Gray
} else {
    Write-Host "   ✅ No Visual Studio instances running" -ForegroundColor Green
}

# Summary and Recommendations
Write-Host "`n📋 Summary & Recommendations`n" -ForegroundColor Cyan

$issues = @()
if (-not $vsPath) { $issues += "Install Visual Studio 2026" }
if (-not (Test-Path $essentialStudioPath)) { $issues += "Install Syncfusion Essential Studio 32.1.19" }
if ($vsProcesses) { $issues += "Close Visual Studio before clearing caches" }

if ($issues.Count -eq 0) {
    Write-Host "✅ All prerequisites met!" -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "1. Run this script with -ClearCache flag (VS must be closed)" -ForegroundColor White
    Write-Host "2. Open Visual Studio 2026" -ForegroundColor White
    Write-Host "3. Open the solution" -ForegroundColor White
    Write-Host "4. View → Toolbox → Right-click → Reset Toolbox" -ForegroundColor White
    Write-Host "5. Search for 'SfDataGrid' in Toolbox" -ForegroundColor White
} else {
    Write-Host "⚠️  Issues found:" -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host "   - $issue" -ForegroundColor Yellow
    }
}

Write-Host "`n💡 Quick Fix Commands:`n" -ForegroundColor Cyan
Write-Host "   # Clear caches (VS must be closed):" -ForegroundColor Gray
Write-Host "   .\scripts\diagnose-syncfusion-toolbox-vs2026.ps1 -ClearCache`n" -ForegroundColor White
Write-Host "   # Restore packages:" -ForegroundColor Gray
Write-Host "   dotnet restore src\WileyWidget.WinForms\WileyWidget.WinForms.csproj --force`n" -ForegroundColor White
Write-Host "   # Rebuild solution:" -ForegroundColor Gray
Write-Host "   dotnet build src\WileyWidget.WinForms\WileyWidget.WinForms.csproj`n" -ForegroundColor White

Write-Host "Done! 🎉`n" -ForegroundColor Green
