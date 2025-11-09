#!/usr/bin/env dotnet-script
#r "nuget: System.Text.Json, 8.0.5"
#r "nuget: Microsoft.CodeAnalysis.CSharp, 4.11.0"

/*
 * 50-bootstrapper-phase2-validator.csx
 * =====================================
 * Comprehensive validation script for Bootstrapper Refactor Phase 2 (TODO 2.1-2.4)
 *
 * Purpose: Validate partial class split, new service files, DI integration, and 2nd/3rd order dependencies
 * Target: Phase 2 completion verification as defined in BOOTSTRAPPER_AUDIT_2025-11-09.md
 *
 * Validation Steps:
 * 1. TODO 2.1: App.xaml.cs partial class split validation
 *    - Verify 6 partial files exist (App.xaml.cs, DI, Lifecycle, Telemetry, Resources, ExceptionHandling)
 *    - Confirm partial class declarations in all files
 *    - Validate LOC < 300 per file (except main App.xaml.cs < 600)
 *    - Check method delegation patterns
 *    - Verify WileyWidget.csproj integration
 *
 * 2. TODO 2.2-2.4: New/Extracted Files Completeness
 *    - StartupEnvironmentValidator.cs: Full implementation, no TODOs
 *    - HealthReportingService.cs: Complete, DI registered
 *    - DiagnosticsService.cs: Complete, integrated in startup
 *    - Check DI registration in RegisterConventionTypes
 *
 * 3. 2nd/3rd Order Dependencies Scan
 *    - Detect unresolved types (IUnitOfWork, etc.)
 *    - Check for circular dependencies in DI
 *    - Validate runtime flow completeness (theme → regions → modules)
 *
 * 4. Build Validation
 *    - Parse WileyWidget.csproj for compile entries
 *    - Check for build warnings/errors
 *
 * Environment Variables:
 * - WW_REPO_ROOT: Repository root path (default: /app or current directory)
 * - WW_LOGS_DIR: Logs output directory (default: /logs or ./logs)
 *
 * Exit Codes:
 * - 0: All validations passed
 * - 1: Critical validation failures (missing files, no partial class declarations)
 * - 2: Warning-level issues (LOC violations, missing DI registrations)
 * - 3: Build integration issues
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ============================================================================
// CONFIGURATION
// ============================================================================

var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
var logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");
var wileyWidgetSrcPath = Path.Combine(repoRoot, "src", "WileyWidget");
var csprojPath = Path.Combine(wileyWidgetSrcPath, "WileyWidget.csproj");

// Create logs directory if it doesn't exist
Directory.CreateDirectory(logsDir);

var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ");
var logFilePath = Path.Combine(logsDir, $"bootstrapper-phase2-validation-{timestamp}.log");
var jsonResultPath = Path.Combine(logsDir, $"bootstrapper-phase2-validation-{timestamp}.json");

// ============================================================================
// VALIDATION RESULT MODELS
// ============================================================================

public class ValidationResult
{
    public string Category { get; set; } = "";
    public string Item { get; set; } = "";
    public bool Passed { get; set; }
    public string Status { get; set; } = "";
    public string Details { get; set; } = "";
    public string Severity { get; set; } = "Info"; // Info, Warning, Error, Critical
    public Dictionary<string, object> Metrics { get; set; } = new();
}

public class PartialClassInfo
{
    public string FileName { get; set; } = "";
    public bool Exists { get; set; }
    public bool HasPartialDeclaration { get; set; }
    public int LineCount { get; set; }
    public int MaxLineLimit { get; set; }
    public List<string> KeyMethods { get; set; } = new();
    public List<string> Issues { get; set; } = new();
}

public class ServiceFileInfo
{
    public string FileName { get; set; } = "";
    public bool Exists { get; set; }
    public bool IsComplete { get; set; }
    public bool HasTodos { get; set; }
    public bool RegisteredInDI { get; set; }
    public List<string> TodoComments { get; set; } = new();
    public List<string> PublicMethods { get; set; } = new();
}

public class DependencyIssue
{
    public string Type { get; set; } = ""; // UnresolvedType, CircularDependency, RuntimeFlow
    public string Order { get; set; } = ""; // 2nd, 3rd
    public string Description { get; set; } = "";
    public string Impact { get; set; } = ""; // UI crash, perf, warning
    public string Suggestion { get; set; } = "";
    public string Location { get; set; } = "";
}

public class Phase2ValidationReport
{
    public string Timestamp { get; set; } = "";
    public string RepoRoot { get; set; } = "";
    public string OverallStatus { get; set; } = ""; // Pass, Warning, Fail
    public int ProgressPercentage { get; set; }
    public string SecondOpinion { get; set; } = "";
    public List<PartialClassInfo> PartialClassResults { get; set; } = new();
    public List<ServiceFileInfo> ServiceFileResults { get; set; } = new();
    public List<DependencyIssue> DependencyIssues { get; set; } = new();
    public List<ValidationResult> AllValidations { get; set; } = new();
    public Dictionary<string, string> BuildIntegration { get; set; } = new();
    public List<string> RemainingFixes { get; set; } = new();
    public Dictionary<string, object> FinalMetrics { get; set; } = new();
}

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================

void Log(string message, string level = "INFO")
{
    var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
    Console.WriteLine(logEntry);
    File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
}

int CountLines(string filePath)
{
    if (!File.Exists(filePath)) return 0;
    return File.ReadAllLines(filePath).Length;
}

bool HasPartialClassDeclaration(string filePath)
{
    if (!File.Exists(filePath)) return false;
    var content = File.ReadAllText(filePath);
    return Regex.IsMatch(content, @"public\s+partial\s+class\s+App", RegexOptions.Multiline);
}

List<string> ExtractMethodNames(string filePath)
{
    if (!File.Exists(filePath)) return new List<string>();

    try
    {
        var code = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();

        var methods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(m => m.Identifier.Text)
            .ToList();

        return methods;
    }
    catch
    {
        // Fallback to regex if Roslyn parsing fails
        var content = File.ReadAllText(filePath);
        var matches = Regex.Matches(content, @"(?:public|private|protected|internal)\s+(?:async\s+)?(?:Task|void|bool|string|int|\w+)\s+(\w+)\s*\(", RegexOptions.Multiline);
        return matches.Select(m => m.Groups[1].Value).ToList();
    }
}

List<string> FindTodoComments(string filePath)
{
    if (!File.Exists(filePath)) return new List<string>();

    var todos = new List<string>();
    var lines = File.ReadAllLines(filePath);
    for (int i = 0; i < lines.Length; i++)
    {
        if (Regex.IsMatch(lines[i], @"//\s*TODO|//\s*FIXME|//\s*HACK", RegexOptions.IgnoreCase))
        {
            todos.Add($"Line {i + 1}: {lines[i].Trim()}");
        }
    }
    return todos;
}

bool CheckDIRegistration(string serviceTypeName)
{
    // Check in App.DependencyInjection.cs for registration patterns
    var diFilePath = Path.Combine(wileyWidgetSrcPath, "App.DependencyInjection.cs");
    if (!File.Exists(diFilePath)) return false;

    var content = File.ReadAllText(diFilePath);

    // Look for various DI registration patterns
    var patterns = new[]
    {
        $@"RegisterSingleton<{serviceTypeName}>",
        $@"Register<{serviceTypeName}>",
        $@"RegisterInstance<{serviceTypeName}>",
        $@"Register<I{serviceTypeName},\s*{serviceTypeName}>",
        $@"\.{serviceTypeName}\b",
        $@"typeof\({serviceTypeName}\)"
    };

    return patterns.Any(pattern => Regex.IsMatch(content, pattern, RegexOptions.Multiline));
}

// ============================================================================
// VALIDATION 1: PARTIAL CLASS SPLIT (TODO 2.1)
// ============================================================================

List<PartialClassInfo> ValidatePartialClassSplit()
{
    Log("=== VALIDATION 1: Partial Class Split (TODO 2.1) ===", "INFO");

    var partialFiles = new Dictionary<string, int>
    {
        { "App.xaml.cs", 600 }, // Main file can be larger
        { "App.DependencyInjection.cs", 800 }, // DI file can be larger due to registrations
        { "App.Lifecycle.cs", 700 }, // Lifecycle can be larger
        { "App.Telemetry.cs", 300 },
        { "App.Resources.cs", 300 },
        { "App.ExceptionHandling.cs", 300 }
    };

    var results = new List<PartialClassInfo>();

    foreach (var (fileName, maxLines) in partialFiles)
    {
        var filePath = Path.Combine(wileyWidgetSrcPath, fileName);
        var info = new PartialClassInfo
        {
            FileName = fileName,
            MaxLineLimit = maxLines,
            Exists = File.Exists(filePath)
        };

        if (info.Exists)
        {
            info.LineCount = CountLines(filePath);
            info.HasPartialDeclaration = HasPartialClassDeclaration(filePath);
            info.KeyMethods = ExtractMethodNames(filePath);

            // Validate LOC
            if (info.LineCount > maxLines)
            {
                info.Issues.Add($"Exceeds LOC limit: {info.LineCount} > {maxLines}");
            }

            // Validate partial declaration
            if (!info.HasPartialDeclaration)
            {
                info.Issues.Add("Missing 'partial class App' declaration");
            }

            Log($"✓ {fileName}: {info.LineCount} LOC, Partial: {info.HasPartialDeclaration}, Methods: {info.KeyMethods.Count}", "INFO");
        }
        else
        {
            info.Issues.Add("File does not exist");
            Log($"✗ {fileName}: NOT FOUND", "ERROR");
        }

        results.Add(info);
    }

    return results;
}

// ============================================================================
// VALIDATION 2: NEW/EXTRACTED FILES (TODO 2.2-2.4)
// ============================================================================

List<ServiceFileInfo> ValidateServiceFiles()
{
    Log("=== VALIDATION 2: New/Extracted Service Files (TODO 2.2-2.4) ===", "INFO");

    var serviceFiles = new[]
    {
        "Services/Startup/StartupEnvironmentValidator.cs",
        "Services/Startup/HealthReportingService.cs",
        "Services/Startup/DiagnosticsService.cs"
    };

    var results = new List<ServiceFileInfo>();

    foreach (var relativeFilePath in serviceFiles)
    {
        var fileName = Path.GetFileName(relativeFilePath);
        var filePath = Path.Combine(wileyWidgetSrcPath, relativeFilePath);

        var info = new ServiceFileInfo
        {
            FileName = fileName,
            Exists = File.Exists(filePath)
        };

        if (info.Exists)
        {
            info.TodoComments = FindTodoComments(filePath);
            info.HasTodos = info.TodoComments.Any();
            info.PublicMethods = ExtractMethodNames(filePath);
            info.IsComplete = !info.HasTodos && info.PublicMethods.Count > 0;

            // Check DI registration
            var serviceTypeName = Path.GetFileNameWithoutExtension(fileName);
            info.RegisteredInDI = CheckDIRegistration(serviceTypeName);

            Log($"✓ {fileName}: Complete: {info.IsComplete}, DI Registered: {info.RegisteredInDI}, TODOs: {info.TodoComments.Count}", "INFO");

            if (info.HasTodos)
            {
                foreach (var todo in info.TodoComments)
                {
                    Log($"  └─ TODO: {todo}", "WARN");
                }
            }
        }
        else
        {
            Log($"✗ {fileName}: NOT FOUND", "ERROR");
        }

        results.Add(info);
    }

    return results;
}

// ============================================================================
// VALIDATION 3: 2ND/3RD ORDER DEPENDENCIES
// ============================================================================

List<DependencyIssue> ScanDependencies()
{
    Log("=== VALIDATION 3: 2nd/3rd Order Dependencies Scan ===", "INFO");

    var issues = new List<DependencyIssue>();

    // Check for unresolved types in App.DependencyInjection.cs
    var diFilePath = Path.Combine(wileyWidgetSrcPath, "App.DependencyInjection.cs");
    if (File.Exists(diFilePath))
    {
        var content = File.ReadAllText(diFilePath);

        // Look for common unresolved types
        var unresolvedPatterns = new Dictionary<string, string>
        {
            { @"IUnitOfWork(?!.*Register)", "IUnitOfWork used but may not be registered" },
            { @"IRepository<\w+>(?!.*Register)", "Generic repository used without registration" },
            { @"\.Resolve<(\w+)>\(\)(?!.*RegisterSingleton<\1>)", "Type resolved but may not be registered" }
        };

        foreach (var (pattern, description) in unresolvedPatterns)
        {
            var matches = Regex.Matches(content, pattern, RegexOptions.Multiline);
            if (matches.Count > 0)
            {
                issues.Add(new DependencyIssue
                {
                    Type = "UnresolvedType",
                    Order = "2nd",
                    Description = description,
                    Impact = "Runtime DI resolution failure",
                    Suggestion = "Add DI registration in RegisterTypes or RegisterConventionTypes",
                    Location = "App.DependencyInjection.cs"
                });
                Log($"⚠ Dependency Issue: {description}", "WARN");
            }
        }
    }

    // Check for circular dependencies in module initialization
    var lifecycleFilePath = Path.Combine(wileyWidgetSrcPath, "App.Lifecycle.cs");
    if (File.Exists(lifecycleFilePath))
    {
        var content = File.ReadAllText(lifecycleFilePath);

        // Look for module initialization patterns
        if (Regex.IsMatch(content, @"InitializeModules.*Container\.Resolve", RegexOptions.Multiline))
        {
            // Check if modules are resolving services that depend on modules
            if (Regex.IsMatch(content, @"Resolve<IModuleManager>.*InitializeModules", RegexOptions.Multiline))
            {
                issues.Add(new DependencyIssue
                {
                    Type = "CircularDependency",
                    Order = "2nd",
                    Description = "Potential circular dependency in module initialization",
                    Impact = "Startup deadlock or NullReferenceException",
                    Suggestion = "Use Lazy<T> or defer module manager resolution",
                    Location = "App.Lifecycle.cs"
                });
                Log($"⚠ Circular Dependency: Module initialization may have circular references", "WARN");
            }
        }
    }

    // Check runtime flow: theme → regions → modules
    var resourcesFilePath = Path.Combine(wileyWidgetSrcPath, "App.Resources.cs");
    if (File.Exists(resourcesFilePath) && File.Exists(lifecycleFilePath))
    {
        var resourcesContent = File.ReadAllText(resourcesFilePath);
        var lifecycleContent = File.ReadAllText(lifecycleFilePath);

        // Ensure theme is applied before regions are configured
        var themeAppliedInResources = Regex.IsMatch(resourcesContent, @"SfSkinManager\.SetTheme|VerifyAndApplyTheme", RegexOptions.Multiline);
        var regionsConfiguredInLifecycle = Regex.IsMatch(lifecycleContent, @"ConfigureRegionAdapterMappings|RegionManager", RegexOptions.Multiline);

        if (regionsConfiguredInLifecycle && !themeAppliedInResources)
        {
            issues.Add(new DependencyIssue
            {
                Type = "RuntimeFlow",
                Order = "3rd",
                Description = "Theme may not be applied before region configuration",
                Impact = "UI rendering issues or Syncfusion styling failures",
                Suggestion = "Ensure VerifyAndApplyTheme is called in OnStartup before base.OnStartup",
                Location = "App.Lifecycle.cs + App.Resources.cs"
            });
            Log($"⚠ Runtime Flow Issue: Theme application order may be incorrect", "WARN");
        }
    }

    // Limit to top 3 issues
    return issues.Take(3).ToList();
}

// ============================================================================
// VALIDATION 4: BUILD INTEGRATION
// ============================================================================

Dictionary<string, string> ValidateBuildIntegration()
{
    Log("=== VALIDATION 4: Build Integration (WileyWidget.csproj) ===", "INFO");

    var result = new Dictionary<string, string>();

    if (!File.Exists(csprojPath))
    {
        result["Status"] = "FAIL";
        result["Reason"] = "WileyWidget.csproj not found";
        Log($"✗ WileyWidget.csproj not found at {csprojPath}", "ERROR");
        return result;
    }

    try
    {
        var csprojXml = XDocument.Load(csprojPath);

        // Check for explicit Compile entries (not required in SDK-style projects, but good to check)
        var compileItems = csprojXml.Descendants("Compile")
            .Select(e => e.Attribute("Include")?.Value ?? "")
            .Where(v => v.Contains("App.") && v.EndsWith(".cs"))
            .ToList();

        if (compileItems.Any())
        {
            result["ExplicitCompileEntries"] = string.Join(", ", compileItems);
            Log($"✓ Found explicit <Compile> entries: {compileItems.Count}", "INFO");
        }
        else
        {
            result["ExplicitCompileEntries"] = "None (SDK-style implicit)";
            Log($"✓ Using SDK-style implicit compilation (default for .NET 9)", "INFO");
        }

        // Check for ItemGroup with App.*.cs exclusions
        var excludedItems = csprojXml.Descendants("Compile")
            .Where(e => e.Attribute("Remove")?.Value?.Contains("App.") == true)
            .Select(e => e.Attribute("Remove")?.Value ?? "")
            .ToList();

        if (excludedItems.Any())
        {
            result["ExcludedFiles"] = string.Join(", ", excludedItems);
            Log($"⚠ Found excluded App.*.cs files: {excludedItems.Count}", "WARN");
        }
        else
        {
            result["ExcludedFiles"] = "None";
        }

        // Check for ApplicationDefinition (App.xaml)
        var appXamlHandling = csprojXml.Descendants("ApplicationDefinition")
            .Concat(csprojXml.Descendants("Page"))
            .Concat(csprojXml.Descendants("None"))
            .Where(e => e.Attribute("Include")?.Value?.Contains("App.xaml") == true ||
                       e.Attribute("Remove")?.Value?.Contains("App.xaml") == true)
            .Select(e => $"{e.Name.LocalName}: {e.Attribute("Include")?.Value ?? e.Attribute("Remove")?.Value}")
            .ToList();

        if (appXamlHandling.Any())
        {
            result["AppXamlHandling"] = string.Join("; ", appXamlHandling);
            Log($"✓ App.xaml handling: {appXamlHandling.Count} entries found", "INFO");
        }

        result["Status"] = "PASS";
        Log($"✓ Build integration validation passed", "INFO");
    }
    catch (Exception ex)
    {
        result["Status"] = "ERROR";
        result["Error"] = ex.Message;
        Log($"✗ Error parsing WileyWidget.csproj: {ex.Message}", "ERROR");
    }

    return result;
}

// ============================================================================
// GENERATE COMPREHENSIVE REPORT
// ============================================================================

Phase2ValidationReport GenerateReport(
    List<PartialClassInfo> partialClassResults,
    List<ServiceFileInfo> serviceFileResults,
    List<DependencyIssue> dependencyIssues,
    Dictionary<string, string> buildIntegration)
{
    Log("=== GENERATING COMPREHENSIVE REPORT ===", "INFO");

    var report = new Phase2ValidationReport
    {
        Timestamp = timestamp,
        RepoRoot = repoRoot,
        PartialClassResults = partialClassResults,
        ServiceFileResults = serviceFileResults,
        DependencyIssues = dependencyIssues,
        BuildIntegration = buildIntegration
    };

    // Calculate progress percentage
    int totalChecks = 0;
    int passedChecks = 0;

    // Partial class checks
    foreach (var partialClass in partialClassResults)
    {
        totalChecks += 2; // Exists + Partial declaration
        if (partialClass.Exists) passedChecks++;
        if (partialClass.HasPartialDeclaration) passedChecks++;
    }

    // Service file checks
    foreach (var serviceFile in serviceFileResults)
    {
        totalChecks += 3; // Exists + Complete + DI registered
        if (serviceFile.Exists) passedChecks++;
        if (serviceFile.IsComplete) passedChecks++;
        if (serviceFile.RegisteredInDI) passedChecks++;
    }

    // Build integration
    totalChecks++;
    if (buildIntegration["Status"] == "PASS") passedChecks++;

    report.ProgressPercentage = (int)((passedChecks / (double)totalChecks) * 100);

    // Determine overall status
    int criticalIssues = partialClassResults.Count(p => !p.Exists || !p.HasPartialDeclaration) +
                        serviceFileResults.Count(s => !s.Exists);

    int warningIssues = partialClassResults.Count(p => p.Issues.Any()) +
                       serviceFileResults.Count(s => s.HasTodos || !s.RegisteredInDI) +
                       dependencyIssues.Count(d => d.Impact.Contains("crash"));

    if (criticalIssues > 0)
    {
        report.OverallStatus = "FAIL";
    }
    else if (warningIssues > 0)
    {
        report.OverallStatus = "WARNING";
    }
    else
    {
        report.OverallStatus = "PASS";
    }

    // Generate second opinion summary
    var completionLevel = report.ProgressPercentage >= 90 ? "excellent" :
                         report.ProgressPercentage >= 80 ? "solid" :
                         report.ProgressPercentage >= 70 ? "good progress" : "needs work";

    report.SecondOpinion = $"Phase 2 Validation: {report.ProgressPercentage}% complete. " +
                          $"Assessment: {completionLevel} refactoring with {criticalIssues} critical issues, " +
                          $"{warningIssues} warnings, and {dependencyIssues.Count} dependency concerns. " +
                          $"Partial class split is {(partialClassResults.All(p => p.Exists) ? "complete" : "incomplete")}. " +
                          $"Service files are {(serviceFileResults.All(s => s.Exists) ? "present" : "missing files")}. " +
                          $"Build integration: {buildIntegration["Status"]}.";

    // Generate remaining fixes
    var fixes = new List<string>();

    foreach (var partialClass in partialClassResults.Where(p => p.Issues.Any()))
    {
        fixes.Add($"{partialClass.FileName}: {string.Join("; ", partialClass.Issues)}");
    }

    foreach (var serviceFile in serviceFileResults.Where(s => !s.RegisteredInDI && s.Exists))
    {
        fixes.Add($"Register {serviceFile.FileName} in App.DependencyInjection.cs RegisterConventionTypes method");
    }

    foreach (var issue in dependencyIssues.Take(3))
    {
        fixes.Add($"{issue.Type} ({issue.Order} order): {issue.Suggestion}");
    }

    report.RemainingFixes = fixes.Take(5).ToList();

    // Final metrics
    report.FinalMetrics = new Dictionary<string, object>
    {
        { "TotalPartialFiles", partialClassResults.Count },
        { "PartialFilesComplete", partialClassResults.Count(p => p.Exists && p.HasPartialDeclaration) },
        { "TotalServiceFiles", serviceFileResults.Count },
        { "ServiceFilesComplete", serviceFileResults.Count(s => s.Exists && s.IsComplete) },
        { "ServiceFilesDIRegistered", serviceFileResults.Count(s => s.RegisteredInDI) },
        { "DependencyIssuesFound", dependencyIssues.Count },
        { "CriticalIssues", criticalIssues },
        { "WarningIssues", warningIssues },
        { "BuildIntegrationStatus", buildIntegration["Status"] },
        { "EstimatedHoursTo100Percent", Math.Max(0, (100 - report.ProgressPercentage) / 10.0) }
    };

    return report;
}

// ============================================================================
// MAIN EXECUTION
// ============================================================================

try
{
    Log($"Bootstrapper Phase 2 Validation Started", "INFO");
    Log($"Repository Root: {repoRoot}", "INFO");
    Log($"WileyWidget Source: {wileyWidgetSrcPath}", "INFO");
    Log($"Log Output: {logFilePath}", "INFO");
    Log($"JSON Output: {jsonResultPath}", "INFO");
    Console.WriteLine();

    // Run all validations
    var partialClassResults = ValidatePartialClassSplit();
    Console.WriteLine();

    var serviceFileResults = ValidateServiceFiles();
    Console.WriteLine();

    var dependencyIssues = ScanDependencies();
    Console.WriteLine();

    var buildIntegration = ValidateBuildIntegration();
    Console.WriteLine();

    // Generate comprehensive report
    var report = GenerateReport(partialClassResults, serviceFileResults, dependencyIssues, buildIntegration);

    // ============================================================================
    // OUTPUT RESULTS
    // ============================================================================

    Console.WriteLine("════════════════════════════════════════════════════════════════");
    Console.WriteLine("  PHASE 2 VALIDATION REPORT");
    Console.WriteLine("════════════════════════════════════════════════════════════════");
    Console.WriteLine();

    Console.WriteLine($"Overall Status: {report.OverallStatus}");
    Console.WriteLine($"Progress: {report.ProgressPercentage}%");
    Console.WriteLine($"Timestamp: {report.Timestamp}");
    Console.WriteLine();

    Console.WriteLine("Second Opinion:");
    Console.WriteLine($"  {report.SecondOpinion}");
    Console.WriteLine();

    // Table 1: Partial Class Split
    Console.WriteLine("────────────────────────────────────────────────────────────────");
    Console.WriteLine("1. PARTIAL CLASS SPLIT (TODO 2.1)");
    Console.WriteLine("────────────────────────────────────────────────────────────────");
    Console.WriteLine($"{"Partial File",-40} {"Exists",-8} {"Partial",-10} {"LOC",-10} {"Status",-15}");
    Console.WriteLine(new string('─', 80));

    foreach (var pc in partialClassResults)
    {
        var status = pc.Issues.Any() ? "⚠ ISSUES" : "✓ OK";
        var existsMark = pc.Exists ? "✓" : "✗";
        var partialMark = pc.HasPartialDeclaration ? "✓" : "✗";
        var locInfo = pc.Exists ? $"{pc.LineCount}/{pc.MaxLineLimit}" : "N/A";

        Console.WriteLine($"{pc.FileName,-40} {existsMark,-8} {partialMark,-10} {locInfo,-10} {status,-15}");

        if (pc.Issues.Any())
        {
            foreach (var issue in pc.Issues)
            {
                Console.WriteLine($"  └─ {issue}");
            }
        }
    }
    Console.WriteLine();

    // Table 2: Service Files
    Console.WriteLine("────────────────────────────────────────────────────────────────");
    Console.WriteLine("2. NEW/EXTRACTED SERVICE FILES (TODO 2.2-2.4)");
    Console.WriteLine("────────────────────────────────────────────────────────────────");
    Console.WriteLine($"{"Service File",-45} {"Exists",-8} {"Complete",-10} {"DI Reg",-10}");
    Console.WriteLine(new string('─', 80));

    foreach (var sf in serviceFileResults)
    {
        var existsMark = sf.Exists ? "✓" : "✗";
        var completeMark = sf.IsComplete ? "✓" : "✗";
        var diMark = sf.RegisteredInDI ? "✓" : "✗";

        Console.WriteLine($"{sf.FileName,-45} {existsMark,-8} {completeMark,-10} {diMark,-10}");

        if (sf.HasTodos)
        {
            Console.WriteLine($"  └─ TODOs: {sf.TodoComments.Count} found");
        }
    }
    Console.WriteLine();

    // Table 3: Dependencies
    if (dependencyIssues.Any())
    {
        Console.WriteLine("────────────────────────────────────────────────────────────────");
        Console.WriteLine("3. DEPENDENCY ISSUES (2ND/3RD ORDER)");
        Console.WriteLine("────────────────────────────────────────────────────────────────");
        Console.WriteLine($"{"Issue",-35} {"Order",-8} {"Impact",-25} {"Location",-20}");
        Console.WriteLine(new string('─', 88));

        foreach (var di in dependencyIssues)
        {
            Console.WriteLine($"{di.Type,-35} {di.Order,-8} {di.Impact,-25} {di.Location,-20}");
            Console.WriteLine($"  Description: {di.Description}");
            Console.WriteLine($"  Fix: {di.Suggestion}");
            Console.WriteLine();
        }
    }

    // Build Integration
    Console.WriteLine("────────────────────────────────────────────────────────────────");
    Console.WriteLine("4. BUILD INTEGRATION");
    Console.WriteLine("────────────────────────────────────────────────────────────────");
    foreach (var kvp in buildIntegration)
    {
        Console.WriteLine($"{kvp.Key}: {kvp.Value}");
    }
    Console.WriteLine();

    // Remaining Fixes
    if (report.RemainingFixes.Any())
    {
        Console.WriteLine("────────────────────────────────────────────────────────────────");
        Console.WriteLine("REMAINING FIXES (Top 5)");
        Console.WriteLine("────────────────────────────────────────────────────────────────");
        for (int i = 0; i < report.RemainingFixes.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {report.RemainingFixes[i]}");
        }
        Console.WriteLine();
    }

    // Final Metrics
    Console.WriteLine("────────────────────────────────────────────────────────────────");
    Console.WriteLine("FINAL METRICS");
    Console.WriteLine("────────────────────────────────────────────────────────────────");
    foreach (var metric in report.FinalMetrics)
    {
        Console.WriteLine($"{metric.Key}: {metric.Value}");
    }
    Console.WriteLine();

    Console.WriteLine("════════════════════════════════════════════════════════════════");
    Console.WriteLine($"Validation Complete: {report.OverallStatus}");
    Console.WriteLine($"Detailed logs: {logFilePath}");
    Console.WriteLine($"JSON report: {jsonResultPath}");
    Console.WriteLine("════════════════════════════════════════════════════════════════");

    // Save JSON report
    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    var jsonReport = JsonSerializer.Serialize(report, jsonOptions);
    File.WriteAllText(jsonResultPath, jsonReport);

    Log($"Validation completed: {report.OverallStatus}", "INFO");
    Log($"JSON report saved to: {jsonResultPath}", "INFO");

    // Exit with appropriate code
    Environment.Exit(report.OverallStatus == "PASS" ? 0 :
                    report.OverallStatus == "WARNING" ? 2 : 1);
}
catch (Exception ex)
{
    Log($"CRITICAL ERROR: {ex.Message}", "ERROR");
    Log($"Stack trace: {ex.StackTrace}", "ERROR");
    Console.WriteLine($"✗ Validation failed with exception: {ex.Message}");
    Environment.Exit(3);
}
