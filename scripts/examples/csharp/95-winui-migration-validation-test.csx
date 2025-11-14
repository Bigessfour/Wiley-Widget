#!/usr/bin/env dotnet-script
// 95-winui-migration-validation-test.csx
// Validates WinUI 3 migration - Prism integration, Syncfusion licensing, and project structure
// Created: November 13, 2025
// 
// USAGE:
//   dotnet script 95-winui-migration-validation-test.csx
//   Can be run from any directory - automatically finds repository root

#r "nuget: Microsoft.Extensions.Configuration, 9.0.10"
#r "nuget: Microsoft.Extensions.Configuration.Json, 9.0.10"
#r "nuget: Serilog, 4.3.0"
#r "nuget: Serilog.Sinks.Console, 6.0.0"
#r "nuget: Serilog.Sinks.File, 7.0.0"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Serilog;

// ============================================================================
// Repository Root Discovery
// ============================================================================

string FindRepositoryRoot()
{
    // Try environment variable first
    var envRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT");
    if (!string.IsNullOrEmpty(envRoot) && Directory.Exists(envRoot))
    {
        var slnPath = Path.Combine(envRoot, "WileyWidget.sln");
        if (File.Exists(slnPath))
        {
            Console.WriteLine($"✓ Using WW_REPO_ROOT: {envRoot}");
            return envRoot;
        }
    }

    // Walk up from current directory looking for repository markers
    var current = Directory.GetCurrentDirectory();
    Console.WriteLine($"Searching for repository root from: {current}");
    
    while (current != null)
    {
        var slnPath = Path.Combine(current, "WileyWidget.sln");
        var gitPath = Path.Combine(current, ".git");
        
        if (File.Exists(slnPath) && Directory.Exists(gitPath))
        {
            Console.WriteLine($"✓ Found repository root: {current}");
            return current;
        }
        
        var parent = Directory.GetParent(current);
        if (parent == null) break;
        current = parent.FullName;
    }
    
    throw new DirectoryNotFoundException(
        "Could not find repository root (WileyWidget.sln + .git). " +
        "Please set WW_REPO_ROOT environment variable or run from within the repository.");
}

// ============================================================================
// Test Configuration
// ============================================================================

var repoRoot = FindRepositoryRoot();
var logsDir = Path.Combine(repoRoot, "logs");
var winuiProjectPath = Path.Combine(repoRoot, "src", "WileyWidget.WinUI", "WileyWidget.WinUI.csproj");
var winuiAppXamlPath = Path.Combine(repoRoot, "src", "WileyWidget.WinUI", "App.xaml");
var winuiAppCsPath = Path.Combine(repoRoot, "src", "WileyWidget.WinUI", "App.xaml.cs");
var winuiMainWindowXamlPath = Path.Combine(repoRoot, "src", "WileyWidget.WinUI", "MainWindow.xaml");
var winuiMainWindowCsPath = Path.Combine(repoRoot, "src", "WileyWidget.WinUI", "MainWindow.xaml.cs");
var cpmPropsPath = Path.Combine(repoRoot, "Directory.Packages.props");

// Test configuration flags
var skipBuildTests = Environment.GetEnvironmentVariable("SKIP_BUILD_TESTS") == "true";

// ============================================================================
// Logger Setup
// ============================================================================

Directory.CreateDirectory(logsDir);
var logPath = Path.Combine(logsDir, "95-winui-migration-validation-test.log");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

Console.WriteLine("\n" + new string('=', 80));
Console.WriteLine("WinUI 3 Migration Validation Test");
Console.WriteLine(new string('=', 80));
Console.WriteLine($"Repository Root: {repoRoot}");
Console.WriteLine($"Test Log: {logPath}");
Console.WriteLine($"Skip Build Tests: {skipBuildTests}");
Console.WriteLine(new string('=', 80) + "\n");

Log.Information("=== WinUI 3 Migration Validation Test ===");
Log.Information("Repository Root: {RepoRoot}", repoRoot);

// ============================================================================
// Test Results Tracking
// ============================================================================

var testResults = new List<(string Category, string TestName, bool Success, string Message)>();
var totalTests = 0;
var passedTests = 0;

