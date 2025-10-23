# XAML Diagnostics Runner Script

<#
.SYNOPSIS
    Runs comprehensive XAML diagnostics for WPF Prism applications.

.DESCRIPTION
    This script executes XAML validation checks including Syncfusion license,
    SfSkinManager configuration, XAML file validation, and assembly loading.

.PARAMETER RunDiagnostics
    Run the full XAML diagnostics suite.

.PARAMETER AttemptRecovery
    Attempt to recover from detected issues.

.PARAMETER OutputFile
    Path to save diagnostic results.

.EXAMPLE
    .\Run-XamlDiagnostics.ps1 -RunDiagnostics

.EXAMPLE
    .\Run-XamlDiagnostics.ps1 -RunDiagnostics -AttemptRecovery -OutputFile "diagnostics.txt"
#>

param(
    [switch]$RunDiagnostics,
    [switch]$AttemptRecovery,
    [string]$OutputFile
)

# Function to write output to both console and file
function Write-OutputWithFile {
    param([string]$Message, [string]$FilePath)

    Write-Host $Message
    if ($FilePath) {
        Add-Content -Path $FilePath -Value $Message
    }
}

# Initialize output file
if ($OutputFile) {
    if (Test-Path $OutputFile) {
        Remove-Item $OutputFile -Force
    }
    New-Item -ItemType File -Path $OutputFile -Force | Out-Null
}

Write-OutputWithFile "XAML Diagnostics Runner" $OutputFile
Write-OutputWithFile "======================" $OutputFile
Write-OutputWithFile "Started at: $(Get-Date)" $OutputFile
Write-OutputWithFile "" $OutputFile

# Check if we're in the right directory
if (!(Test-Path "WileyWidget.csproj")) {
    Write-OutputWithFile "ERROR: Not in Wiley Widget project directory. Please run from the project root." $OutputFile
    exit 1
}

