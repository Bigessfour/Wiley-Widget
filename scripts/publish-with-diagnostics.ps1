# Enhanced publish script with comprehensive diagnostics
# Use this to identify missing dependencies and publishing issues

param(
    [string]$Configuration = "Release",
    [string]$RuntimeId = "win-x64",
    [switch]$Verbose,
    [switch]$CleanFirst
)

$ErrorActionPreference = "Continue"
$ProjectFile = "WileyWidget.csproj"

Write-Host "🔍 === ENHANCED PUBLISH DIAGNOSTICS ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Runtime ID: $RuntimeId" -ForegroundColor Yellow
Write-Host "Project: $ProjectFile" -ForegroundColor Yellow

# Clean if requested
if ($CleanFirst) {
    Write-Host "🧹 Cleaning previous build..." -ForegroundColor Yellow
    dotnet clean $ProjectFile --configuration $Configuration --verbosity detailed
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Clean failed with exit code $LASTEXITCODE"
    }
}

# Set diagnostic environment variables
$env:MSBUILD_VERBOSITY = "detailed"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

# Build with enhanced diagnostics first
Write-Host "🔨 Building with enhanced diagnostics..." -ForegroundColor Yellow
$buildArgs = @(
    "build"
    $ProjectFile
    "--configuration", $Configuration
    "--runtime", $RuntimeId
    "--verbosity", "detailed"
    "--property:ShowDependencies=true"
    "--property:LogLevel=Information"
    "--property:MSBuildVerbosity=detailed"
)

if ($Verbose) {
    $buildArgs += "--property:MSBuildLoggerVerbosity=diagnostic"
}

Write-Host "Build command: dotnet $($buildArgs -join ' ')" -ForegroundColor Gray
& dotnet @buildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# Publish with enhanced diagnostics
Write-Host "📦 Publishing with enhanced diagnostics..." -ForegroundColor Yellow
$publishArgs = @(
    "publish"
    $ProjectFile
    "--configuration", $Configuration
    "--runtime", $RuntimeId
    "--self-contained", "true"
    "--verbosity", "detailed"
    "--property:PublishProfile=FolderProfile"
    "--property:ShowDependencies=true"
    "--property:LogLevel=Information"
    "--property:MSBuildVerbosity=detailed"
    "--property:PublishReferencesDocumentationFiles=true"
    "--property:CopyOutputSymbolsToPublishDirectory=true"
    "--property:ErrorReport=detailed"
    "--property:WarningLevel=4"
)

if ($Verbose) {
    $publishArgs += "--property:MSBuildLoggerVerbosity=diagnostic"
}

Write-Host "Publish command: dotnet $($publishArgs -join ' ')" -ForegroundColor Gray
& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# Post-publish analysis
Write-Host "🔍 === POST-PUBLISH ANALYSIS ===" -ForegroundColor Cyan

$publishDir = "publish"
if (Test-Path $publishDir) {
    $allFiles = Get-ChildItem $publishDir -Recurse -File
    $syncfusionFiles = $allFiles | Where-Object { $_.Name -like "*Syncfusion*" }
    $wpfFiles = $allFiles | Where-Object { $_.Name -like "*Wpf*" -or $_.Name -like "*WPF*" }
    $themeFiles = $allFiles | Where-Object { $_.Name -like "*Theme*" -or $_.Name -like "*Fluent*" }
    
    Write-Host "📊 File Analysis:" -ForegroundColor Green
    Write-Host "  Total files: $($allFiles.Count)" -ForegroundColor White
    Write-Host "  Syncfusion files: $($syncfusionFiles.Count)" -ForegroundColor White
    Write-Host "  WPF files: $($wpfFiles.Count)" -ForegroundColor White
    Write-Host "  Theme-related files: $($themeFiles.Count)" -ForegroundColor White
    
    if ($syncfusionFiles.Count -gt 0) {
        Write-Host "🎨 Syncfusion files found:" -ForegroundColor Green
        $syncfusionFiles | ForEach-Object { Write-Host "  ✅ $($_.Name)" -ForegroundColor Green }
    } else {
        Write-Warning "⚠️ No Syncfusion files found in publish output!"
    }
    
    # Check for specific critical files
    $criticalFiles = @(
        "Syncfusion.SfSkinManager.WPF.dll",
        "Syncfusion.Themes.FluentDark.WPF.dll",
        "Syncfusion.Licensing.dll",
        "WileyWidget.exe",
        "WileyWidget.dll",
        "license.key"
    )
    
    Write-Host "🔍 Critical files check:" -ForegroundColor Yellow
    foreach ($file in $criticalFiles) {
        $found = $allFiles | Where-Object { $_.Name -eq $file }
        if ($found) {
            Write-Host "  ✅ $file" -ForegroundColor Green
        } else {
            Write-Host "  ❌ $file (MISSING)" -ForegroundColor Red
        }
    }
    
    # Check for runtime dependencies
    Write-Host "🔧 Runtime dependencies:" -ForegroundColor Yellow
    $runtimesDir = Join-Path $publishDir "runtimes"
    if (Test-Path $runtimesDir) {
        $runtimeFiles = Get-ChildItem $runtimesDir -Recurse -File
        Write-Host "  ✅ Runtimes directory found with $($runtimeFiles.Count) files" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️ No runtimes directory found" -ForegroundColor Yellow
    }
    
} else {
    Write-Error "❌ Publish directory not found: $publishDir"
    exit 1
}

Write-Host "✅ === PUBLISH DIAGNOSTICS COMPLETE ===" -ForegroundColor Cyan
Write-Host "📁 Published files are in: $publishDir" -ForegroundColor Green

# Test the published application
Write-Host "🧪 Testing published application..." -ForegroundColor Yellow
$exePath = Join-Path $publishDir "WileyWidget.exe"
if (Test-Path $exePath) {
    Write-Host "✅ WileyWidget.exe found at: $exePath" -ForegroundColor Green
    Write-Host "💡 To test manually, run: & '$exePath'" -ForegroundColor Cyan
} else {
    Write-Error "❌ WileyWidget.exe not found in publish output"
    exit 1
}

Write-Host "🎉 Enhanced publish with diagnostics completed successfully!" -ForegroundColor Green
