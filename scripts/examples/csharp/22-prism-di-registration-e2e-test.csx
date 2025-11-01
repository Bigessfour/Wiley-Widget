// E2E test for Prism DI registration validation - validates all service registrations before module initialization
// Based on Prism-Samples-Wpf patterns: https://github.com/PrismLibrary/Prism-Samples-Wpf
// Specifically validates IModuleHealthService and other critical services are registered BEFORE modules call OnInitialized
// Runs under C# MCP Server inside Docker and parses startup logs for DI-related errors

using System;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;

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
            Console.WriteLine($"  FAILURE DETAILS: {details}");
        }
    }
}

Console.WriteLine("=== PRISM DI REGISTRATION E2E TEST ===");
Console.WriteLine("Validates service registration order and availability");
Console.WriteLine("Based on Prism-Samples-Wpf best practices\n");

// ========================
// Locate Latest Startup Log
// ========================
string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

Console.WriteLine($"Repo root: {repoRoot}");
Console.WriteLine($"Logs dir: {logsDir}");

if (!Directory.Exists(logsDir))
{
    Console.WriteLine($"ERROR: Logs directory not found: {logsDir}");
    Console.WriteLine("Set WW_LOGS_DIR environment variable or run from repository root.");
    Environment.Exit(2);
}

string[] patterns = new[] { "startup-*.log", "wiley-widget-*.log" };
FileInfo? latest = null;

foreach (var pattern in patterns)
{
    try
    {
        var files = Directory.EnumerateFiles(logsDir, pattern)
            .Take(100)
            .Select(f => new FileInfo(f))
            .ToArray();

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
        Console.WriteLine($"Warning: Error enumerating {pattern}: {ex.Message}");
    }
}

if (latest == null)
{
    Console.WriteLine("ERROR: No startup logs found.");
    Console.WriteLine("Please run the application first to generate logs.");
    Environment.Exit(2);
}

string logPath = latest.FullName;
Console.WriteLine($"Using log: {logPath}");
Console.WriteLine($"Size: {latest.Length / 1024.0:F2} KB ({latest.Length / (1024.0 * 1024.0):F2} MB)");
Console.WriteLine($"Last modified: {latest.LastWriteTimeUtc:u}\n");

// ========================
// Parse Log for DI Issues
// ========================
Console.WriteLine("Analyzing DI registration sequence...\n");

// Critical services that MUST be registered before module initialization
// Note: Prism framework services (ILogger, IRegionManager, etc.) are auto-registered
// and don't appear in logs, so we focus on application-specific services
var criticalServices = new HashSet<string>
{
    "IModuleHealthService"  // Critical app service that modules depend on
};

// Optional/framework services that should be available but may not be logged
var frameworkServices = new HashSet<string>
{
    "ILoggerFactory",
    "ILogger",
    "IConfiguration",
    "IRegionManager",
    "IContainerProvider",
    "IContainerRegistry",
    "IEventAggregator",
    "IDialogService",
    "IRegionNavigationJournal"
};

// Track registration events
var registeredServices = new Dictionary<string, (int LineNumber, string LogEntry)>();
var moduleInitAttempts = new Dictionary<string, (int LineNumber, string Phase)>();
var diFailures = new List<(string Module, string Service, string Error, int LineNumber, string[] Context)>();
var registrationOrder = new List<(int LineNumber, string Service, string LogEntry)>();

int lineNumber = 0;
const int maxLines = 500000;
const long maxSizeMB = 100;

