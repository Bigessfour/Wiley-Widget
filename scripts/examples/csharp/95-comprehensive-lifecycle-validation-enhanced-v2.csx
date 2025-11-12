#!/usr/bin/env dotnet-script
#r "nuget: Serilog, 4.1.0"
#r "nuget: Serilog.Sinks.Console, 6.0.0"
#r "nuget: Serilog.Sinks.File, 6.0.0"

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;

/*
 * LIGHTWEIGHT CSX Test #95-v2: Comprehensive Lifecycle Validation (Memory-Optimized)
 *
 * Purpose: Validate lifecycle phases, DI registrations, and logging without heavy dependencies
 * Enhancements: Enhanced regex, Syncfusion detection, security flagging, performance tracking
 * Memory: Optimized for low-memory environments (no Roslyn, streaming file reads)
 */

// =======================
// Configuration
// =======================

var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
var logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");
var testLogPath = Path.Combine(logsDir, $"csx-test-95-v2-{DateTime.Now:yyyyMMdd-HHmmss}.log");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(testLogPath)
    .CreateLogger();

Log.Information("=".PadRight(80, '='));
Log.Information("CSX Test #95-v2: Lightweight Comprehensive Lifecycle Validation");
Log.Information("=".PadRight(80, '='));
Log.Information("Repository Root: {RepoRoot}", repoRoot);
Log.Information("Memory-Optimized Mode: Enabled");
Log.Information("");

// =======================
// Data Structures (Lightweight)
// =======================

var testResults = new Dictionary<string, (bool Passed, string Message, TimeSpan Duration, Dictionary<string, object> Metrics)>();
var lifecyclePhases = new List<(string Name, int Order, bool Critical, List<string> LogPoints, string Status)>();
var interfaceRegistry = new Dictionary<string, (string ImplName, string Lifetime, bool Registered, bool Resolvable, List<string> Deps, bool IsSf, bool IsSecurity)>();
var loggingGaps = new List<(string Location, string Type, string Priority, string Recommendation)>();
var stopwatch = Stopwatch.StartNew();

// =======================
// TEST 1: Parse Lifecycle Phases (Enhanced Regex)
// =======================

Log.Information("📋 TEST 1: Analyzing App.Lifecycle.cs Structure");
stopwatch.Restart();

var lifecycleFile = Path.Combine(repoRoot, "src", "WileyWidget", "App.Lifecycle.cs");
if (File.Exists(lifecycleFile))
{
    var content = File.ReadAllText(lifecycleFile);

    // Enhanced phase detection with better pattern matching
    var phasePatterns = new[]
    {
        (@"Phase 1[:\s]+(.+?)(?=Phase 2|\Z)", "Phase 1", true),
        (@"Phase 2[:\s]+(.+?)(?=Phase 3|\Z)", "Phase 2", true),
        (@"Phase 3[:\s]+(.+?)(?=Phase 4|\Z)", "Phase 3", true),
        (@"Phase 4[:\s]+(.+?)$", "Phase 4", false)
    };

    int phaseCount = 0;
    foreach (var (pattern, name, critical) in phasePatterns)
    {
        var match = Regex.Match(content, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var logPoints = Regex.Matches(match.Value, @"Log\.(Information|Debug|Warning|Error)\s*\(")
                .Select(m => m.Value).ToList();

            lifecyclePhases.Add((name, ++phaseCount, critical, logPoints, "Parsed"));
            Log.Information("  ✓ {Phase}: {Desc} ({LogCount} logs)", name, match.Groups[1].Value.Trim().Substring(0, Math.Min(50, match.Groups[1].Value.Trim().Length)), logPoints.Count);
        }
    }

    testResults["lifecycle_parsing"] = (true, $"Found {lifecyclePhases.Count} phases", stopwatch.Elapsed,
        new Dictionary<string, object> { ["PhaseCount"] = lifecyclePhases.Count, ["TotalLogPoints"] = lifecyclePhases.Sum(p => p.LogPoints.Count) });
}
else
{
    testResults["lifecycle_parsing"] = (false, "App.Lifecycle.cs not found", stopwatch.Elapsed, new Dictionary<string, object>());
    Log.Error("  ❌ File not found: {Path}", lifecycleFile);
}

