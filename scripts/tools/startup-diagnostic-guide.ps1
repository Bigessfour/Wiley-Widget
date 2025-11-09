# Comprehensive Startup Diagnostic Usage Guide
# ==================================================
#
# This script demonstrates how to use the fully implemented diagnostic system
# for debugging Wiley Widget startup issues.
#
# Date: November 9, 2025

Write-Host "🔍 WILEY WIDGET STARTUP DIAGNOSTIC SYSTEM" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Enable Verbose Logging Configuration
Write-Host "📋 STEP 1: ENABLE VERBOSE LOGGING" -ForegroundColor Green
Write-Host "Add the following to config/development/appsettings.json:" -ForegroundColor Yellow
Write-Host @"
{
  "Diagnostics": {
    "Startup": {
      "EnableVerboseLogging": true,
      "EnableBreakpointDebugging": true,
      "EnablePhaseIsolation": false,
      "EnableRuntimeProfiler": false
    },
    "PhaseIsolation": {
      "SkipResourceLoading": false,
      "SkipTelemetryInitialization": false,
      "SkipModuleInitialization": false
    },
    "Runtime": {
      "EnableDotnetTrace": false,
      "TraceProviders": [
        "Microsoft-DotNETCore-SampleProfiler",
        "Microsoft-Windows-DotNETRuntime"
      ]
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "WileyWidget": "Verbose",
        "WileyWidget.Startup": "Debug",
        "WileyWidget.DI": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/startup-.txt",
          "rollingInterval": "Day",
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u4}] {SourceContext} {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
"@ -ForegroundColor White
Write-Host ""

# Step 2: Breakpoint Debugging Instructions
Write-Host "🐛 STEP 2: BREAKPOINT DEBUGGING" -ForegroundColor Green
Write-Host "The diagnostic system automatically performs breakpoint debugging at key phases:" -ForegroundColor Yellow
Write-Host "  • Phase 1 (Pre-Prism): Before container initialization" -ForegroundColor White
Write-Host "  • Phase 2 (Post-Prism): After container setup" -ForegroundColor White
Write-Host "  • Phase 3 (OnInitialized): During module initialization" -ForegroundColor White
Write-Host ""
Write-Host "To manually inspect:" -ForegroundColor Yellow
Write-Host "1. Set breakpoint in App.Lifecycle.cs -> OnStartup() after 'base.OnStartup(e)'" -ForegroundColor White
Write-Host "2. In debugger, inspect:" -ForegroundColor White
Write-Host "   - this.Container?.IsRegistered<IConfiguration>()" -ForegroundColor Gray
Write-Host "   - SfSkinManager.ApplicationTheme (should not be null)" -ForegroundColor Gray
Write-Host "   - Environment.WorkingSet (memory usage)" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Check logs/breakpoint-*.txt files for detailed reports" -ForegroundColor White
Write-Host ""

# Step 3: Phase Isolation Instructions
Write-Host "🧪 STEP 3: PHASE ISOLATION FOR SYSTEMATIC DEBUGGING" -ForegroundColor Green
Write-Host "To isolate problematic phases, modify appsettings.json:" -ForegroundColor Yellow
Write-Host @"
"Diagnostics": {
  "Startup": {
    "EnablePhaseIsolation": true
  },
  "PhaseIsolation": {
    "SkipResourceLoading": true,     // Comment out LoadApplicationResourcesSync()
    "SkipTelemetryInitialization": true, // Comment out InitializeSigNozTelemetry()
    "SkipModuleInitialization": true     // Comment out base.OnInitialized()
  }
}
"@ -ForegroundColor White
Write-Host ""
Write-Host "This will systematically skip phases to identify which one is causing issues." -ForegroundColor Yellow
Write-Host ""

# Step 4: Runtime Profiler Instructions
Write-Host "📊 STEP 4: RUNTIME PROFILER (PERFORMANCE ANALYSIS)" -ForegroundColor Green
Write-Host "1. Install dotnet-trace (if not already installed):" -ForegroundColor Yellow
Write-Host "   dotnet tool install --global dotnet-trace" -ForegroundColor White
Write-Host ""
Write-Host "2. Enable in appsettings.json:" -ForegroundColor Yellow
Write-Host @"
"Diagnostics": {
  "Runtime": {
    "EnableDotnetTrace": true
  }
}
"@ -ForegroundColor White
Write-Host ""
Write-Host "3. Run app - trace file will be created in logs/ directory" -ForegroundColor Yellow
Write-Host "4. Analyze with: dotnet trace convert logs/wiley-widget-trace-*.nettrace" -ForegroundColor White
Write-Host ""

# Step 5: Log Analysis Instructions
Write-Host "📋 STEP 5: STARTUP LOG ANALYSIS" -ForegroundColor Green
Write-Host "The system automatically analyzes logs for:" -ForegroundColor Yellow
Write-Host "  • Exception stack traces" -ForegroundColor White
Write-Host "  • 'Failed to resolve' dependency injection errors" -ForegroundColor White
Write-Host "  • Registration failures" -ForegroundColor White
Write-Host ""
Write-Host "Check these log files:" -ForegroundColor Yellow
Write-Host "  📁 logs/startup-*.txt - General startup logs" -ForegroundColor White
Write-Host "  📁 logs/startup-verbose-*.txt - Verbose diagnostic logs" -ForegroundColor White
Write-Host "  📁 logs/breakpoint-*.txt - Breakpoint analysis reports" -ForegroundColor White
Write-Host ""

# Common Debugging Scenarios
Write-Host "🔧 COMMON DEBUGGING SCENARIOS" -ForegroundColor Green
Write-Host ""

Write-Host "Scenario 1: 'Failed to resolve service' errors" -ForegroundColor Yellow
Write-Host "✓ Check container registration in App.DependencyInjection.cs" -ForegroundColor White
Write-Host "✓ Look for circular dependencies" -ForegroundColor White
Write-Host "✓ Check service lifetimes (Singleton vs Transient)" -ForegroundColor White
Write-Host ""

Write-Host "Scenario 2: Theme-related failures" -ForegroundColor Yellow
Write-Host "✓ Verify SfSkinManager.ApplicationTheme is not null" -ForegroundColor White
Write-Host "✓ Check Syncfusion license configuration" -ForegroundColor White
Write-Host "✓ Ensure theme is applied before Prism initialization" -ForegroundColor White
Write-Host ""

Write-Host "Scenario 3: Memory issues" -ForegroundColor Yellow
Write-Host "✓ Check working set in breakpoint reports" -ForegroundColor White
Write-Host "✓ Monitor GC collections" -ForegroundColor White
Write-Host "✓ Look for assembly loading issues" -ForegroundColor White
Write-Host ""

Write-Host "Scenario 4: Module initialization failures" -ForegroundColor Yellow
Write-Host "✓ Enable phase isolation to skip module init" -ForegroundColor White
Write-Host "✓ Check if issue is in LoadApplicationResourcesSync()" -ForegroundColor White
Write-Host "✓ Review module dependencies and registration order" -ForegroundColor White
Write-Host ""

# Quick Start Commands
Write-Host "⚡ QUICK START COMMANDS" -ForegroundColor Green
Write-Host ""
Write-Host "Enable all diagnostics:" -ForegroundColor Yellow
$quickConfig = @"
{
  "Diagnostics": {
    "Startup": {
      "EnableVerboseLogging": true,
      "EnableBreakpointDebugging": true,
      "EnablePhaseIsolation": false,
      "EnableRuntimeProfiler": false
    }
  },
  "Serilog": {
    "MinimumLevel": { "Default": "Debug", "Override": { "WileyWidget": "Verbose" } },
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/startup-.txt" } }
    ]
  }
}
"@

Write-Host "Run with full diagnostics:" -ForegroundColor Yellow
Write-Host "1. Copy config above to config/development/appsettings.json" -ForegroundColor White
Write-Host "2. Build and run: dotnet run" -ForegroundColor White
Write-Host "3. Check logs/ directory for diagnostic output" -ForegroundColor White
Write-Host ""

Write-Host "Check for issues:" -ForegroundColor Yellow
Write-Host "Get-ChildItem logs/ | Select-String 'Exception|Failed to resolve'" -ForegroundColor White
Write-Host ""

Write-Host "🎯 The diagnostic system is now fully integrated into the startup process!" -ForegroundColor Cyan
Write-Host "All diagnostic steps run automatically when enabled via configuration." -ForegroundColor Green
Write-Host ""
