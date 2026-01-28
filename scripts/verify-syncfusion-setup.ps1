<#
.SYNOPSIS
    Verifies Syncfusion Toolbox and MCP Server setup is complete

.DESCRIPTION
    Runs comprehensive checks to ensure:
    - MCP server configurations exist
    - Syncfusion packages are installed
    - NuGet packages target .NET 10
    - Environment variables are set
    - Documentation is in place

.EXAMPLE
    .\scripts\verify-syncfusion-setup.ps1

.NOTES
    Author: Wiley-Widget Project
    Version: 1.0.0
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$checks = @()
$warnings = @()
$errors = @()

Write-Host ""
Write-Host "🔍 Verifying Syncfusion Toolbox & MCP Server Setup..." -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Gray
Write-Host ""

# --- Check 1: MCP Configuration Files ---
Write-Host "📁 Checking MCP Configuration Files..." -ForegroundColor Yellow

$mcpFiles = @(
    @{ Path = ".vscode\mcp.json"; Required = $true; Description = "VS Code MCP config" },
    @{ Path = ".vs\mcp.json"; Required = $false; Description = "Visual Studio MCP config (user-specific)" },
    @{ Path = ".vs\mcp.json.template"; Required = $true; Description = "VS MCP config template" },
    @{ Path = "scripts\generate-vs-mcp-config.ps1"; Required = $true; Description = "VS MCP generator script" }
)

foreach ($file in $mcpFiles) {
    if (Test-Path $file.Path) {
        Write-Host "   ✅ $($file.Description)" -ForegroundColor Green
        $checks += "MCP: $($file.Path) exists"
    } else {
        if ($file.Required) {
            Write-Host "   ❌ $($file.Description) - MISSING" -ForegroundColor Red
            $errors += "$($file.Path) is missing (required)"
        } else {
            Write-Host "   ⚠️  $($file.Description) - Missing (will be generated)" -ForegroundColor Yellow
            $warnings += "$($file.Path) needs to be generated"
        }
    }
}

Write-Host ""

# --- Check 2: Syncfusion API Key ---
Write-Host "🔐 Checking Syncfusion API Key..." -ForegroundColor Yellow

$apiKey = [System.Environment]::GetEnvironmentVariable("SYNCFUSION_MCP_API_KEY", "User")
if ($apiKey) {
    Write-Host "   ✅ SYNCFUSION_MCP_API_KEY environment variable is set" -ForegroundColor Green
    $checks += "API Key: Set (length: $($apiKey.Length) chars)"
} else {
    Write-Host "   ❌ SYNCFUSION_MCP_API_KEY environment variable NOT SET" -ForegroundColor Red
    $errors += "Syncfusion API key not set - get from https://syncfusion.com/account/api-key"
}

Write-Host ""

# --- Check 3: Project File ---
Write-Host "📦 Checking Project Configuration..." -ForegroundColor Yellow

$projectPath = "src\WileyWidget.WinForms\WileyWidget.WinForms.csproj"
if (Test-Path $projectPath) {
    Write-Host "   ✅ Project file exists" -ForegroundColor Green
    
    $projectContent = Get-Content $projectPath -Raw
    
    # Check target framework
    if ($projectContent -match '<TargetFramework>net10.0-windows</TargetFramework>') {
        Write-Host "   ✅ Project targets .NET 10 (net10.0-windows)" -ForegroundColor Green
        $checks += "Project: Targets .NET 10"
    } else {
        Write-Host "   ❌ Project does NOT target .NET 10" -ForegroundColor Red
        $errors += "Project must target net10.0-windows"
    }
    
    # Check UseWindowsForms
    if ($projectContent -match '<UseWindowsForms>true</UseWindowsForms>') {
        Write-Host "   ✅ UseWindowsForms is enabled" -ForegroundColor Green
        $checks += "Project: UseWindowsForms enabled"
    } else {
        Write-Host "   ⚠️  UseWindowsForms not explicitly set" -ForegroundColor Yellow
        $warnings += "UseWindowsForms should be explicitly set to true"
    }
} else {
    Write-Host "   ❌ Project file NOT FOUND: $projectPath" -ForegroundColor Red
    $errors += "Project file missing"
}

Write-Host ""

# --- Check 4: Syncfusion NuGet Packages ---
Write-Host "📚 Checking Syncfusion NuGet Packages..." -ForegroundColor Yellow

$requiredPackages = @(
    "Syncfusion.Core.WinForms",
    "Syncfusion.SfDataGrid.WinForms",
    "Syncfusion.Chart.Windows",
    "Syncfusion.Gauge.Windows",
    "Syncfusion.Tools.Windows",
    "Syncfusion.Shared.Base"
)

try {
    $installedPackages = dotnet list "src\WileyWidget.WinForms\WileyWidget.WinForms.csproj" package 2>&1 | Select-String "Syncfusion"
    
    foreach ($pkg in $requiredPackages) {
        $found = $installedPackages | Where-Object { $_ -match $pkg }
        if ($found) {
            Write-Host "   ✅ $pkg installed" -ForegroundColor Green
            $checks += "Package: $pkg"
        } else {
            Write-Host "   ⚠️  $pkg NOT found (may be transitive)" -ForegroundColor Yellow
            $warnings += "$pkg not explicitly referenced"
        }
    }
} catch {
    Write-Host "   ⚠️  Could not list packages (project may need restore)" -ForegroundColor Yellow
    $warnings += "Run 'dotnet restore' to verify packages"
}

Write-Host ""

# --- Check 5: NuGet Cache for .NET 10 Support ---
Write-Host "🗂️  Checking NuGet Cache for .NET 10 Support..." -ForegroundColor Yellow