if (latest.Length > maxSizeMB * 1024 * 1024)
{
    Console.WriteLine($"Warning: Log file is very large ({latest.Length / (1024 * 1024)} MB)");
    Console.WriteLine($"Processing first {maxLines} lines only.");
}

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
        }

        // Track service registrations
        // Pattern: "✓ Registered IModuleHealthService as singleton"
        var regMatch = Regex.Match(line, @"✓\s*Registered\s+([A-Za-z0-9<>_]+)", RegexOptions.IgnoreCase);
        if (regMatch.Success)
        {
            string service = regMatch.Groups[1].Value;
            if (!registeredServices.ContainsKey(service))
            {
                registeredServices[service] = (lineNumber, line);
                registrationOrder.Add((lineNumber, service, line));
            }
        }

        // Track module initialization attempts
        // Pattern: "Initializing module: CoreModule" or "CoreModule.OnInitialized"
        var initMatch = Regex.Match(line, @"Initializing\s+(?:module:\s*)?([A-Za-z]+Module)", RegexOptions.IgnoreCase);
        if (initMatch.Success)
        {
            string module = initMatch.Groups[1].Value;
            if (!moduleInitAttempts.ContainsKey(module))
            {
                moduleInitAttempts[module] = (lineNumber, "Initializing");
            }
        }

        var onInitMatch = Regex.Match(line, @"([A-Za-z]+Module)\.OnInitialized", RegexOptions.IgnoreCase);
        if (onInitMatch.Success)
        {
            string module = onInitMatch.Groups[1].Value;
            if (!moduleInitAttempts.ContainsKey(module))
            {
                moduleInitAttempts[module] = (lineNumber, "OnInitialized");
            }
        }

        // Detect DI resolution failures
        // Pattern: "Unable to resolve resolution root WileyWidget.Services.IModuleHealthService"
        if (line.Contains("Unable to resolve", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("UnableToResolve", StringComparison.OrdinalIgnoreCase))
        {
            var serviceMatch = Regex.Match(line, @"Unable to resolve.*?([A-Za-z0-9.]+I[A-Za-z]+Service)");
            if (serviceMatch.Success)
            {
                string service = serviceMatch.Groups[1].Value;
                // Extract just the interface name
                if (service.Contains('.'))
                {
                    service = service.Substring(service.LastIndexOf('.') + 1);
                }

                // Try to find which module caused this
                string? causingModule = null;
                foreach (var kvp in moduleInitAttempts.OrderByDescending(m => m.Value.LineNumber))
                {
                    if (kvp.Value.LineNumber < lineNumber)
                    {
                        causingModule = kvp.Key;
                        break;
                    }
                }

                // Capture context (next few lines for error details)
                var context = new List<string> { line };
                for (int i = 0; i < 10 && !reader.EndOfStream; i++)
                {
                    var contextLine = reader.ReadLine();
                    if (contextLine != null)
                    {
                        context.Add(contextLine);
                        lineNumber++;
                    }
                }

                diFailures.Add((
                    causingModule ?? "Unknown",
                    service,
                    line,
                    lineNumber,
                    context.ToArray()
                ));
            }
        }
    }
}

Console.WriteLine($"Processed {lineNumber} lines\n");

// ========================
// Test 1: Critical Services Registered
// ========================
Console.WriteLine("TEST 1: Critical Service Registration");
Console.WriteLine("======================================\n");

foreach (var service in criticalServices)
{
    bool isRegistered = registeredServices.ContainsKey(service);
    string details = isRegistered
        ? $"Registered at line {registeredServices[service].LineNumber}"
        : "NOT REGISTERED - This will cause module initialization failures!";

    Assert(isRegistered, $"Critical service '{service}' is registered", details);
}

// ========================
// Test 2: Registration Order
// ========================
Console.WriteLine("\nTEST 2: Service Registration Order");
Console.WriteLine("===================================\n");

// IModuleHealthService MUST be registered before any module initialization
if (registeredServices.ContainsKey("IModuleHealthService"))
{
    var healthServiceLine = registeredServices["IModuleHealthService"].LineNumber;
    var firstModuleInit = moduleInitAttempts.Values.Min(m => m.LineNumber);

    bool correctOrder = healthServiceLine < firstModuleInit;
    string details = correctOrder
        ? $"IModuleHealthService registered at line {healthServiceLine}, first module init at {firstModuleInit}"
        : $"ERROR: IModuleHealthService registered at line {healthServiceLine}, but module init started at {firstModuleInit}!";

    Assert(correctOrder, "IModuleHealthService registered before module initialization", details);
}
else
{
    Assert(false, "IModuleHealthService registered before module initialization",
        "IModuleHealthService was never registered!");
}

// ========================
// Test 3: DI Failure Analysis
// ========================
Console.WriteLine("\nTEST 3: DI Resolution Failure Analysis");
Console.WriteLine("=======================================\n");

Assert(diFailures.Count == 0, "No DI resolution failures occurred",
    diFailures.Count > 0 ? $"Found {diFailures.Count} DI resolution failures" : "");

if (diFailures.Count > 0)
{
    Console.WriteLine("\nDETAILED FAILURE ANALYSIS:");
    Console.WriteLine("==========================\n");

    var failuresByModule = diFailures.GroupBy(f => f.Module);

    foreach (var group in failuresByModule)
    {
        Console.WriteLine($"Module: {group.Key}");
        Console.WriteLine(new string('-', 60));

        foreach (var failure in group)
        {
            Console.WriteLine($"  ✗ {failure.Service}");
            Console.WriteLine($"    Phase: {(moduleInitAttempts.ContainsKey(failure.Module) ? moduleInitAttempts[failure.Module].Phase : "Unknown")}");
            Console.WriteLine($"    Error: {failure.Error.Substring(0, Math.Min(120, failure.Error.Length))}");

            // Check if service was ever registered
            if (registeredServices.ContainsKey(failure.Service))
            {
                var regInfo = registeredServices[failure.Service];
                Console.WriteLine($"    Note: Service WAS registered at line {regInfo.LineNumber}");
                Console.WriteLine($"    Registration: {regInfo.LogEntry}");

                if (regInfo.LineNumber > failure.LineNumber)
                {
                    Console.WriteLine($"    ⚠ TIMING ISSUE: Registered AFTER module init attempt!");
                }
            }
            else
            {
                Console.WriteLine($"    ⚠ MISSING: Service '{failure.Service}' was NEVER registered!");
                Console.WriteLine($"    Fix: Add registration in App.xaml.cs RegisterTypes() method BEFORE ConfigureModuleCatalog()");
            }

            // Show context
            Console.WriteLine($"    Context (line {failure.LineNumber}):");
            foreach (var ctx in failure.Context.Take(3))
            {
                Console.WriteLine($"      {ctx}");
            }
            Console.WriteLine();
        }
    }
}

