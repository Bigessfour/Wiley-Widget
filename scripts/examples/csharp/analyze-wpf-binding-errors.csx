#!/usr/bin/env dotnet-script

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

Console.WriteLine("=== C# MCP: WPF Binding Error Analysis ===\n");

string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Environment.CurrentDirectory;
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

Console.WriteLine($"Repository: {repoRoot}");
Console.WriteLine($"Logs: {logsDir}\n");

// ========== TASK 2: Parse logs for WPF binding errors ==========
Console.WriteLine("=== TASK 2: Scanning Application Logs for WPF Binding Errors ===\n");

if (!Directory.Exists(logsDir))
{
    Console.WriteLine($"❌ Logs directory not found: {logsDir}");
    return 1;
}

var logFiles = Directory.GetFiles(logsDir, "*.log", SearchOption.AllDirectories);
Console.WriteLine($"Found {logFiles.Length} log files\n");

// Pattern to match WPF binding errors
// Example: "System.Windows.Data Error: 40 : BindingExpression path error: 'HealthScore' property not found on 'object' ''DashboardPanelViewModel'"
var bindingErrorPattern = new Regex(
    @"System\.Windows\.Data Error:\s*40\s*:.*?'(\w+)'\s+property not found on\s+'object'\s+''(\w+)''",
    RegexOptions.IgnoreCase
);

// Also match alternative formats
var altPattern = new Regex(
    @"BindingExpression path error:.*?'(\w+)'.*?property.*?'(\w+)'",
    RegexOptions.IgnoreCase
);

// Storage for all binding errors
var bindingErrors = new List<(string Property, string ViewModel, string FullError, string LogFile, int LineNum)>();

Console.WriteLine("Scanning log files for WPF binding errors...\n");

foreach (var logFile in logFiles)
{
    Console.WriteLine($"Scanning: {Path.GetFileName(logFile)}");

    try
    {
        string[] lines = File.ReadAllLines(logFile);
        int errorsInFile = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            // Try primary pattern
            var match = bindingErrorPattern.Match(line);
            if (match.Success)
            {
                string property = match.Groups[1].Value;
                string viewModel = match.Groups[2].Value;
                bindingErrors.Add((property, viewModel, line.Trim(), Path.GetFileName(logFile), i + 1));
                errorsInFile++;
            }
            else
            {
                // Try alternative pattern
                var altMatch = altPattern.Match(line);
                if (altMatch.Success && line.Contains("BindingExpression"))
                {
                    string property = altMatch.Groups[1].Value;
                    string viewModel = altMatch.Groups[2].Value;
                    bindingErrors.Add((property, viewModel, line.Trim(), Path.GetFileName(logFile), i + 1));
                    errorsInFile++;
                }
            }
        }

        Console.WriteLine($"  Found: {errorsInFile} binding errors");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Error reading file: {ex.Message}");
    }
}

Console.WriteLine($"\n✓ Total WPF Binding Errors Found: {bindingErrors.Count}\n");

if (bindingErrors.Count == 0)
{
    Console.WriteLine("⚠ No binding errors found in logs.");
    Console.WriteLine("   This may mean:");
    Console.WriteLine("   1. The application hasn't been run yet (no runtime logs)");
    Console.WriteLine("   2. Logs are in a different location");
    Console.WriteLine("   3. Errors are formatted differently");
    Console.WriteLine("\n   Switching to static XAML analysis mode...\n");

    // FALLBACK: Analyze XAML files for binding expressions
    Console.WriteLine("=== Analyzing XAML Files for Binding Expressions ===\n");

    var xamlFiles = Directory.GetFiles(repoRoot, "*.xaml", SearchOption.AllDirectories)
        .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
        .ToArray();

    Console.WriteLine($"Found {xamlFiles.Length} XAML files\n");

    var bindingPattern = new Regex(@"\{Binding\s+(\w+)", RegexOptions.IgnoreCase);
    var bindings = new Dictionary<string, List<(string File, int Line)>>();

    foreach (var xamlFile in xamlFiles.Take(50)) // Limit to avoid overwhelming output
    {
        try
        {
            string[] lines = File.ReadAllLines(xamlFile);
            for (int i = 0; i < lines.Length; i++)
            {
                var matches = bindingPattern.Matches(lines[i]);
                foreach (Match m in matches)
                {
                    string property = m.Groups[1].Value;
                    if (!bindings.ContainsKey(property))
                        bindings[property] = new List<(string, int)>();
                    bindings[property].Add((Path.GetFileName(xamlFile), i + 1));
                }
            }
        }
        catch { }
    }

    Console.WriteLine($"Found {bindings.Count} unique binding properties across XAML files");
    Console.WriteLine("\nMost Common Bindings (Top 20):");

    foreach (var binding in bindings.OrderByDescending(b => b.Value.Count).Take(20))
    {
        Console.WriteLine($"  {binding.Key,-30} ({binding.Value.Count} occurrences)");
    }

    Console.WriteLine("\n⚠ Cannot determine which are errors without runtime logs");
    Console.WriteLine("   Please run the application to generate binding error logs");

    return 1;
}

