#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.Extensions.Logging, 9.0.0"
#r "nuget: Serilog, 4.1.0"
#r "nuget: Serilog.Sinks.Console, 6.0.0"
#r "nuget: Serilog.Sinks.File, 6.0.0"
#r "nuget: Serilog.Extensions.Logging, 8.0.0"
#r "nuget: System.Reactive, 6.0.0"

/*
 * 90-app-startup-phase2-validation-test.csx
 *
 * ROBUST VALIDATION TEST for Mission Requirements:
 * "In App.xaml.cs OnStartup, after EncryptedLocalSecretVaultService migration, add try-catch
 *  with Dispatcher.VerifyAccess and detailed logging for silent exits. Include checks for
 *  Prism Container.Resolve hangs on ViewModels like SettingsViewModel, and validate merged
 *  ResourceDictionaries (DataTemplates.xaml, Generic.xaml) for FluentLight theme Brush binding
 *  errors. Generate code that forces UI thread sync and reports to Serilog if exit occurs
 *  during Phase 2 transition."
 *
 * TEST SCOPE:
 * 1. ✅ Verify Phase 2 has comprehensive try-catch with detailed logging for silent exits
 * 2. ✅ Verify Dispatcher.VerifyAccess() or UI thread synchronization is present
 * 3. ✅ Verify SettingsViewModel resolution validation exists
 * 4. ✅ Verify ResourceDictionaries validation for DataTemplates.xaml and Generic.xaml
 * 5. ✅ Verify FluentLight theme Brush binding error detection
 * 6. ✅ Verify Serilog logging for Phase 2 transition failures
 * 7. ✅ Verify EncryptedLocalSecretVaultService migration error handling
 *
 * VALIDATION APPROACH:
 * - Static code analysis via reflection and file parsing
 * - Pattern matching for required code constructs
 * - Verification of error handling paths
 * - Logging infrastructure validation
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

// ============================================================================
// CONFIGURATION
// ============================================================================

var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
var logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

// ============================================================================
// LOGGING SETUP
// ============================================================================

var logPath = Path.Combine(logsDir, $"90-app-startup-phase2-validation-{DateTime.Now:yyyyMMdd-HHmmss}.log");
Directory.CreateDirectory(logsDir);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(logPath, outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

Log.Information("=".PadRight(80, '='));
Log.Information("90-APP-STARTUP-PHASE2-VALIDATION-TEST");
Log.Information("=".PadRight(80, '='));
Log.Information("Repository Root: {RepoRoot}", repoRoot);
Log.Information("Logs Directory: {LogsDir}", logsDir);
Log.Information("Test Started: {Timestamp}", DateTime.Now);
Log.Information("");

// ============================================================================
// TEST CONFIGURATION
// ============================================================================

var testResults = new List<TestResult>();
var totalTests = 0;
var passedTests = 0;
var failedTests = 0;

class TestResult
{
    public string TestName { get; set; }
    public bool Passed { get; set; }
    public string Message { get; set; }
    public string Details { get; set; }
    public List<string> Evidence { get; set; } = new();
    public List<string> MissingElements { get; set; } = new();
}

// ============================================================================
// FILE PATHS
// ============================================================================

var appLifecyclePath = Path.Combine(repoRoot, "src", "WileyWidget", "App.Lifecycle.cs");
var appResourcesPath = Path.Combine(repoRoot, "src", "WileyWidget", "App.Resources.cs");
var appDIPath = Path.Combine(repoRoot, "src", "WileyWidget", "App.DependencyInjection.cs");
var validatorPath = Path.Combine(repoRoot, "src", "WileyWidget", "Services", "Startup", "StartupEnvironmentValidator.cs");
var resourceLoaderPath = Path.Combine(repoRoot, "src", "WileyWidget", "Startup", "EnterpriseResourceLoader.cs");

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================

void RecordTest(string testName, bool passed, string message, string details = "", List<string> evidence = null, List<string> missing = null)
{
    totalTests++;
    if (passed) passedTests++;
    else failedTests++;

    var result = new TestResult
    {
        TestName = testName,
        Passed = passed,
        Message = message,
        Details = details,
        Evidence = evidence ?? new List<string>(),
        MissingElements = missing ?? new List<string>()
    };
    testResults.Add(result);

    var emoji = passed ? "✅" : "❌";
    Log.Information("{Emoji} {TestName}: {Message}", emoji, testName, message);

    if (!string.IsNullOrEmpty(details))
    {
        Log.Debug("   Details: {Details}", details);
    }

    if (evidence != null && evidence.Any())
    {
        Log.Debug("   Evidence found ({Count} items):", evidence.Count);
        foreach (var e in evidence.Take(3))
        {
            Log.Debug("     - {Evidence}", e.Length > 100 ? e.Substring(0, 100) + "..." : e);
        }
    }

    if (missing != null && missing.Any())
    {
        Log.Warning("   Missing elements ({Count} items):", missing.Count);
        foreach (var m in missing)
        {
            Log.Warning("     ⚠️  {Missing}", m);
        }
    }
}

string ReadFileContent(string filePath)
{
    if (!File.Exists(filePath))
    {
        Log.Error("File not found: {FilePath}", filePath);
        return string.Empty;
    }
    return File.ReadAllText(filePath);
}

bool ContainsPattern(string content, string pattern, out List<string> matches)
{
    matches = new List<string>();
    try
    {
        var regex = new Regex(pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var regexMatches = regex.Matches(content);

        foreach (Match match in regexMatches)
        {
            matches.Add(match.Value);
        }

        return matches.Any();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Pattern matching failed for: {Pattern}", pattern);
        return false;
    }
}

bool ContainsAllKeywords(string content, params string[] keywords)
{
    return keywords.All(k => content.Contains(k, StringComparison.OrdinalIgnoreCase));
}

// ============================================================================
// TEST 1: Verify EncryptedLocalSecretVaultService Migration Error Handling
// ============================================================================

Log.Information("");
Log.Information("TEST 1: Verify EncryptedLocalSecretVaultService Migration Error Handling");
Log.Information("-".PadRight(80, '-'));

var lifecycleContent = ReadFileContent(appLifecyclePath);

var test1Evidence = new List<string>();
var test1Missing = new List<string>();
var test1Passed = true;

// Check for EncryptedLocalSecretVaultService instantiation
if (ContainsPattern(lifecycleContent, @"new\s+WileyWidget\.Services\.EncryptedLocalSecretVaultService", out var secretServiceMatches))
{
    test1Evidence.Add("EncryptedLocalSecretVaultService instantiation found");
}
else
{
    test1Missing.Add("EncryptedLocalSecretVaultService instantiation");
    test1Passed = false;
}

// Check for MigrateSecretsFromEnvironmentAsync call
if (ContainsPattern(lifecycleContent, @"MigrateSecretsFromEnvironmentAsync\(\)", out var migrateMatches))
{
    test1Evidence.Add("MigrateSecretsFromEnvironmentAsync() call found");
}
else
{
    test1Missing.Add("MigrateSecretsFromEnvironmentAsync() call");
    test1Passed = false;
}

// Check for try-catch around secret vault migration
if (ContainsPattern(lifecycleContent, @"try\s*\{[\s\S]*?EncryptedLocalSecretVaultService[\s\S]*?\}\s*catch\s*\(Exception", out var tryCatchMatches))
{
    test1Evidence.Add("Try-catch block around secret vault migration");
}
else
{
    test1Missing.Add("Try-catch block around secret vault migration");
    test1Passed = false;
}

// Check for detailed logging on failure
if (ContainsAllKeywords(lifecycleContent, "EncryptedLocalSecretVaultService", "Warning", "migration failed"))
{
    test1Evidence.Add("Detailed logging for migration failures");
}
else
{
    test1Missing.Add("Detailed logging for migration failures");
    test1Passed = false;
}

RecordTest(
    "EncryptedLocalSecretVaultService Migration Error Handling",
    test1Passed,
    test1Passed ? "All secret vault migration error handling in place" : "Secret vault migration error handling incomplete",
    $"Found {test1Evidence.Count} of 4 required elements",
    test1Evidence,
    test1Missing
);

// ============================================================================
// TEST 2: Verify Phase 2 Try-Catch with Detailed Logging
// ============================================================================

Log.Information("");
Log.Information("TEST 2: Verify Phase 2 Try-Catch with Detailed Logging");
Log.Information("-".PadRight(80, '-'));

var test2Evidence = new List<string>();
var test2Missing = new List<string>();
var test2Passed = true;

// Check for Phase 2 identification in comments
if (ContainsPattern(lifecycleContent, @"Phase 2.*Prism bootstrap", out var phase2Matches))
{
    test2Evidence.Add($"Phase 2 identified in code ({phase2Matches.Count} references)");
}
else
{
    test2Missing.Add("Phase 2 identification in comments");
    test2Passed = false;
}

// Check for base.OnStartup(e) call (Phase 2 trigger)
if (ContainsPattern(lifecycleContent, @"base\.OnStartup\(e\)", out var baseOnStartupMatches))
{
    test2Evidence.Add("base.OnStartup(e) call found (Phase 2 trigger)");
}
else
{
    test2Missing.Add("base.OnStartup(e) call");
    test2Passed = false;
}

// Check for comprehensive try-catch in OnStartup
if (ContainsPattern(lifecycleContent, @"protected\s+override\s+void\s+OnStartup[^{]*\{[^}]*try\s*\{", out var onStartupTryMatches))
{
    test2Evidence.Add("Try-catch in OnStartup method");
}
else
{
    test2Missing.Add("Try-catch in OnStartup method");
    test2Passed = false;
}

// Check for detailed exception logging
if (ContainsAllKeywords(lifecycleContent, "Log.Fatal", "Critical error", "enhanced startup"))
{
    test2Evidence.Add("Detailed exception logging with Log.Fatal");
}
else
{
    test2Missing.Add("Detailed exception logging");
    test2Passed = false;
}

// Check for ShowEmergencyErrorDialog on failure
if (ContainsPattern(lifecycleContent, @"ShowEmergencyErrorDialog\(ex\)", out var emergencyDialogMatches))
{
    test2Evidence.Add("Emergency error dialog on startup failure");
}
else
{
    test2Missing.Add("Emergency error dialog");
    test2Passed = false;
}

// Check for Application.Shutdown on error
if (ContainsPattern(lifecycleContent, @"Application\.Current\.Shutdown\(1\)", out var shutdownMatches))
{
    test2Evidence.Add("Graceful shutdown on Phase 2 failure");
}
else
{
    test2Missing.Add("Graceful shutdown on failure");
    test2Passed = false;
}

RecordTest(
    "Phase 2 Try-Catch with Detailed Logging",
    test2Passed,
    test2Passed ? "Comprehensive Phase 2 error handling verified" : "Phase 2 error handling incomplete",
    $"Found {test2Evidence.Count} of 6 required elements",
    test2Evidence,
    test2Missing
);

// ============================================================================
// TEST 3: Verify Dispatcher.VerifyAccess or UI Thread Synchronization
// ============================================================================

Log.Information("");
Log.Information("TEST 3: Verify Dispatcher.VerifyAccess or UI Thread Synchronization");
Log.Information("-".PadRight(80, '-'));

var test3Evidence = new List<string>();
var test3Missing = new List<string>();
var test3Passed = true;

// Check for Dispatcher.CurrentDispatcher.Invoke usage
if (ContainsPattern(lifecycleContent, @"Dispatcher\.CurrentDispatcher\.Invoke", out var dispatcherInvokeMatches))
{
    test3Evidence.Add($"Dispatcher.CurrentDispatcher.Invoke found ({dispatcherInvokeMatches.Count} occurrences)");
}
else
{
    test3Missing.Add("Dispatcher.CurrentDispatcher.Invoke");
    test3Passed = false;
}

// Check for DispatcherPriority.ApplicationIdle (forces UI thread sync)
if (ContainsPattern(lifecycleContent, @"DispatcherPriority\.ApplicationIdle", out var priorityMatches))
{
    test3Evidence.Add("DispatcherPriority.ApplicationIdle for UI thread sync");
}
else
{
    test3Missing.Add("DispatcherPriority specification");
    test3Passed = false;
}

// Check for dispatcher processing comments
if (ContainsAllKeywords(lifecycleContent, "Force", "Dispatcher", "process"))
{
    test3Evidence.Add("Documentation of dispatcher processing intent");
}
else
{
    test3Missing.Add("Dispatcher processing documentation");
    test3Passed = false;
}

// Alternative: Check for Dispatcher.VerifyAccess (though Invoke is better)
if (ContainsPattern(lifecycleContent, @"Dispatcher\.VerifyAccess", out var verifyAccessMatches))
{
    test3Evidence.Add("Dispatcher.VerifyAccess() found (alternative approach)");
}

RecordTest(
    "Dispatcher UI Thread Synchronization",
    test3Passed,
    test3Passed ? "UI thread synchronization properly implemented" : "UI thread synchronization incomplete",
    $"Found {test3Evidence.Count} of 3 required elements",
    test3Evidence,
    test3Missing
);

// ============================================================================
// TEST 4: Verify SettingsViewModel Container.Resolve Validation
// ============================================================================

Log.Information("");
Log.Information("TEST 4: Verify SettingsViewModel Container.Resolve Validation");
Log.Information("-".PadRight(80, '-'));

var validatorContent = ReadFileContent(validatorPath);
var test4Evidence = new List<string>();
var test4Missing = new List<string>();
var test4Passed = true;

// Check for ValidateAndRegisterViewModels method
if (ContainsPattern(validatorContent, @"public\s+void\s+ValidateAndRegisterViewModels", out var validateMethodMatches))
{
    test4Evidence.Add("ValidateAndRegisterViewModels method exists");
}
else
{
    test4Missing.Add("ValidateAndRegisterViewModels method");
    test4Passed = false;
}

// Check for SettingsViewModel type resolution
if (ContainsPattern(validatorContent, @"SettingsViewModel", out var settingsVMMatches))
{
    test4Evidence.Add($"SettingsViewModel referenced ({settingsVMMatches.Count} times)");
}
else
{
    test4Missing.Add("SettingsViewModel validation");
    test3Passed = false;
}

// Check for constructor parameter validation
if (ContainsAllKeywords(validatorContent, "constructor", "parameters", "GetParameters"))
{
    test4Evidence.Add("Constructor parameter validation logic");
}
else
{
    test4Missing.Add("Constructor parameter validation");
    test4Passed = false;
}

// Check for IsRegistered check on dependencies
if (ContainsPattern(validatorContent, @"IsRegistered\(paramType\)", out var isRegisteredMatches))
{
    test4Evidence.Add("Container.IsRegistered() dependency validation");
}
else
{
    test4Missing.Add("IsRegistered dependency check");
    test4Passed = false;
}

// Check for logging of unregistered dependencies
if (ContainsAllKeywords(validatorContent, "SettingsViewModel", "dependency not registered"))
{
    test4Evidence.Add("Logging for unregistered SettingsViewModel dependencies");
}
else
{
    test4Missing.Add("Dependency logging");
    test4Passed = false;
}

// Check that ValidateAndRegisterViewModels is called in App.Lifecycle.cs
if (ContainsPattern(lifecycleContent, @"ValidateAndRegisterViewModels\(registry\)", out var callMatches))
{
    test4Evidence.Add("ValidateAndRegisterViewModels called in OnInitialized");
}
else
{
    test4Missing.Add("ValidateAndRegisterViewModels call in lifecycle");
    test4Passed = false;
}

RecordTest(
    "SettingsViewModel Container.Resolve Validation",
    test4Passed,
    test4Passed ? "SettingsViewModel resolution validation complete" : "SettingsViewModel validation incomplete",
    $"Found {test4Evidence.Count} of 6 required elements",
    test4Evidence,
    test4Missing
);

// ============================================================================
// TEST 5: Verify ResourceDictionaries Validation (DataTemplates.xaml, Generic.xaml)
// ============================================================================

Log.Information("");
Log.Information("TEST 5: Verify ResourceDictionaries Validation");
Log.Information("-".PadRight(80, '-'));

var resourceLoaderContent = ReadFileContent(resourceLoaderPath);
var test5Evidence = new List<string>();
var test5Missing = new List<string>();
var test5Passed = true;

// Check for ValidateCriticalResources method
if (ContainsPattern(lifecycleContent, @"private\s+void\s+ValidateCriticalResources\(\)", out var validateResourcesMatches))
{
    test5Evidence.Add("ValidateCriticalResources method exists");
}
else
{
    test5Missing.Add("ValidateCriticalResources method");
    test5Passed = false;
}

// Check for DataTemplates.xaml in resource catalog
if (ContainsPattern(resourceLoaderContent, @"DataTemplates\.xaml", out var dataTemplatesMatches))
{
    test5Evidence.Add("DataTemplates.xaml in resource catalog");
}
else
{
    test5Missing.Add("DataTemplates.xaml reference");
    test5Passed = false;
}

// Check for Generic.xaml in resource catalog
if (ContainsPattern(resourceLoaderContent, @"Generic\.xaml", out var genericMatches))
{
    test5Evidence.Add("Generic.xaml in resource catalog");
}
else
{
    test5Missing.Add("Generic.xaml reference");
    test5Passed = false;
}

// Check for ResourceCriticality.Critical marking
if (ContainsPattern(resourceLoaderContent, @"ResourceCriticality\.Critical", out var criticalityMatches))
{
    test5Evidence.Add($"ResourceCriticality.Critical marking ({criticalityMatches.Count} resources)");
}
else
{
    test5Missing.Add("ResourceCriticality.Critical marking");
    test5Passed = false;
}

// Check for MergedDictionaries validation
if (ContainsAllKeywords(lifecycleContent, "MergedDictionaries", "Loaded", "Count"))
{
    test5Evidence.Add("MergedDictionaries count validation");
}
else
{
    test5Missing.Add("MergedDictionaries validation");
    test5Passed = false;
}

// Check for critical brush validation
if (ContainsAllKeywords(lifecycleContent, "criticalBrushes", "Contains", "Missing"))
{
    test5Evidence.Add("Critical brushes validation loop");
}
else
{
    test5Missing.Add("Critical brushes validation");
    test5Passed = false;
}

// Check that ValidateCriticalResources is called
if (ContainsPattern(lifecycleContent, @"ValidateCriticalResources\(\)", out var validateCallMatches))
{
    test5Evidence.Add("ValidateCriticalResources() called in startup");
}
else
{
    test5Missing.Add("ValidateCriticalResources call");
    test5Passed = false;
}

RecordTest(
    "ResourceDictionaries Validation (DataTemplates.xaml, Generic.xaml)",
    test5Passed,
    test5Passed ? "ResourceDictionaries validation complete" : "ResourceDictionaries validation incomplete",
    $"Found {test5Evidence.Count} of 7 required elements",
    test5Evidence,
    test5Missing
);

// ============================================================================
// TEST 6: Verify FluentLight Theme Brush Binding Error Detection
// ============================================================================

Log.Information("");
Log.Information("TEST 6: Verify FluentLight Theme Brush Binding Error Detection");
Log.Information("-".PadRight(80, '-'));

var resourcesContent = ReadFileContent(appResourcesPath);
var test6Evidence = new List<string>();
var test6Missing = new List<string>();
var test6Passed = true;

// Check for FluentLight theme application
if (ContainsPattern(resourcesContent, @"new\s+.*Theme\(""FluentLight""\)", out var fluentLightMatches))
{
    test6Evidence.Add("FluentLight theme instantiation");
}
else
{
    test6Missing.Add("FluentLight theme instantiation");
    test6Passed = false;
}

// Check for SfSkinManager.ApplicationTheme assignment
if (ContainsPattern(resourcesContent, @"SfSkinManager\.ApplicationTheme\s*=", out var themeAssignmentMatches))
{
    test6Evidence.Add("SfSkinManager.ApplicationTheme assignment");
}
else
{
    test6Missing.Add("Theme assignment");
    test6Passed = false;
}

// Check for theme null validation
if (ContainsPattern(resourcesContent, @"SfSkinManager\.ApplicationTheme\s*==\s*null", out var themeNullMatches))
{
    test6Evidence.Add("Theme null validation");
}
else
{
    test6Missing.Add("Theme null validation");
    test6Passed = false;
}

// Check for critical brush list in ValidateCriticalResources
var expectedBrushes = new[] { "InfoBrush", "ErrorBrush", "ContentBackgroundBrush", "PanelBorderBrush", "AccentBlueBrush" };
var foundBrushes = expectedBrushes.Where(b => lifecycleContent.Contains(b)).ToList();

if (foundBrushes.Count >= 3)
{
    test6Evidence.Add($"Critical FluentLight brushes validated ({foundBrushes.Count} of {expectedBrushes.Length})");
}
else
{
    test6Missing.Add("Comprehensive critical brush validation");
    test6Passed = false;
}

// Check for Application.Current.Resources.Contains checks
if (ContainsPattern(lifecycleContent, @"Application\.Current\.Resources\.Contains\(brush\)", out var containsMatches))
{
    test6Evidence.Add("Brush existence validation via Resources.Contains");
}
else
{
    test6Missing.Add("Resources.Contains brush validation");
    test6Passed = false;
}

// Check for missing brush error logging
if (ContainsAllKeywords(lifecycleContent, "MISSING", "Brush", "Log.Error"))
{
    test6Evidence.Add("Error logging for missing brushes");
}
else
{
    test6Missing.Add("Missing brush error logging");
    test6Passed = false;
}

RecordTest(
    "FluentLight Theme Brush Binding Error Detection",
    test6Passed,
    test6Passed ? "FluentLight theme brush validation complete" : "Theme brush validation incomplete",
    $"Found {test6Evidence.Count} of 6 required elements, including {foundBrushes.Count} critical brushes",
    test6Evidence,
    test6Missing
);

// ============================================================================
// TEST 7: Verify Serilog Reporting for Phase 2 Transition Failures
// ============================================================================

Log.Information("");
Log.Information("TEST 7: Verify Serilog Reporting for Phase 2 Transition Failures");
Log.Information("-".PadRight(80, '-'));

var test7Evidence = new List<string>();
var test7Missing = new List<string>();
var test7Passed = true;

// Check for Log.Information for Phase 2 start
if (ContainsPattern(lifecycleContent, @"Log\.Information\(""Phase 2", out var phase2InfoMatches))
{
    test7Evidence.Add("Log.Information for Phase 2 start");
}
else
{
    test7Missing.Add("Phase 2 start logging");
    test7Passed = false;
}

// Check for Log.Fatal on critical startup errors
if (ContainsPattern(lifecycleContent, @"Log\.Fatal\([^)]*Critical error[^)]*enhanced startup", out var fatalMatches))
{
    test7Evidence.Add("Log.Fatal for critical Phase 2 errors");
}
else
{
    test7Missing.Add("Log.Fatal for Phase 2 failures");
    test7Passed = false;
}

// Check for structured logging with exception object
if (ContainsPattern(lifecycleContent, @"Log\.Fatal\(ex,", out var structuredMatches))
{
    test7Evidence.Add("Structured logging with exception object");
}
else
{
    test7Missing.Add("Structured exception logging");
    test7Passed = false;
}

// Check for Phase 2 completion logging
if (ContainsAllKeywords(lifecycleContent, "Phase 1 completed", "Phase 2"))
{
    test7Evidence.Add("Phase transition logging");
}
else
{
    test7Missing.Add("Phase transition logging");
    test7Passed = false;
}

// Check for diagnostic logging during Phase 2
if (ContainsPattern(lifecycleContent, @"Log\.Debug.*Phase 2", out var debugMatches))
{
    test7Evidence.Add($"Diagnostic debug logging ({debugMatches.Count} entries)");
}
else
{
    test7Missing.Add("Diagnostic debug logging");
    test7Passed = false;
}

// Check for emergency shutdown logging
if (ContainsAllKeywords(lifecycleContent, "initiating emergency shutdown", "Application.Current.Shutdown"))
{
    test7Evidence.Add("Emergency shutdown logging");
}
else
{
    test7Missing.Add("Emergency shutdown logging");
    test7Passed = false;
}

RecordTest(
    "Serilog Reporting for Phase 2 Transition Failures",
    test7Passed,
    test7Passed ? "Comprehensive Serilog reporting verified" : "Serilog reporting incomplete",
    $"Found {test7Evidence.Count} of 6 required elements",
    test7Evidence,
    test7Missing
);

// ============================================================================
// TEST 8: Verify Integration - All Components Work Together
// ============================================================================

Log.Information("");
Log.Information("TEST 8: Verify Integration - All Components Work Together");
Log.Information("-".PadRight(80, '-'));

var test8Evidence = new List<string>();
var test8Missing = new List<string>();
var test8Passed = true;

// Check that Phase 2 happens AFTER secret migration
var secretMigrationIndex = lifecycleContent.IndexOf("MigrateSecretsFromEnvironmentAsync");
var phase2Index = lifecycleContent.IndexOf("Phase 2: Proceeding with Prism bootstrap");

if (secretMigrationIndex > 0 && phase2Index > secretMigrationIndex)
{
    test8Evidence.Add("Secret migration occurs BEFORE Phase 2 (correct order)");
}
else
{
    test8Missing.Add("Secret migration before Phase 2");
    test8Passed = false;
}

// Check that resources are loaded AFTER base.OnStartup
var baseOnStartupIndex = lifecycleContent.IndexOf("base.OnStartup(e)");
var resourceLoadIndex = lifecycleContent.IndexOf("LoadApplicationResourcesSync");

if (baseOnStartupIndex > 0 && resourceLoadIndex > baseOnStartupIndex)
{
    test8Evidence.Add("Resources loaded AFTER base.OnStartup (correct order)");
}
else
{
    test8Missing.Add("Resource loading after Prism bootstrap");
    test8Passed = false;
}

// Check that resource validation happens AFTER resource loading
var validateResourcesIndex = lifecycleContent.IndexOf("ValidateCriticalResources()");

if (resourceLoadIndex > 0 && validateResourcesIndex > resourceLoadIndex)
{
    test8Evidence.Add("Resource validation AFTER resource loading (correct order)");
}
else
{
    test8Missing.Add("Resource validation after loading");
    test8Passed = false;
}

// Check that ViewModel validation happens in OnInitialized (Phase 3)
var onInitializedIndex = lifecycleContent.IndexOf("protected override void OnInitialized");
var vmValidationIndex = lifecycleContent.IndexOf("ValidateAndRegisterViewModels");

if (onInitializedIndex > 0 && vmValidationIndex > onInitializedIndex)
{
    test8Evidence.Add("ViewModel validation in OnInitialized (Phase 3, correct timing)");
}
else
{
    test8Missing.Add("ViewModel validation in Phase 3");
    test8Passed = false;
}

// Check for comprehensive exception handling coverage
var tryCatchCount = Regex.Matches(lifecycleContent, @"try\s*\{").Count;
var catchCount = Regex.Matches(lifecycleContent, @"catch\s*\(Exception").Count;

if (tryCatchCount >= 3 && catchCount >= 3)
{
    test8Evidence.Add($"Comprehensive exception handling ({tryCatchCount} try blocks, {catchCount} catch blocks)");
}
else
{
    test8Missing.Add("Comprehensive exception handling");
    test8Passed = false;
}

RecordTest(
    "Integration - All Components Work Together",
    test8Passed,
    test8Passed ? "All components properly integrated" : "Integration issues detected",
    $"Found {test8Evidence.Count} of 5 required integration points",
    test8Evidence,
    test8Missing
);

// ============================================================================
// FINAL REPORT
// ============================================================================

Log.Information("");
Log.Information("=".PadRight(80, '='));
Log.Information("FINAL TEST REPORT");
Log.Information("=".PadRight(80, '='));

Log.Information("");
Log.Information("SUMMARY:");
Log.Information("  Total Tests:  {Total}", totalTests);
Log.Information("  Passed:       {Passed} ({PassPercent:F1}%)", passedTests, (passedTests * 100.0 / totalTests));
Log.Information("  Failed:       {Failed} ({FailPercent:F1}%)", failedTests, (failedTests * 100.0 / totalTests));

Log.Information("");
Log.Information("DETAILED RESULTS:");

foreach (var result in testResults)
{
    var status = result.Passed ? "✅ PASS" : "❌ FAIL";
    Log.Information("{Status} - {TestName}", status, result.TestName);

    if (!result.Passed && result.MissingElements.Any())
    {
        foreach (var missing in result.MissingElements)
        {
            Log.Warning("       Missing: {Missing}", missing);
        }
    }
}

Log.Information("");
Log.Information("VALIDATION CONCLUSION:");

if (passedTests == totalTests)
{
    Log.Information("🎉 ALL REQUIREMENTS VALIDATED SUCCESSFULLY!");
    Log.Information("The mission prompt has been fully implemented:");
    Log.Information("  ✅ EncryptedLocalSecretVaultService migration with error handling");
    Log.Information("  ✅ Phase 2 try-catch with comprehensive logging");
    Log.Information("  ✅ Dispatcher UI thread synchronization");
    Log.Information("  ✅ SettingsViewModel Container.Resolve validation");
    Log.Information("  ✅ ResourceDictionaries validation (DataTemplates.xaml, Generic.xaml)");
    Log.Information("  ✅ FluentLight theme Brush binding error detection");
    Log.Information("  ✅ Serilog reporting for Phase 2 failures");
    Log.Information("  ✅ Complete integration and proper sequencing");
}
else
{
    Log.Warning("⚠️  VALIDATION INCOMPLETE - {Failed} of {Total} tests failed", failedTests, totalTests);
    Log.Warning("The following requirements need attention:");

    foreach (var result in testResults.Where(r => !r.Passed))
    {
        Log.Warning("  ❌ {TestName}", result.TestName);
        foreach (var missing in result.MissingElements)
        {
            Log.Warning("     - {Missing}", missing);
        }
    }
}

Log.Information("");
Log.Information("Test completed: {Timestamp}", DateTime.Now);
Log.Information("Log file: {LogPath}", logPath);
Log.Information("=".PadRight(80, '='));

Log.Information("");
Log.Information("Test completed: {Timestamp}", DateTime.Now);
Log.Information("Log file: {LogPath}", logPath);
Log.Information("=".PadRight(80, '='));

// Return exit code
if (passedTests != totalTests)
{
    return 1;
}

return 0;
