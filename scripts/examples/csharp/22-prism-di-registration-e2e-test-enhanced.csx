// ENHANCED E2E test for Prism DI registration validation
// Validates all service registrations, ViewModels, and detects nuanced DryIoc/Prism errors
// Based on Prism-Samples-Wpf patterns: https://github.com/PrismLibrary/Prism-Samples-Wpf
// Specifically validates IModuleHealthService and critical services are registered BEFORE modules call OnInitialized
// Detects: ContainerException, MissingMethodException, XamlParseException, null resolutions, factory issues
// Runs under C# MCP Server inside Docker and parses startup logs for DI-related errors

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

Console.WriteLine("=== ENHANCED PRISM DI REGISTRATION E2E TEST ===");
Console.WriteLine("Validates service registration order, availability, and error detection");
Console.WriteLine("Based on Prism-Samples-Wpf best practices");
Console.WriteLine("Detects: ContainerException, MissingMethodException, XamlParseException, null resolutions\n");

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
// Enhanced Error Detection Patterns
// ========================
var errorPatterns = new Dictionary<string, Regex>
{
    ["ServiceResolutionFailed"] = new Regex(@"\[WRN\]\s+Service resolution failed for\s+([A-Za-z0-9<>_]+)", RegexOptions.IgnoreCase),
    ["ResolvedToNull"] = new Regex(@"⚠\s+([A-Za-z0-9<>_]+)\s+resolved to null", RegexOptions.IgnoreCase),
    ["ContainerException"] = new Regex(@"DryIoc\.ContainerException:\s+code:\s+(Error\.[A-Za-z]+)", RegexOptions.IgnoreCase),
    ["MissingMethodException"] = new Regex(@"MissingMethodException:\s+Cannot dynamically create an instance of type\s+'([A-Za-z0-9.]+)'", RegexOptions.IgnoreCase),
    ["XamlParseException"] = new Regex(@"XamlParseException:\s+'Set property\s+'Prism\.Mvvm\.ViewModelLocator\.AutoWireViewModel'", RegexOptions.IgnoreCase),
    ["ViewModelLocationProviderFailed"] = new Regex(@"\[ERR\]\s+ViewModelLocationProvider:\s+container failed to resolve\s+([A-Za-z0-9.]+)", RegexOptions.IgnoreCase),
    ["ContainerResolutionException"] = new Regex(@"ContainerResolutionException:\s+An unexpected error occurred while resolving\s+'([A-Za-z0-9.]+)'", RegexOptions.IgnoreCase),
    ["ArgumentException"] = new Regex(@"ArgumentException:\s+Expression of type\s+'System\.Object'\s+cannot be used for parameter of type\s+'Prism\.Ioc\.IContainerProvider'", RegexOptions.IgnoreCase),
    ["PassedMemberIsNotStatic"] = new Regex(@"PassedMemberIsNotStaticButInstanceFactoryIsNull", RegexOptions.IgnoreCase),
    ["UnableToResolve"] = new Regex(@"Unable to resolve.*?([A-Za-z0-9.]+I[A-Za-z]+)", RegexOptions.IgnoreCase)
};

// ========================
// Critical Services & ViewModels
// ========================
var criticalServices = new HashSet<string>
{
    "IModuleHealthService",
    "ISettingsService",
    "IDbContextFactory<AppDbContext>",
    "AppDbContext"
};

var criticalViewModels = new HashSet<string>
{
    "SettingsViewModel",
    "ReportsViewModel",
    "UtilityCustomerViewModel",
    "DashboardViewModel"
};

// Framework services that should be available but may not be logged
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

// ========================
// Tracking Structures
// ========================
var registeredServices = new Dictionary<string, (int LineNumber, string LogEntry)>();
var registeredViewModels = new Dictionary<string, (int LineNumber, string LogEntry)>();
var moduleInitAttempts = new Dictionary<string, (int LineNumber, string Phase)>();
var registrationOrder = new List<(int LineNumber, string Service, string LogEntry)>();

// Enhanced error tracking
var errorsByType = new Dictionary<string, List<(int LineNumber, string Service, string Message, string[] Context)>>();
var viewModelErrors = new List<(string ViewModel, string ErrorType, int LineNumber, string[] Context)>();
var factoryErrors = new List<(string Service, string Issue, int LineNumber, string[] Context)>();
var nullResolutions = new List<(string Service, int LineNumber, string Context)>();

// Phase tracking
string currentPhase = "Initialization";
int registrationCompleteLineNumber = 0;
int catalogConfigLineNumber = 0;
int moduleInitStartLineNumber = 0;

