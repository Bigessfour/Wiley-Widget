#!/usr/bin/env dotnet-script
#load "lib/ModuleAnalyzer.csx"

using System;
using System.IO;

/*
 * Script: CoreModule Connection Analysis & Validation
 * Purpose: Analyze CoreModule.cs to discover connections (DI resolves, region registrations,
 *          health tracking) and list modules that depend on CoreModule. Emits a JSON report
 *          and returns non-zero if required connections are missing.
 *
 * Usage (inside csx container):
 *   dotnet-script 37-coremodule-connection-analysis.csx
 */

var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
var logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");
Directory.CreateDirectory(logsDir);
var outJson = Path.Combine(logsDir, "coremodule-connection-report.json");

Console.WriteLine("=== CoreModule Connection Analysis ===\n");

CoreModuleReport report;
try
{
    report = ModuleAnalyzer.AnalyzeCoreModule(repoRoot);
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Analysis failed: {ex.Message}");
    return 2;
}

ModuleAnalyzer.Print(report);
ModuleAnalyzer.WriteJson(report, outJson);

Console.WriteLine($"\nReport written: {outJson}");

// Run validation
var exit = ModuleAnalyzer.Validate(report);
return exit;
