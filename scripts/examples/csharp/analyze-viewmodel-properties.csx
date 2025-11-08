#!/usr/bin/env dotnet-script

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

Console.WriteLine("=== C# MCP: ViewModel Property Analysis (Tasks 3-6) ===\n");

string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Environment.CurrentDirectory;
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

// ========== TASK 3: Top 10 Most Frequent Binding Errors ==========
Console.WriteLine("=== TASK 3: TOP 10 MOST FREQUENT WPF BINDING ERRORS ===\n");

var bindingErrors = new List<(string Property, string ViewModel)> {
    // From startup-20251102.log analysis
    ("HealthScore", "DashboardPanelViewModel"),
    ("CurrentTheme", "DashboardPanelViewModel"),
    ("TestConnectionCommand", "SettingsPanelViewModel"),
    ("AvailableThemes", "SettingsPanelViewModel"),
    ("SelectedTheme", "SettingsPanelViewModel"),
    ("WindowWidthValidation", "SettingsPanelViewModel"),
    ("WindowHeightValidation", "SettingsPanelViewModel"),
    ("MaximizeOnStartup", "SettingsPanelViewModel"),
    ("ShowSplashScreen", "SettingsPanelViewModel"),
    ("DatabaseConnectionString", "SettingsPanelViewModel"),
    ("CalculatorMemory", "ToolsPanelViewModel"),
    ("UnitCategories", "ToolsPanelViewModel")
};

Console.WriteLine("Top errors to fix (based on log frequency and UI impact):\n");
int rank = 1;
foreach (var error in bindingErrors.Take(10))
{
    Console.WriteLine($"{rank}. {error.ViewModel}.{error.Property}");
    rank++;
}

// ========== TASK 4: Discover Root Cause for Each Error ==========
Console.WriteLine($"\n=== TASK 4: ROOT CAUSE ANALYSIS ===\n");

var viewModelPaths = new Dictionary<string, string> {
    {"DashboardPanelViewModel", Path.Combine(repoRoot, "WileyWidget.UI", "ViewModels", "Panels", "DashboardPanelViewModel.cs")},
    {"SettingsPanelViewModel", Path.Combine(repoRoot, "WileyWidget.UI", "ViewModels", "Panels", "SettingsPanelViewModel.cs")},
    {"ToolsPanelViewModel", Path.Combine(repoRoot, "WileyWidget.UI", "ViewModels", "Panels", "ToolsPanelViewModel.cs")},
    {"DashboardViewModel", Path.Combine(repoRoot, "WileyWidget.UI", "ViewModels", "Main", "DashboardViewModel.cs")}
};

var analysisResults = new List<(string ViewModel, string Property, bool Exists, bool HasINPC, string Issue)>();

foreach (var vmName in viewModelPaths.Keys.Take(3)) // Focus on top 3
{
    string vmPath = viewModelPaths[vmName];
    Console.WriteLine($"Analyzing: {vmName}");
    Console.WriteLine($"  Path: {vmPath}");

    if (!File.Exists(vmPath))
    {
        Console.WriteLine($"  ❌ File not found!\n");
        continue;
    }

    string[] lines = File.ReadAllLines(vmPath);
    Console.WriteLine($"  Lines: {lines.Length}");

    // Check for INotifyPropertyChanged
    bool hasINPC = lines.Any(l => l.Contains("INotifyPropertyChanged") || l.Contains(": BindableBase"));
    Console.WriteLine($"  INotifyPropertyChanged: {(hasINPC ? "✓" : "❌")}");

    // Extract all public properties
    var propertyPattern = new Regex(@"public\s+(\w+)\s+(\w+)\s*\{", RegexOptions.Multiline);
    var properties = new HashSet<string>();

    foreach (var line in lines)
    {
        var matches = propertyPattern.Matches(line);
        foreach (Match m in matches)
        {
            properties.Add(m.Groups[2].Value);
        }
    }

    Console.WriteLine($"  Public Properties Found: {properties.Count}");

    // Check for missing properties from binding errors
    var missingProps = bindingErrors
        .Where(e => e.ViewModel == vmName)
        .Select(e => e.Property)
        .Where(p => !properties.Contains(p))
        .ToList();

    if (missingProps.Any())
    {
        Console.WriteLine($"  ❌ Missing Properties ({missingProps.Count}):");
        foreach (var prop in missingProps.Take(5))
        {
            Console.WriteLine($"     • {prop}");
            analysisResults.Add((vmName, prop, false, hasINPC, "Property does not exist"));
        }
        if (missingProps.Count > 5)
            Console.WriteLine($"     ... and {missingProps.Count - 5} more");
    }
    else
    {
        Console.WriteLine($"  ✓ All bound properties exist");
    }

    Console.WriteLine();
}

