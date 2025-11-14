#!/usr/bin/env dotnet-script
// 96-winui-theme-validation-test.csx
// Deep validation of Syncfusion theming in WinUI 3
// Tests: Fluent Design integration, theme resource availability, control styling
// Created: November 14, 2025

#r "nuget: Serilog, 4.3.0"
#r "nuget: Serilog.Sinks.Console, 6.0.0"
#r "nuget: Serilog.Sinks.File, 7.0.0"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Serilog;

// ============================================================================
// Repository Root Discovery
// ============================================================================

string FindRepositoryRoot()
{
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
var winuiAppXamlPath = Path.Combine(repoRoot, "src", "WileyWidget.WinUI", "App.xaml");
var winuiAppCsPath = Path.Combine(repoRoot, "src", "WileyWidget.WinUI", "App.xaml.cs");
var winuiMainWindowXamlPath = Path.Combine(repoRoot, "src", "WileyWidget.WinUI", "MainWindow.xaml");
var cpmPropsPath = Path.Combine(repoRoot, "Directory.Packages.props");
var winuiProjectPath = Path.Combine(repoRoot, "src", "WileyWidget.WinUI", "WileyWidget.WinUI.csproj");

// ============================================================================
// Logger Setup
// ============================================================================

Directory.CreateDirectory(logsDir);
var logPath = Path.Combine(logsDir, "96-winui-theme-validation-test.log");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

Console.WriteLine("\n" + new string('=', 80));
Console.WriteLine("WinUI 3 Syncfusion Theme Validation Test");
Console.WriteLine(new string('=', 80));
Console.WriteLine($"Repository Root: {repoRoot}");
Console.WriteLine($"Test Log: {logPath}");
Console.WriteLine(new string('=', 80) + "\n");

Log.Information("=== WinUI 3 Syncfusion Theme Validation Test ===");
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
// Category 1: Theme Package Architecture
// ============================================================================

Console.WriteLine("\n[Category 1] Theme Package Architecture");
Log.Information("[Category 1] Theme Package Architecture");

try
{
    // Test: Syncfusion.Core.WinUI contains theming
    var cpmXml = XDocument.Load(cpmPropsPath);
    var ns = cpmXml.Root?.Name.Namespace ?? XNamespace.None;
    
    var syncfusionCoreVersion = cpmXml.Descendants(ns + "PackageVersion")
        .FirstOrDefault(e => e.Attribute("Include")?.Value == "Syncfusion.Core.WinUI")
        ?.Attribute("Version")?.Value;
    
    var hasCorePackage = !string.IsNullOrEmpty(syncfusionCoreVersion);
    RecordTest("Architecture", "Syncfusion.Core.WinUI package defined", hasCorePackage,
        hasCorePackage ? $"Version: {syncfusionCoreVersion}" : "Core package missing");
    
    // Test: No separate theme packages (WinUI architecture difference)
    var fluentLightPackage = cpmXml.Descendants(ns + "PackageVersion")
        .Any(e => e.Attribute("Include")?.Value?.Contains("Syncfusion.Themes.FluentLight.WinUI") == true);
    
    var fluentDarkPackage = cpmXml.Descendants(ns + "PackageVersion")
        .Any(e => e.Attribute("Include")?.Value?.Contains("Syncfusion.Themes.FluentDark.WinUI") == true);
    
    var correctArchitecture = !fluentLightPackage && !fluentDarkPackage;
    RecordTest("Architecture", "No separate theme packages (correct WinUI architecture)", correctArchitecture,
        correctArchitecture 
            ? "WinUI uses built-in Fluent Design + Syncfusion.Core.WinUI"
            : "ERROR: Found WPF-style theme packages that don't exist for WinUI");
    
    // Test: Syncfusion version consistency
    var allSyncfusionPackages = cpmXml.Descendants(ns + "PackageVersion")
        .Where(e => e.Attribute("Include")?.Value?.StartsWith("Syncfusion.") == true &&
                   e.Attribute("Include")?.Value?.Contains("WinUI") == true)
        .Select(e => new { 
            Name = e.Attribute("Include")?.Value,
            Version = e.Attribute("Version")?.Value
        })
        .ToList();
    
    var versionGroups = allSyncfusionPackages.GroupBy(p => p.Version).ToList();
    var consistentVersions = versionGroups.Count == 1;
    
    RecordTest("Architecture", "All Syncfusion WinUI packages use same version", consistentVersions,
        consistentVersions 
            ? $"All packages use version {versionGroups[0].Key}"
            : $"Version mismatch: {string.Join(", ", versionGroups.Select(g => $"{g.Key} ({g.Count()} packages)"))}");
    
}
catch (Exception ex)
{
    RecordTest("Architecture", "Theme package architecture validation", false, $"Error: {ex.Message}");
    Log.Error(ex, "Error validating theme architecture");
}

// ============================================================================
// Category 2: App.xaml Theme Configuration
// ============================================================================

Console.WriteLine("\n[Category 2] App.xaml Theme Configuration");
Log.Information("[Category 2] App.xaml Theme Configuration");

try
{
    if (File.Exists(winuiAppXamlPath))
    {
        var appXamlContent = File.ReadAllText(winuiAppXamlPath);
        
        // Test: No WPF-style theme resource dictionaries
        var hasWpfStyleThemes = appXamlContent.Contains("Syncfusion.Themes.Fluent") &&
                               appXamlContent.Contains("ms-appx:");
        
        RecordTest("App.xaml", "No WPF-style theme resource dictionaries", !hasWpfStyleThemes,
            !hasWpfStyleThemes 
                ? "Correctly configured for WinUI architecture"
                : "WARNING: Found WPF-style theme references that won't work in WinUI");
        
        // Test: Documents WinUI theming approach
        var documentsFluentDesign = appXamlContent.Contains("WinUI 3 uses built-in Fluent Design") ||
                                   appXamlContent.Contains("built-in Fluent Design System");
        
        RecordTest("App.xaml", "Documents WinUI Fluent Design approach", documentsFluentDesign,
            documentsFluentDesign 
                ? "Theme architecture clearly documented"
                : "Consider adding comment about WinUI's built-in Fluent Design");
        
        // Test: Has ResourceDictionary structure
        var hasResourceDictionary = appXamlContent.Contains("<ResourceDictionary>") &&
                                   appXamlContent.Contains("</ResourceDictionary>");
        
        RecordTest("App.xaml", "ResourceDictionary structure present", hasResourceDictionary,
            hasResourceDictionary ? "Resource structure exists" : "Missing ResourceDictionary");
        
        // Test: Defines application-level resources
        var hasAppResources = appXamlContent.Contains("SolidColorBrush") ||
                             appXamlContent.Contains("x:Key=");
        
        RecordTest("App.xaml", "Application-level resources defined", hasAppResources,
            hasAppResources 
                ? "App-level brushes/resources found"
                : "Consider adding app-level color resources");
    }
    else
    {
        RecordTest("App.xaml", "App.xaml exists", false, $"File not found: {winuiAppXamlPath}");
    }
}
catch (Exception ex)
{
    RecordTest("App.xaml", "App.xaml theme configuration", false, $"Error: {ex.Message}");
    Log.Error(ex, "Error validating App.xaml theme configuration");
}

// ============================================================================
// Category 3: Code-Behind Theme Initialization
// ============================================================================

Console.WriteLine("\n[Category 3] Code-Behind Theme Initialization");
Log.Information("[Category 3] Code-Behind Theme Initialization");

try
{
    if (File.Exists(winuiAppCsPath))
    {
        var appCsContent = File.ReadAllText(winuiAppCsPath);
        
        // Test: No WPF SfSkinManager usage
        var hasSfSkinManager = appCsContent.Contains("SfSkinManager") ||
                              appCsContent.Contains("SkinManager");
        
        RecordTest("App.xaml.cs", "No WPF SfSkinManager usage", !hasSfSkinManager,
            !hasSfSkinManager 
                ? "Correct: WinUI doesn't use SfSkinManager"
                : "WARNING: Found SfSkinManager (WPF-only, remove for WinUI)");
        
        // Test: No SetTheme calls
        var hasSetTheme = appCsContent.Contains("SetTheme") ||
                         appCsContent.Contains("SfSkinManager.SetTheme");
        
        RecordTest("App.xaml.cs", "No SetTheme calls (WPF-specific)", !hasSetTheme,
            !hasSetTheme 
                ? "Correct: WinUI handles theming automatically"
                : "WARNING: Found SetTheme calls (WPF-only API)");
        
        // Test: Syncfusion license registration present
        var hasLicenseRegistration = appCsContent.Contains("SyncfusionLicenseProvider.RegisterLicense");
        
        RecordTest("App.xaml.cs", "Syncfusion license registration present", hasLicenseRegistration,
            hasLicenseRegistration 
                ? "License registration found in static constructor"
                : "Missing license registration");
        
        // Test: Static constructor exists
        var hasStaticCtor = appCsContent.Contains("static App()");
        
        RecordTest("App.xaml.cs", "Static constructor for initialization", hasStaticCtor,
            hasStaticCtor 
                ? "Static constructor present"
                : "Missing static constructor for license/logging setup");
    }
    else
    {
        RecordTest("App.xaml.cs", "App.xaml.cs exists", false, $"File not found: {winuiAppCsPath}");
    }
}
catch (Exception ex)
{
    RecordTest("App.xaml.cs", "Code-behind theme initialization", false, $"Error: {ex.Message}");
    Log.Error(ex, "Error validating App.xaml.cs theme initialization");
}

// ============================================================================
// Category 4: Control Theming Requirements
// ============================================================================

Console.WriteLine("\n[Category 4] Control Theming Requirements");
Log.Information("[Category 4] Control Theming Requirements");

try
{
    var projectXml = XDocument.Load(winuiProjectPath);
    var ns = projectXml.Root?.Name.Namespace ?? XNamespace.None;
    
    var packageRefs = projectXml.Descendants(ns + "PackageReference")
        .Select(e => e.Attribute("Include")?.Value)
        .Where(p => p != null)
        .ToList();
    
    // Test: Core package provides theming foundation
    var hasSyncfusionCore = packageRefs.Contains("Syncfusion.Core.WinUI");
    RecordTest("Controls", "Syncfusion.Core.WinUI referenced (theme foundation)", hasSyncfusionCore,
        hasSyncfusionCore 
            ? "Core package provides WinUI theming support"
            : "Missing Syncfusion.Core.WinUI package");
    
    // Test: Control packages present
    var controlPackages = new[] {
        "Syncfusion.Grid.WinUI",
        "Syncfusion.Chart.WinUI",
        "Syncfusion.Gauge.WinUI",
        "Syncfusion.Editors.WinUI"
    };
    
    var controlPackagesPresent = controlPackages.Where(p => packageRefs.Contains(p)).ToList();
    var hasControlPackages = controlPackagesPresent.Count >= 2;
    
    RecordTest("Controls", "Syncfusion control packages referenced", hasControlPackages,
        hasControlPackages 
            ? $"Found {controlPackagesPresent.Count} control packages: {string.Join(", ", controlPackagesPresent.Select(p => p.Replace("Syncfusion.", "").Replace(".WinUI", "")))}"
            : "Missing Syncfusion control packages");
    
    // Test: Licensing package present
    var hasLicensing = packageRefs.Contains("Syncfusion.Licensing");
    RecordTest("Controls", "Syncfusion.Licensing package referenced", hasLicensing,
        hasLicensing ? "Licensing package present" : "Missing Syncfusion.Licensing");
    
}
catch (Exception ex)
{
    RecordTest("Controls", "Control theming requirements", false, $"Error: {ex.Message}");
    Log.Error(ex, "Error validating control theming requirements");
}

// ============================================================================
// Category 5: WinUI vs WPF Theme Differences
// ============================================================================

Console.WriteLine("\n[Category 5] WinUI vs WPF Theme Architecture Differences");
Log.Information("[Category 5] WinUI vs WPF Theme Architecture Differences");

try
{
    // Read all relevant files
    var appXamlContent = File.Exists(winuiAppXamlPath) ? File.ReadAllText(winuiAppXamlPath) : "";
    var appCsContent = File.Exists(winuiAppCsPath) ? File.ReadAllText(winuiAppCsPath) : "";
    
    // Test: No pack:// URIs (WPF-specific)
    var hasPackUris = appXamlContent.Contains("pack://application") ||
                     appCsContent.Contains("pack://application");
    
    RecordTest("Architecture", "No WPF pack:// URIs", !hasPackUris,
        !hasPackUris 
            ? "Correct: WinUI uses ms-appx:// scheme"
            : "WARNING: Found WPF pack:// URIs (not supported in WinUI)");
    
    // Test: Uses ms-appx:// for resources (if any)
    var usesCorrectScheme = !appXamlContent.Contains("Source=\"/") ||
                           appXamlContent.Contains("ms-appx://");
    
    RecordTest("Architecture", "Uses ms-appx:// URI scheme (WinUI)", usesCorrectScheme,
        usesCorrectScheme 
            ? "Correct URI scheme for WinUI resources"
            : "Found relative paths that may need ms-appx:// scheme");
    
    // Test: No MahApps.Metro (WPF-only)
    var hasMahApps = appXamlContent.Contains("MahApps") ||
                    appCsContent.Contains("MahApps");
    
    RecordTest("Architecture", "No MahApps.Metro dependencies", !hasMahApps,
        !hasMahApps 
            ? "No WPF-specific UI libraries detected"
            : "WARNING: Found MahApps.Metro references (WPF-only library)");
    
    // Test: No MaterialDesignThemes (WPF-only)
    var hasMaterialDesign = appXamlContent.Contains("MaterialDesign") ||
                           appCsContent.Contains("MaterialDesign");
    
    RecordTest("Architecture", "No MaterialDesignThemes dependencies", !hasMaterialDesign,
        !hasMaterialDesign 
            ? "No WPF-specific theme libraries detected"
            : "WARNING: Found MaterialDesignThemes references (WPF-only library)");
    
    // Test: Uses Microsoft.UI.Xaml namespace (WinUI)
    var usesMicrosoftUI = appXamlContent.Contains("Microsoft.UI.Xaml") ||
                         appXamlContent.Contains("http://schemas.microsoft.com/winfx/2006/xaml/presentation");
    
    RecordTest("Architecture", "Uses WinUI XML namespaces", usesMicrosoftUI,
        usesMicrosoftUI 
            ? "Correct WinUI namespaces detected"
            : "Missing Microsoft.UI.Xaml namespace references");
}
catch (Exception ex)
{
    RecordTest("Architecture", "WinUI vs WPF architecture validation", false, $"Error: {ex.Message}");
    Log.Error(ex, "Error validating WinUI architecture");
}

// ============================================================================
// Category 6: Theme Resource Best Practices
// ============================================================================

Console.WriteLine("\n[Category 6] Theme Resource Best Practices");
Log.Information("[Category 6] Theme Resource Best Practices");

try
{
    if (File.Exists(winuiAppXamlPath))
    {
        var appXamlContent = File.ReadAllText(winuiAppXamlPath);
        
        // Test: Defines custom color resources
        var hasColorResources = appXamlContent.Contains("SolidColorBrush") &&
                               appXamlContent.Contains("Color=");
        
        RecordTest("Resources", "Defines custom color brushes", hasColorResources,
            hasColorResources 
                ? "Application-level color resources defined"
                : "Consider defining app-specific color brushes");
        
        // Test: Uses x:Key for resource identification
        var usesKeys = appXamlContent.Contains("x:Key=");
        
        RecordTest("Resources", "Resources use x:Key identification", usesKeys,
            usesKeys 
                ? "Resources properly keyed"
                : "No keyed resources found");
        
        // Test: Has TODO for additional resource dictionaries
        var hasTodoForResources = appXamlContent.Contains("TODO") &&
                                 (appXamlContent.Contains("Generic.xaml") ||
                                  appXamlContent.Contains("DataTemplates") ||
                                  appXamlContent.Contains("Strings"));
        
        RecordTest("Resources", "Plans for additional resource dictionaries", hasTodoForResources,
            hasTodoForResources 
                ? "Resource dictionary migration planned"
                : "Consider adding TODO comments for resource dictionary migration");
    }
}
catch (Exception ex)
{
    RecordTest("Resources", "Theme resource best practices", false, $"Error: {ex.Message}");
    Log.Error(ex, "Error validating theme resource best practices");
}

// ============================================================================
// Final Summary
// ============================================================================

Console.WriteLine("\n" + new string('=', 80));
Console.WriteLine("THEME VALIDATION RESULTS SUMMARY");
Console.WriteLine(new string('=', 80));

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
if (successRate >= 90)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✓ THEME VALIDATION PASSED");
    Console.WriteLine("  WinUI 3 theme architecture is correctly configured");
    Console.WriteLine("  Syncfusion controls will use built-in Fluent Design System");
    Console.ResetColor();
}
else if (successRate >= 70)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("⚠ THEME VALIDATION - WARNINGS PRESENT");
    Console.WriteLine("  Review warnings above - may indicate WPF-style theming remnants");
    Console.ResetColor();
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("✗ THEME VALIDATION FAILED");
    Console.WriteLine("  Significant theme configuration issues detected");
    Console.WriteLine("  Review failures above and consult WinUI 3 theming documentation");
    Console.ResetColor();
}

Console.WriteLine($"\nDetailed log: {logPath}\n");

Log.Information("Theme Test Summary - Total: {Total}, Passed: {Passed}, Failed: {Failed}, Rate: {Rate:F1}%",
    totalTests, passedTests, totalTests - passedTests, successRate);
Log.CloseAndFlush();

Environment.Exit(successRate >= 90 ? 0 : 1);
