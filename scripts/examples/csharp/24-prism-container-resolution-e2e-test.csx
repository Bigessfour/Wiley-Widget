// E2E test for container resolution validation - ensures all registered services can be successfully resolved
// Based on Prism-Samples-Wpf DI patterns: https://github.com/PrismLibrary/Prism-Samples-Wpf
// Validates that container configuration is complete and consistent
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

Console.WriteLine("=== PRISM CONTAINER RESOLUTION E2E TEST ===");
Console.WriteLine("Validates all registered services can be resolved");
Console.WriteLine("Based on Prism-Samples-Wpf DI best practices\n");

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
// Parse Resolution Data
// ========================
Console.WriteLine("Analyzing container resolutions...\n");

// Track registrations and resolutions
var registeredServices = new HashSet<string>();
var successfulResolutions = new HashSet<string>();
var failedResolutions = new List<(string Service, string Error, string? Context, int Line)>();
var circularDependencies = new List<(string Service, string[] Chain, int Line)>();
var missingRegistrations = new HashSet<string>();

// Track resolution attempts by category
var infrastructureServices = new HashSet<string> { "ILoggerFactory", "ILogger", "IConfiguration", "IEventAggregator" };
var containerServices = new HashSet<string> { "IContainerRegistry", "IContainerProvider" };
var prismServices = new HashSet<string> { "IRegionManager", "IModuleCatalog", "IModuleManager", "IDialogService" };
var moduleServices = new HashSet<string> { "IModuleHealthService" };
var viewModelServices = new HashSet<string>(); // Will populate from log

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
        }        // Track registrations
        // Pattern: "✓ Registered IService" or "Registered Service as singleton/transient"
        var regMatch = Regex.Match(line, @"✓?\s*Registered\s+([A-Za-z0-9<>_]+)(?:\s+as\s+)?", RegexOptions.IgnoreCase);
        if (regMatch.Success)
        {
            string service = regMatch.Groups[1].Value;
            registeredServices.Add(service);
        }

        // Track successful resolutions
        // Pattern: "Resolved IService successfully" or "IService resolved"
        var resolveMatch = Regex.Match(line, @"Resolved\s+([A-Za-z0-9<>_]+)\s+successfully", RegexOptions.IgnoreCase);
        if (resolveMatch.Success)
        {
            string service = resolveMatch.Groups[1].Value;
            successfulResolutions.Add(service);
        }

        // Track failed resolutions
        // Pattern: "Unable to resolve resolution root IService"
        if (line.Contains("Unable to resolve", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("UnableToResolve", StringComparison.OrdinalIgnoreCase))
        {
            var serviceMatch = Regex.Match(line, @"Unable to resolve.*?([A-Za-z0-9.]+I[A-Za-z]+(?:Service|Factory|Provider|Manager|Adapter))");
            if (serviceMatch.Success)
            {
                string service = serviceMatch.Groups[1].Value;

                // Clean up namespace
                if (service.Contains('.'))
                {
                    service = service.Substring(service.LastIndexOf('.') + 1);
                }

                // Capture context
                string? context = null;
                var contextMatch = Regex.Match(line, @"from container (.*)");
                if (contextMatch.Success)
                {
                    context = contextMatch.Groups[1].Value;
                }

                // Determine error type
                string error = line.Contains("UnknownService") ? "Service not registered" :
                               line.Contains("recursive") || line.Contains("circular") ? "Circular dependency" :
                               line.Contains("ambiguous") ? "Ambiguous registration" :
                               "Resolution failed";

                // Check for circular dependency
                if (error == "Circular dependency")
                {
                    // Try to extract dependency chain
                    var chainMatches = Regex.Matches(line, @"[A-Z][a-zA-Z0-9]+(?:Service|Factory|Provider|Manager)");
                    var chain = chainMatches.Cast<Match>().Select(m => m.Value).Distinct().ToArray();
                    if (chain.Length > 0)
                    {
                        circularDependencies.Add((service, chain, lineNumber));
                    }
                }
                else if (error == "Service not registered")
                {
                    missingRegistrations.Add(service);
                }

                failedResolutions.Add((service, error, context, lineNumber));
            }
        }

        // Track ViewModels being resolved (to check ViewModel resolution)
        if (line.Contains("ViewModel", StringComparison.OrdinalIgnoreCase))
        {
            var vmMatch = Regex.Match(line, @"([A-Z][a-zA-Z0-9]+ViewModel)");
            if (vmMatch.Success)
            {
                var vm = vmMatch.Groups[1].Value;
                viewModelServices.Add(vm);

                // Check if it was successfully resolved
                if (line.Contains("resolved", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("created", StringComparison.OrdinalIgnoreCase))
                {
                    successfulResolutions.Add(vm);
                }
            }
        }
    }
}

