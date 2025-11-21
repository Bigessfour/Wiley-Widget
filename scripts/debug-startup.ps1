#Requires -Version 7.0
&lt;#
.SYNOPSIS
Automated Wiley Widget Startup Debugger
.DESCRIPTION
Runs the app with enhanced logging, validators, and analysis. Outputs report.json and summary.txt.
.PARAMETER FullTrace
Enable dotnet-trace for performance profiling.
.PARAMETER NoRun
Validation only (no app execution).
.PARAMETER OutputDir
Custom output directory (default: ./debug-reports).
#&gt;

param(
    [switch]$FullTrace,
    [switch]$NoRun,
    [string]$OutputDir = &quot;./debug-reports&quot;
)

# Setup
$ErrorActionPreference = &quot;Stop&quot;
$timestamp = Get-Date -Format &quot;yyyyMMdd-HHmmss&quot;
$logDir = &quot;logs&quot;
$outputPath = Join-Path $OutputDir &quot;startup-debug-$timestamp&quot;
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

Write-Host &quot;=== Wiley Widget Startup Debugger (v1.0) ===&quot; -ForegroundColor Green
Write-Host &quot;Timestamp: $timestamp&quot; -ForegroundColor Yellow
Write-Host &quot;Output: $outputPath&quot; -ForegroundColor Yellow

# Step 1: Environment Setup
$env:DOTNET_ENVIRONMENT = &quot;Development&quot;
$env:DOTNET_LOGGING__Console__LogLevel = &quot;Debug&quot;

# Step 2: Run Validators (always)
Write-Host &quot;Running validators...&quot; -ForegroundColor Cyan
&amp; python tools/startup_validator.py --debug --output (Join-Path $outputPath &quot;validator.json&quot;)
&amp; pwsh scripts/maintenance/validate-di-registrations.ps1 -OutputJson (Join-Path $outputPath &quot;di-validations.json&quot;)
&amp; pwsh tools/run-startup-validator.ps1 -Verbose -Output (Join-Path $outputPath &quot;ps-validator.txt&quot;)

# Step 3: Run App if not NoRun
if (-not $NoRun) {
    Write-Host &quot;Running app with enhanced logging...&quot; -ForegroundColor Cyan
    
    # Capture dotnet run output
    $appOutput = &amp; dotnet run --project Wiley-Widget.csproj --verbosity detailed 2&gt;&amp;1
    
    # Save app output
    $appOutput | Out-File (Join-Path $outputPath &quot;app-output.txt&quot;)
    
    # If FullTrace, run profiler
    if ($FullTrace) {
        dotnet tool install -g dotnet-trace 2&gt;$null
        $tracePid = (Get-Process dotnet -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like &quot;*Wiley Widget*&quot; }).Id
        if ($tracePid) {
            dotnet-trace collect --process-id $tracePid --providers Microsoft-DotNETCore-SampleProfiler --output (Join-Path $outputPath &quot;startup-trace.nettrace&quot;)
            Write-Host &quot;Trace collected: startup-trace.nettrace&quot; -ForegroundColor Green
        }
    }
    
    # Wait for app to exit (or timeout)
    Start-Sleep -Seconds 30  # Adjust based on startup time
}

# Step 4: Analyze Logs
Write-Host &quot;Analyzing logs...&quot; -ForegroundColor Cyan
$logFiles = Get-ChildItem $logDir -Filter &quot;startup-debug-*.txt&quot; | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($logFiles) {
    $latestLog = $logFiles.FullName
    $logContent = Get-Content $latestLog -Raw
    
    # Simple analysis: Count errors, extract phases
    $errorCount = ($logContent | Select-String &quot;ERROR|Fatal|Exception&quot; -AllMatches).Matches.Count
    $phaseMatches = ($logContent | Select-String &quot;Phase \d+:.*Complete&quot;).Matches.Count
    $unresolved = ($logContent | Select-String &quot;Unable to resolve|not resolved&quot; -AllMatches).Matches.Count
    
    $analysis = @{
        Timestamp = $timestamp
        ErrorCount = $errorCount
        PhaseCompletions = $phaseMatches
        UnresolvedDependencies = $unresolved
        Recommendations = @()
    }
    
    if ($errorCount -gt 0) { $analysis.Recommendations += &quot;High error count: Check app-output.txt and $latestLog&quot; }
    if ($unresolved -gt 0) { $analysis.Recommendations += &quot;DI issues: Review di-validations.json&quot; }
    if ($phaseMatches -lt 3) { $analysis.Recommendations += &quot;Incomplete phases: App may not have fully started&quot; }
    
    $analysis | ConvertTo-Json -Depth 3 | Out-File (Join-Path $outputPath &quot;report.json&quot;)
    $analysis | ConvertTo-Json | Out-File (Join-Path $outputPath &quot;summary.txt&quot;)
    
    Write-Host &quot;Analysis complete. Errors: $errorCount | Phases: $phaseMatches&quot; -ForegroundColor $(if ($errorCount -eq 0) { &quot;Green&quot; } else { &quot;Red&quot; })
    Write-Host &quot;Report: $outputPath/report.json&quot; -ForegroundColor Yellow
} else {
    Write-Host &quot;No logs found – check if app ran.&quot; -ForegroundColor Red
}

Write-Host &quot;Debug session complete. Review $outputPath/&quot; -ForegroundColor Green