// ========== TASK 3: Identify top 5-10 most frequent errors ==========
Console.WriteLine("=== TASK 3: Ranking Binding Errors by Frequency ===\n");

// Group by Property + ViewModel combination
var errorGroups = bindingErrors
    .GroupBy(e => new { e.Property, e.ViewModel })
    .Select(g => new {
        Property = g.Key.Property,
        ViewModel = g.Key.ViewModel,
        Count = g.Count(),
        Errors = g.ToList()
    })
    .OrderByDescending(g => g.Count)
    .ToList();

Console.WriteLine($"Unique Property/ViewModel Combinations: {errorGroups.Count}\n");

Console.WriteLine("=== TOP 10 MOST FREQUENT BINDING ERRORS ===\n");
var top10 = errorGroups.Take(10).ToList();

int rank = 1;
foreach (var error in top10)
{
    Console.WriteLine($"{rank}. Property: '{error.Property}' in ViewModel: '{error.ViewModel}'");
    Console.WriteLine($"   Occurrences: {error.Count}");
    Console.WriteLine($"   Sample Error: {error.Errors.First().FullError.Substring(0, Math.Min(100, error.Errors.First().FullError.Length))}...");
    Console.WriteLine();
    rank++;
}

// Focus on specific ViewModels mentioned in prompt
var targetViewModels = new[] {
    "DashboardPanelViewModel",
    "SettingsPanelViewModel",
    "ToolsPanelViewModel",
    "DashboardViewModel"
};

Console.WriteLine("=== ERRORS IN TARGET VIEWMODELS ===\n");

foreach (var vm in targetViewModels)
{
    var vmErrors = errorGroups.Where(e => e.ViewModel == vm).ToList();
    if (vmErrors.Any())
    {
        Console.WriteLine($"{vm}: {vmErrors.Count} unique property errors ({vmErrors.Sum(e => e.Count)} total occurrences)");
        foreach (var err in vmErrors.Take(5))
        {
            Console.WriteLine($"  • {err.Property} ({err.Count}x)");
        }
        Console.WriteLine();
    }
    else
    {
        Console.WriteLine($"{vm}: No errors found");
    }
}

// ========== Export detailed report ==========
string reportPath = Path.Combine(logsDir, $"wpf-binding-errors-report-{DateTime.Now:yyyyMMdd-HHmmss}.log");

try
{
    using (var writer = new StreamWriter(reportPath))
    {
        writer.WriteLine("=== WPF Binding Errors Report ===");
        writer.WriteLine($"Generated: {DateTime.Now}");
        writer.WriteLine($"Total Errors: {bindingErrors.Count}");
        writer.WriteLine($"Unique Combinations: {errorGroups.Count}");
        writer.WriteLine();

        writer.WriteLine("=== TOP 10 ERRORS ===");
        writer.WriteLine();

        rank = 1;
        foreach (var error in top10)
        {
            writer.WriteLine($"{rank}. {error.ViewModel}.{error.Property}");
            writer.WriteLine($"   Count: {error.Count}");
            writer.WriteLine($"   Locations:");
            foreach (var instance in error.Errors.Take(3))
            {
                writer.WriteLine($"     - {instance.LogFile} (line {instance.LineNum})");
            }
            writer.WriteLine();
            rank++;
        }

        writer.WriteLine("=== ALL ERRORS BY VIEWMODEL ===");
        writer.WriteLine();

        var byViewModel = errorGroups.GroupBy(e => e.ViewModel).OrderBy(g => g.Key);
        foreach (var vmGroup in byViewModel)
        {
            writer.WriteLine($"{vmGroup.Key}:");
            foreach (var error in vmGroup.OrderByDescending(e => e.Count))
            {
                writer.WriteLine($"  {error.Property} ({error.Count}x)");
            }
            writer.WriteLine();
        }
    }

    Console.WriteLine($"\n✓ Detailed report saved to: {reportPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠ Could not write report: {ex.Message}");
}

// ========== Summary for next steps ==========
Console.WriteLine("\n=== SUMMARY FOR TASK 4 (Root Cause Analysis) ===\n");

Console.WriteLine("ViewModels requiring investigation:");
var distinctViewModels = errorGroups.Select(e => e.ViewModel).Distinct().OrderBy(v => v).ToList();
foreach (var vm in distinctViewModels.Take(10))
{
    Console.WriteLine($"  • {vm}");
}

Console.WriteLine($"\nTotal properties to verify: {top10.Count}");
Console.WriteLine("\nNext: Use MCP to check if these properties exist in their respective ViewModels");

return 0;
