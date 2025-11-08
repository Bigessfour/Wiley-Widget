#!/usr/bin/env dotnet-script

using System;
using System.IO;
using System.Text.RegularExpressions;

/*
 * C# Script: Deep Dive Test Analysis
 * Purpose: Find the exact issue causing test hangs
 */

Console.WriteLine("=== Deep Dive Test Analysis ===");
Console.WriteLine();

var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();

// Check DashboardViewModel constructor
var dashboardVMFile = Path.Combine(repoRoot, "WileyWidget.UI", "ViewModels", "Main", "DashboardViewModel.cs");

if (!File.Exists(dashboardVMFile))
{
    Console.WriteLine($"❌ File not found: {dashboardVMFile}");
    return 1;
}

var vmContent = File.ReadAllText(dashboardVMFile);

Console.WriteLine("1. Analyzing DashboardViewModel constructor...");
Console.WriteLine();

// Find the constructor
var constructorMatch = Regex.Match(vmContent,
    @"public DashboardViewModel\([^)]*\)\s*\{(.*?)(?=\n\s{8}private void InitializeCommands)",
    RegexOptions.Singleline);

if (constructorMatch.Success)
{
    var constructorBody = constructorMatch.Groups[1].Value;

    // Check for fire-and-forget calls
    var fireAndForgetPattern = @"_\s*=\s*(Task\.|LoadDashboardDataAsync)";
    var fireAndForgetMatches = Regex.Matches(constructorBody, fireAndForgetPattern);

    Console.WriteLine($"   Fire-and-forget async calls: {fireAndForgetMatches.Count}");

    foreach (Match match in fireAndForgetMatches)
    {
        var context = vmContent.Substring(Math.Max(0, match.Index - 50), Math.Min(150, vmContent.Length - match.Index));
        var lines = context.Split('\n');
        Console.WriteLine($"      ⚠️  Found: {lines.FirstOrDefault(l => l.Contains(match.Value))?.Trim()}");
    }

    // Check if LoadDashboardDataAsync is called
    if (constructorBody.Contains("LoadDashboardDataAsync"))
    {
        Console.WriteLine();
        Console.WriteLine("   ❌ CRITICAL: Constructor calls LoadDashboardDataAsync()");
        Console.WriteLine("      This is a fire-and-forget async call that will cause hangs!");
        Console.WriteLine("      LoadDashboardDataAsync calls multiple async methods.");
    }

    // Check if _cacheLoadingTask is properly assigned
    if (constructorBody.Contains("_cacheLoadingTask = Task.Run"))
    {
        Console.WriteLine();
        Console.WriteLine("   ✅ _cacheLoadingTask is assigned (good)");
    }
    else if (constructorBody.Contains("Task.Run"))
    {
        Console.WriteLine();
        Console.WriteLine("   ⚠️  Task.Run exists but NOT assigned to _cacheLoadingTask");
    }
}
else
{
    Console.WriteLine("   ⚠️  Could not parse constructor");
}

Console.WriteLine();
Console.WriteLine("2. Checking LoadDashboardDataAsync method...");
Console.WriteLine();

var loadDashboardMatch = Regex.Match(vmContent,
    @"public async Task LoadDashboardDataAsync\(\)(.*?)(?=\n\s{8}(?:public|private|internal|protected))",
    RegexOptions.Singleline);

if (loadDashboardMatch.Success)
{
    var methodBody = loadDashboardMatch.Groups[1].Value;

    // Find all async method calls
    var asyncCalls = Regex.Matches(methodBody, @"(Load\w+Async)\(\)");

    Console.WriteLine($"   Async methods called: {asyncCalls.Count}");
    foreach (Match call in asyncCalls.Cast<Match>().Take(10))
    {
        Console.WriteLine($"      - {call.Groups[1].Value}()");
    }

    Console.WriteLine();
    Console.WriteLine("   ⚠️  Each of these methods needs mock setups in tests!");
}

Console.WriteLine();
Console.WriteLine("3. Solution:");
Console.WriteLine();
Console.WriteLine("   The constructor should NOT call LoadDashboardDataAsync directly.");
Console.WriteLine("   Options:");
Console.WriteLine("   A) Remove the call entirely - let tests/UI trigger it explicitly");
Console.WriteLine("   B) Make it awaitable and ensure tests wait for it");
Console.WriteLine("   C) Track it like _cacheLoadingTask");

Console.WriteLine();
Console.WriteLine("=== Analysis Complete ===");

return 0;