Log.Information("");

// =======================
// TEST 2: Discover Interfaces (Memory-Efficient Streaming)
// =======================

Log.Information("🔍 TEST 2: Discovering Service Interfaces");
stopwatch.Restart();

var interfaceDirs = new[] {
    "src/WileyWidget.Services.Abstractions",
    "src/WileyWidget.Business/Interfaces",
    "src/WileyWidget.Abstractions",
    "src/WileyWidget/ViewModels" // Added for WPF
};

int discoveredCount = 0;
foreach (var dir in interfaceDirs.Select(d => Path.Combine(repoRoot, d)).Where(Directory.Exists))
{
    foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
    {
        var fileName = Path.GetFileNameWithoutExtension(file);
        if (fileName.StartsWith("I") && char.IsUpper(fileName[1]))
        {
            var isSyncfusion = fileName.Contains("Sf") || fileName.Contains("DataGrid") || fileName.Contains("Chart");
            var isSecurity = fileName.Contains("Secret") || fileName.Contains("Vault") || fileName.Contains("Telemetry") || fileName.Contains("Audit");

            interfaceRegistry[fileName] = ("", "Unknown", false, false, new List<string>(), isSyncfusion, isSecurity);
            discoveredCount++;

            if (isSyncfusion) Log.Debug("  → Syncfusion: {Interface}", fileName);
            if (isSecurity) Log.Debug("  → Security: {Interface}", fileName);
        }
    }
}

testResults["interface_discovery"] = (discoveredCount > 0, $"Discovered {discoveredCount} interfaces", stopwatch.Elapsed,
    new Dictionary<string, object> {
        ["Total"] = discoveredCount,
        ["Syncfusion"] = interfaceRegistry.Count(i => i.Value.IsSf),
        ["Security"] = interfaceRegistry.Count(i => i.Value.IsSecurity)
    });

Log.Information("  ✓ {Count} interfaces ({Sf} Syncfusion, {Sec} Security)", discoveredCount,
    interfaceRegistry.Count(i => i.Value.IsSf), interfaceRegistry.Count(i => i.Value.IsSecurity));
Log.Information("");

// =======================
// TEST 3: Parse DI Registrations (Enhanced Patterns)
// =======================

Log.Information("📝 TEST 3: Parsing DI Registrations");
stopwatch.Restart();

var diFile = Path.Combine(repoRoot, "src", "WileyWidget", "App.DependencyInjection.cs");
int registeredCount = 0;