void RecordTest(string category, string testName, bool success, string message = "")
{
    totalTests++;
    if (success) passedTests++;
    testResults.Add((category, testName, success, message));
    
    var status = success ? "✓ PASS" : "✗ FAIL";
    var color = success ? ConsoleColor.Green : ConsoleColor.Red;
    
    Console.ForegroundColor = color;
    Console.Write($"  {status}");
    Console.ResetColor();
    Console.WriteLine($" - {testName}");
    
    if (!string.IsNullOrEmpty(message))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"      → {message}");
        Console.ResetColor();
    }
    
    Log.Information("{Status} [{Category}] {TestName} - {Message}", 
        success ? "PASS" : "FAIL", category, testName, message);
}

// ============================================================================
// Category 1: Project Structure & CPM
// ============================================================================

Console.WriteLine("\n[Category 1] Project Structure & Central Package Management");
Log.Information("[Category 1] Project Structure & Central Package Management");

try
{
    // Test: Directory.Packages.props exists
    var cpmExists = File.Exists(cpmPropsPath);
    RecordTest("Structure", "Directory.Packages.props exists", cpmExists,
        cpmExists ? cpmPropsPath : $"File not found: {cpmPropsPath}");
    
    if (cpmExists)
    {
        var cpmXml = XDocument.Load(cpmPropsPath);
        var ns = cpmXml.Root?.Name.Namespace ?? XNamespace.None;
        
        // Test: CPM has Prism WinUI packages
        var prismWinUIPackages = cpmXml.Descendants(ns + "PackageVersion")
            .Where(e => e.Attribute("Include")?.Value?.Contains("Prism") == true &&
                       e.Attribute("Include")?.Value?.Contains("WinUI") == true)
            .Select(e => e.Attribute("Include")?.Value)
            .ToList();
        
        var expectedPrismPackages = new[] { "Prism.Uno.WinUI", "Prism.DryIoc.Uno.WinUI" };
        var hasPrismWinUI = expectedPrismPackages.All(p => prismWinUIPackages.Contains(p));
        
        RecordTest("CPM", "Prism WinUI packages defined", hasPrismWinUI,
            hasPrismWinUI 
                ? $"Found: {string.Join(", ", prismWinUIPackages)}"
                : $"Expected {string.Join(", ", expectedPrismPackages)}, found {prismWinUIPackages.Count} packages");
        
        // Test: CPM has Syncfusion WinUI packages
        var syncfusionWinUIPackages = cpmXml.Descendants(ns + "PackageVersion")
            .Where(e => e.Attribute("Include")?.Value?.Contains("Syncfusion") == true &&
                       e.Attribute("Include")?.Value?.Contains("WinUI") == true)
            .Select(e => e.Attribute("Include")?.Value)
            .ToList();
        
        var hasSyncfusionWinUI = syncfusionWinUIPackages.Count >= 5;
        RecordTest("CPM", "Syncfusion WinUI packages defined", hasSyncfusionWinUI,
            hasSyncfusionWinUI
                ? $"Found {syncfusionWinUIPackages.Count} packages"
                : $"Expected at least 5, found {syncfusionWinUIPackages.Count}");
    }
    
    // Test: WileyWidget.WinUI.csproj exists
    var projectExists = File.Exists(winuiProjectPath);
    RecordTest("Structure", "WileyWidget.WinUI.csproj exists", projectExists,
        projectExists ? winuiProjectPath : $"File not found: {winuiProjectPath}");
    
    if (projectExists)
    {
        var projectXml = XDocument.Load(winuiProjectPath);
        var ns = projectXml.Root?.Name.Namespace ?? XNamespace.None;
        
        // Test: CPM enabled in project
        var cpmEnabled = projectXml.Descendants(ns + "ManagePackageVersionsCentrally")
            .Any(e => e.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
        
        RecordTest("Project", "CPM enabled (ManagePackageVersionsCentrally=true)", cpmEnabled,
            cpmEnabled ? "Central Package Management is enabled" : "CPM property not found or not set to true");
        
        // Test: Target framework
        var targetFramework = projectXml.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value;
        var isWinUITarget = targetFramework?.Contains("windows10.0") ?? false;
        
        RecordTest("Project", "WinUI target framework", isWinUITarget,
            isWinUITarget ? $"Target: {targetFramework}" : $"Expected windows10.0.x, found: {targetFramework ?? "NOT FOUND"}");
        
        // Test: UseWinUI property
        var useWinUI = projectXml.Descendants(ns + "UseWinUI")
            .Any(e => e.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
        
        RecordTest("Project", "UseWinUI property set", useWinUI,
            useWinUI ? "UseWinUI=true" : "UseWinUI property missing or false");
    }
}
catch (Exception ex)
{
    RecordTest("Structure", "Project structure validation", false, $"Error: {ex.Message}");
    Log.Error(ex, "Error validating project structure");
}

// ============================================================================
// Category 2: Package References
// ============================================================================

Console.WriteLine("\n[Category 2] Package References");
Log.Information("[Category 2] Package References");

try
{
    if (File.Exists(winuiProjectPath))
    {
        var projectXml = XDocument.Load(winuiProjectPath);
        var ns = projectXml.Root?.Name.Namespace ?? XNamespace.None;
        
        var packageRefs = projectXml.Descendants(ns + "PackageReference")
            .Select(e => e.Attribute("Include")?.Value)
            .Where(p => p != null)
            .ToList();
        
        // Test: Prism packages
        var requiredPrismPackages = new[] {
            "Prism.Core",
            "Prism.Uno.WinUI",
            "Prism.DryIoc.Uno.WinUI",
            "Prism.Events"
        };
        
        foreach (var package in requiredPrismPackages)
        {
            var hasPackage = packageRefs.Contains(package);
            RecordTest("Packages", $"References {package}", hasPackage,
                hasPackage ? "Package referenced" : "Package reference missing");
        }
        
        // Test: Syncfusion packages (no separate theme packages for WinUI)
        var requiredSyncfusionPackages = new[] {
            "Syncfusion.Licensing",
            "Syncfusion.Core.WinUI"
        };
        
        foreach (var package in requiredSyncfusionPackages)
        {
            var hasPackage = packageRefs.Contains(package);
            RecordTest("Packages", $"References {package}", hasPackage,
                hasPackage ? "Package referenced" : "Package reference missing");
        }
        
        // Test: No version attributes (CPM compliance)
        var packageRefsWithVersion = projectXml.Descendants(ns + "PackageReference")
            .Where(e => e.Attribute("Version") != null)
            .Select(e => e.Attribute("Include")?.Value)
            .ToList();
        
        var cpmCompliant = packageRefsWithVersion.Count == 0;
        RecordTest("Packages", "All packages use CPM (no Version attributes)", cpmCompliant,
            cpmCompliant 
                ? "All package references use CPM"
                : $"Found {packageRefsWithVersion.Count} packages with Version attributes: {string.Join(", ", packageRefsWithVersion)}");
    }
}
catch (Exception ex)
{
    RecordTest("Packages", "Package reference validation", false, $"Error: {ex.Message}");
    Log.Error(ex, "Error validating package references");
}

// ============================================================================
// Category 3: XAML Files
// ============================================================================

Console.WriteLine("\n[Category 3] XAML Configuration");
Log.Information("[Category 3] XAML Configuration");

try
{
    // Test: App.xaml exists
    var appXamlExists = File.Exists(winuiAppXamlPath);
    RecordTest("XAML", "App.xaml exists", appXamlExists,
        appXamlExists ? winuiAppXamlPath : $"File not found: {winuiAppXamlPath}");
    
    if (appXamlExists)
    {
        var appXamlContent = File.ReadAllText(winuiAppXamlPath);
        
        // Test: Prism base class
        var hasPrismBase = appXamlContent.Contains("prism:PrismApplication") ||
                          appXamlContent.Contains("<prism:PrismApplication");
        RecordTest("XAML", "App.xaml uses PrismApplication base", hasPrismBase,
            hasPrismBase ? "Base class: prism:PrismApplication" : "PrismApplication base class not found");
        
        // Test: Prism namespace
        var hasPrismNamespace = appXamlContent.Contains("xmlns:prism=") &&
                               appXamlContent.Contains("Prism");
        RecordTest("XAML", "Prism namespace declared", hasPrismNamespace,
            hasPrismNamespace ? "Prism XML namespace found" : "Prism namespace declaration missing");
        
        // Test: Syncfusion Core (WinUI uses built-in Fluent Design)
        var hasSyncfusionCore = appXamlContent.Contains("Syncfusion") ||
                               appXamlContent.Contains("WinUI 3 uses built-in Fluent Design");
        RecordTest("XAML", "Syncfusion integration configured", hasSyncfusionCore,
            hasSyncfusionCore ? "Syncfusion configured for WinUI" : "Syncfusion configuration missing");
    }
    
    // Test: MainWindow.xaml exists
    var mainWindowExists = File.Exists(winuiMainWindowXamlPath);
    RecordTest("XAML", "MainWindow.xaml exists", mainWindowExists,
        mainWindowExists ? winuiMainWindowXamlPath : $"File not found: {winuiMainWindowXamlPath}");
    
    if (mainWindowExists)
    {
        var mainWindowContent = File.ReadAllText(winuiMainWindowXamlPath);
        
        // Test: Prism region
        var hasRegion = mainWindowContent.Contains("prism:RegionManager.RegionName") ||
                       mainWindowContent.Contains("RegionName=\"ContentRegion\"");
        RecordTest("XAML", "MainWindow has Prism region", hasRegion,
            hasRegion ? "Prism region configured" : "No Prism region found");
        
        // Test: Frame control
        var hasFrame = mainWindowContent.Contains("<Frame") &&
                      mainWindowContent.Contains("ContentFrame");
        RecordTest("XAML", "MainWindow has Frame for navigation", hasFrame,
            hasFrame ? "Frame control found" : "Frame control missing");
    }
}
catch (Exception ex)
{
    RecordTest("XAML", "XAML file validation", false, $"Error: {ex.Message}");
    Log.Error(ex, "Error validating XAML files");
}

// ============================================================================
// Category 4: C# Implementation
// ============================================================================

Console.WriteLine("\n[Category 4] C# Implementation");
Log.Information("[Category 4] C# Implementation");

try
{
    // Test: App.xaml.cs exists
    var appCsExists = File.Exists(winuiAppCsPath);
    RecordTest("C#", "App.xaml.cs exists", appCsExists,
        appCsExists ? winuiAppCsPath : $"File not found: {winuiAppCsPath}");
    
    if (appCsExists)
    {
        var appCsContent = File.ReadAllText(winuiAppCsPath);
        
        // Test: Prism base class inheritance
        var inheritsPrism = appCsContent.Contains(": PrismApplication");
        RecordTest("C#", "App inherits PrismApplication", inheritsPrism,
            inheritsPrism ? "Base class: PrismApplication" : "PrismApplication inheritance not found");
        
        // Test: Static constructor
        var hasStaticCtor = appCsContent.Contains("static App()");
        RecordTest("C#", "Static constructor for licensing", hasStaticCtor,
            hasStaticCtor ? "Static constructor found" : "Static constructor missing");
        
        // Test: Syncfusion license registration
        var hasSyncfusionLicense = appCsContent.Contains("SyncfusionLicenseProvider.RegisterLicense");
        RecordTest("C#", "Syncfusion license registration", hasSyncfusionLicense,
            hasSyncfusionLicense ? "License registration code found" : "License registration missing");
        
        // Test: Prism method overrides
        var prismOverrides = new[] {
            ("CreateShell", appCsContent.Contains("CreateShell()")),
            ("RegisterTypes", appCsContent.Contains("RegisterTypes(")),
            ("ConfigureModuleCatalog", appCsContent.Contains("ConfigureModuleCatalog(")),
            ("CreateContainerExtension", appCsContent.Contains("CreateContainerExtension()"))
        };
        
        foreach (var (method, found) in prismOverrides)
        {
            RecordTest("C#", $"Overrides {method}", found,
                found ? "Method override found" : "Method override missing");
        }
        
        // Test: Exception handling
        var hasExceptionHandling = appCsContent.Contains("UnhandledException") &&
                                   appCsContent.Contains("OnUnhandledException");
        RecordTest("C#", "Exception handling configured", hasExceptionHandling,
            hasExceptionHandling ? "Global exception handlers found" : "Exception handling missing");
        
        // Test: Serilog initialization
        var hasSerilog = appCsContent.Contains("Log.Logger = new LoggerConfiguration()");
        RecordTest("C#", "Serilog initialization", hasSerilog,
            hasSerilog ? "Serilog configured in static constructor" : "Serilog initialization missing");
    }
    
    // Test: MainWindow.xaml.cs exists
    var mainWindowCsExists = File.Exists(winuiMainWindowCsPath);
    RecordTest("C#", "MainWindow.xaml.cs exists", mainWindowCsExists,
        mainWindowCsExists ? winuiMainWindowCsPath : $"File not found: {winuiMainWindowCsPath}");
}
catch (Exception ex)
{
    RecordTest("C#", "C# implementation validation", false, $"Error: {ex.Message}");
    Log.Error(ex, "Error validating C# implementation");
}

// ============================================================================
// Category 5: Build Validation (Optional)
// ============================================================================

if (!skipBuildTests)
{
    Console.WriteLine("\n[Category 5] Build Validation");
    Log.Information("[Category 5] Build Validation");
    
    try
    {
        var restoreProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"restore \"{winuiProjectPath}\" --verbosity quiet",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        Log.Information("Running: dotnet restore (this may take a moment...)");
        Console.Write("  Restoring NuGet packages... ");
        
        restoreProcess.Start();
        restoreProcess.WaitForExit(TimeSpan.FromMinutes(2));
        
        var restoreSuccess = restoreProcess.ExitCode == 0;
        
        Console.WriteLine(restoreSuccess ? "✓" : "✗");
        
        RecordTest("Build", "NuGet package restore", restoreSuccess,
            restoreSuccess ? "Packages restored successfully" : $"Restore failed with exit code {restoreProcess.ExitCode}");
        
        if (!restoreSuccess)
        {
            var output = restoreProcess.StandardOutput.ReadToEnd();
            var error = restoreProcess.StandardError.ReadToEnd();
            Log.Error("Restore Output: {Output}", output);
            Log.Error("Restore Error: {Error}", error);
        }
    }
    catch (Exception ex)
    {
        RecordTest("Build", "Package restore", false, $"Error: {ex.Message}");
        Log.Error(ex, "Error running package restore");
    }
}
else
{
    Console.WriteLine("\n[Category 5] Build Validation - SKIPPED (set SKIP_BUILD_TESTS=false to enable)");
    Log.Information("[Category 5] Build Validation - SKIPPED");
}

// ============================================================================
// Final Summary
// ============================================================================

Console.WriteLine("\n" + new string('=', 80));
Console.WriteLine("TEST RESULTS SUMMARY");
Console.WriteLine(new string('=', 80));

// Group by category
var categoryGroups = testResults.GroupBy(t => t.Category).OrderBy(g => g.Key);

foreach (var group in categoryGroups)
{
    var categoryPassed = group.Count(t => t.Success);
    var categoryTotal = group.Count();
    var categoryRate = (double)categoryPassed / categoryTotal * 100;
    
    Console.WriteLine($"\n{group.Key}:");
    Console.WriteLine($"  {categoryPassed}/{categoryTotal} passed ({categoryRate:F0}%)");
    
    foreach (var (_, testName, success, message) in group.Where(t => !t.Success))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  ✗ ");
        Console.ResetColor();
        Console.WriteLine(testName);
        
        if (!string.IsNullOrEmpty(message))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"    ↳ {message}");
            Console.ResetColor();
        }
    }
}

Console.WriteLine("\n" + new string('-', 80));
Console.WriteLine($"Total Tests: {totalTests}");
Console.WriteLine($"Passed: {passedTests}");
Console.WriteLine($"Failed: {totalTests - passedTests}");

var successRate = totalTests > 0 ? (double)passedTests / totalTests * 100 : 0;
Console.WriteLine($"Success Rate: {successRate:F1}%");
Console.WriteLine(new string('=', 80));

Console.WriteLine();
if (successRate >= 80)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✓ MIGRATION VALIDATION PASSED - Ready for xUnit test creation");
    Console.ResetColor();
}
else if (successRate >= 50)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("⚠ MIGRATION PARTIALLY COMPLETE - Review failures above");
    Console.ResetColor();
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("✗ MIGRATION VALIDATION FAILED - Significant issues detected");
    Console.ResetColor();
}

Console.WriteLine($"\nDetailed log: {logPath}\n");

Log.Information("Test Summary - Total: {Total}, Passed: {Passed}, Failed: {Failed}, Rate: {Rate:F1}%",
    totalTests, passedTests, totalTests - passedTests, successRate);
Log.CloseAndFlush();

Environment.Exit(successRate >= 80 ? 0 : 1);