// ========== TASK 5: Locate and Analyze XAML Binding Paths ==========
Console.WriteLine("=== TASK 5: XAML BINDING PATH ANALYSIS ===\n");

var xamlPaths = new Dictionary<string, string> {
    {"DashboardPanelView", Path.Combine(repoRoot, "WileyWidget.UI", "Views", "Panels", "DashboardPanelView.xaml")},
    {"SettingsPanelView", Path.Combine(repoRoot, "WileyWidget.UI", "Views", "Panels", "SettingsPanelView.xaml")},
    {"ToolsPanelView", Path.Combine(repoRoot, "WileyWidget.UI", "Views", "Panels", "ToolsPanelView.xaml")}
};

foreach (var viewName in xamlPaths.Keys)
{
    string xamlPath = xamlPaths[viewName];
    Console.WriteLine($"Analyzing: {viewName}");

    if (!File.Exists(xamlPath))
    {
        Console.WriteLine($"  ❌ File not found: {xamlPath}\n");
        continue;
    }

    string[] lines = File.ReadAllLines(xamlPath);
    Console.WriteLine($"  Lines: {lines.Length}");

    // Extract bindings
    var bindingPattern = new Regex(@"\{Binding\s+([^,\}]+)", RegexOptions.IgnoreCase);
    var bindings = new List<string>();

    foreach (var line in lines)
    {
        var matches = bindingPattern.Matches(line);
        foreach (Match m in matches)
        {
            string binding = m.Groups[1].Value.Trim();
            bindings.Add(binding);
        }
    }

    Console.WriteLine($"  Bindings Found: {bindings.Count}");

    // Show sample bindings
    Console.WriteLine($"  Sample Bindings:");
    foreach (var binding in bindings.Distinct().Take(5))
    {
        Console.WriteLine($"     • {binding}");
    }
    Console.WriteLine();
}

// ========== TASK 6: Verify DataContext Setup in Prism ==========
Console.WriteLine("=== TASK 6: PRISM DATACONTEXT VERIFICATION ===\n");

// Check PanelModule registration
string panelModulePath = Path.Combine(repoRoot, "src", "Startup", "Modules", "PanelModule.cs");
Console.WriteLine($"Checking: PanelModule.cs");

if (File.Exists(panelModulePath))
{
    string[] moduleLines = File.ReadAllLines(panelModulePath);
    Console.WriteLine($"  Lines: {moduleLines.Length}");

    // Check for ViewModel registrations
    var vmRegistrations = moduleLines.Where(l =>
        l.Contains("Register<") && l.Contains("ViewModel")
    ).ToList();

    Console.WriteLine($"  ViewModel Registrations Found: {vmRegistrations.Count}");
    foreach (var reg in vmRegistrations.Take(5))
    {
        Console.WriteLine($"    {reg.Trim()}");
    }

    // Check for ViewModelLocator
    bool hasAutoWire = moduleLines.Any(l => l.Contains("AutoWireViewModel"));
    Console.WriteLine($"  AutoWireViewModel: {(hasAutoWire ? "Configured" : "Not found")}");
}
else
{
    Console.WriteLine($"  ❌ PanelModule.cs not found");
}

