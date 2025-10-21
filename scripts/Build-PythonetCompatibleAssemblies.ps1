<#
.SYNOPSIS
    Builds WileyWidget assemblies without nullable annotations for pythonnet compatibility.

.DESCRIPTION
    Pythonnet cannot load types that use C# 8+ nullable reference types because it fails
    to load System.Runtime.CompilerServices.NullableContextAttribute. This script builds
    the assemblies with nullable disabled specifically for Python interop testing.

.NOTES
    Issue: System.TypeLoadException: Could not load type 'System.Runtime.CompilerServices.NullableContextAttribute'
    Solution: Build with <Nullable>disable</Nullable> for pythonnet consumption
    See: https://github.com/pythonnet/pythonnet/issues/1600
    See: https://stackoverflow.com/questions/62648189/
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Configuration = "Debug",

    [Parameter()]
    [string]$OutputDir = "tools\python\clr_tests\assemblies"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Information "🔧 Building pythonnet-compatible assemblies..." -InformationAction Continue
Write-Information "   Configuration: $Configuration" -InformationAction Continue
Write-Information "   Output: $OutputDir" -InformationAction Continue
Write-Information "" -InformationAction Continue

# Get workspace root
$WorkspaceRoot = Split-Path $PSScriptRoot -Parent
$OutputPath = Join-Path $WorkspaceRoot $OutputDir

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# Build with nullable disabled using MSBuild property override
Write-Information "📦 Building WileyWidget.csproj without nullable..." -InformationAction Continue

$buildArgs = @(
    "build"
    (Join-Path $WorkspaceRoot "WileyWidget.csproj")
    "-c"
    $Configuration
    "-p:Nullable=disable"           # Override nullable setting
    "-p:OutputPath=$OutputPath"     # Output to test assemblies folder
    "-p:GenerateDocumentationFile=false"  # Skip XML docs
    "-p:WarningLevel=0"             # Suppress warnings from nullable disabled
    "--nologo"
)

$process = Start-Process -FilePath "dotnet" -ArgumentList $buildArgs -NoNewWindow -Wait -PassThru

if ($process.ExitCode -ne 0) {
    Write-Error "❌ Build failed with exit code $($process.ExitCode)"
    exit $process.ExitCode
}

Write-Information "✅ Build completed successfully!" -InformationAction Continue
Write-Information "" -InformationAction Continue

# Verify key assemblies exist
$requiredAssemblies = @(
    "WileyWidget.dll",
    "WileyWidget.pdb"
)

Write-Information "🔍 Verifying assemblies..." -InformationAction Continue
$missingAssemblies = @()

foreach ($assembly in $requiredAssemblies) {
    $assemblyPath = Join-Path $OutputPath $assembly
    if (Test-Path $assemblyPath) {
        $fileInfo = Get-Item $assemblyPath
        Write-Information "   ✓ $assembly ($($fileInfo.Length) bytes)" -InformationAction Continue
    }
    else {
        Write-Information "   ✗ $assembly MISSING" -InformationAction Continue
        $missingAssemblies += $assembly
    }
}

if ($missingAssemblies.Count -gt 0) {
    Write-Information "" -InformationAction Continue
    Write-Error "❌ Missing assemblies: $($missingAssemblies -join ', ')"
    exit 1
}

Write-Information "" -InformationAction Continue
Write-Information "✅ All assemblies verified and ready for pythonnet!" -InformationAction Continue
Write-Information "   Location: $OutputPath" -InformationAction Continue
Write-Information "" -InformationAction Continue
Write-Information "💡 Now you can run: python -m unittest tools.python.clr_tests.test_theme_manager" -InformationAction Continue