Console.WriteLine($"Processed {lineNumber} lines\n");

// ========================
// Test 1: Service Registration Coverage
// ========================
Console.WriteLine("TEST 1: Service Registration Coverage");
Console.WriteLine("======================================\n");

Console.WriteLine($"Total services registered: {registeredServices.Count}");

// Check critical service categories
void CheckCategory(string name, HashSet<string> services)
{
    var registered = services.Where(s => registeredServices.Contains(s)).ToList();
    var missing = services.Where(s => !registeredServices.Contains(s)).ToList();

    Console.WriteLine($"\n{name}:");
    Console.WriteLine($"  Registered: {registered.Count}/{services.Count}");

    foreach (var svc in registered)
    {
        Console.WriteLine($"    ✓ {svc}");
    }

    foreach (var svc in missing)
    {
        Console.WriteLine($"    ✗ {svc} - NOT REGISTERED");
    }

    Assert(missing.Count == 0, $"All {name} are registered",
        missing.Count > 0 ? $"Missing: {string.Join(", ", missing)}" : "");
}

CheckCategory("Infrastructure Services", infrastructureServices);
CheckCategory("Container Services", containerServices);
CheckCategory("Prism Services", prismServices);
CheckCategory("Module Services", moduleServices);

Console.WriteLine();

// ========================
// Test 2: Resolution Success Rate
// ========================
Console.WriteLine("\nTEST 2: Resolution Success Rate");
Console.WriteLine("================================\n");

Console.WriteLine($"Successful resolutions: {successfulResolutions.Count}");
Console.WriteLine($"Failed resolutions: {failedResolutions.Count}");

if (successfulResolutions.Count > 0 || failedResolutions.Count > 0)
{
    double successRate = (double)successfulResolutions.Count / (successfulResolutions.Count + failedResolutions.Count) * 100;
    Console.WriteLine($"Success rate: {successRate:F1}%");

    // Aim for 90%+ success rate
    Assert(successRate >= 90.0, "Resolution success rate >= 90%",
        $"Current rate: {successRate:F1}%");
}

Console.WriteLine();

// ========================
// Test 3: Failed Resolution Analysis
// ========================
Console.WriteLine("TEST 3: Failed Resolution Analysis");
Console.WriteLine("===================================\n");

Assert(failedResolutions.Count == 0, "No failed resolutions",
    failedResolutions.Count > 0 ? $"Found {failedResolutions.Count} failures" : "");

if (failedResolutions.Count > 0)
{
    Console.WriteLine($"\nTotal failures: {failedResolutions.Count}");

    // Group by error type
    var failuresByError = failedResolutions.GroupBy(f => f.Error);

    foreach (var group in failuresByError)
    {
        Console.WriteLine($"\n{group.Key}: {group.Count()} occurrences");
        Console.WriteLine(new string('-', 60));

        foreach (var failure in group.Take(10)) // Show first 10 of each type
        {
            Console.WriteLine($"  ✗ {failure.Service}");
            Console.WriteLine($"    Line: {failure.Line}");
            if (failure.Context != null)
            {
                Console.WriteLine($"    Context: {failure.Context}");
            }

            // Check if service was registered
            if (registeredServices.Contains(failure.Service))
            {
                Console.WriteLine($"    Note: Service WAS registered (timing issue or wrong lifetime?)");
            }
            else
            {
                Console.WriteLine($"    Note: Service was NEVER registered");
            }
        }

        if (group.Count() > 10)
        {
            Console.WriteLine($"  ... and {group.Count() - 10} more");
        }
    }
}