if (File.Exists(diFile))
{
    var content = File.ReadAllText(diFile);

    // Enhanced registration patterns (including Prism, auto-registration, lambdas, generics)
    var patterns = new[]
    {
        // Standard Microsoft.Extensions.DependencyInjection
        (@"Add(Singleton|Scoped|Transient)<(I\w+)(?:,\s*(\w+))?>", 1, 2, 3),
        // Prism DryIoc patterns
        (@"containerRegistry\.Register(Singleton|Scoped)?<(I\w+),\s*(\w+)>", 1, 2, 3),
        (@"registry\.Register(Singleton|Scoped)?<(I\w+),\s*(\w+)>", 1, 2, 3),
        // RegisterInstance patterns
        (@"RegisterInstance<(I\w+)>", -1, 1, -1),
        // typeof patterns
        (@"services\.(AddSingleton|AddScoped|AddTransient)\(typeof\((I\w+)\),\s*typeof\((\w+)\)", 1, 2, 3),
        // Auto-registration inference (look for "✓ IInterface -> Implementation" in comments/logs)
        (@"//.*✓\s+(I\w+)\s*->\s*(\w+)", -1, 1, 2),
        (@"✓\s+(I\w+)\s*->\s*(\w+)", -1, 1, 2),
        // Convention-based logging patterns (added for RegisterMany detection)
        (@"✅\s+(I\w+)\s*->\s*(\w+)\s*\(Singleton\)", 1, 1, 2),
        (@"✅\s+(I\w+)\s*->\s*(\w+)\s*\(Scoped\)", 1, 1, 2),
        (@"✅\s+(\w+ViewModel)\s*->\s*(\w+)\s*\(Transient\)", 1, 1, 2),
        // Log-based registration detection
        (@"Log\.Information\(\s*""\s*✅\s+(I\w+)\s*->\s*(\w+)", -1, 1, 2)
    };

    foreach (var (pattern, lifetimeIdx, ifaceIdx, implIdx) in patterns)
    {
        var matches = Regex.Matches(content, pattern);
        foreach (Match m in matches)
        {
            var iface = m.Groups[ifaceIdx].Value;
            var impl = implIdx > 0 && m.Groups[implIdx].Success ? m.Groups[implIdx].Value : "";
            var lifetime = lifetimeIdx > 0 && m.Groups[lifetimeIdx].Success ? m.Groups[lifetimeIdx].Value : "Scoped";
            if (string.IsNullOrEmpty(lifetime)) lifetime = "Singleton"; // Default for Prism

            if (interfaceRegistry.ContainsKey(iface))
            {
                var existing = interfaceRegistry[iface];
                interfaceRegistry[iface] = (impl, lifetime, true, existing.Resolvable, existing.Deps, existing.IsSf, existing.IsSecurity);
                registeredCount++;
            }
        }
    }

    Log.Information("  ✓ {Count} explicit registrations found", registeredCount);
    testResults["di_registration"] = (registeredCount >= 4, $"{registeredCount} registrations", stopwatch.Elapsed,
        new Dictionary<string, object> { ["Explicit"] = registeredCount });
}
else
{
    testResults["di_registration"] = (false, "DI file not found", stopwatch.Elapsed, new Dictionary<string, object>());
}

Log.Information("");

// =======================
// TEST 4: Analyze Startup Logs (Streaming for Memory)
// =======================

Log.Information("📊 TEST 4: Analyzing Startup Logs");
stopwatch.Restart();

var latestLog = Directory.Exists(logsDir)
    ? Directory.EnumerateFiles(logsDir, "wiley-widget-*.log").OrderByDescending(File.GetLastWriteTime).FirstOrDefault()
    : null;

int logRegistrations = 0;
int phasesCompleted = 0;
var criticalErrors = new List<string>();

if (latestLog != null)
{
    foreach (var line in File.ReadLines(latestLog)) // Streaming read
    {
        if (line.Contains("✓") && Regex.IsMatch(line, @"I\w+\s*->\s*\w+"))
        {
            var match = Regex.Match(line, @"(I\w+)\s*->\s*(\w+)");
            if (match.Success)
            {
                var iface = match.Groups[1].Value;
                if (interfaceRegistry.ContainsKey(iface))
                {
                    var existing = interfaceRegistry[iface];
                    interfaceRegistry[iface] = (match.Groups[2].Value, existing.Lifetime, existing.Registered, true, existing.Deps, existing.IsSf, existing.IsSecurity);
                    logRegistrations++;
                }
            }
        }

        if (Regex.IsMatch(line, @"Phase \d+ (complete|finished)", RegexOptions.IgnoreCase))
            phasesCompleted++;

        if (line.Contains("[ERR]") && (line.Contains("cast") || line.Contains("resolve") || line.Contains("Failed")))
            criticalErrors.Add(line.Substring(line.IndexOf("[ERR]")));
    }

    Log.Information("  ✓ {LogRegs} log registrations, {Phases} phases completed", logRegistrations, phasesCompleted);
    if (criticalErrors.Any())
    {
        Log.Warning("  ⚠️ {Count} critical errors in log", criticalErrors.Count);
        foreach (var err in criticalErrors.Take(3))
            Log.Debug("    → {Error}", err.Substring(0, Math.Min(100, err.Length)));
    }

    testResults["log_analysis"] = (logRegistrations > 10 && phasesCompleted >= 1,
        $"{logRegistrations} registrations, {phasesCompleted} phases", stopwatch.Elapsed,
        new Dictionary<string, object> { ["Registrations"] = logRegistrations, ["Phases"] = phasesCompleted, ["Errors"] = criticalErrors.Count });
}
else
{
    testResults["log_analysis"] = (false, "No startup log found", stopwatch.Elapsed, new Dictionary<string, object>());
}