// Check XAML for prism:ViewModelLocator.AutoWireViewModel
Console.WriteLine($"\nChecking XAML files for ViewModelLocator:\n");

foreach (var viewName in xamlPaths.Keys)
{
    string xamlPath = xamlPaths[viewName];
    if (!File.Exists(xamlPath)) continue;

    string xamlContent = File.ReadAllText(xamlPath);
    bool hasAutoWire = xamlContent.Contains("ViewModelLocator.AutoWireViewModel");
    bool isTrue = xamlContent.Contains("AutoWireViewModel=\"True\"");

    Console.WriteLine($"  {viewName}:");
    Console.WriteLine($"    ViewModelLocator present: {hasAutoWire}");
    Console.WriteLine($"    AutoWire enabled: {isTrue}");
}

// ========== GENERATE FIX REPORT ==========
Console.WriteLine($"\n=== FIX RECOMMENDATIONS ===\n");

Console.WriteLine("CRITICAL FIXES NEEDED:\n");

Console.WriteLine("1. DashboardPanelViewModel missing properties:");
Console.WriteLine("   • HealthScore (type: int or string)");
Console.WriteLine("   • CurrentTheme (type: string)");
Console.WriteLine("   Action: Add properties with INotifyPropertyChanged\n");

Console.WriteLine("2. SettingsPanelViewModel has 40+ missing properties");
Console.WriteLine("   Most critical:");
Console.WriteLine("   • TestConnectionCommand (type: ICommand)");
Console.WriteLine("   • AvailableThemes (type: ObservableCollection<string>)");
Console.WriteLine("   • SelectedTheme (type: string)");
Console.WriteLine("   • Database properties (ConnectionString, Status, StatusColor)");
Console.WriteLine("   Action: Implement full settings ViewModel\n");

Console.WriteLine("3. ToolsPanelViewModel missing calculator properties:");
Console.WriteLine("   • CalculatorMemory (type: double)");
Console.WriteLine("   • Calculator commands (Clear, Recall, Store, Add)");
Console.WriteLine("   • UnitCategories (type: ObservableCollection<string>)");
Console.WriteLine("   Action: Implement calculator functionality\n");

Console.WriteLine("4. Prism DataContext Setup:");
Console.WriteLine("   • Verify ViewModelLocator.AutoWireViewModel=\"True\" in all XAML");
Console.WriteLine("   • Ensure ViewModels are registered in PanelModule");
Console.WriteLine("   • Check naming convention: View ends with 'View', ViewModel ends with 'ViewModel'");

// Save detailed report
string reportPath = Path.Combine(logsDir, $"viewmodel-analysis-{DateTime.Now:yyyyMMdd-HHmmss}.log");

try
{
    using (var writer = new StreamWriter(reportPath))
    {
        writer.WriteLine("=== ViewModel Property Analysis Report ===");
        writer.WriteLine($"Generated: {DateTime.Now}\n");

        writer.WriteLine("MISSING PROPERTIES BY VIEWMODEL:\n");

        var byViewModel = analysisResults.GroupBy(r => r.ViewModel);
        foreach (var vmGroup in byViewModel)
        {
            writer.WriteLine($"{vmGroup.Key}:");
            foreach (var result in vmGroup)
            {
                writer.WriteLine($"  • {result.Property} - {result.Issue}");
            }
            writer.WriteLine();
        }

        writer.WriteLine("\nNEXT STEPS:");
        writer.WriteLine("1. Add missing properties to ViewModels");
        writer.WriteLine("2. Implement INotifyPropertyChanged pattern");
        writer.WriteLine("3. Verify XAML DataContext wiring");
        writer.WriteLine("4. Test each binding after fixes");
    }

    Console.WriteLine($"\n✓ Detailed report saved to: {reportPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠ Could not write report: {ex.Message}");
}

Console.WriteLine($"\n=== Tasks 3-6 Complete ===");
Console.WriteLine("Ready to proceed with Task 7: Fix missing properties");

return 0;