Console.WriteLine();

// ========================
// Test 4: Missing Registration Analysis
// ========================
Console.WriteLine("TEST 4: Missing Registration Analysis");
Console.WriteLine("======================================\n");

Assert(missingRegistrations.Count == 0, "No services resolved without registration",
    missingRegistrations.Count > 0 ? $"Found {missingRegistrations.Count} unregistered services" : "");

if (missingRegistrations.Count > 0)
{
    Console.WriteLine($"\nServices resolved but never registered: {missingRegistrations.Count}");

    foreach (var service in missingRegistrations.OrderBy(s => s))
    {
        Console.WriteLine($"  ✗ {service}");

        // Suggest where to register
        if (service.EndsWith("Service"))
        {
            Console.WriteLine($"    Suggestion: Register in App.RegisterTypes() or appropriate module's RegisterTypes()");
        }
        else if (service.EndsWith("ViewModel"))
        {
            Console.WriteLine($"    Suggestion: Register in module's RegisterTypes() method");
        }
        else if (service.EndsWith("Factory") || service.EndsWith("Provider"))
        {
            Console.WriteLine($"    Suggestion: Register as singleton in App.RegisterTypes()");
        }
    }
}

Console.WriteLine();

// ========================
// Test 5: Circular Dependency Detection
// ========================
Console.WriteLine("TEST 5: Circular Dependency Detection");
Console.WriteLine("======================================\n");

Assert(circularDependencies.Count == 0, "No circular dependencies detected",
    circularDependencies.Count > 0 ? $"Found {circularDependencies.Count} circular dependencies" : "");

if (circularDependencies.Count > 0)
{
    Console.WriteLine($"\nCircular dependencies detected: {circularDependencies.Count}");

    foreach (var circular in circularDependencies)
    {
        Console.WriteLine($"\n  ✗ {circular.Service} (line {circular.Line})");
        Console.WriteLine($"    Dependency chain: {string.Join(" -> ", circular.Chain)}");
        Console.WriteLine($"    Fix: Break the cycle by:");
        Console.WriteLine($"      - Using Lazy<T> for one of the dependencies");
        Console.WriteLine($"      - Introducing an interface/mediator");
        Console.WriteLine($"      - Using event aggregator pattern");
    }
}

Console.WriteLine();

// ========================
// Test 6: ViewModel Resolution
// ========================
Console.WriteLine("TEST 6: ViewModel Resolution");
Console.WriteLine("=============================\n");

Console.WriteLine($"ViewModels discovered: {viewModelServices.Count}");
Console.WriteLine($"ViewModels resolved successfully: {successfulResolutions.Where(s => viewModelServices.Contains(s)).Count()}");

if (viewModelServices.Count > 0)
{
    var resolvedVMs = viewModelServices.Where(vm => successfulResolutions.Contains(vm)).ToList();
    var failedVMs = viewModelServices.Where(vm => !successfulResolutions.Contains(vm)).ToList();

    Console.WriteLine("\nResolved ViewModels:");
    foreach (var vm in resolvedVMs.Take(10))
    {
        Console.WriteLine($"  ✓ {vm}");
    }
    if (resolvedVMs.Count > 10)
    {
        Console.WriteLine($"  ... and {resolvedVMs.Count - 10} more");
    }

    if (failedVMs.Count > 0)
    {
        Console.WriteLine("\nFailed ViewModels:");
        foreach (var vm in failedVMs)
        {
            Console.WriteLine($"  ✗ {vm}");
            Console.WriteLine($"    Fix: Ensure ViewModel has parameterless constructor OR all dependencies are registered");
        }
    }

    double vmSuccessRate = viewModelServices.Count > 0
        ? (double)resolvedVMs.Count / viewModelServices.Count * 100
        : 100.0;

    Assert(vmSuccessRate >= 90.0, "ViewModel resolution success rate >= 90%",
        $"Current rate: {vmSuccessRate:F1}%");
}