$nugetPath = "$env:USERPROFILE\.nuget\packages\syncfusion.sfdatagrid.winforms\32.1.19\lib"
if (Test-Path $nugetPath) {
    $tfms = Get-ChildItem $nugetPath -Directory | Select-Object -ExpandProperty Name
    if ($tfms -contains "net10.0-windows7.0") {
        Write-Host "   ✅ Syncfusion packages support .NET 10 (net10.0-windows7.0)" -ForegroundColor Green
        $checks += "NuGet: .NET 10 TFM present"
    } else {
        Write-Host "   ❌ .NET 10 TFM NOT FOUND in packages" -ForegroundColor Red
        Write-Host "      Available TFMs: $($tfms -join ', ')" -ForegroundColor Gray
        $errors += "Syncfusion packages do not include net10.0-windows7.0 target"
    }
} else {
    Write-Host "   ⚠️  NuGet cache not found - run 'dotnet restore'" -ForegroundColor Yellow
    $warnings += "Run 'dotnet restore' to download packages"
}

Write-Host ""

# --- Check 6: Documentation ---
Write-Host "📖 Checking Documentation..." -ForegroundColor Yellow

$docs = @(
    "docs\SYNCFUSION_TOOLBOX_VS2026_GUIDE.md",
    "docs\MCP_SERVER_SETUP_GUIDE.md",
    "SYNCFUSION_SETUP_COMPLETE.md",
    "QUICK_START.md"
)

foreach ($doc in $docs) {
    if (Test-Path $doc) {
        Write-Host "   ✅ $doc" -ForegroundColor Green
        $checks += "Doc: $doc"
    } else {
        Write-Host "   ⚠️  $doc - Missing" -ForegroundColor Yellow
        $warnings += "$doc not found"
    }
}

Write-Host ""

# --- Check 7: Build Status ---
Write-Host "🔨 Checking Build Status..." -ForegroundColor Yellow

try {
    $buildResult = dotnet build "src\WileyWidget.WinForms\WileyWidget.WinForms.csproj" --no-restore 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Project builds successfully" -ForegroundColor Green
        $checks += "Build: Success"
    } else {
        Write-Host "   ❌ Project has build errors" -ForegroundColor Red
        $errors += "Fix build errors before using Designer"
    }
} catch {
    Write-Host "   ⚠️  Could not verify build status" -ForegroundColor Yellow
    $warnings += "Run 'dotnet build' manually to verify"
}

Write-Host ""

# --- Summary ---
Write-Host "=" * 70 -ForegroundColor Gray
Write-Host ""
Write-Host "📊 VERIFICATION SUMMARY" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Gray
Write-Host ""

Write-Host "✅ Passed Checks: $($checks.Count)" -ForegroundColor Green
if ($checks.Count -gt 0) {
    $checks | ForEach-Object { Write-Host "   • $_" -ForegroundColor Gray }
}

Write-Host ""

if ($warnings.Count -gt 0) {
    Write-Host "⚠️  Warnings: $($warnings.Count)" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host "   • $_" -ForegroundColor Yellow }
    Write-Host ""
}

if ($errors.Count -gt 0) {
    Write-Host "❌ Errors: $($errors.Count)" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "   • $_" -ForegroundColor Red }
    Write-Host ""
}

# --- Next Steps ---
Write-Host "📋 NEXT STEPS:" -ForegroundColor Cyan
Write-Host ""

if ($errors.Count -eq 0 -and $warnings.Count -eq 0) {
    Write-Host "🎉 Setup is COMPLETE! You're ready to use Syncfusion controls." -ForegroundColor Green
    Write-Host ""
    Write-Host "To activate in Visual Studio:" -ForegroundColor Yellow
    Write-Host "   1. Restart Visual Studio" -ForegroundColor Gray
    Write-Host "   2. View → Toolbox → Right-click → Reset Toolbox" -ForegroundColor Gray
    Write-Host "   3. Search for 'SfDataGrid' in Toolbox" -ForegroundColor Gray
    Write-Host "   4. Test MCP: Copilot Chat → @SyncfusionWinFormsAssistant" -ForegroundColor Gray
} else {
    if (-not $apiKey) {
        Write-Host "1. Set Syncfusion API Key:" -ForegroundColor Yellow
        Write-Host "   [System.Environment]::SetEnvironmentVariable('SYNCFUSION_MCP_API_KEY', 'your-key', 'User')" -ForegroundColor Gray
        Write-Host "   Get key from: https://syncfusion.com/account/api-key" -ForegroundColor Gray
        Write-Host ""
    }
    
    if (-not (Test-Path ".vs\mcp.json")) {
        Write-Host "2. Generate Visual Studio MCP config:" -ForegroundColor Yellow
        Write-Host "   .\scripts\generate-vs-mcp-config.ps1" -ForegroundColor Gray
        Write-Host ""
    }
    
    if ($errors -match "build") {
        Write-Host "3. Fix build errors:" -ForegroundColor Yellow
        Write-Host "   dotnet clean" -ForegroundColor Gray
        Write-Host "   dotnet restore --force" -ForegroundColor Gray
        Write-Host "   dotnet build" -ForegroundColor Gray
        Write-Host ""
    }
    
    Write-Host "4. Follow the complete guide:" -ForegroundColor Yellow
    Write-Host "   Read: SYNCFUSION_SETUP_COMPLETE.md" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=" * 70 -ForegroundColor Gray
Write-Host ""

# Exit code reflects status
if ($errors.Count -gt 0) {
    exit 1
} elseif ($warnings.Count -gt 0) {
    exit 2
} else {
    exit 0
}
