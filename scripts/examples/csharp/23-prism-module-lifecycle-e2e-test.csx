// E2E test for Prism module lifecycle validation - validates RegisterTypes -> OnInitialized sequence
// Based on Prism-Samples-Wpf module patterns: https://github.com/PrismLibrary/Prism-Samples-Wpf
// Ensures proper ordering: RegisterTypes (all modules) -> OnInitialized (all modules)
// Runs under C# MCP Server inside Docker and parses startup logs

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

#nullable enable

// ========================
// Test Harness
// ========================
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
        Console.WriteLine($"✗ {name}");
        if (!string.IsNullOrWhiteSpace(details))
        {
            Console.WriteLine($"  DETAILS: {details}");
        }
    }
}

Console.WriteLine("=== PRISM MODULE LIFECYCLE E2E TEST ===");
Console.WriteLine("Validates RegisterTypes -> OnInitialized sequence");
Console.WriteLine("Based on Prism-Samples-Wpf module initialization patterns\n");

// ========================
// Locate Latest Log
// ========================
string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

Console.WriteLine($"Repo root: {repoRoot}");
Console.WriteLine($"Logs dir: {logsDir}\n");

if (!Directory.Exists(logsDir))
{
    Console.WriteLine($"ERROR: Logs directory not found: {logsDir}");
    Environment.Exit(2);
}

string[] patterns = new[] { "startup-*.log", "wiley-widget-*.log" };
FileInfo? latest = null;

