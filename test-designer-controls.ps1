#Requires -Version 7.0
<#
.SYNOPSIS
    Tests if Syncfusion controls are properly loaded for the WinForms designer
.DESCRIPTION
    Checks design-time assembly loading and compatibility
#>

Write-Host "`n🔍 Testing Designer Control Availability`n" -ForegroundColor Cyan

# 1. Check if design-time assemblies exist
Write-Host "1️⃣  Checking design-time assemblies..." -ForegroundColor Yellow
$designAssemblies = @(
    "$env:USERPROFILE\.nuget\packages\syncfusion.sfdatagrid.winforms\32.2.3\lib\net10.0-windows7.0\Syncfusion.SfDataGrid.WinForms.dll",
    "$env:USERPROFILE\.nuget\packages\syncfusion.core.winforms\32.2.3\lib\net10.0-windows7.0\Syncfusion.Core.WinForms.dll",
    "$env:USERPROFILE\.nuget\packages\syncfusion.tools.windows\32.2.3\lib\net10.0-windows7.0\Syncfusion.Tools.Windows.dll"
)

foreach ($asm in $designAssemblies) {
    if (Test-Path $asm) {
        $name = Split-Path $asm -Leaf
        Write-Host "   ✅ $name" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Missing: $asm" -ForegroundColor Red
    }
}

# 2. Check if VS can load .NET 10 assemblies
Write-Host "`n2️⃣  Checking .NET 10 compatibility..." -ForegroundColor Yellow
$targetFramework = "net10.0-windows"
$projectFile = "src\WileyWidget.WinForms\WileyWidget.WinForms.csproj"

if (Test-Path $projectFile) {
    [xml]$proj = Get-Content $projectFile
    $framework = $proj.Project.PropertyGroup.TargetFramework
    if ($framework -eq $targetFramework) {
        Write-Host "   ✅ Project targets $framework (correct)" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Project targets $framework (expected $targetFramework)" -ForegroundColor Yellow
    }
}

# 3. Check designer cache
Write-Host "`n3️⃣  Checking designer state..." -ForegroundColor Yellow
$vsInstances = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\VisualStudio" -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match "^\d+\.\d+" }

if ($vsInstances) {
    foreach ($instance in $vsInstances) {
        $designerCache = Join-Path $instance.FullName "Designer\ShadowCache"
        if (Test-Path $designerCache) {
            Write-Host "   ✅ Designer cache exists: $($instance.Name)" -ForegroundColor Green
        }
    }
} else {
    Write-Host "   ⚠️  No Visual Studio instance folders found" -ForegroundColor Yellow
}

# 4. Recommendations
Write-Host "`n📋 Recommendations:`n" -ForegroundColor Cyan

Write-Host "To enable controls in designer:" -ForegroundColor White
Write-Host "1. Close all designer windows in VS" -ForegroundColor Gray
Write-Host "2. Build → Clean Solution" -ForegroundColor Gray
Write-Host "3. Build → Rebuild Solution" -ForegroundColor Gray
Write-Host "4. Close and reopen designer windows" -ForegroundColor Gray
Write-Host "5. If still disabled, manually add controls via 'Choose Items...'" -ForegroundColor Gray

Write-Host "`nManual Add Path:" -ForegroundColor White
Write-Host "$env:USERPROFILE\.nuget\packages\syncfusion.sfdatagrid.winforms\32.2.3\lib\net10.0-windows7.0\" -ForegroundColor Cyan

Write-Host "`nDone! 🎉`n" -ForegroundColor Green
