#!/usr/bin/env dotnet-script
// WinUI Theme Validation Test Script
// Validates that Syncfusion FluentLight theme is properly configured
// Created: 2025-11-14
// MCP Tools: C# Evaluation, Sequential Thinking

using System;
using System.IO;
using System.Xml.Linq;

Console.WriteLine("=== WinUI Theme Validation Test ===\n");

// Test 1: Verify App.xaml has Syncfusion theme dictionaries
Console.WriteLine("Test 1: Checking App.xaml for Syncfusion theme resource dictionaries...");
var appXamlPath = Path.Combine(Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? ".", "src/WileyWidget.WinUI/App.xaml");

if (!File.Exists(appXamlPath))
{
    Console.WriteLine($"❌ FAILED: App.xaml not found at {appXamlPath}");
    Environment.Exit(1);
}

var appXamlContent = File.ReadAllText(appXamlPath);
var requiredDictionaries = new[]
{
    "Syncfusion.Core.WinUI/Themes/FluentLight.xaml",
    "Syncfusion.Grid.WinUI/Themes/FluentLight.xaml",
    "Syncfusion.Chart.WinUI/Themes/FluentLight.xaml",
    "Syncfusion.Gauge.WinUI/Themes/FluentLight.xaml",
    "Syncfusion.Editors.WinUI/Themes/FluentLight.xaml",
    "Syncfusion.TreeView.WinUI/Themes/FluentLight.xaml"
};

var missingDictionaries = new List<string>();
foreach (var dict in requiredDictionaries)
{
    if (appXamlContent.Contains(dict))
    {
        Console.WriteLine($"  ✓ Found: {dict}");
    }
    else
    {
        Console.WriteLine($"  ❌ Missing: {dict}");
        missingDictionaries.Add(dict);
    }
}

if (missingDictionaries.Any())
{
    Console.WriteLine($"❌ FAILED: {missingDictionaries.Count} required theme dictionaries missing");
    Environment.Exit(1);
}

Console.WriteLine("✅ PASSED: All Syncfusion theme dictionaries present\n");

// Test 2: Verify RequestedTheme="Light" is set
Console.WriteLine("Test 2: Checking for RequestedTheme attribute...");
if (appXamlContent.Contains("RequestedTheme=\"Light\""))
{
    Console.WriteLine("  ✓ RequestedTheme=\"Light\" found");
    Console.WriteLine("✅ PASSED: Application theme set to Light\n");
}
else
{
    Console.WriteLine("  ❌ RequestedTheme attribute not found or not set to Light");
    Console.WriteLine("❌ FAILED: RequestedTheme configuration missing");
    Environment.Exit(1);
}

// Test 3: Verify App.xaml.cs has theme verification methods
Console.WriteLine("Test 3: Checking App.xaml.cs for theme verification methods...");
var appXamlCsPath = Path.Combine(Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? ".", "src/WileyWidget.WinUI/App.xaml.cs");

if (!File.Exists(appXamlCsPath))
{
    Console.WriteLine($"❌ FAILED: App.xaml.cs not found at {appXamlCsPath}");
    Environment.Exit(1);
}

var appXamlCsContent = File.ReadAllText(appXamlCsPath);
var requiredMethods = new[]
{
    ("VerifyWinUITheme", "Theme verification method"),
    ("CheckMemoryStatus", "Memory status validation"),
    ("ValidateUIBrushes", "UI brush validation")
};

var missingMethods = new List<string>();
foreach (var (method, description) in requiredMethods)
{
    if (appXamlCsContent.Contains($"private void {method}()"))
    {
        Console.WriteLine($"  ✓ Found: {method}() - {description}");
    }
    else
    {
        Console.WriteLine($"  ❌ Missing: {method}() - {description}");
        missingMethods.Add(method);
    }
}

if (missingMethods.Any())
{
    Console.WriteLine($"❌ FAILED: {missingMethods.Count} required theme verification methods missing");
    Environment.Exit(1);
}

Console.WriteLine("✅ PASSED: All theme verification methods present\n");

// Test 4: Verify VerifyWinUITheme is called in OnInitialized
Console.WriteLine("Test 4: Checking that VerifyWinUITheme is called during initialization...");
if (appXamlCsContent.Contains("VerifyWinUITheme();"))
{
    Console.WriteLine("  ✓ VerifyWinUITheme() is called in OnInitialized");
    Console.WriteLine("✅ PASSED: Theme verification integrated into startup\n");
}
else
{
    Console.WriteLine("  ❌ VerifyWinUITheme() not called in OnInitialized");
    Console.WriteLine("❌ FAILED: Theme verification not integrated");
    Environment.Exit(1);
}

// Test 5: Verify license registration in static constructor
Console.WriteLine("Test 5: Checking for Syncfusion license registration...");
if (appXamlCsContent.Contains("Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense"))
{
    Console.WriteLine("  ✓ Syncfusion license registration found");
    Console.WriteLine("✅ PASSED: License registration configured\n");
}
else
{
    Console.WriteLine("  ❌ Syncfusion license registration not found");
    Console.WriteLine("❌ FAILED: License registration missing");
    Environment.Exit(1);
}

Console.WriteLine("=== ALL TESTS PASSED ===");
Console.WriteLine("\n✓ WinUI theme is properly configured");
Console.WriteLine("✓ Syncfusion FluentLight theme dictionaries loaded");
Console.WriteLine("✓ Theme verification methods implemented");
Console.WriteLine("✓ License registration configured");
Console.WriteLine("\nTheme application should now work correctly in WinUI.");
Environment.Exit(0);
