#Requires -Version 7.5

<#
.SYNOPSIS
    Tests DI container registrations by building and analyzing the WileyWidget application.

.DESCRIPTION
    Builds the WileyWidget.WinUI project and analyzes startup logs to identify
    missing or misconfigured dependency injection registrations.

    This script:
    1. Builds the solution
    2. Runs the application briefly to trigger DI validation
    3. Analyzes logs for validation results
    4. Generates a report of missing registrations

.PARAMETER Clean
    Performs a clean build before validation.

.PARAMETER OutputPath
    Path to write the validation report. Defaults to logs/di-validation-report.txt

.PARAMETER ShowAllServices
    If specified, shows all discovered services, not just missing ones.

.EXAMPLE
    Test-DiRegistrations.ps1
    Runs DI validation and reports missing services.

.EXAMPLE
    Test-DiRegistrations.ps1 -Clean -ShowAllServices
    Clean build, validate, and show all discovered services.

.NOTES
    Part of Wiley Widget DI validation infrastructure.
    Requires: .NET 9.0 SDK, WileyWidget solution
    Author: Wiley Widget Team
    Version: 1.0.0
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Clean,

    [Parameter()]
    [string]$OutputPath = "logs/di-validation-report.txt",

    [Parameter()]
    [switch]$ShowAllServices
)

$ErrorActionPreference = "Stop"
# Get repo root (scripts/tools -> scripts -> repo root)
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

Write-Information "═══════════════════════════════════════════════════════" -InformationAction Continue
Write-Information "   WileyWidget DI Registration Validation" -InformationAction Continue
Write-Information "═══════════════════════════════════════════════════════" -InformationAction Continue
Write-Information "" -InformationAction Continue

# Step 1: Build the solution
Write-Verbose "[1/4] Building solution..."
Write-Information "[1/4] Building solution..." -InformationAction Continue

try {
    Push-Location $RepoRoot

    if ($Clean) {
        Write-Verbose "Performing clean build..."
        Write-Information "  Performing clean build..." -InformationAction Continue
        & dotnet clean WileyWidget.sln --verbosity quiet
    }

    $buildArgs = @(
        "build"
        "WileyWidget.sln"
        "--configuration", "Debug"
        "--verbosity", "minimal"
        "/p:RunAnalyzers=false"
    )

    & dotnet @buildArgs

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }

    Write-Information "  ✓ Build successful" -InformationAction Continue
}
catch {
    Write-Error "Build failed: $_"
    exit 1
}
finally {
    Pop-Location
}

# Step 2: Clear old logs and run app briefly to trigger validation
Write-Information "" -InformationAction Continue
Write-Verbose "[2/4] Running DI validation..."
Write-Information "[2/4] Running DI validation..." -InformationAction Continue

try {
    $logsDir = Join-Path $RepoRoot "logs"
    $startupLog = Get-ChildItem -Path $logsDir -Filter "startup-*.log" -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($startupLog) {
        Write-Verbose "Clearing existing startup log: $($startupLog.Name)"
        Write-Information "  Clearing existing startup log: $($startupLog.Name)" -InformationAction Continue
        Remove-Item $startupLog.FullName -Force
    }

    # Run the app briefly (it will validate on startup)
    Write-Verbose "Launching application (will exit automatically after validation)..."
    Write-Information "  Launching application (will exit automatically after validation)..." -InformationAction Continue

    $exePath = Join-Path $RepoRoot "src\WileyWidget.WinUI\bin\Debug\net9.0-windows10.0.26100.0\win-x64\WileyWidget.WinUI.exe"

    if (-not (Test-Path $exePath)) {
        throw "Application executable not found: $exePath"
    }

    # Start the app and wait a few seconds for DI validation to run
    $process = Start-Process -FilePath $exePath -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 3

    # Kill the process
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }

    Write-Information "  ✓ Validation complete" -InformationAction Continue
}
catch {
    Write-Warning "Failed to run application: $_"
    Write-Information "  Continuing with log analysis..." -InformationAction Continue
}

# Step 3: Analyze logs for validation results
Write-Information "" -InformationAction Continue
Write-Verbose "[3/4] Analyzing validation results..."
Write-Information "[3/4] Analyzing validation results..." -InformationAction Continue