// ========================
// Parse Log for DI Issues
// ========================
Console.WriteLine("Analyzing DI registration sequence with enhanced error detection...\n");

int lineNumber = 0;
const int maxLines = 500000;
const long maxSizeMB = 100;
const int contextLines = 25; // Increased context capture

if (latest.Length > maxSizeMB * 1024 * 1024)
{
    Console.WriteLine($"Warning: Log file is very large ({latest.Length / (1024 * 1024)} MB)");
    Console.WriteLine($"Processing first {maxLines} lines only.");
}

var allLines = new List<string>();

using (var reader = new StreamReader(logPath))
{
    string? line;
    while ((line = reader.ReadLine()) != null && lineNumber < maxLines)
    {
        lineNumber++;
        allLines.Add(line);

        if (lineNumber % 1000 == 0)
        {
            Console.Write(".");
            if (lineNumber % 10000 == 0)
            {
                Console.WriteLine($" {lineNumber} lines");
            }
        }

        // Track phases
        if (line.Contains("=== DI Container Registration Complete ==="))
        {
            currentPhase = "Registration Complete";
            registrationCompleteLineNumber = lineNumber;
        }
        else if (line.Contains("=== Configuring Prism Module Catalog"))
        {
            currentPhase = "Module Catalog Configuration";
            catalogConfigLineNumber = lineNumber;
        }
        else if (line.Contains("Modules initializing..."))
        {
            currentPhase = "Module Initialization";
            moduleInitStartLineNumber = lineNumber;
        }

        // Track service registrations
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

        // Track ViewModel registrations specifically
        var vmRegMatch = Regex.Match(line, @"✓\s*Registered\s+([A-Za-z0-9]+ViewModel)", RegexOptions.IgnoreCase);
        if (vmRegMatch.Success)
        {
            string vm = vmRegMatch.Groups[1].Value;
            if (!registeredViewModels.ContainsKey(vm))
            {
                registeredViewModels[vm] = (lineNumber, line);
            }
        }

        // Track module initialization attempts
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

        // Detect all error patterns
        foreach (var kvp in errorPatterns)
        {
            var match = kvp.Value.Match(line);
            if (match.Success)
            {
                string errorType = kvp.Key;
                string service = match.Groups.Count > 1 ? match.Groups[1].Value : "Unknown";

                // Clean up service name
                if (service.Contains('.'))
                {
                    service = service.Substring(service.LastIndexOf('.') + 1);
                }

                // Capture extensive context
                var context = new List<string>();
                int startIdx = Math.Max(0, allLines.Count - 1);
                int endIdx = Math.Min(allLines.Count - 1 + contextLines, lineNumber - 1);

                for (int i = startIdx; i < allLines.Count; i++)
                {
                    context.Add(allLines[i]);
                }

                if (!errorsByType.ContainsKey(errorType))
                {
                    errorsByType[errorType] = new List<(int, string, string, string[])>();
                }

                errorsByType[errorType].Add((lineNumber, service, line, context.ToArray()));

                // Track ViewModel-specific errors
                if (service.EndsWith("ViewModel") || errorType == "ViewModelLocationProviderFailed" || errorType == "MissingMethodException")
                {
                    viewModelErrors.Add((service, errorType, lineNumber, context.ToArray()));
                }

                // Track factory errors
                if (errorType == "PassedMemberIsNotStatic")
                {
                    factoryErrors.Add((service, "Non-static factory method without instance", lineNumber, context.ToArray()));
                }
            }
        }

        // Detect null resolutions
        if (line.Contains("resolved to null"))
        {
            var nullMatch = Regex.Match(line, @"⚠\s+([A-Za-z0-9<>_]+)\s+resolved to null");
            if (nullMatch.Success)
            {
                string service = nullMatch.Groups[1].Value;
                nullResolutions.Add((service, lineNumber, line));
            }
        }
    }
}

Console.WriteLine($"\nProcessed {lineNumber} lines\n");

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
// Test 2: Registration Order & Phases
// ========================
Console.WriteLine("\nTEST 2: Service Registration Order & Phases");
Console.WriteLine("============================================\n");

Console.WriteLine($"Phase Timeline:");
Console.WriteLine($"  Registration Complete: Line {registrationCompleteLineNumber}");
Console.WriteLine($"  Module Catalog Config: Line {catalogConfigLineNumber}");
Console.WriteLine($"  Module Init Start: Line {moduleInitStartLineNumber}\n");