Log.Information("");

// =======================
// TEST 5: Identify Missing & Unresolvable
// =======================

Log.Information("🔴 TEST 5: Missing & Unresolvable Services");
stopwatch.Restart();

var missing = interfaceRegistry.Where(i => !i.Value.Registered).Select(i => i.Key).ToList();
var unresolvable = interfaceRegistry.Where(i => i.Value.Registered && !i.Value.Resolvable).Select(i => i.Key).ToList();
var securityGaps = interfaceRegistry.Where(i => i.Value.IsSecurity && !i.Value.Resolvable).Select(i => i.Key).ToList();

var registeredPct = (double)interfaceRegistry.Count(i => i.Value.Registered) / interfaceRegistry.Count * 100;
var resolvablePct = (double)interfaceRegistry.Count(i => i.Value.Resolvable) / interfaceRegistry.Count * 100;

Log.Information("  Missing: {Count} ({Pct:F1}%)", missing.Count, 100 - registeredPct);
Log.Information("  Unresolvable: {Count}", unresolvable.Count);
if (unresolvable.Any())
{
    Log.Debug("  Unresolvable services:");
    foreach (var svc in unresolvable.Take(10))
        Log.Debug("    → {Service}", svc);
    if (unresolvable.Count > 10)
        Log.Debug("    ... and {More} more", unresolvable.Count - 10);
}
if (securityGaps.Any())
    Log.Warning("  ⚠️ Security gaps: {Count} ({Services})", securityGaps.Count, string.Join(", ", securityGaps.Take(3)));

testResults["missing_services"] = (missing.Count < 10 && registeredPct >= 80,
    $"{missing.Count} missing, {unresolvable.Count} unresolvable", stopwatch.Elapsed,
    new Dictionary<string, object> {
        ["Missing"] = missing.Count,
        ["Unresolvable"] = unresolvable.Count,
        ["RegisteredPct"] = registeredPct,
        ["ResolvablePct"] = resolvablePct,
        ["SecurityGaps"] = securityGaps.Count
    });

Log.Information("");

// =======================
// TEST 6: Logging Gaps
// =======================

Log.Information("📉 TEST 6: Detecting Logging Gaps");
stopwatch.Restart();

var expectedLogs = new[] {
    ("RegisterTypes", "entry", "Medium"),
    ("RegisterTypes", "exit", "Medium"),
    ("Phase 4", "complete", "High")
};

int gapsFound = 0;
if (latestLog != null)
{
    var logContent = File.ReadAllText(latestLog);
    foreach (var (location, type, priority) in expectedLogs)
    {
        if (!logContent.Contains(location) || !logContent.Contains(type))
        {
            loggingGaps.Add((location, type, priority, $"Add log for {location} {type}"));
            gapsFound++;
            if (priority == "High")
                Log.Warning("  ⚠️ HIGH: Missing {Location} {Type}", location, type);
        }
    }
}

testResults["logging_gaps"] = (gapsFound == 0, $"{gapsFound} gaps found", stopwatch.Elapsed,
    new Dictionary<string, object> { ["Total"] = gapsFound, ["High"] = loggingGaps.Count(g => g.Priority == "High") });

Log.Information("  Found {Count} logging gaps ({High} high priority)", gapsFound, loggingGaps.Count(g => g.Priority == "High"));
Log.Information("");

