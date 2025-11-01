// E2E test for Prism region adapter validation - validates custom region adapters are properly registered
// Based on Prism-Samples-Wpf region patterns: https://github.com/PrismLibrary/Prism-Samples-Wpf
// Validates SfDataGrid, DockingManager, and other custom region adapters
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

Console.WriteLine("=== PRISM REGION ADAPTERS E2E TEST ===");
Console.WriteLine("Validates custom region adapter registration and usage");
Console.WriteLine("Based on Prism-Samples-Wpf region patterns\n");

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
// Parse Region Adapter Data
// ========================
Console.WriteLine("Analyzing region adapter registration and usage...\n");

// Track custom region adapters
var expectedAdapters = new Dictionary<string, (bool Registered, int? RegisterLine, bool Used, int? UseLine)>
{
    ["SfDataGridRegionAdapter"] = (false, null, false, null),
    ["DockingManagerRegionAdapter"] = (false, null, false, null),
    ["SfNavigationDrawerRegionAdapter"] = (false, null, false, null),
    ["TabControlRegionAdapter"] = (false, null, false, null),
    ["ContentControlRegionAdapter"] = (false, null, false, null)
};

// Track region registrations
var regionRegistrations = new List<(string Region, string View, int Line, bool Success)>();
var regionCreations = new List<(string Region, string Adapter, int Line)>();
var regionErrors = new List<(string Region, string Error, int Line, string[] Context)>();

