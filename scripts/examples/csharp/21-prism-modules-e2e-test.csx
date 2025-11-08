// No external NuGet references required — script is self-contained to analyze Prism modules E2E via logs.
// Runs under C# MCP Server inside Docker. It parses the latest startup logs and validates module catalog/initialization.
// Updated: Streamed log reading to prevent freezes on large files.

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

Console.WriteLine("=== PRISM MODULES E2E LOG TEST ===\n");

Console.WriteLine("Starting test execution...");

string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");
Console.WriteLine($"Repo root: {repoRoot}");
Console.WriteLine($"Logs dir: {logsDir}, exists: {Directory.Exists(logsDir)}");
if (!Directory.Exists(logsDir)) { Console.WriteLine($"Logs directory not found: {logsDir}"); Environment.Exit(2); }

string[] patterns = new[] { "startup-*.log", "wiley-widget-*.log" };
FileInfo? latest = null;
foreach (var p in patterns)
{
    try
    {
        var files = Directory.EnumerateFiles(logsDir, p).Take(100).ToArray(); // Limit enumeration
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
if (latest == null) { Console.WriteLine("No logs found"); Environment.Exit(2); }
string logPath = latest!.FullName;
Console.WriteLine($"Using log: {logPath} (size: {latest.Length / 1024 / 1024} MB)");

const long maxFileSizeMB = 50;
if (latest.Length > maxFileSizeMB * 1024 * 1024)
{
    Console.WriteLine($"Log too large (> {maxFileSizeMB} MB). Truncate or increase limit.");
    Environment.Exit(2);
}

Console.WriteLine("Streaming log for analysis...\n");

var matches = new Dictionary<string, List<string>>(); // Track matches with context (e.g., module names)
int lineCount = 0;
const int maxLines = 1000000;

// Diagnostics capture
var tsRegex = new Regex(@"^\d{4}-\d{2}-\d{2} ");
var failingModules = new List<(string Module, string Reason, string? Region, string? View, string? ViewModel, string? File, int? Line)>();
var diUnknowns = new List<(string Service, string? File, int? Line, string? Module)>();
var statusSectionSeen = false; // "=== Validating Module Initialization and Region Availability ==="
var modulesHealthySeen = false; // "Modules Healthy: x/y"
var perModuleStatuses = new List<string>(); // from "? Module 'X' status: ..."
var initModuleNames = new List<string>(); // from "Initializing module: XModule"/"Initializing XModule"

List<string> CaptureFollowing(StreamReader r, int max = 60)
{
    var list = new List<string>();
    for (int i = 0; i < max; i++)
    {
        if (r.Peek() == -1) break;
        var next = r.ReadLine();
        if (next == null) break;
        lineCount++;
        list.Add(next);
        if (tsRegex.IsMatch(next)) break;
    }
    return list;
}

void RecordMatch(string key, string? context = null)
{
    if (!matches.ContainsKey(key)) matches[key] = new List<string>();
    if (context != null) matches[key].Add(context);
    if (lineCount % 10000 == 0 || matches[key].Count == 1) Console.WriteLine($"  Progress: Found '{key}' at line {lineCount}");
}

using (var reader = new StreamReader(logPath))
{
    string? line;
    while ((line = reader.ReadLine()) != null && lineCount < maxLines)
    {
        lineCount++;
        if (lineCount % 10000 == 0) Console.WriteLine($"  Processed {lineCount} lines...");

        if (line.Contains("Configuring Prism Module Catalog (explicit registration)", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("CustomModuleManager: Registered modules", StringComparison.OrdinalIgnoreCase)) RecordMatch("Module catalog configuration present");
        if (line.Contains("Registered SfDataGridRegionAdapter", StringComparison.OrdinalIgnoreCase) || line.Contains("SfDataGridRegionAdapter", StringComparison.OrdinalIgnoreCase)) RecordMatch("SfDataGrid region adapter registered");
        if (line.Contains("DockingManagerRegionAdapter", StringComparison.OrdinalIgnoreCase)) RecordMatch("DockingManager region adapter registered");
        if (line.Contains("Registered default Prism region behaviors", StringComparison.OrdinalIgnoreCase) || line.Contains("default Prism region behaviors", StringComparison.OrdinalIgnoreCase)) RecordMatch("Default region behaviors registered");
        if (line.Contains("MainWindow initialization complete", StringComparison.OrdinalIgnoreCase) || line.Contains("Modules initializing", StringComparison.OrdinalIgnoreCase)) RecordMatch("Application initialized and began module init");
        if (Regex.IsMatch(line, @"Module health status:.*", RegexOptions.IgnoreCase)) RecordMatch("Module health status reported", line);
        if (line.IndexOf("=== Validating Module Initialization and Region Availability ===", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            statusSectionSeen = true;
            // Treat as health reported if consolidated line missing
            RecordMatch("Module health status reported", line);
        }
        if (line.IndexOf("Modules Healthy:", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            modulesHealthySeen = true;
            RecordMatch("Module health status reported", line);
        }
        if (line.Contains("Initializing ", StringComparison.OrdinalIgnoreCase))
        {
            var modMatch = Regex.Match(line, @"Initializing (.*Module)", RegexOptions.IgnoreCase);
            if (modMatch.Success)
            {
                var name = modMatch.Groups[1].Value;
                initModuleNames.Add(name);
                RecordMatch("Module lifecycle observed", name);
            }
        }
        // Capture per-module status lines
        if (Regex.IsMatch(line, @"\?\s*Module '([^']+Module)' status:\s*", RegexOptions.IgnoreCase))
        {
            var m = Regex.Match(line, @"\?\s*Module '([^']+Module)' status:\s*([^\r\n]*)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                perModuleStatuses.Add(m.Groups[1].Value);
                RecordMatch("Module health status reported", line);
            }
        }
        if (line.Contains("Module initialization failed", StringComparison.OrdinalIgnoreCase) || line.Contains("ModuleInitializeException", StringComparison.OrdinalIgnoreCase)) RecordMatch("Module failures reported");
        if (line.Contains("Application will continue", StringComparison.OrdinalIgnoreCase) || line.Contains("Modules initialized.", StringComparison.OrdinalIgnoreCase)) RecordMatch("App continues after module failures");

        // Enriched diagnostics: capture failure details per module
        if (line.IndexOf("ModuleInitializeException", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("Module initialization failed", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var module = Regex.Match(line, @"module '([^']+)'", RegexOptions.IgnoreCase).Groups[1].Value;
            var block = CaptureFollowing(reader, 80);
            string? region = null, view = null, viewModel = null, file = null, reason = null; int? ln = null;

            // Reasons to detect
            // 1) Unable to resolve resolution root <Service>
            var svcLine = block.FirstOrDefault(l => l.IndexOf("Unable to resolve resolution root", StringComparison.OrdinalIgnoreCase) >= 0);
            if (svcLine != null)
            {
                var sm = Regex.Match(svcLine, @"Unable to resolve resolution root ([^\s]+)");
                if (sm.Success)
                {
                    reason = $"UnknownService: {sm.Groups[1].Value}";
                    diUnknowns.Add((sm.Groups[1].Value, null, null, string.IsNullOrEmpty(module) ? null : module));
                }
            }
            // 2) View registration failure => region + view + MissingMethod on ViewModel
            var regLine = block.FirstOrDefault(l => l.IndexOf("ViewRegistrationException", StringComparison.OrdinalIgnoreCase) >= 0 || l.IndexOf("add a view to region", StringComparison.OrdinalIgnoreCase) >= 0);
            if (regLine != null)
            {
                var rm = Regex.Match(regLine, @"region '([^']+)'", RegexOptions.IgnoreCase);
                if (rm.Success) region = rm.Groups[1].Value;
            }
            foreach (var l in block)
            {
                if (view == null)
                {
                    var vm = Regex.Match(l, @"resolving '([^']*Views\.[^']+)'", RegexOptions.IgnoreCase);
                    if (vm.Success) view = vm.Groups[1].Value;
                }
                if (viewModel == null)
                {
                    var vmm = Regex.Match(l, @"type '([^']*ViewModel)'", RegexOptions.IgnoreCase);
                    if (vmm.Success) viewModel = vmm.Groups[1].Value;
                }
                if (file == null)
                {
                    var fm = Regex.Match(l, @"in (.*?):line (\d+)");
                    if (fm.Success)
                    {
                        file = fm.Groups[1].Value;
                        if (int.TryParse(fm.Groups[2].Value, out var n)) ln = n;
                    }
                }
            }
            if (reason == null && !string.IsNullOrEmpty(viewModel))
                reason = "MissingParameterlessCtorOrDIResolveFailed(ViewModel)";
            if (string.IsNullOrEmpty(reason)) reason = "Unknown";
            failingModules.Add((string.IsNullOrEmpty(module) ? "<unknown>" : module, reason!, region, view, viewModel, file, ln));
        }

        // Early exit logic here if all expected matches found
    }
}

Console.WriteLine($"Log analysis complete: {lineCount} lines processed.\n");

// ------------------------
// Tests (using recorded matches)
// ------------------------

// 1) Module catalog configured explicitly
Assert(matches.ContainsKey("Module catalog configuration present"), "Module catalog configuration present");

// 2) Region adapters registered (common offenders for UI integration)
Assert(matches.ContainsKey("SfDataGrid region adapter registered"), "SfDataGrid region adapter registered");
Assert(matches.ContainsKey("DockingManager region adapter registered"), "DockingManager region adapter registered");
Assert(matches.ContainsKey("Default region behaviors registered"), "Default region behaviors registered");

// 3) Shell shows, modules begin initialization
Assert(matches.ContainsKey("Application initialized and began module init"), "Application initialized and began module init");

// 4) Modules listed in health/status
var healthLines = matches.ContainsKey("Module health status reported") ? matches["Module health status reported"] : new List<string>();
bool healthReported = healthLines.Count > 0 || statusSectionSeen || modulesHealthySeen || perModuleStatuses.Count > 0;
Assert(healthReported, "Module health status reported");

// 5) Build module name set from multiple sources: consolidated line, per-module status, and init lines
var moduleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var hl in healthLines)
{
    foreach (Match m in Regex.Matches(hl, "([A-Za-z]+Module)", RegexOptions.IgnoreCase))
        moduleNames.Add(m.Groups[1].Value);
}
foreach (var n in perModuleStatuses) moduleNames.Add(n);
foreach (var n in initModuleNames) moduleNames.Add(n);
Assert(moduleNames.Count > 0, "Parsed module names from health status");

foreach (var mod in moduleNames)
{
    bool sawLifecycle = matches.ContainsKey("Module lifecycle observed") && matches["Module lifecycle observed"].Any(c => c.Contains(mod, StringComparison.OrdinalIgnoreCase));
    bool sawFail = matches.ContainsKey("Module failures reported");
    Assert(sawLifecycle || sawFail, $"Module lifecycle observed: {mod}");
}

// 6) Fail-friendly behavior: app continues after any module failures
if (matches.ContainsKey("Module failures reported"))
{
    Assert(matches.ContainsKey("App continues after module failures"), "App continues after module failures");
}
else
{
    Assert(true, "No module failures reported");
}

Console.WriteLine($"\n=== Test Results ===");
Console.WriteLine($"Passed: {pass}/{total} ({(pass * 100) / Math.Max(total,1)}%)");
Console.WriteLine("\nNotes:");
Console.WriteLine("- This script validates module catalog wiring, region adapter registration, and fail-friendly initialization using logs.");
Console.WriteLine("- Streamed reading prevents freezes; adjust limits for your env.");
Console.WriteLine("- For deeper checks, enable extended diagnostics and module-level verbose logs.");

// Diagnostics summary to help pinpoint root causes
if (failingModules.Count > 0)
{
    Console.WriteLine("\n=== Diagnostics Summary ===");
    foreach (var g in failingModules.GroupBy(f => f.Module))
    {
        var any = g.First();
        Console.WriteLine($"Module: {g.Key} | Reason: {any.Reason} | Region: {any.Region} | View: {any.View} | ViewModel: {any.ViewModel}");
        Console.WriteLine($"  First seen at {any.File}:{any.Line}");
        if (any.Reason.StartsWith("UnknownService:") && any.Reason.IndexOf("IModuleHealthService", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Console.WriteLine("  Hint: Don’t pass containerProvider into Resolve for IModuleHealthService; resolve with no args and let DI build it.");
        }
        if (any.Reason.StartsWith("MissingParameterlessCtorOrDIResolveFailed"))
        {
            Console.WriteLine("  Hint: Ensure ViewModelLocator uses container.Resolve(ViewModel); register ViewModel and its dependencies. No parameterless ctor needed when DI works.");
        }
    }
}

Environment.Exit(pass == total ? 0 : 3);