// =======================
// TEST 7: Critical Services (Dynamic Detection)
// =======================

Log.Information("🔗 TEST 7: Critical Service Validation");
stopwatch.Restart();

var criticalServices = interfaceRegistry.Where(i =>
    i.Value.IsSecurity ||
    i.Key.Contains("Repository") ||
    i.Key.Contains("Service") && (i.Key.Contains("Settings") || i.Key.Contains("Audit"))
).Select(i => i.Key).ToList();

int validatedCritical = criticalServices.Count(s => interfaceRegistry[s].Resolvable);
var criticalPct = criticalServices.Any() ? (double)validatedCritical / criticalServices.Count * 100 : 100;

Log.Information("  ✓ {Validated}/{Total} critical services validated ({Pct:F1}%)",
    validatedCritical, criticalServices.Count, criticalPct);

testResults["critical_services"] = (criticalPct >= 80, $"{validatedCritical}/{criticalServices.Count} validated", stopwatch.Elapsed,
    new Dictionary<string, object> { ["Validated"] = validatedCritical, ["Total"] = criticalServices.Count, ["Percentage"] = criticalPct });

Log.Information("");

// =======================
// REPORT GENERATION (Memory-Efficient)
// =======================

Log.Information("=".PadRight(80, '='));
Log.Information("📊 COMPREHENSIVE REPORT");
Log.Information("=".PadRight(80, '='));
Log.Information("");

var passedTests = testResults.Count(t => t.Value.Passed);
var totalTests = testResults.Count;

Log.Information("Test Results: {Passed}/{Total} passed ({Pct:F1}%)", passedTests, totalTests, (double)passedTests / totalTests * 100);
Log.Information("");

// ASCII Bar Charts
Log.Information("Coverage Metrics:");
Log.Information("Registered:  {Bar} {Pct:F1}%", new string('█', (int)(registeredPct / 10)), registeredPct);
Log.Information("Resolvable:  {Bar} {Pct:F1}%", new string('█', (int)(resolvablePct / 10)), resolvablePct);
Log.Information("Critical:    {Bar} {Pct:F1}%", new string('█', (int)(criticalPct / 10)), criticalPct);
Log.Information("");

// Key Metrics
Log.Information("Key Metrics:");
foreach (var (test, result) in testResults)
{
    var icon = result.Passed ? "✅" : "❌";
    Log.Information("  {Icon} {Test}: {Message} ({Duration}ms)", icon, test, result.Message, result.Duration.TotalMilliseconds);
}
Log.Information("");

// Recommendations
var recommendations = new List<string>();
if (registeredPct < 80) recommendations.Add($"Register {missing.Count} missing interfaces");
if (unresolvable.Any()) recommendations.Add($"Fix {unresolvable.Count} unresolvable services");
if (securityGaps.Any()) recommendations.Add($"Resolve {securityGaps.Count} security-critical services");
if (gapsFound > 3) recommendations.Add($"Add {gapsFound} missing log points");

Log.Information("🎯 Top Recommendations:");
foreach (var rec in recommendations.Take(5))
    Log.Information("  → {Recommendation}", rec);
Log.Information("");

// JSON Report
var report = new
{
    Timestamp = DateTime.Now,
    Summary = new { PassedTests = passedTests, TotalTests = totalTests, PassRate = (double)passedTests / totalTests * 100 },
    Metrics = new { RegisteredPct = registeredPct, ResolvablePct = resolvablePct, CriticalPct = criticalPct },
    Interfaces = interfaceRegistry.Count,
    Missing = missing.Count,
    MissingList = missing,
    Unresolvable = unresolvable.Count,
    UnresolvableList = unresolvable,
    SecurityGaps = securityGaps.Count,
    SecurityGapsList = securityGaps,
    LoggingGaps = gapsFound,
    Recommendations = recommendations,
    TestDetails = testResults.ToDictionary(t => t.Key, t => new { t.Value.Passed, t.Value.Message, t.Value.Metrics })
};