// Track default Prism behaviors
bool defaultBehaviorsRegistered = false;
int? defaultBehaviorsLine = null;

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
        }        // Track adapter registrations
        // Pattern: "Registered SfDataGridRegionAdapter" or "SfDataGridRegionAdapter registered"
        foreach (var adapter in expectedAdapters.Keys.ToList())
        {
            if (line.Contains(adapter, StringComparison.OrdinalIgnoreCase))
            {
                if (line.Contains("register", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("added", StringComparison.OrdinalIgnoreCase))
                {
                    var current = expectedAdapters[adapter];
                    if (!current.Registered)
                    {
                        expectedAdapters[adapter] = (true, lineNumber, current.Used, current.UseLine);
                    }
                }

                // Track usage
                if (line.Contains("using", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("adapt", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("created", StringComparison.OrdinalIgnoreCase))
                {
                    var current = expectedAdapters[adapter];
                    if (!current.Used)
                    {
                        expectedAdapters[adapter] = (current.Registered, current.RegisterLine, true, lineNumber);
                    }
                }
            }
        }

        // Track default Prism region behaviors
        if (line.Contains("default Prism region behaviors", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Registered default region behaviors", StringComparison.OrdinalIgnoreCase))
        {
            defaultBehaviorsRegistered = true;
            defaultBehaviorsLine = lineNumber;
        }

        // Track region registrations
        // Pattern: "RegisterViewWithRegion('RegionName', typeof(ViewName))"
        var regRegionMatch = Regex.Match(line, @"RegisterViewWithRegion.*?['""]([A-Za-z]+Region)['""].*?typeof\(([A-Za-z.]+)\)", RegexOptions.IgnoreCase);
        if (regRegionMatch.Success)
        {
            string region = regRegionMatch.Groups[1].Value;
            string view = regRegionMatch.Groups[2].Value;
            bool success = !line.Contains("failed", StringComparison.OrdinalIgnoreCase) &&
                          !line.Contains("error", StringComparison.OrdinalIgnoreCase);

            regionRegistrations.Add((region, view, lineNumber, success));
        }

        // Alternative pattern: "Registering view 'ViewName' to region 'RegionName'"
        var altRegionMatch = Regex.Match(line, @"Registering view ['""]([A-Za-z.]+)['""] to region ['""]([A-Za-z]+Region)['""]", RegexOptions.IgnoreCase);
        if (altRegionMatch.Success)
        {
            string view = altRegionMatch.Groups[1].Value;
            string region = altRegionMatch.Groups[2].Value;
            bool success = !line.Contains("failed", StringComparison.OrdinalIgnoreCase);

            regionRegistrations.Add((region, view, lineNumber, success));
        }

        // Track region creation
        // Pattern: "Region 'RegionName' created with adapter 'AdapterName'"
        var createMatch = Regex.Match(line, @"Region ['""]([A-Za-z]+Region)['""] created.*?(?:with|using) (?:adapter )?['""]?([A-Za-z]+RegionAdapter)", RegexOptions.IgnoreCase);
        if (createMatch.Success)
        {
            string region = createMatch.Groups[1].Value;
            string adapter = createMatch.Groups[2].Value;

            regionCreations.Add((region, adapter, lineNumber));
        }

        // Track region errors
        // Pattern: "ViewRegistrationException" or "Failed to add view to region"
        if (line.Contains("ViewRegistrationException", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Failed to add view to region", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Region.*not found", StringComparison.OrdinalIgnoreCase))
        {
            var regionMatch = Regex.Match(line, @"region ['""]([A-Za-z]+Region)['""]", RegexOptions.IgnoreCase);
            string region = regionMatch.Success ? regionMatch.Groups[1].Value : "Unknown";

            string error = line.Contains("not found") ? "Region not found" :
                          line.Contains("ViewRegistrationException") ? "View registration failed" :
                          "Unknown error";

            // Capture context
            var context = new List<string> { line };
            for (int i = 0; i < 5 && !reader.EndOfStream; i++)
            {
                var contextLine = reader.ReadLine();
                if (contextLine != null)
                {
                    context.Add(contextLine);
                    lineNumber++;
                }
            }

            regionErrors.Add((region, error, lineNumber, context.ToArray()));
        }
    }
}

Console.WriteLine($"Processed {lineNumber} lines\n");

// ========================
// Test 1: Custom Region Adapter Registration
// ========================
Console.WriteLine("TEST 1: Custom Region Adapter Registration");
Console.WriteLine("===========================================\n");

int registeredCount = expectedAdapters.Count(a => a.Value.Registered);
int totalExpected = expectedAdapters.Count;

Console.WriteLine($"Custom adapters registered: {registeredCount}/{totalExpected}\n");

foreach (var kvp in expectedAdapters.OrderBy(a => a.Key))
{
    string adapter = kvp.Key;
    var info = kvp.Value;

    string status = info.Registered ? "✓" : "✗";
    string regInfo = info.Registered ? $"(line {info.RegisterLine})" : "NOT REGISTERED";

    Console.WriteLine($"  {status} {adapter} {regInfo}");

    Assert(info.Registered, $"{adapter} is registered", regInfo);
}

Console.WriteLine();

// ========================
// Test 2: Default Prism Behaviors
// ========================
Console.WriteLine("TEST 2: Default Prism Region Behaviors");
Console.WriteLine("=======================================\n");

Assert(defaultBehaviorsRegistered, "Default Prism region behaviors are registered",
    defaultBehaviorsRegistered ? $"Registered at line {defaultBehaviorsLine}" : "NOT REGISTERED");

if (defaultBehaviorsRegistered)
{
    Console.WriteLine($"Default behaviors registered at line {defaultBehaviorsLine}");
}
else
{
    Console.WriteLine("⚠ Default behaviors not detected in logs");
    Console.WriteLine("  This may cause region-related functionality issues");
}

Console.WriteLine();

// ========================
// Test 3: Region Adapter Usage
// ========================
Console.WriteLine("TEST 3: Region Adapter Usage");
Console.WriteLine("=============================\n");

int usedCount = expectedAdapters.Count(a => a.Value.Used);

Console.WriteLine($"Custom adapters used: {usedCount}/{registeredCount} (of registered)\n");

foreach (var kvp in expectedAdapters.Where(a => a.Value.Registered).OrderBy(a => a.Key))
{
    string adapter = kvp.Key;
    var info = kvp.Value;

    if (info.Used)
    {
        Console.WriteLine($"  ✓ {adapter} - Used at line {info.UseLine}");
    }
    else
    {
        Console.WriteLine($"  ⚠ {adapter} - Registered but not used (may be unused or usage not logged)");
    }
}

// It's OK if not all adapters are used (they may be registered for future use)
Console.WriteLine("\nNote: Unused adapters are acceptable if they're registered for future features");

Console.WriteLine();

// ========================
// Test 4: Region View Registration
// ========================
Console.WriteLine("TEST 4: Region View Registration");
Console.WriteLine("=================================\n");

Console.WriteLine($"Total region view registrations: {regionRegistrations.Count}");

if (regionRegistrations.Count > 0)
{
    var successful = regionRegistrations.Where(r => r.Success).ToList();
    var failed = regionRegistrations.Where(r => !r.Success).ToList();

    Console.WriteLine($"  Successful: {successful.Count}");
    Console.WriteLine($"  Failed: {failed.Count}\n");

    // Group by region
    var byRegion = regionRegistrations.GroupBy(r => r.Region);

    Console.WriteLine("Registrations by region:");
    foreach (var group in byRegion.OrderBy(g => g.Key))
    {
        Console.WriteLine($"\n  {group.Key}: {group.Count()} view(s)");
        foreach (var reg in group.Take(5))
        {
            string status = reg.Success ? "✓" : "✗";
            Console.WriteLine($"    {status} {reg.View} (line {reg.Line})");
        }
        if (group.Count() > 5)
        {
            Console.WriteLine($"    ... and {group.Count() - 5} more");
        }
    }

    Assert(failed.Count == 0, "All region view registrations successful",
        failed.Count > 0 ? $"{failed.Count} failed registrations" : "");
}
else
{
    Console.WriteLine("⚠ No region view registrations detected in logs");
    Console.WriteLine("  This may indicate:");
    Console.WriteLine("  - Views are registered but not logged");
    Console.WriteLine("  - No views are using regions (unlikely)");
    Console.WriteLine("  - Log level is too high");
}

Console.WriteLine();

// ========================
// Test 5: Region Creation
// ========================
Console.WriteLine("TEST 5: Region Creation and Adapter Binding");
Console.WriteLine("============================================\n");

Console.WriteLine($"Regions created: {regionCreations.Count}\n");

if (regionCreations.Count > 0)
{
    var byAdapter = regionCreations.GroupBy(r => r.Adapter);

    Console.WriteLine("Regions by adapter:");
    foreach (var group in byAdapter.OrderBy(g => g.Key))
    {
        Console.WriteLine($"\n  {group.Key}: {group.Count()} region(s)");
        foreach (var creation in group.Take(5))
        {
            Console.WriteLine($"    ✓ {creation.Region} (line {creation.Line})");
        }
        if (group.Count() > 5)
        {
            Console.WriteLine($"    ... and {group.Count() - 5} more");
        }
    }

    // Verify custom adapters are being used
    bool customAdaptersUsed = regionCreations.Any(r =>
        r.Adapter != "ContentControlRegionAdapter" &&
        r.Adapter != "ItemsControlRegionAdapter" &&
        r.Adapter != "SelectorRegionAdapter");

    Assert(customAdaptersUsed, "Custom region adapters are being used",
        customAdaptersUsed ? "Custom adapters detected" : "Only default adapters used");
}

Console.WriteLine();

// ========================
// Test 6: Region Error Analysis
// ========================
Console.WriteLine("TEST 6: Region Error Analysis");
Console.WriteLine("==============================\n");

Assert(regionErrors.Count == 0, "No region-related errors",
    regionErrors.Count > 0 ? $"Found {regionErrors.Count} errors" : "");

if (regionErrors.Count > 0)
{
    Console.WriteLine($"\nTotal region errors: {regionErrors.Count}");

    var errorsByType = regionErrors.GroupBy(e => e.Error);

    foreach (var group in errorsByType)
    {
        Console.WriteLine($"\n{group.Key}: {group.Count()} occurrence(s)");
        Console.WriteLine(new string('-', 60));

        foreach (var error in group.Take(5))
        {
            Console.WriteLine($"  ✗ {error.Region}");
            Console.WriteLine($"    Line: {error.Line}");
            Console.WriteLine($"    Context:");
            foreach (var ctx in error.Context.Take(2))
            {
                Console.WriteLine($"      {ctx}");
            }
        }

        if (group.Count() > 5)
        {
            Console.WriteLine($"  ... and {group.Count() - 5} more");
        }
    }

    // Provide specific recommendations
    Console.WriteLine("\n⚠ Region Error Recommendations:");

    if (errorsByType.Any(g => g.Key == "Region not found"))
    {
        Console.WriteLine("\n  'Region not found' errors:");
        Console.WriteLine("    - Ensure region is defined in XAML with prism:RegionManager.RegionName");
        Console.WriteLine("    - Check that Shell/MainWindow has loaded before registering views");
        Console.WriteLine("    - Verify region name spelling matches exactly");
    }

    if (errorsByType.Any(g => g.Key == "View registration failed"))
    {
        Console.WriteLine("\n  'View registration failed' errors:");
        Console.WriteLine("    - Ensure ViewModel has parameterless constructor OR all dependencies registered");
        Console.WriteLine("    - Check that custom region adapter is registered before use");
        Console.WriteLine("    - Verify View and ViewModel types are resolvable");
    }
}

Console.WriteLine();

// ========================
// Test 7: Registration Timing
// ========================
Console.WriteLine("TEST 7: Registration Timing Analysis");
Console.WriteLine("=====================================\n");

// Check that adapters are registered before regions are used
if (regionCreations.Count > 0 && expectedAdapters.Any(a => a.Value.Registered))
{
    int firstRegisterLine = expectedAdapters.Where(a => a.Value.Registered)
        .Min(a => a.Value.RegisterLine ?? int.MaxValue);

    int firstCreationLine = regionCreations.Min(r => r.Line);

    bool correctTiming = firstRegisterLine < firstCreationLine;

    Assert(correctTiming, "Region adapters registered before regions are created",
        correctTiming
            ? $"Adapters at line {firstRegisterLine}, first region at {firstCreationLine}"
            : $"ERROR: First region at {firstCreationLine}, but adapter not registered until {firstRegisterLine}");

    if (!correctTiming)
    {
        Console.WriteLine("\n⚠ TIMING ISSUE:");
        Console.WriteLine("  Region adapters must be registered in ConfigureRegionAdapterMappings()");
        Console.WriteLine("  BEFORE any views are registered with regions in module OnInitialized()");
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
    Console.WriteLine("\n✓ ALL TESTS PASSED - Region adapters are configured correctly!");
    Environment.Exit(0);
}
else
{
    Console.WriteLine("\n✗ TESTS FAILED - Region adapter issues detected!");
    Console.WriteLine("\nPrism Region Adapter Best Practices:");
    Console.WriteLine("=====================================\n");

    Console.WriteLine("1. REGISTRATION LOCATION:");
    Console.WriteLine("   Override ConfigureRegionAdapterMappings() in App.xaml.cs:");
    Console.WriteLine("   protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings mappings)");
    Console.WriteLine("   {");
    Console.WriteLine("       base.ConfigureRegionAdapterMappings(mappings);");
    Console.WriteLine("       mappings.RegisterMapping(typeof(SfDataGrid), Container.Resolve<SfDataGridRegionAdapter>());");
    Console.WriteLine("   }");
    Console.WriteLine();

    Console.WriteLine("2. REGISTRATION TIMING:");
    Console.WriteLine("   - ConfigureRegionAdapterMappings() is called BEFORE module initialization");
    Console.WriteLine("   - Register ALL custom adapters before any module tries to use regions");
    Console.WriteLine("   - This ensures adapters are available when modules register views");
    Console.WriteLine();

    Console.WriteLine("3. REGION DEFINITION:");
    Console.WriteLine("   - Define regions in XAML: prism:RegionManager.RegionName=\"MyRegion\"");
    Console.WriteLine("   - Ensure Shell/MainWindow is loaded before registering views");
    Console.WriteLine("   - Use consistent naming: \"MyRegion\" not \"myRegion\"");
    Console.WriteLine();

    Console.WriteLine("4. VIEW REGISTRATION:");
    Console.WriteLine("   - Register views in module's OnInitialized() method:");
    Console.WriteLine("   regionManager.RegisterViewWithRegion(\"MyRegion\", typeof(MyView));");
    Console.WriteLine("   - Or use navigation: regionManager.RequestNavigate(\"MyRegion\", nameof(MyView));");
    Console.WriteLine();

    Console.WriteLine("5. CUSTOM ADAPTER IMPLEMENTATION:");
    Console.WriteLine("   - Inherit from RegionAdapterBase<TControl>");
    Console.WriteLine("   - Implement Adapt() and CreateRegion() methods");
    Console.WriteLine("   - Handle control-specific view activation/deactivation");
    Console.WriteLine("   - See Prism-Samples-Wpf/RegionAdaptersSample for examples");

    Environment.Exit(1);
}