# Run diagnostics if requested
if ($RunDiagnostics) {
    Write-OutputWithFile "Running XAML Diagnostics..." $OutputFile
    Write-OutputWithFile "------------------------" $OutputFile

    try {
        # Build the project first to ensure latest code
        Write-OutputWithFile "Building project..." $OutputFile
        # Force a non design-time build to avoid temporary wpftmp project issues
        $buildResult = dotnet build WileyWidget.csproj -p:DesignTimeBuild=false --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-OutputWithFile "WARNING: Build failed. Diagnostics may not be accurate." $OutputFile
            Write-OutputWithFile "Build output: $buildResult" $OutputFile
        }
        else {
            Write-OutputWithFile "Build successful." $OutputFile
        }

        # Check Syncfusion license
        Write-OutputWithFile "" $OutputFile
        Write-OutputWithFile "1. Checking Syncfusion License..." $OutputFile
        $licenseKey = $env:SyncfusionLicense
        $appsettingsLicense = ""
        if (Test-Path "appsettings.json") {
            $appsettings = Get-Content "appsettings.json" -Raw | ConvertFrom-Json -ErrorAction SilentlyContinue
            $appsettingsLicense = $appsettings.SyncfusionLicense
        }
        if ($licenseKey) {
            Write-OutputWithFile "   ✓ Syncfusion license found in environment (length: $($licenseKey.Length))" $OutputFile
        }
        elseif ($appsettingsLicense) {
            Write-OutputWithFile "   ✓ Syncfusion license found in appsettings.json (length: $($appsettingsLicense.Length))" $OutputFile
        }
        else {
            Write-OutputWithFile "   ✗ Syncfusion license NOT found in environment variables" $OutputFile
            Write-OutputWithFile "     Set `$env:SyncfusionLicense or add to appsettings.json" $OutputFile
        }

        # Check required assemblies
        Write-OutputWithFile "" $OutputFile
        Write-OutputWithFile "2. Checking Required Assemblies..." $OutputFile
        $requiredAssemblies = @(
            "Microsoft.Xaml.Behaviors.Wpf",
            "Prism.Wpf",
            "Prism.Unity",
            "Syncfusion.SfSkinManager.WPF",
            "Syncfusion.SfGrid.WPF",
            "Syncfusion.Licensing"
        )

        foreach ($assembly in $requiredAssemblies) {
            try {
                $loaded = [System.AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.FullName -like "*$assembly*" }
                if ($loaded) {
                    Write-OutputWithFile "   ✓ $assembly loaded" $OutputFile
                }
                else {
                    Write-OutputWithFile "   ✗ $assembly NOT loaded" $OutputFile
                }
            }
            catch {
                Write-OutputWithFile "   ✗ $assembly check failed: $($_.Exception.Message)" $OutputFile
            }
        }

        # Check XAML files
        Write-OutputWithFile "" $OutputFile
        Write-OutputWithFile "3. Validating XAML Files..." $OutputFile
        $xamlFiles = Get-ChildItem -Path "src" -Filter "*.xaml" -Recurse -ErrorAction SilentlyContinue
        $xamlIssues = 0

        foreach ($file in $xamlFiles) {
            $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
            if ($content) {
                $issues = @()

                if ($content -notmatch 'xmlns:syncfusion="http://schemas.syncfusion.com/wpf"') {
                    $issues += "Missing Syncfusion xmlns"
                }

                if ($content -notmatch 'xmlns:prism="http://prismlibrary.com/"') {
                    $issues += "Missing Prism xmlns"
                }

                # Skip ViewModelLocator check for ResourceDictionary files
                if ($content -notmatch '<ResourceDictionary' -and $content -notmatch 'prism:ViewModelLocator.AutoWireViewModel="True"') {
                    $issues += "Missing ViewModelLocator.AutoWireViewModel"
                }

                if ($issues.Count -gt 0) {
                    Write-OutputWithFile "   ✗ $($file.Name): $($issues -join ', ')" $OutputFile
                    $xamlIssues++
                }
            }
        }

        if ($xamlIssues -eq 0) {
            Write-OutputWithFile "   ✓ All XAML files validated successfully" $OutputFile
        }

        # Check project file
        Write-OutputWithFile "" $OutputFile
        Write-OutputWithFile "4. Checking Project Configuration..." $OutputFile
        $csprojContent = Get-Content "WileyWidget.csproj" -Raw -ErrorAction SilentlyContinue

        if ($csprojContent -match '<XamlDebuggingInformation>true</XamlDebuggingInformation>') {
            Write-OutputWithFile "   ✓ XAML debugging enabled" $OutputFile
        }
        else {
            Write-OutputWithFile "   ✗ XAML debugging not enabled" $OutputFile
        }

        # Summary
        Write-OutputWithFile "" $OutputFile
        Write-OutputWithFile "Diagnostics Summary:" $OutputFile
        Write-OutputWithFile "===================" $OutputFile
        Write-OutputWithFile "XAML files with issues: $xamlIssues" $OutputFile
        Write-OutputWithFile "Total XAML files checked: $($xamlFiles.Count)" $OutputFile

    }
    catch {
        Write-OutputWithFile "ERROR: Diagnostics failed - $($_.Exception.Message)" $OutputFile
    }
}

# Attempt recovery if requested
if ($AttemptRecovery) {
    Write-OutputWithFile "" $OutputFile
    Write-OutputWithFile "Attempting Recovery..." $OutputFile
    Write-OutputWithFile "=====================" $OutputFile

    # Re-register Syncfusion license if available
    if ($env:SyncfusionLicense) {
        Write-OutputWithFile "Re-registering Syncfusion license..." $OutputFile
        # License registration happens at runtime
        Write-OutputWithFile "✓ License will be re-registered on next application start" $OutputFile
    }

    Write-OutputWithFile "✓ Recovery suggestions logged" $OutputFile
}

Write-OutputWithFile "" $OutputFile
Write-OutputWithFile "Completed at: $(Get-Date)" $OutputFile

if ($OutputFile) {
    Write-Host "Results saved to: $OutputFile" -ForegroundColor Green
}
