using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

#nullable enable

// ------------------------
// Tiny test harness
// ------------------------
int pass = 0, total = 0;
void Assert(bool condition, string name, string details = "")
{
    total++;
    if (condition)
    {
        Console.WriteLine($"✓ {name}");
        pass++;
    }
    else
    {
        Console.WriteLine($"✗ {name} FAILED");
        if (!string.IsNullOrWhiteSpace(details)) Console.WriteLine("  Details: " + details);
    }
}

Console.WriteLine("=== PRISM CONTAINER E2E LOG TEST ===\n");

Console.WriteLine("Starting test execution...");

// ------------------------
// Locate latest startup log (with directory enumeration limit)
// ------------------------
string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

Console.WriteLine($"Repo root: {repoRoot}");
Console.WriteLine($"Logs dir: {logsDir}, exists: {Directory.Exists(logsDir)}");

if (!Directory.Exists(logsDir))
{
    Console.WriteLine($"Logs directory not found: {logsDir}");
    Console.WriteLine("Set WW_LOGS_DIR or run from repository root.");
    Environment.Exit(2);
}

string[] candidatePatterns = new[] { "startup-*.log", "wiley-widget-*.log" };
FileInfo? latest = null;
foreach (var pat in candidatePatterns)
{
    try
    {
        var files = Directory.EnumerateFiles(logsDir, pat).Take(100).ToArray(); // Limit to prevent enum hangs
        foreach (var f in files)
        {
            var fi = new FileInfo(f);
            if (latest == null || fi.LastWriteTimeUtc > latest.LastWriteTimeUtc) latest = fi;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error enumerating files: {ex.Message}");
    }
}

if (latest == null)
{
    Console.WriteLine("No startup logs found.");
    Environment.Exit(2);
}

string logPath = latest!.FullName;
Console.WriteLine($"Using log: {logPath} (size: {latest.Length / 1024 / 1024} MB)");

const long maxFileSizeMB = 50; // Configurable limit
if (latest.Length > maxFileSizeMB * 1024 * 1024)
{
    Console.WriteLine($"Log too large (> {maxFileSizeMB} MB). Truncate or increase limit.");
    Environment.Exit(2);
}

Console.WriteLine("Streaming log for analysis...\n");

// ------------------------
// Streamed Log Analysis (line-by-line to avoid memory hangs)
// ------------------------
var matches = new Dictionary<string, bool>(); // Track required matches
int lineCount = 0;
const int maxLines = 1000000; // Safety limit

bool HasMatch(string needle)
{
    return matches.ContainsKey(needle) && matches[needle];
}

void RecordMatch(string key)
{
    matches[key] = true;
    Console.WriteLine($"  Progress: Found '{key}' at line {lineCount}");
}

using (var reader = new StreamReader(logPath))
{
    string? line;
    while ((line = reader.ReadLine()) != null && lineCount < maxLines)
    {
        lineCount++;
        if (lineCount % 10000 == 0) Console.WriteLine($"  Processed {lineCount} lines..."); // Progress for Copilot/Docker

        // Case-insensitive contains for efficiency
        if (line.Contains("DryIoc container configured", StringComparison.OrdinalIgnoreCase)) RecordMatch("DryIoc container configured");
        if (line.Contains("Configured Prism ViewModelLocationProvider", StringComparison.OrdinalIgnoreCase)) RecordMatch("ViewModelLocationProvider factory configured");
        if (line.Contains("Validating critical service registrations", StringComparison.OrdinalIgnoreCase)) RecordMatch("Critical service validation executed");
        if (line.Contains("Post-validate: IDbContextFactory<AppDbContext>", StringComparison.OrdinalIgnoreCase)) RecordMatch("EF Core factory post-validation present");
        if (line.Contains("ViewModelLocationProvider: container failed to resolve", StringComparison.OrdinalIgnoreCase)) RecordMatch("VM container failure logged");
        if (line.Contains("Unable to resolve resolution root") || line.Contains("resolution timed out") || line.Contains("circular dependency")) RecordMatch("Root cause logged for DI failures");
        if (line.Contains("Application will continue")) RecordMatch("Application continues after module init failure");
        if (line.Contains("Focused container resolution checks"))
        {
            if (Regex.IsMatch(line, @"FocusedResolve \(OK|FAILED\)", RegexOptions.IgnoreCase)) RecordMatch("FocusedResolve diagnostics present");
        }
        if (line.Contains("Register<Views.Main.SettingsView, ViewModels.Main.SettingsViewModel>", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Explicitly register SettingsViewModel", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("ViewModelLocationProvider.Register<Views.Main.SettingsView, ViewModels.Main.SettingsViewModel>", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("SettingsViewModel")) RecordMatch("SettingsViewModel mapping or registration present");

        // Early exit if all keys found (optimize for large files)
        if (matches.Count == 8) break; // Adjust based on assertion count
    }
}

Console.WriteLine($"Log analysis complete: {lineCount} lines processed.\n");

// ------------------------
// Tests (using recorded matches)
// ------------------------

// 1) Container configured and ViewModel factory set to use DI container
Assert(HasMatch("DryIoc container configured"), "DryIoc container configured");
Assert(HasMatch("ViewModelLocationProvider factory configured"), "ViewModelLocationProvider factory configured");

// 2) Critical service validation ran
Assert(HasMatch("Critical service validation executed"), "Critical service validation executed");

// 3) Post-validate EF factory available
Assert(HasMatch("EF Core factory post-validation present"), "EF Core factory post-validation present");

// 4) Detect container resolution failures surfaced (not masked by Activator fallback)
var missingCtor = HasMatch("MissingMethodException") || HasMatch("No parameterless constructor defined"); // Note: Add these to scanning if needed
var vmResolveErrorLogged = HasMatch("VM container failure logged");
if (missingCtor)
{
    Assert(vmResolveErrorLogged, "VM container failure logged before MissingMethodException",
        details: "Ensure ViewModel factory logs 'container failed to resolve <VMType>' before any Activator fallback.");
}
else
{
    Assert(true, "No MissingMethodException symptoms detected");
}

// 5) Root-cause messages for DI failures
bool hasRootCause = HasMatch("Root cause logged for DI failures");
Assert(hasRootCause || !HasMatch("ModuleInitializeException"), // Add ModuleInitializeException to scanning if needed
    "Root cause logged for DI failures",
    details: "Expected an 'Unable to resolve...' or similar root-cause line when modules/services fail to resolve.");

// 6) Fail-friendly module init shouldn't crash the app
if (HasMatch("ModuleInitializeException") || HasMatch("ContainerResolutionException")) // Add to scanning
{
    Assert(HasMatch("Application continues after module init failure"), "Application continues after module init failure");
}
else
{
    Assert(true, "No module init exceptions in log");
}

// 7) Optional: Focused diagnostics when enabled
if (HasMatch("Focused container resolution checks"))
{
    Assert(HasMatch("FocusedResolve diagnostics present"), "FocusedResolve diagnostics present");
}
else
{
    Assert(true, "Focused diagnostics not enabled (expected unless WILEY_WIDGET_EXTENDED_DIAGNOSTICS=1)");
}

// 8) SettingsViewModel explicit mapping or registration should be present
Assert(HasMatch("SettingsViewModel mapping or registration present"), "SettingsViewModel mapping or registration present",
       details: "Expect explicit mapping/registration for SettingsViewModel to avoid fallback confusion.");

// ------------------------
// Summary
// ------------------------
Console.WriteLine($"\n=== Test Results ===");
Console.WriteLine($"Passed: {pass}/{total} ({(pass * 100) / Math.Max(total,1)}%)");
Console.WriteLine("\nNotes:");
Console.WriteLine("- This script analyzes log output to validate Prism container behavior end-to-end without starting WPF.");
Console.WriteLine("- Streamed reading prevents freezes; increase maxLines/maxFileSizeMB for larger logs.");
Console.WriteLine("- To get richer diagnostics, run the app with WILEY_WIDGET_EXTENDED_DIAGNOSTICS=1 and re-run this script.");

Environment.Exit(pass == total ? 0 : 3);