// ========================
// Test 4: Module Initialization Status
// ========================
Console.WriteLine("\nTEST 4: Module Initialization Status");
Console.WriteLine("=====================================\n");

Console.WriteLine($"Modules attempted to initialize: {moduleInitAttempts.Count}");

foreach (var kvp in moduleInitAttempts.OrderBy(m => m.Value.LineNumber))
{
    string module = kvp.Key;
    int initLine = kvp.Value.LineNumber;
    string phase = kvp.Value.Phase;

    // Check if this module had DI failures
    var failures = diFailures.Where(f => f.Module == module).ToArray();

    if (failures.Length == 0)
    {
        Assert(true, $"{module} initialized without DI errors", $"Started at line {initLine} ({phase})");
    }
    else
    {
        Assert(false, $"{module} initialized without DI errors",
            $"Had {failures.Length} DI failure(s): {string.Join(", ", failures.Select(f => f.Service))}");
    }
}

// ========================
// Test 5: Registration Completeness
// ========================
Console.WriteLine("\nTEST 5: Registration Completeness Check");
Console.WriteLine("========================================\n");

Console.WriteLine($"Total services registered: {registeredServices.Count}");
Console.WriteLine("\nFirst 10 registrations (in order):");

foreach (var reg in registrationOrder.Take(10))
{
    Console.WriteLine($"  Line {reg.LineNumber}: {reg.Service}");
}

// Check for common missing registrations
var commonServices = new[]
{
    "IModuleHealthService",
    "IEventAggregator",
    "IDialogService",
    "IRegionNavigationJournal"
};

foreach (var service in commonServices)
{
    if (registeredServices.ContainsKey(service))
    {
        Console.WriteLine($"  ✓ {service}");
    }
    else
    {
        Console.WriteLine($"  ⚠ {service} - Not found (may be optional)");
    }
}

// ========================
// Summary
// ========================
Console.WriteLine("\n" + new string('=', 70));
Console.WriteLine("TEST SUMMARY");
Console.WriteLine(new string('=', 70));
Console.WriteLine($"PASSED: {pass}/{total}");
Console.WriteLine($"FAILED: {total - pass}/{total}");

if (pass == total)
{
    Console.WriteLine("\n✓ ALL TESTS PASSED - DI registration is correct!");
    Environment.Exit(0);
}
else
{
    Console.WriteLine("\n✗ TESTS FAILED - DI registration issues detected!");
    Console.WriteLine("\nRECOMMENDED FIXES:");
    Console.WriteLine("==================\n");

    Console.WriteLine("1. Ensure IModuleHealthService is registered in App.xaml.cs RegisterTypes():");
    Console.WriteLine("   containerRegistry.RegisterSingleton<IModuleHealthService, ModuleHealthService>();");
    Console.WriteLine();

    Console.WriteLine("2. Register BEFORE ConfigureModuleCatalog() is called:");
    Console.WriteLine("   protected override void RegisterTypes(IContainerRegistry containerRegistry)");
    Console.WriteLine("   {");
    Console.WriteLine("       // Register critical services FIRST");
    Console.WriteLine("       containerRegistry.RegisterSingleton<IModuleHealthService, ModuleHealthService>();");
    Console.WriteLine("       // ... other registrations");
    Console.WriteLine("   }");
    Console.WriteLine();

    Console.WriteLine("3. Verify module OnInitialized methods only resolve services that are registered:");
    Console.WriteLine("   - Check each failing module's OnInitialized method");
    Console.WriteLine("   - Ensure all Resolve<T>() calls have corresponding registrations");
    Console.WriteLine();

    Console.WriteLine("4. Based on Prism-Samples-Wpf patterns:");
    Console.WriteLine("   - Register infrastructure services in App.RegisterTypes()");
    Console.WriteLine("   - Register module-specific services in Module.RegisterTypes()");
    Console.WriteLine("   - Only resolve services in Module.OnInitialized() after registration is complete");

    Environment.Exit(1);
}
