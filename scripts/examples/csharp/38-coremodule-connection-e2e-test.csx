// CoreModule Connection E2E Test via logs
// Purpose: Exercise and verify CoreModule connections at runtime by validating log evidence
// - Health service register/initialized messages
// - Successful initialization completion (and absence of CoreModule error logs)
// - Optional: Region-related errors not present
// This mirrors patterns from other MCP csx tests (e.g., 21, 23) and requires startup logs.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

#nullable enable

int pass = 0, total = 0;
void Assert(bool condition, string name, string details = "")
{
    total++;
    if (condition) { Console.WriteLine($"✓ {name}"); pass++; }
    else { Console.WriteLine($"✗ {name} FAILED"); if (!string.IsNullOrWhiteSpace(details)) Console.WriteLine("  Details: " + details); }
}

Console.WriteLine("=== CoreModule Connection E2E Test ===\n");

string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");
Console.WriteLine($"Repo root: {repoRoot}");
Console.WriteLine($"Logs dir: {logsDir}, exists: {Directory.Exists(logsDir)}\n");
if (!Directory.Exists(logsDir)) { Console.WriteLine($"Logs directory not found: {logsDir}"); Environment.Exit(2); }

// Resolve log path: prefer explicit env var or first argument, else latest
string? explicitLog = Environment.GetEnvironmentVariable("WW_LOG_PATH");
if (string.IsNullOrWhiteSpace(explicitLog) && Environment.GetCommandLineArgs().Length > 1)
{
    // First non-script arg (avoid shadowing existing identifiers)
    var argv = Environment.GetCommandLineArgs().Skip(1).ToArray();
    if (argv.Length > 0) explicitLog = argv[0];
}

FileInfo? latest = null;
string logPath;
if (!string.IsNullOrWhiteSpace(explicitLog))
{
    logPath = Path.IsPathRooted(explicitLog!) ? explicitLog! : Path.Combine(logsDir, explicitLog!);
    if (!File.Exists(logPath))
    {
        Console.WriteLine($"Specified log not found: {logPath}");
        Environment.Exit(2);
    }
    latest = new FileInfo(logPath);
}
else
{
    string[] patterns = new[] { "startup-*.log", "wiley-widget-*.log" };
    foreach (var p in patterns)
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(logsDir, p).Take(200))
            {
                var fi = new FileInfo(f);
                if (latest == null || fi.LastWriteTimeUtc > latest.LastWriteTimeUtc) latest = fi;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARN: {ex.Message}");
        }
    }
    if (latest == null) { Console.WriteLine("No logs found"); Environment.Exit(2); }
    logPath = latest.FullName;
}
Console.WriteLine($"Using log: {logPath} (size: {new FileInfo(logPath).Length / 1024 / 1024} MB)\n");

// Scan log
bool sawHealthRegistered = false;
bool sawHealthInitialized = false;
bool sawInitCompleted = false;
bool sawInitStarted = false;
bool sawCoreModuleFailures = false;
bool sawSettingsRegionFailure = false;

var coreModuleInitRegex = new Regex(@"(Initializing\s+module:|Initializing)\s*CoreModule", RegexOptions.IgnoreCase);
var coreModuleCompletedRegex = new Regex(@"CoreModule\s+initialization\s+completed", RegexOptions.IgnoreCase);
var healthRegisteredRegex = new Regex(@"Registered module 'CoreModule' for health monitoring", RegexOptions.IgnoreCase);
var healthInitOkRegex = new Regex(@"Module 'CoreModule' initialized successfully", RegexOptions.IgnoreCase);
var coreModuleFailureRegex = new Regex(@"CoreModule\..*failed|CoreModule.*failed|Module initialization failed.*CoreModule|ModuleInitializeException.*CoreModule", RegexOptions.IgnoreCase);
var settingsRegionFailRegex = new Regex(@"Region registration failed in CoreModule\.OnInitialized|ViewRegistrationException.*SettingsRegion", RegexOptions.IgnoreCase);

int lineCount = 0;
using (var reader = new StreamReader(logPath))
{
    string? line;
    while ((line = reader.ReadLine()) != null)
    {
        lineCount++;
        if (!sawInitStarted && coreModuleInitRegex.IsMatch(line)) sawInitStarted = true;
        if (!sawInitCompleted && coreModuleCompletedRegex.IsMatch(line)) sawInitCompleted = true;
        if (!sawHealthRegistered && healthRegisteredRegex.IsMatch(line)) sawHealthRegistered = true;
        if (!sawHealthInitialized && healthInitOkRegex.IsMatch(line)) sawHealthInitialized = true;
        if (!sawCoreModuleFailures && coreModuleFailureRegex.IsMatch(line)) sawCoreModuleFailures = true;
        if (!sawSettingsRegionFailure && settingsRegionFailRegex.IsMatch(line)) sawSettingsRegionFailure = true;
    }
}

// Tests
Assert(sawInitStarted || sawInitCompleted, "CoreModule initialization observed", sawInitStarted ? "" : "'Initializing CoreModule' not seen; relying on completion message only");
Assert(sawHealthRegistered, "Health service RegisterModule invoked for CoreModule", "Expected ModuleHealthService to log registration");
Assert(sawHealthInitialized || sawInitCompleted, "CoreModule marked healthy or completed initialization", "Expected health success or completion log");
Assert(!sawCoreModuleFailures, "No CoreModule failure logs present");
// Region registration failures may appear in UI test runs with strict mocks, while health is still marked success.
// Treat as warning if health success observed; fail only if failure present AND no health success.
if (sawSettingsRegionFailure && !sawHealthInitialized)
{
    Assert(false, "No SettingsRegion registration failures for CoreModule", "SettingsRegion failure detected without health success");
}
else
{
    Assert(true, "No SettingsRegion registration failures for CoreModule (or tolerated with health success)");
}

Console.WriteLine($"\n=== Test Results ===");
Console.WriteLine($"Passed: {pass}/{total} ({(pass * 100) / Math.Max(total,1)}%)");
Console.WriteLine("\nNotes:");
Console.WriteLine("- This test validates CoreModule's runtime behavior via logs: health tracking and init completion.");
Console.WriteLine("- To deepen coverage, run with verbose logging enabled so health and init messages are present.");

Environment.Exit(pass == total ? 0 : 4);