var reportPath = Path.Combine(logsDir, $"lifecycle-validation-enhanced-{DateTime.Now:yyyyMMdd-HHmmss}.json");

// Handle NaN values that can't be serialized to JSON
try
{
    var sanitizedReport = SanitizeForJson(report);
    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
    File.WriteAllText(reportPath, JsonSerializer.Serialize(sanitizedReport, options));
}
catch (Exception ex)
{
    Console.WriteLine($"JSON serialization failed, saving report as text: {ex.Message}");
    // Fallback: save as formatted text instead of JSON
    var textReport = new StringBuilder();
    textReport.AppendLine("=== LIFECYCLE VALIDATION REPORT ===");
    textReport.AppendLine($"Timestamp: {DateTime.Now}");
    textReport.AppendLine($"Test Results: {passedTests}/{totalTests} passed ({(totalTests > 0 ? (passedTests * 100.0 / totalTests) : 0):F1}%)");
    textReport.AppendLine();
    textReport.AppendLine("Coverage Metrics:");
    textReport.AppendLine($"Registered: {(double.IsNaN(registeredPct) ? "N/A" : $"{registeredPct:F1}%")}");
    textReport.AppendLine($"Resolvable: {(double.IsNaN(resolvablePct) ? "N/A" : $"{resolvablePct:F1}%")}");
    textReport.AppendLine($"Critical: {(double.IsNaN(criticalPct) ? "N/A" : $"{criticalPct:F1}%")}");
    textReport.AppendLine();
    textReport.AppendLine("Key Metrics:");
    foreach (var test in testResults)
    {
        textReport.AppendLine($"  {(test.Value.Passed ? "✅" : "❌")} {test.Key}: {test.Value.Message} ({test.Value.Duration.TotalMilliseconds:F2}ms)");
    }
    // Always write text report to test-logs directory (which is writable)
    var testLogsDir = Environment.GetEnvironmentVariable("WW_TEST_LOGS_DIR") ?? Path.Combine(repoRoot, "test-logs");
    Directory.CreateDirectory(testLogsDir); // Ensure directory exists
    var textReportPath = Path.Combine(testLogsDir, Path.GetFileNameWithoutExtension(Path.GetFileName(reportPath)) + ".txt");
    File.WriteAllText(textReportPath, textReport.ToString());
    reportPath = textReportPath;
}
Log.Information("📄 Report: {Path}", reportPath);
Log.Information("");

var exitCode = passedTests >= (totalTests * 0.6) ? 0 : 1; // 60% pass threshold
Log.Information("=".PadRight(80, '='));
Log.Information("Exit Code: {Code} ({Status})", exitCode, exitCode == 0 ? "PASS" : "FAIL");
Log.Information("=".PadRight(80, '='));

Log.CloseAndFlush();
Environment.Exit(exitCode);

// Helper method to sanitize objects for JSON serialization
object? SanitizeForJson(object? obj)
{
    if (obj == null) return null;

    if (obj is double d && (double.IsNaN(d) || double.IsInfinity(d)))
        return null;

    if (obj is float f && (float.IsNaN(f) || float.IsInfinity(f)))
        return null;

    if (obj is Dictionary<string, object> dict)
    {
        var sanitized = new Dictionary<string, object>();
        foreach (var kvp in dict)
        {
            sanitized[kvp.Key] = SanitizeForJson(kvp.Value)!;
        }
        return sanitized;
    }

    if (obj is IEnumerable<KeyValuePair<string, object>> enumerable)
    {
        var sanitized = new Dictionary<string, object>();
        foreach (var kvp in enumerable)
        {
            sanitized[kvp.Key] = SanitizeForJson(kvp.Value)!;
        }
        return sanitized;
    }

    if (obj is IEnumerable<object> list)
    {
        return list.Select(SanitizeForJson).ToList();
    }

    return obj;
}
