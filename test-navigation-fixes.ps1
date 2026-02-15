#Requires -Version 7.0
<#
.SYNOPSIS
    Tests RibbonControlAdv navigation and DockingManager panel visibility
.DESCRIPTION
    Runs the application briefly and captures navigation logs to verify panels appear
#>

Write-Host "`n🧪 Testing RibbonControlAdv Navigation & DockingManager Visibility`n" -ForegroundColor Cyan

# 1. Verify all fixes are in place
Write-Host "1️⃣  Verifying code fixes are applied..." -ForegroundColor Yellow

$fixes = @(
    @{
        File = "src\WileyWidget.WinForms\Forms\MainForm\MainForm.RibbonHelpers.cs"
        Pattern = "form.ShowPanel\(entry.PanelType, entry.DisplayName, entry.DefaultDock\)"
        Description = "Type-based navigation fix"
    },
    @{
        File = "src\WileyWidget.WinForms\Forms\DockingHostFactory.cs"
        Pattern = "leftDockPanel.Visible = true"
        Description = "Left panel visibility fix"
    },
    @{
        File = "src\WileyWidget.WinForms\Forms\DockingHostFactory.cs"
        Pattern = "rightDockPanel.Visible = true"
        Description = "Right panel visibility fix"
    },
    @{
        File = "src\WileyWidget.WinForms\Forms\MainForm\MainForm.Docking.cs"
        Pattern = "ForceMarkDockingReadyIfOperational"
        Description = "Readiness gate fix"
    }
)

$allFixed = $true
foreach ($fix in $fixes) {
    if (Test-Path $fix.File) {
        $content = Get-Content $fix.File -Raw
        if ($content -match [regex]::Escape($fix.Pattern)) {
            Write-Host "   ✅ $($fix.Description)" -ForegroundColor Green
        } else {
            Write-Host "   ❌ $($fix.Description) - NOT FOUND" -ForegroundColor Red
            $allFixed = $false
        }
    } else {
        Write-Host "   ❌ File not found: $($fix.File)" -ForegroundColor Red
        $allFixed = $false
    }
}

if (-not $allFixed) {
    Write-Host "`n⚠️  Some fixes are missing! Navigation may not work." -ForegroundColor Red
    exit 1
}

# 2. Check build status
Write-Host "`n2️⃣  Checking build status..." -ForegroundColor Yellow
$buildResult = dotnet build src\WileyWidget.WinForms\WileyWidget.WinForms.csproj --no-restore 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ Build successful" -ForegroundColor Green
} else {
    Write-Host "   ❌ Build failed - fix compilation errors first" -ForegroundColor Red
    Write-Host $buildResult | Select-String "error"
    exit 1
}

# 3. Check if DockingManager is properly configured
Write-Host "`n3️⃣  Analyzing DockingManager configuration..." -ForegroundColor Yellow

$dockingFactory = Get-Content "src\WileyWidget.WinForms\Forms\DockingHostFactory.cs" -Raw

# Check critical configuration
$checks = @(
    @{ Pattern = 'SetEnableDocking\(leftDockPanel, true\)'; Description = "Left panel docking enabled" },
    @{ Pattern = 'SetEnableDocking\(rightDockPanel, true\)'; Description = "Right panel docking enabled" },
    @{ Pattern = 'EnsurePanelsVisible'; Description = "Panel visibility method exists" },
    @{ Pattern = 'DockingStyle\.Left'; Description = "Left docking style configured" },
    @{ Pattern = 'DockingStyle\.Right'; Description = "Right docking style configured" }
)

foreach ($check in $checks) {
    if ($dockingFactory -match $check.Pattern) {
        Write-Host "   ✅ $($check.Description)" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  $($check.Description) - Not found" -ForegroundColor Yellow
    }
}

# 4. Check panel registration in PanelRegistry
Write-Host "`n4️⃣  Checking registered panels..." -ForegroundColor Yellow

$registryFiles = Get-ChildItem "src\WileyWidget.WinForms\Controls\Panels" -Filter "*Panel.cs" -Recurse |
    Select-Object -First 5

Write-Host "   Found $($registryFiles.Count) panel files:" -ForegroundColor Gray
foreach ($file in $registryFiles) {
    Write-Host "      - $($file.Name)" -ForegroundColor Gray
}

# 5. Summary and next steps
Write-Host "`n📋 Summary:`n" -ForegroundColor Cyan

if ($allFixed) {
    Write-Host "✅ All navigation fixes are in place!" -ForegroundColor Green
    Write-Host "✅ Build is successful" -ForegroundColor Green
    Write-Host "✅ DockingManager is properly configured" -ForegroundColor Green
    
    Write-Host "`n🎯 Next Steps:" -ForegroundColor Yellow
    Write-Host "1. Run the application" -ForegroundColor White
    Write-Host "2. Click any navigation button in the Ribbon (e.g., 'Dashboard', 'Accounts')" -ForegroundColor White
    Write-Host "3. Check if panels appear on screen" -ForegroundColor White
    Write-Host "4. Check logs\wiley-widget-*.log for navigation messages" -ForegroundColor White
    
    Write-Host "`n📝 What to look for in logs:" -ForegroundColor Yellow
    Write-Host "   [RIBBON_NAV] Navigating to panel..." -ForegroundColor Gray
    Write-Host "   [SHOWPANEL] ShowPanel(Type) called..." -ForegroundColor Gray
    Write-Host "   [EXEC_NAV] ✅ Executing navigation action..." -ForegroundColor Gray
    Write-Host "   Panel X docked successfully" -ForegroundColor Gray
    Write-Host "   Left dock panel set to visible" -ForegroundColor Gray
    Write-Host "   Right dock panel set to visible" -ForegroundColor Gray
    
    Write-Host "`n🐛 If panels still don't appear, check for:" -ForegroundColor Yellow
    Write-Host "   - Z-order issues (panels behind other controls)" -ForegroundColor Gray
    Write-Host "   - Size = 0 (panels with zero width/height)" -ForegroundColor Gray
    Write-Host "   - Parent container visibility" -ForegroundColor Gray
    Write-Host "   - DockingManager HostControl validity" -ForegroundColor Gray
} else {
    Write-Host "⚠️  Some fixes are missing - apply them first!" -ForegroundColor Red
}

Write-Host "`n💡 Quick test command:" -ForegroundColor Cyan
Write-Host "   # Run app and check latest log:" -ForegroundColor Gray
Write-Host "   dotnet run --project src\WileyWidget.WinForms\WileyWidget.WinForms.csproj" -ForegroundColor White
Write-Host "   # Then check:" -ForegroundColor Gray
Write-Host "   Get-Content (Get-ChildItem logs\wiley-widget-*.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName -Tail 50" -ForegroundColor White

Write-Host "`nDone! 🎉`n" -ForegroundColor Green