// IModuleHealthService MUST be registered before any module initialization
if (registeredServices.ContainsKey("IModuleHealthService"))
{
    var healthServiceLine = registeredServices["IModuleHealthService"].LineNumber;
    var firstModuleInit = moduleInitAttempts.Any() ? moduleInitAttempts.Values.Min(m => m.LineNumber) : int.MaxValue;

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

// Check for late registrations (after catalog config)
if (catalogConfigLineNumber > 0)
{
    var lateRegistrations = registrationOrder.Where(r => r.LineNumber > catalogConfigLineNumber).ToList();
    Assert(lateRegistrations.Count == 0, "No registrations after module catalog configuration",
        lateRegistrations.Count > 0
            ? $"Found {lateRegistrations.Count} late registrations: {string.Join(", ", lateRegistrations.Take(5).Select(r => r.Service))}"
            : "");
}

// ========================
// Test 3: Enhanced DI Error Detection
// ========================
Console.WriteLine("\nTEST 3: Enhanced DI Error Detection");
Console.WriteLine("====================================\n");

int totalErrors = errorsByType.Sum(kvp => kvp.Value.Count);
Assert(totalErrors == 0, "No DI resolution errors detected",
    totalErrors > 0 ? $"Found {totalErrors} DI errors across {errorsByType.Count} error types" : "");

if (totalErrors > 0)
{
    Console.WriteLine("\n╔════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║              DETAILED ERROR ANALYSIS BY TYPE                       ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝\n");

    foreach (var kvp in errorsByType.OrderByDescending(e => e.Value.Count))
    {
        string errorType = kvp.Key;
        var errors = kvp.Value;

        Console.WriteLine($"┌─ {errorType} ({errors.Count} occurrence(s)) ─────────────────────────────");

        foreach (var error in errors.Take(3)) // Show first 3 of each type
        {
            Console.WriteLine($"│  Line {error.LineNumber}: {error.Service}");
            Console.WriteLine($"│  Message: {error.Message.Substring(0, Math.Min(100, error.Message.Length))}...");

            // Show stack trace snippet (first 5 lines)
            Console.WriteLine($"│  Stack Trace:");
            foreach (var ctx in error.Context.Where(c => c.Contains("at ")).Take(5))
            {
                Console.WriteLine($"│    {ctx.Trim()}");
            }
            Console.WriteLine($"│");
        }

        Console.WriteLine($"└────────────────────────────────────────────────────────────────────\n");
    }
}

// ========================
// Test 4: Null Resolutions
// ========================
Console.WriteLine("\nTEST 4: Null Resolution Detection");
Console.WriteLine("==================================\n");

Assert(nullResolutions.Count == 0, "No services resolved to null",
    nullResolutions.Count > 0
        ? $"Found {nullResolutions.Count} null resolutions: {string.Join(", ", nullResolutions.Select(n => n.Service))}"
        : "");

if (nullResolutions.Count > 0)
{
    Console.WriteLine("Null Resolutions Detected:");
    foreach (var nr in nullResolutions)
    {
        Console.WriteLine($"  ✗ {nr.Service} (Line {nr.LineNumber})");
        Console.WriteLine($"    Context: {nr.Context}");
    }
    Console.WriteLine();
}

// ========================
// Test 5: Factory Issues
// ========================
Console.WriteLine("\nTEST 5: Factory Configuration Issues");
Console.WriteLine("=====================================\n");

Assert(factoryErrors.Count == 0, "No factory configuration errors",
    factoryErrors.Count > 0
        ? $"Found {factoryErrors.Count} factory errors"
        : "");

if (factoryErrors.Count > 0)
{
    Console.WriteLine("Factory Configuration Issues:");
    foreach (var fe in factoryErrors)
    {
        Console.WriteLine($"  ✗ {fe.Service}: {fe.Issue} (Line {fe.LineNumber})");
        Console.WriteLine($"    FIX: Register as instance or use static factory:");
        Console.WriteLine($"    containerRegistry.RegisterInstance(new {fe.Service}());");
        Console.WriteLine($"    OR ensure factory method is static\n");
    }
}

// ========================
// Test 6: ViewModel-Specific Validation
// ========================
Console.WriteLine("\nTEST 6: ViewModel Auto-Wiring & Registration");
Console.WriteLine("=============================================\n");

Assert(viewModelErrors.Count == 0, "No ViewModel auto-wiring failures",
    viewModelErrors.Count > 0
        ? $"Found {viewModelErrors.Count} ViewModel errors"
        : "");

if (viewModelErrors.Count > 0)
{
    Console.WriteLine("ViewModel Error Details:");
    Console.WriteLine("────────────────────────────────────────────────────────────────────\n");

    var groupedByVM = viewModelErrors.GroupBy(e => e.ViewModel);

    foreach (var group in groupedByVM)
    {
        Console.WriteLine($"ViewModel: {group.Key}");
        Console.WriteLine($"Errors: {group.Count()}");

        // Check if explicitly registered
        bool isRegistered = registeredViewModels.ContainsKey(group.Key);
        Console.WriteLine($"Explicitly Registered: {(isRegistered ? "YES" : "NO")}");

        if (!isRegistered)
        {
            Console.WriteLine($"⚠ PRISM SAMPLES RECOMMENDATION:");
            Console.WriteLine($"  Register ViewModel explicitly in App.RegisterTypes():");
            Console.WriteLine($"  containerRegistry.Register<{group.Key}>();");
            Console.WriteLine($"  OR add parameterless constructor to {group.Key}");
        }

        foreach (var error in group)
        {
            Console.WriteLine($"  Error Type: {error.ErrorType} (Line {error.LineNumber})");

            // Show relevant context
            var relevantContext = error.Context
                .Where(c => c.Contains("ViewModel") || c.Contains("constructor") || c.Contains("dependencies"))
                .Take(3);

            if (relevantContext.Any())
            {
                Console.WriteLine($"  Context:");
                foreach (var ctx in relevantContext)
                {
                    Console.WriteLine($"    {ctx.Trim()}");
                }
            }
        }
        Console.WriteLine();
    }
}

// Check critical ViewModels
Console.WriteLine("Critical ViewModel Registration Status:");
foreach (var vm in criticalViewModels)
{
    bool isRegistered = registeredViewModels.ContainsKey(vm);
    bool hasErrors = viewModelErrors.Any(e => e.ViewModel.Contains(vm));

    string status = isRegistered ? "✓ Registered" : (hasErrors ? "✗ Failed" : "⚠ Not Found");
    Console.WriteLine($"  {status}: {vm}");
}
Console.WriteLine();

// ========================
// Test 7: Module Initialization Status
// ========================
Console.WriteLine("\nTEST 7: Module Initialization Status");
Console.WriteLine("=====================================\n");

Console.WriteLine($"Modules attempted to initialize: {moduleInitAttempts.Count}");

foreach (var kvp in moduleInitAttempts.OrderBy(m => m.Value.LineNumber))
{
    string module = kvp.Key;
    int initLine = kvp.Value.LineNumber;
    string phase = kvp.Value.Phase;

    // Check if this module had DI errors in its phase
    var phaseErrors = errorsByType.Values
        .SelectMany(errors => errors)
        .Where(e => e.LineNumber > initLine && e.LineNumber < initLine + 100)
        .ToArray();

    if (phaseErrors.Length == 0)
    {
        Assert(true, $"{module} initialized without DI errors", $"Started at line {initLine} ({phase})");
    }
    else
    {
        var errorServices = phaseErrors.Select(e => e.Service).Distinct();
        Assert(false, $"{module} initialized without DI errors",
            $"Had {phaseErrors.Length} error(s) affecting: {string.Join(", ", errorServices)}");
    }
}

// ========================
// Test 8: Registration Completeness
// ========================
Console.WriteLine("\nTEST 8: Registration Completeness Check");
Console.WriteLine("========================================\n");

Console.WriteLine($"Total services registered: {registeredServices.Count}");
Console.WriteLine($"Total ViewModels registered: {registeredViewModels.Count}");
Console.WriteLine("\nFirst 10 registrations (in order):");

foreach (var reg in registrationOrder.Take(10))
{
    Console.WriteLine($"  Line {reg.LineNumber}: {reg.Service}");
}

Console.WriteLine("\nFramework Services Check:");
foreach (var service in frameworkServices)
{
    if (registeredServices.ContainsKey(service))
    {
        Console.WriteLine($"  ✓ {service}");
    }
    else
    {
        Console.WriteLine($"  ⚠ {service} - Not found (may be auto-registered by Prism)");
    }
}

// ========================
// Summary
// ========================
Console.WriteLine("\n" + new string('═', 70));
Console.WriteLine("║                         TEST SUMMARY                               ║");
Console.WriteLine(new string('═', 70));
Console.WriteLine($"PASSED: {pass}/{total}");
Console.WriteLine($"FAILED: {total - pass}/{total}");
Console.WriteLine();
Console.WriteLine($"Error Summary:");
Console.WriteLine($"  Total DI Errors: {totalErrors}");
Console.WriteLine($"  ViewModel Errors: {viewModelErrors.Count}");
Console.WriteLine($"  Null Resolutions: {nullResolutions.Count}");
Console.WriteLine($"  Factory Errors: {factoryErrors.Count}");
Console.WriteLine($"  Final Phase: {currentPhase}");  // Added usage for currentPhase
Console.WriteLine(new string('═', 70));

if (pass == total)
{
    Console.WriteLine("\n✓ ALL TESTS PASSED - DI registration is correct!");
    Console.WriteLine("No ViewModel auto-wiring issues detected.");
    Console.WriteLine("All services properly registered before module initialization.");
    Environment.Exit(0);
}
else
{
    Console.WriteLine("\n✗ TESTS FAILED - DI registration issues detected!");
    Console.WriteLine("\n╔════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                    RECOMMENDED FIXES                                ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝\n");

    // Prism-Samples-Wpf based recommendations
    Console.WriteLine("Based on Prism-Samples-Wpf patterns:\n");

    Console.WriteLine("1. CRITICAL SERVICES - Register in App.RegisterTypes() BEFORE ConfigureModuleCatalog():");
    Console.WriteLine("   ────────────────────────────────────────────────────────────────────");
    Console.WriteLine("   protected override void RegisterTypes(IContainerRegistry containerRegistry)");
    Console.WriteLine("   {");
    Console.WriteLine("       // Register critical services FIRST");
    Console.WriteLine("       containerRegistry.RegisterSingleton<IModuleHealthService, ModuleHealthService>();");
    Console.WriteLine("       containerRegistry.RegisterSingleton<ISettingsService, SettingsService>();");
    Console.WriteLine("       // ... other infrastructure services");
    Console.WriteLine("   }\n");

    Console.WriteLine("2. VIEWMODEL REGISTRATION - Add explicit registrations:");
    Console.WriteLine("   ────────────────────────────────────────────────────────────────────");
    if (viewModelErrors.Any())
    {
        var failedVMs = viewModelErrors.Select(e => e.ViewModel).Distinct();
        foreach (var vm in failedVMs.Take(5))
        {
            Console.WriteLine($"   containerRegistry.Register<{vm}>();");
        }
        Console.WriteLine("   // OR add parameterless constructors to ViewModels\n");
    }

    Console.WriteLine("3. FACTORY ISSUES - Fix non-static factory methods:");
    Console.WriteLine("   ────────────────────────────────────────────────────────────────────");
    if (factoryErrors.Any())
    {
        foreach (var fe in factoryErrors.Take(3))
        {
            Console.WriteLine($"   // For {fe.Service}:");
            Console.WriteLine($"   containerRegistry.RegisterInstance(new {fe.Service}());");
            Console.WriteLine($"   // OR use static factory method\n");
        }
    }

    Console.WriteLine("4. NULL RESOLUTIONS - Verify registration and dependencies:");
    Console.WriteLine("   ────────────────────────────────────────────────────────────────────");
    if (nullResolutions.Any())
    {
        foreach (var nr in nullResolutions.Take(3))
        {
            Console.WriteLine($"   // Check {nr.Service} registration and its dependencies");
        }
        Console.WriteLine();
    }

    Console.WriteLine("5. MODULE INITIALIZATION - Only resolve after registration complete:");
    Console.WriteLine("   ────────────────────────────────────────────────────────────────────");
    Console.WriteLine("   public void OnInitialized(IContainerProvider containerProvider)");
    Console.WriteLine("   {");
    Console.WriteLine("       // All services should be registered by this point");
    Console.WriteLine("       var service = containerProvider.Resolve<IMyService>();");
    Console.WriteLine("   }\n");

    Console.WriteLine("6. PRISM BEST PRACTICES:");
    Console.WriteLine("   ────────────────────────────────────────────────────────────────────");
    Console.WriteLine("   • Register infrastructure services in App.RegisterTypes()");
    Console.WriteLine("   • Register module-specific services in Module.RegisterTypes()");
    Console.WriteLine("   • Resolve services only in Module.OnInitialized() after registration");
    Console.WriteLine("   • Use explicit ViewModel registrations for constructor dependencies");
    Console.WriteLine("   • Validate critical services early in CreateShell() or CreateBootstrapper()");
    Console.WriteLine();

    Console.WriteLine("Reference: https://github.com/PrismLibrary/Prism-Samples-Wpf");
    Console.WriteLine("See: ModularApp, HelloWorld samples for proper registration patterns\n");

    Environment.Exit(1);
}
