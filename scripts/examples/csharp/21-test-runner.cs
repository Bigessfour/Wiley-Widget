using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

#nullable enable

#pragma warning disable CA1303 // Do not pass literals as localized parameters

namespace Scripts.ModuleTests
{
public static class Test21ModuleLogVerifier
{
    private static int pass = 0, total = 0;

    private static void Assert(bool condition, string name, string details = "")
    {
        total++;
        if (condition) { Console.WriteLine($"✓ {name}"); pass++; }
        else { Console.WriteLine($"✗ {name} FAILED"); if (!string.IsNullOrWhiteSpace(details)) Console.WriteLine("  Details: " + details); }
    }

    // Note: No Main() here to avoid multiple entry point conflicts in combined compilations.
    // This method can be invoked by a harness if needed.
    public static int Execute()
    {
        Console.WriteLine("=== PRISM MODULES E2E LOG TEST ===\n");
        Console.WriteLine("Starting test execution...");

        string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
        string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");
        Console.WriteLine($"Repo root: {repoRoot}");
        Console.WriteLine($"Logs dir: {logsDir}, exists: {Directory.Exists(logsDir)}");
        if (!Directory.Exists(logsDir)) { Console.WriteLine($"Logs directory not found: {logsDir}"); return 2; }

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
        if (latest == null) { Console.WriteLine("No logs found"); return 2; }
        string logPath = latest!.FullName;
        Console.WriteLine($"Using log: {logPath} (size: {latest.Length / 1024 / 1024} MB)");

        const long maxFileSizeMB = 50;
        if (latest.Length > maxFileSizeMB * 1024 * 1024)
        {
            Console.WriteLine($"Log too large (> {maxFileSizeMB} MB). Truncate or increase limit.");
            return 2;
        }

        Console.WriteLine("Streaming log for analysis...\n");

        var matches = new Dictionary<string, List<string>>(); // Track matches with context (e.g., module names)
        int lineCount = 0;
        const int maxLines = 1000000;

        // Enriched diagnostics
        var tsRegex = new Regex("^\\d{4}-\\d{2}-\\d{2} ");
        var failingModules = new List<(string Module, string Reason, string? Region, string? View, string? ViewModel, string? File, int? Line, List<string> Snippet)>();
        bool statusSectionSeen = false;
        bool modulesHealthySeen = false;
        var perModuleStatuses = new List<string>();
        var initModuleNames = new List<string>();

        List<string> CaptureFollowing(StreamReader r, int max = 80)
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
                if (line.Contains("Module initialization failed", StringComparison.OrdinalIgnoreCase) || line.Contains("ModuleInitializeException", StringComparison.OrdinalIgnoreCase)) RecordMatch("Module failures reported");
                if (line.Contains("Application will continue", StringComparison.OrdinalIgnoreCase) || line.Contains("Modules initialized.", StringComparison.OrdinalIgnoreCase)) RecordMatch("App continues after module failures");

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

                // Enriched diagnostics: capture failure details per module
                if (line.IndexOf("ModuleInitializeException", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Module initialization failed", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var module = Regex.Match(line, @"module '([^']+)'", RegexOptions.IgnoreCase).Groups[1].Value;
                    var block = CaptureFollowing(reader, 100);
                    string? region = null, view = null, viewModel = null, file = null, reason = null; int? ln = null;

                    // Unknown service
                    var svcLine = block.FirstOrDefault(l => l.IndexOf("Unable to resolve resolution root", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (svcLine != null)
                    {
                        var sm = Regex.Match(svcLine, @"Unable to resolve resolution root ([^\s]+)");
                        if (sm.Success)
                        {
                            reason = $"UnknownService: {sm.Groups[1].Value}";
                        }
                    }
                    // Region and view/viewmodel details
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

                    // Keep a short snippet (first 5 lines) for context
                    var snippet = block.Take(5).ToList();
                    failingModules.Add((string.IsNullOrEmpty(module) ? "<unknown>" : module, reason ?? "Unknown", region, view, viewModel, file, ln, snippet));
                }
            }
        }

        Console.WriteLine($"Log analysis complete: {lineCount} lines processed.\n");

        // Tests
        Assert(matches.ContainsKey("Module catalog configuration present"), "Module catalog configuration present");
        Assert(matches.ContainsKey("SfDataGrid region adapter registered"), "SfDataGrid region adapter registered");
        Assert(matches.ContainsKey("DockingManager region adapter registered"), "DockingManager region adapter registered");
        Assert(matches.ContainsKey("Default region behaviors registered"), "Default region behaviors registered");
        Assert(matches.ContainsKey("Application initialized and began module init"), "Application initialized and began module init");

        var healthLines = matches.ContainsKey("Module health status reported") ? matches["Module health status reported"] : new List<string>();
        bool healthReported = healthLines.Count > 0 || statusSectionSeen || modulesHealthySeen || perModuleStatuses.Count > 0;
        Assert(healthReported, "Module health status reported");

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

        // Diagnostics summary
        if (failingModules.Count > 0)
        {
            Console.WriteLine("\n=== Diagnostics Summary ===");
            foreach (var fm in failingModules)
            {
                Console.WriteLine($"Module: {fm.Module} | Reason: {fm.Reason} | Region: {fm.Region} | View: {fm.View} | ViewModel: {fm.ViewModel}");
                Console.WriteLine($"  First seen at {fm.File}:{fm.Line}");
                if (fm.Snippet.Count > 0)
                {
                    Console.WriteLine("  Context:");
                    foreach (var s in fm.Snippet) Console.WriteLine("    " + s);
                }
                if (fm.Reason.StartsWith("UnknownService:", StringComparison.Ordinal) && fm.Reason.IndexOf("IModuleHealthService", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine("  Hint: Don’t pass containerProvider into Resolve for IModuleHealthService; resolve with no args and let DI build it.");
                }
                if (fm.Reason.StartsWith("MissingParameterlessCtorOrDIResolveFailed", StringComparison.Ordinal))
                {
                    Console.WriteLine("  Hint: Ensure ViewModelLocator uses container.Resolve(ViewModel); register ViewModel and its dependencies. No parameterless ctor needed when DI works.");
                }
            }
        }

    return pass == total ? 0 : 3;
    }
}
}