try {
    $startupLog = Get-ChildItem -Path $logsDir -Filter "startup-*.log" -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $startupLog) {
        throw "No startup log found in $logsDir"
    }

    Write-Verbose "Reading log: $($startupLog.Name)"
    Write-Information "  Reading log: $($startupLog.Name)" -InformationAction Continue

    $logContent = Get-Content $startupLog.FullName -Raw

    # Extract validation summary
    $summaryPattern = '\[DI\] Full validation: (.+)'
    $summaryMatch = [regex]::Match($logContent, $summaryPattern)

    # Extract missing services
    $missingPattern = '\[DI\] Missing registration: (.+)'
    $missingMatches = [regex]::Matches($logContent, $missingPattern)

    # Extract errors
    $errorPattern = '\[DI\] Registration error: (.+)'
    $errorMatches = [regex]::Matches($logContent, $errorPattern)

    # Extract discovered services if requested
    $discoveredServices = @()
    if ($ShowAllServices) {
        $discoveredPattern = '\[DI_VALIDATION\] ✓ (.+)'
        $discoveredMatches = [regex]::Matches($logContent, $discoveredPattern)
        $discoveredServices = $discoveredMatches | ForEach-Object { $_.Groups[1].Value }
    }

    Write-Information "  ✓ Analysis complete" -InformationAction Continue

    # Display results
    Write-Information "" -InformationAction Continue
    Write-Verbose "[4/4] Validation Results:"
    Write-Information "[4/4] Validation Results:" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    if ($summaryMatch.Success) {
        Write-Information "  Summary: $($summaryMatch.Groups[1].Value)" -InformationAction Continue
        Write-Information "" -InformationAction Continue
    }

    if ($missingMatches.Count -gt 0) {
        Write-Information "  ⚠ Missing Registrations ($($missingMatches.Count)):" -InformationAction Continue
        foreach ($match in $missingMatches) {
            $serviceName = $match.Groups[1].Value
            Write-Warning "    • $serviceName"
        }
        Write-Information "" -InformationAction Continue
    }
    else {
        Write-Information "  ✓ No missing registrations found!" -InformationAction Continue
        Write-Information "" -InformationAction Continue
    }

    if ($errorMatches.Count -gt 0) {
        Write-Information "  ❌ Registration Errors ($($errorMatches.Count)):" -InformationAction Continue
        foreach ($match in $errorMatches) {
            Write-Error "    • $($match.Groups[1].Value)"
        }
        Write-Information "" -InformationAction Continue
    }

    if ($ShowAllServices -and $discoveredServices.Count -gt 0) {
        Write-Information "  ✓ Discovered Services ($($discoveredServices.Count)):" -InformationAction Continue
        foreach ($service in $discoveredServices) {
            Write-Verbose "    • $service"
        }
        Write-Information "" -InformationAction Continue
    }

    # Generate report file
    $reportPath = Join-Path $RepoRoot $OutputPath
    $reportDir = Split-Path -Parent $reportPath
    if (-not (Test-Path $reportDir)) {
        New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    }

    $reportContent = @"
═══════════════════════════════════════════════════════
WileyWidget DI Validation Report
Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
═══════════════════════════════════════════════════════

SUMMARY:
$($summaryMatch.Groups[1].Value)

MISSING REGISTRATIONS ($($missingMatches.Count)):
$($missingMatches | ForEach-Object { "  • $($_.Groups[1].Value)" } | Out-String)

REGISTRATION ERRORS ($($errorMatches.Count)):
$($errorMatches | ForEach-Object { "  • $($_.Groups[1].Value)" } | Out-String)

"@

    if ($ShowAllServices -and $discoveredServices.Count -gt 0) {
        $reportContent += @"
DISCOVERED SERVICES ($($discoveredServices.Count)):
$($discoveredServices | ForEach-Object { "  • $_" } | Out-String)

"@
    }

    $reportContent += @"
RECOMMENDED ACTIONS:
1. Review missing services and add registrations to DependencyInjection.cs
2. Verify all repository interfaces have implementations registered
3. Check for circular dependencies causing resolution errors
4. Ensure scoped services (DbContext, Repositories) use AddScoped<>()
5. Ensure singleton services (Settings, Telemetry) use AddSingleton<>()

For detailed logs, see: $($startupLog.FullName)
"@

    Set-Content -Path $reportPath -Value $reportContent -Encoding UTF8
    Write-Information "  Report saved to: $OutputPath" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    # Exit with error code if issues found
    if ($missingMatches.Count -gt 0 -or $errorMatches.Count -gt 0) {
        Write-Warning "⚠ DI validation found issues - see report for details"
        exit 1
    }
    else {
        Write-Information "✓ All DI registrations validated successfully!" -InformationAction Continue
        exit 0
    }
}
catch {
    Write-Error "Log analysis failed: $_"
    exit 1
}