foreach (var pattern in patterns)
{
    try
    {
        var files = Directory.EnumerateFiles(logsDir, pattern).Take(100).Select(f => new FileInfo(f)).ToArray();
        foreach (var file in files)
        {
            if (latest == null || file.LastWriteTimeUtc > latest.LastWriteTimeUtc)
            {
                latest = file;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: {ex.Message}");
    }
}

if (latest == null)
{
    Console.WriteLine("ERROR: No startup logs found.");
    Environment.Exit(2);
}

string logPath = latest.FullName;
Console.WriteLine($"Using log: {logPath}");
Console.WriteLine($"Size: {latest.Length / 1024.0:F2} KB\n");

// ========================
// Track Module Lifecycle Events
// ========================
Console.WriteLine("Analyzing module lifecycle events...\n");

// Track events with line numbers for ordering verification
var registerTypesEvents = new Dictionary<string, int>(); // module -> line number
var onInitializedEvents = new Dictionary<string, int>(); // module -> line number
var moduleFailures = new List<(string Module, string Phase, string Error, int Line)>();
var catalogEvents = new List<(string Event, int Line)>();

int lineNumber = 0;
const int maxLines = 500000;

using (var reader = new StreamReader(logPath))
{
    string? line;
    while ((line = reader.ReadLine()) != null && lineNumber < maxLines)
    {
        lineNumber++;

        if (lineNumber % 1000 == 0)
        {
            Console.Write(".");
            if (lineNumber % 10000 == 0)
            {
                Console.WriteLine($" {lineNumber} lines");
            }
        }        // Track module catalog configuration
        if (line.Contains("Configuring Prism Module Catalog", StringComparison.OrdinalIgnoreCase))
        {
            catalogEvents.Add(("Catalog Configuration Started", lineNumber));
        }

        // Track RegisterTypes phase
        // Pattern: "CoreModule types registered" or "Registering types for XModule"
        var regTypesMatch = Regex.Match(line, @"([A-Za-z]+Module)\s+types\s+registered", RegexOptions.IgnoreCase);
        if (regTypesMatch.Success)
        {
            string module = regTypesMatch.Groups[1].Value;
            if (!registerTypesEvents.ContainsKey(module))
            {
                registerTypesEvents[module] = lineNumber;
            }
        }

        // Alternative pattern for RegisterTypes
        var regTypesMatch2 = Regex.Match(line, @"Registering\s+types\s+for\s+([A-Za-z]+Module)", RegexOptions.IgnoreCase);
        if (regTypesMatch2.Success)
        {
            string module = regTypesMatch2.Groups[1].Value;
            if (!registerTypesEvents.ContainsKey(module))
            {
                registerTypesEvents[module] = lineNumber;
            }
        }

        // Track OnInitialized phase
        // Pattern: "Initializing module: CoreModule" or "CoreModule initialization completed"
        var initMatch = Regex.Match(line, @"Initializing\s+(?:module:\s*)?([A-Za-z]+Module)", RegexOptions.IgnoreCase);
        if (initMatch.Success)
        {
            string module = initMatch.Groups[1].Value;
            if (!onInitializedEvents.ContainsKey(module))
            {
                onInitializedEvents[module] = lineNumber;
            }
        }

        var initCompleteMatch = Regex.Match(line, @"([A-Za-z]+Module)\s+initialization\s+completed", RegexOptions.IgnoreCase);
        if (initCompleteMatch.Success)
        {
            string module = initCompleteMatch.Groups[1].Value;
            if (!onInitializedEvents.ContainsKey(module))
            {
                onInitializedEvents[module] = lineNumber;
            }
        }

        // Track module failures
        if (line.Contains("Module initialization failed", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("ModuleInitializeException", StringComparison.OrdinalIgnoreCase))
        {
            var moduleMatch = Regex.Match(line, @"module\s+'?([A-Za-z]+Module)'?", RegexOptions.IgnoreCase);
            var module = moduleMatch.Success ? moduleMatch.Groups[1].Value : "Unknown";

            var phase = "OnInitialized"; // Failures typically happen in OnInitialized

            // Extract error (simplified)
            var error = line.Contains("Unable to resolve") ? "DI Resolution Failed" :
                        line.Contains("ViewRegistrationException") ? "View Registration Failed" :
                        "Unknown Error";

            moduleFailures.Add((module, phase, error, lineNumber));
        }

        // Track module catalog population complete
        if (line.Contains("Module catalog populated", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("CustomModuleManager: Registered modules", StringComparison.OrdinalIgnoreCase))
        {
            catalogEvents.Add(("Catalog Population Complete", lineNumber));
        }
    }
}

Console.WriteLine($"Processed {lineNumber} lines\n");

// ========================
// Test 1: Module Catalog Configuration
// ========================
Console.WriteLine("TEST 1: Module Catalog Configuration");
Console.WriteLine("=====================================\n");

Assert(catalogEvents.Count > 0, "Module catalog was configured",
    catalogEvents.Count == 0 ? "No catalog configuration events found!" : "");

if (catalogEvents.Count > 0)
{
    Console.WriteLine("Catalog events:");
    foreach (var evt in catalogEvents)
    {
        Console.WriteLine($"  Line {evt.Line}: {evt.Event}");
    }
    Console.WriteLine();
}

// ========================
// Test 2: RegisterTypes Phase
// ========================
Console.WriteLine("TEST 2: RegisterTypes Phase Analysis");
Console.WriteLine("=====================================\n");

Console.WriteLine($"Modules with RegisterTypes events: {registerTypesEvents.Count}");

if (registerTypesEvents.Count > 0)
{
    Console.WriteLine("\nRegisterTypes execution order:");
    foreach (var kvp in registerTypesEvents.OrderBy(e => e.Value))
    {
        Console.WriteLine($"  Line {kvp.Value}: {kvp.Key}");
    }
}
else
{
    Console.WriteLine("⚠ Warning: No RegisterTypes events found in logs.");
    Console.WriteLine("  This may indicate:");
    Console.WriteLine("  - Modules are not logging their type registrations");
    Console.WriteLine("  - Log level is too high to capture debug messages");
}

Console.WriteLine();

// ========================
// Test 3: OnInitialized Phase
// ========================
Console.WriteLine("TEST 3: OnInitialized Phase Analysis");
Console.WriteLine("=====================================\n");

Console.WriteLine($"Modules with OnInitialized events: {onInitializedEvents.Count}");

if (onInitializedEvents.Count > 0)
{
    Console.WriteLine("\nOnInitialized execution order:");
    foreach (var kvp in onInitializedEvents.OrderBy(e => e.Value))
    {
        Console.WriteLine($"  Line {kvp.Value}: {kvp.Key}");
    }
}

Console.WriteLine();

// ========================
// Test 4: Lifecycle Ordering Validation
// ========================
Console.WriteLine("TEST 4: Lifecycle Ordering Validation");
Console.WriteLine("======================================\n");

// Per Prism documentation, ALL modules should have RegisterTypes called
// BEFORE ANY module has OnInitialized called

if (registerTypesEvents.Count > 0 && onInitializedEvents.Count > 0)
{
    int lastRegisterTypes = registerTypesEvents.Values.Max();
    int firstOnInitialized = onInitializedEvents.Values.Min();

    bool correctOrder = lastRegisterTypes < firstOnInitialized;

    string details = correctOrder
        ? $"Last RegisterTypes at line {lastRegisterTypes}, first OnInitialized at line {firstOnInitialized}"
        : $"VIOLATION: Last RegisterTypes at line {lastRegisterTypes}, but OnInitialized started at {firstOnInitialized}!";

    Assert(correctOrder, "All RegisterTypes complete before any OnInitialized", details);

    if (!correctOrder)
    {
        Console.WriteLine("\n⚠ ORDERING VIOLATION DETECTED:");
        Console.WriteLine("  Per Prism lifecycle, ALL modules' RegisterTypes should execute");
        Console.WriteLine("  BEFORE ANY module's OnInitialized is called.");
        Console.WriteLine("\n  Modules that violated ordering:");

        foreach (var init in onInitializedEvents.OrderBy(e => e.Value))
        {
            var violatingRegs = registerTypesEvents.Where(r => r.Value > init.Value).ToArray();
            if (violatingRegs.Length > 0)
            {
                Console.WriteLine($"    {init.Key} OnInitialized at line {init.Value}");
                Console.WriteLine($"      But these RegisterTypes ran AFTER:");
                foreach (var reg in violatingRegs.Take(5))
                {
                    Console.WriteLine($"        - {reg.Key} at line {reg.Value}");
                }
            }
        }
    }
}
else
{
    Console.WriteLine("⚠ Cannot validate lifecycle ordering - insufficient events captured.");
}

Console.WriteLine();

// ========================
// Test 5: Module-Specific Lifecycle Validation
// ========================
Console.WriteLine("TEST 5: Per-Module Lifecycle Validation");
Console.WriteLine("========================================\n");

// Get all unique modules
var allModules = registerTypesEvents.Keys
    .Union(onInitializedEvents.Keys)
    .Distinct()
    .OrderBy(m => m)
    .ToList();

Console.WriteLine($"Total modules discovered: {allModules.Count}\n");

foreach (var module in allModules)
{
    bool hasRegisterTypes = registerTypesEvents.ContainsKey(module);
    bool hasOnInitialized = onInitializedEvents.ContainsKey(module);
    bool hasFailed = moduleFailures.Any(f => f.Module.Equals(module, StringComparison.OrdinalIgnoreCase));

    Console.WriteLine($"Module: {module}");
    Console.WriteLine($"  RegisterTypes: {(hasRegisterTypes ? $"✓ (line {registerTypesEvents[module]})" : "⚠ Not detected")}");
    Console.WriteLine($"  OnInitialized: {(hasOnInitialized ? $"✓ (line {onInitializedEvents[module]})" : "⚠ Not detected")}");

    if (hasFailed)
    {
        var failures = moduleFailures.Where(f => f.Module.Equals(module, StringComparison.OrdinalIgnoreCase)).ToList();
        Console.WriteLine($"  Status: ✗ FAILED");
        foreach (var failure in failures)
        {
            Console.WriteLine($"    Phase: {failure.Phase}");
            Console.WriteLine($"    Error: {failure.Error}");
            Console.WriteLine($"    Line: {failure.Line}");
        }
    }
    else if (hasOnInitialized)
    {
        Console.WriteLine($"  Status: ✓ Initialized successfully");
    }
    else
    {
        Console.WriteLine($"  Status: ⚠ Incomplete (no initialization detected)");
    }

    // Validate lifecycle order for this specific module
    if (hasRegisterTypes && hasOnInitialized)
    {
        bool correctOrder = registerTypesEvents[module] < onInitializedEvents[module];
        if (correctOrder)
        {
            Console.WriteLine($"  Lifecycle: ✓ Correct order (RegisterTypes before OnInitialized)");
        }
        else
        {
            Console.WriteLine($"  Lifecycle: ✗ INCORRECT (OnInitialized before RegisterTypes!)");
        }
    }

    Console.WriteLine();
}

// ========================
// Test 6: Module Failure Analysis
// ========================
Console.WriteLine("TEST 6: Module Failure Analysis");
Console.WriteLine("================================\n");

Assert(moduleFailures.Count == 0, "No module initialization failures",
    moduleFailures.Count > 0 ? $"Found {moduleFailures.Count} module failures" : "");

if (moduleFailures.Count > 0)
{
    Console.WriteLine("\nDetailed failure breakdown:");

    var failuresByModule = moduleFailures.GroupBy(f => f.Module);
    foreach (var group in failuresByModule)
    {
        Console.WriteLine($"\n{group.Key}:");
        foreach (var failure in group)
        {
            Console.WriteLine($"  ✗ {failure.Phase}: {failure.Error} (line {failure.Line})");
        }
    }

    // Provide actionable recommendations
    Console.WriteLine("\nCommon failure patterns and fixes:");

    if (moduleFailures.Any(f => f.Error.Contains("DI Resolution")))
    {
        Console.WriteLine("\n  DI Resolution Failures:");
        Console.WriteLine("    - Ensure services are registered in RegisterTypes BEFORE OnInitialized");
        Console.WriteLine("    - Check that infrastructure services are registered in App.RegisterTypes()");
        Console.WriteLine("    - Verify IModuleHealthService is registered as singleton early");
    }

    if (moduleFailures.Any(f => f.Error.Contains("View Registration")))
    {
        Console.WriteLine("\n  View Registration Failures:");
        Console.WriteLine("    - Ensure ViewModels have parameterless constructors OR all dependencies are registered");
        Console.WriteLine("    - Check that IRegionManager is properly initialized");
        Console.WriteLine("    - Verify custom region adapters are registered before module initialization");
    }
}

// ========================
// Summary & Recommendations
// ========================
Console.WriteLine("\n" + new string('=', 70));
Console.WriteLine("TEST SUMMARY");
Console.WriteLine(new string('=', 70));
Console.WriteLine($"PASSED: {pass}/{total}");
Console.WriteLine($"FAILED: {total - pass}/{total}");

if (pass == total)
{
    Console.WriteLine("\n✓ ALL TESTS PASSED - Module lifecycle is correct!");
    Environment.Exit(0);
}
else
{
    Console.WriteLine("\n✗ TESTS FAILED - Module lifecycle issues detected!");
    Console.WriteLine("\nPrism Module Lifecycle Best Practices:");
    Console.WriteLine("=======================================\n");

    Console.WriteLine("Per Prism-Samples-Wpf patterns:");
    Console.WriteLine();
    Console.WriteLine("1. MODULE INITIALIZATION SEQUENCE:");
    Console.WriteLine("   a) Prism calls RegisterTypes() on ALL modules");
    Console.WriteLine("   b) Then Prism calls OnInitialized() on ALL modules");
    Console.WriteLine();

    Console.WriteLine("2. REGISTERTYPES RESPONSIBILITIES:");
    Console.WriteLine("   - Register module-specific services");
    Console.WriteLine("   - Register views for navigation (if using navigation)");
    Console.WriteLine("   - Do NOT resolve services or interact with container");
    Console.WriteLine();

    Console.WriteLine("3. ONINITIALIZED RESPONSIBILITIES:");
    Console.WriteLine("   - Resolve services from container");
    Console.WriteLine("   - Register views with regions");
    Console.WriteLine("   - Perform module startup logic");
    Console.WriteLine("   - Subscribe to application-wide events");
    Console.WriteLine();

    Console.WriteLine("4. COMMON MISTAKES TO AVOID:");
    Console.WriteLine("   - Resolving services in RegisterTypes (container not ready)");
    Console.WriteLine("   - Depending on other modules' registrations in RegisterTypes");
    Console.WriteLine("   - Forgetting to register infrastructure services in App.RegisterTypes()");
    Console.WriteLine("   - Not logging RegisterTypes completion for diagnostics");

    Environment.Exit(1);
}
