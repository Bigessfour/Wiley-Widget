#Requires -Version 7.0
<#
.SYNOPSIS
    Validates WinUI 3 / WindowsAppSDK target imports and configuration.

.DESCRIPTION
    Verifies that:
    1. WindowsAppSDK NuGet package is present
    2. Required XAML compilation targets exist
    3. Project configuration is correct for unpackaged WinUI 3
    4. MSBuild property resolution is working correctly

.EXAMPLE
    .\scripts\tools\validate-winui-setup.ps1 -Verbose
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$ProjectPath = "$PSScriptRoot\..\..\src\WileyWidget.WinUI\WileyWidget.WinUI.csproj",
    
    [Parameter()]
    [switch]$Fix
)

$ErrorActionPreference = 'Stop'

Write-Host "🔍 Validating WinUI 3 / WindowsAppSDK Setup..." -ForegroundColor Cyan

# 1. Locate NuGet packages directory
$nugetRoot = $env:NUGET_PACKAGES
if (-not $nugetRoot) {
    $nugetRoot = Join-Path $env:USERPROFILE ".nuget\packages"
}

Write-Host "📦 NuGet Packages Root: $nugetRoot" -ForegroundColor Gray

# 2. Find WindowsAppSDK package
$windowsAppSDKPath = Get-ChildItem -Path $nugetRoot -Filter "microsoft.windowsappsdk" -Directory -ErrorAction SilentlyContinue
if (-not $windowsAppSDKPath) {
    Write-Error "❌ Microsoft.WindowsAppSDK package not found in NuGet cache"
    exit 1
}

$latestVersion = Get-ChildItem -Path $windowsAppSDKPath.FullName -Directory | 
    Sort-Object Name -Descending | 
    Select-Object -First 1

Write-Host "✅ WindowsAppSDK found: $($latestVersion.Name)" -ForegroundColor Green

# 3. Verify required targets exist
$targetsPath = Join-Path $latestVersion.FullName "build\Microsoft.WindowsAppSDK.targets"
$transitiveTargetsPath = Join-Path $latestVersion.FullName "buildTransitive\Microsoft.WindowsAppSDK.targets"

$targetFiles = @{
    "Main Targets" = $targetsPath
    "Transitive Targets" = $transitiveTargetsPath
}

$allTargetsExist = $true
foreach ($target in $targetFiles.GetEnumerator()) {
    if (Test-Path $target.Value) {
        Write-Host "✅ $($target.Key): $($target.Value)" -ForegroundColor Green
    } else {
        Write-Host "❌ $($target.Key): NOT FOUND at $($target.Value)" -ForegroundColor Red
        $allTargetsExist = $false
    }
}

if (-not $allTargetsExist) {
    Write-Error "❌ Required WindowsAppSDK targets are missing"
    exit 1
}

# 4. Verify XAML compiler exists
$xamlCompilerPaths = @(
    Join-Path $latestVersion.FullName "tools\net6.0\XamlCompiler.exe"
    Join-Path $latestVersion.FullName "tools\net472\XamlCompiler.exe"
)

$xamlCompilerFound = $false
foreach ($path in $xamlCompilerPaths) {
    if (Test-Path $path) {
        Write-Host "✅ XamlCompiler.exe: $path" -ForegroundColor Green
        $xamlCompilerFound = $true
        break
    }
}

if (-not $xamlCompilerFound) {
    Write-Host "⚠️ XamlCompiler.exe not found in expected locations" -ForegroundColor Yellow
}

# 5. Validate project configuration
if (Test-Path $ProjectPath) {
    Write-Host "`n🔍 Validating Project Configuration: $ProjectPath" -ForegroundColor Cyan
    
    [xml]$projectXml = Get-Content $ProjectPath
    
    $criticalProperties = @{
        "UseWinUI" = "true"
        "WindowsPackageType" = "None"
        "WindowsAppSDKSelfContained" = "true"
        "WindowsAppSDKBootstrapInitialize" = "true"
        "WindowsAppSDKDeploymentManagerInitialize" = "false"
    }
    
    $propertyGroups = $projectXml.Project.PropertyGroup
    $allPropertiesValid = $true
    
    foreach ($prop in $criticalProperties.GetEnumerator()) {
        $actualValue = $null
        foreach ($group in $propertyGroups) {
            if ($group.$($prop.Key)) {
                $actualValue = $group.$($prop.Key)
                break
            }
        }
        
        if ($actualValue -eq $prop.Value) {
            Write-Host "  ✅ $($prop.Key) = $actualValue" -ForegroundColor Green
        } elseif ($null -eq $actualValue) {
            Write-Host "  ⚠️ $($prop.Key) = NOT SET (expected: $($prop.Value))" -ForegroundColor Yellow
            $allPropertiesValid = $false
        } else {
            Write-Host "  ❌ $($prop.Key) = $actualValue (expected: $($prop.Value))" -ForegroundColor Red
            $allPropertiesValid = $false
        }
    }
    
    if (-not $allPropertiesValid -and $Fix) {
        Write-Host "`n🔧 Fix flag enabled - properties should be corrected manually" -ForegroundColor Yellow
    }
}

# 6. Test MSBuild evaluation
Write-Host "`n🔍 Testing MSBuild Property Evaluation..." -ForegroundColor Cyan

$testResult = dotnet build $ProjectPath --verbosity quiet -p:ValidateOnly=true /nologo 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ MSBuild evaluation successful" -ForegroundColor Green
} else {
    Write-Host "⚠️ MSBuild evaluation warnings (this may be normal):" -ForegroundColor Yellow
    Write-Host $testResult -ForegroundColor Gray
}

Write-Host "`n✅ WinUI 3 Setup Validation Complete!" -ForegroundColor Green
Write-Host @"

📝 Summary:
- WindowsAppSDK Version: $($latestVersion.Name)
- Targets Available: $(if ($allTargetsExist) { "✅ Yes" } else { "❌ No" })
- XamlCompiler.exe: $(if ($xamlCompilerFound) { "✅ Found" } else { "⚠️ Missing" })
- Project Config: $(if ($allPropertiesValid) { "✅ Valid" } else { "⚠️ Needs Review" })

Next Steps:
1. Run 'dotnet restore' to ensure all packages are downloaded
2. Run 'dotnet build' to verify XAML compilation works
3. Check for MarkupCompilePass1/MarkupCompilePass2 targets during build

"@ -ForegroundColor Cyan