Console.WriteLine();

// ========================
// Test 7: Container Health
// ========================
Console.WriteLine("TEST 7: Overall Container Health");
Console.WriteLine("=================================\n");

int healthScore = 100;

// Deduct points for issues
if (failedResolutions.Count > 0)
{
    int deduction = Math.Min(30, failedResolutions.Count * 5);
    healthScore -= deduction;
    Console.WriteLine($"  Failed resolutions: -{deduction} points ({failedResolutions.Count} failures)");
}

if (missingRegistrations.Count > 0)
{
    int deduction = Math.Min(25, missingRegistrations.Count * 5);
    healthScore -= deduction;
    Console.WriteLine($"  Missing registrations: -{deduction} points ({missingRegistrations.Count} missing)");
}

if (circularDependencies.Count > 0)
{
    int deduction = Math.Min(20, circularDependencies.Count * 10);
    healthScore -= deduction;
    Console.WriteLine($"  Circular dependencies: -{deduction} points ({circularDependencies.Count} cycles)");
}

Console.WriteLine($"\nContainer Health Score: {healthScore}/100");

if (healthScore >= 90)
{
    Console.WriteLine("Status: ✓ EXCELLENT");
}
else if (healthScore >= 75)
{
    Console.WriteLine("Status: ⚠ GOOD (some issues)");
}
else if (healthScore >= 50)
{
    Console.WriteLine("Status: ⚠ FAIR (multiple issues)");
}
else
{
    Console.WriteLine("Status: ✗ POOR (critical issues)");
}

Assert(healthScore >= 75, "Container health score >= 75", $"Score: {healthScore}/100");

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
    Console.WriteLine("\n✓ ALL TESTS PASSED - Container configuration is excellent!");
    Environment.Exit(0);
}
else
{
    Console.WriteLine("\n✗ TESTS FAILED - Container resolution issues detected!");
    Console.WriteLine("\nRecommended Fixes (Prism Best Practices):");
    Console.WriteLine("==========================================\n");

    Console.WriteLine("1. REGISTRATION TIMING:");
    Console.WriteLine("   - Register infrastructure services in App.RegisterTypes() FIRST");
    Console.WriteLine("   - Register before ConfigureModuleCatalog() is called");
    Console.WriteLine("   - Use explicit registration, avoid auto-discovery during startup");
    Console.WriteLine();

    Console.WriteLine("2. LIFETIME MANAGEMENT:");
    Console.WriteLine("   - Use Singleton for stateless services (most services)");
    Console.WriteLine("   - Use Transient for ViewModels and Views");
    Console.WriteLine("   - Avoid Scoped lifetime unless truly needed");
    Console.WriteLine();

    Console.WriteLine("3. DEPENDENCY PATTERNS:");
    Console.WriteLine("   - Constructor injection for required dependencies");
    Console.WriteLine("   - Property injection for optional dependencies");
    Console.WriteLine("   - Use Lazy<T> to break circular dependencies");
    Console.WriteLine("   - Use IEventAggregator for loosely coupled communication");
    Console.WriteLine();

    Console.WriteLine("4. VIEWMODEL BEST PRACTICES:");
    Console.WriteLine("   - Prefer ViewModelLocator convention (automatic resolution)");
    Console.WriteLine("   - Ensure all constructor parameters are registered");
    Console.WriteLine("   - Use INavigationAware, IConfirmNavigationRequest interfaces");
    Console.WriteLine();

    Console.WriteLine("5. DEBUGGING TIPS:");
    Console.WriteLine("   - Enable container diagnostics: IContainer.WithDependencyInjectionAdapter()");
    Console.WriteLine("   - Log all registrations during startup");
    Console.WriteLine("   - Use container validation in debug builds");
    Console.WriteLine("   - Check DryIoc rules configuration");

    Environment.Exit(1);
}
