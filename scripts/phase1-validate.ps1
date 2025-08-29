# Phase 1 Validation Script
# Validates that all Phase 1 components are properly implemented

param(
    [switch]$Detailed
)

Write-Host "🔍 Wiley Widget Phase 1 Validation" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan

$projectDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$errors = @()
$warnings = @()

# Check Models
Write-Host "📋 Checking Models..." -ForegroundColor Yellow
$models = @(
    "Models\Enterprise.cs",
    "Models\BudgetInteraction.cs",
    "Models\OverallBudget.cs"
)

foreach ($model in $models) {
    $modelPath = Join-Path $projectDir $model
    if (!(Test-Path $modelPath)) {
        $errors += "Missing model file: $model"
    } else {
        $content = Get-Content $modelPath -Raw
        if ($Detailed) {
            Write-Host "  ✅ Found $model" -ForegroundColor Green
        }
    }
}

# Check Data Layer
Write-Host "🗄️  Checking Data Layer..." -ForegroundColor Yellow
$dataFiles = @(
    "Data\AppDbContext.cs",
    "Data\IEnterpriseRepository.cs",
    "Data\EnterpriseRepository.cs",
    "Data\DatabaseSeeder.cs"
)

foreach ($file in $dataFiles) {
    $filePath = Join-Path $projectDir $file
    if (!(Test-Path $filePath)) {
        $errors += "Missing data file: $file"
    } else {
        if ($Detailed) {
            Write-Host "  ✅ Found $file" -ForegroundColor Green
        }
    }
}

# Check ViewModels
Write-Host "🎯 Checking ViewModels..." -ForegroundColor Yellow
$vmFiles = @(
    "ViewModels\EnterpriseViewModel.cs"
)

foreach ($file in $vmFiles) {
    $filePath = Join-Path $projectDir $file
    if (!(Test-Path $filePath)) {
        $errors += "Missing ViewModel file: $file"
    } else {
        if ($Detailed) {
            Write-Host "  ✅ Found $file" -ForegroundColor Green
        }
    }
}

# Check Test Application
Write-Host "🧪 Checking Test Application..." -ForegroundColor Yellow
$testFiles = @(
    "WileyWidget.TestModels\Program.cs",
    "WileyWidget.TestModels\WileyWidget.TestModels.csproj"
)

foreach ($file in $testFiles) {
    $filePath = Join-Path $projectDir $file
    if (!(Test-Path $filePath)) {
        $errors += "Missing test file: $file"
    } else {
        if ($Detailed) {
            Write-Host "  ✅ Found $file" -ForegroundColor Green
        }
    }
}

# Check Project Configuration
Write-Host "⚙️  Checking Project Configuration..." -ForegroundColor Yellow
$csprojPath = Join-Path $projectDir "WileyWidget.csproj"
if (!(Test-Path $csprojPath)) {
    $errors += "Missing project file: WileyWidget.csproj"
} else {
    $csprojContent = Get-Content $csprojPath -Raw
    $requiredPackages = @(
        "Microsoft.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.SqlServer",
        "CommunityToolkit.Mvvm"
    )

    foreach ($package in $requiredPackages) {
        if ($csprojContent -notmatch $package) {
            $warnings += "Package not found in csproj: $package"
        }
    }

    if ($Detailed) {
        Write-Host "  ✅ Found WileyWidget.csproj" -ForegroundColor Green
    }
}

# Check Database Configuration
Write-Host "💾 Checking Database Configuration..." -ForegroundColor Yellow
$appsettingsPath = Join-Path $projectDir "appsettings.json"
if (!(Test-Path $appsettingsPath)) {
    $warnings += "Missing appsettings.json - database connection may not be configured"
} else {
    $appsettingsContent = Get-Content $appsettingsPath -Raw
    if ($appsettingsContent -notmatch "ConnectionStrings") {
        $warnings += "No ConnectionStrings found in appsettings.json"
    }
    if ($Detailed) {
        Write-Host "  ✅ Found appsettings.json" -ForegroundColor Green
    }
}

# Check Migrations
Write-Host "🔄 Checking Migrations..." -ForegroundColor Yellow
$migrationsPath = Join-Path $projectDir "Migrations"
if (!(Test-Path $migrationsPath)) {
    $warnings += "No Migrations directory found - database may not be initialized"
} else {
    $migrationFiles = Get-ChildItem $migrationsPath -Filter "*.cs" | Where-Object { $_.Name -notmatch "Designer" -and $_.Name -notmatch "ModelSnapshot" }
    if ($null -eq $migrationFiles -or @($migrationFiles).Count -eq 0) {
        $warnings += "No migration files found in Migrations directory"
    } else {
        if ($Detailed) {
            Write-Host "  ✅ Found $(@($migrationFiles).Count) migration(s)" -ForegroundColor Green
        }
    }
}

# Report Results
Write-Host "" -ForegroundColor White
Write-Host "📊 Validation Results:" -ForegroundColor Cyan
Write-Host "====================" -ForegroundColor Cyan

if ($errors.Count -eq 0) {
    Write-Host "✅ All critical components present!" -ForegroundColor Green
} else {
    Write-Host "❌ Critical Issues Found:" -ForegroundColor Red
    foreach ($error in $errors) {
        Write-Host "  - $error" -ForegroundColor Red
    }
}

if ($warnings.Count -gt 0) {
    Write-Host "⚠️  Warnings:" -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host "  - $warning" -ForegroundColor Yellow
    }
}

# Summary
$totalComponents = $models.Count + $dataFiles.Count + $vmFiles.Count + $testFiles.Count + 3 # +3 for csproj, appsettings, migrations
$foundComponents = $totalComponents - $errors.Count

Write-Host "" -ForegroundColor White
Write-Host "📈 Summary: $foundComponents/$totalComponents components validated" -ForegroundColor Cyan

if ($errors.Count -eq 0) {
    Write-Host "🎉 Phase 1 validation complete! Ready for build and test." -ForegroundColor Green
} else {
    Write-Host "🔧 Please resolve critical issues before proceeding." -ForegroundColor Red
}
