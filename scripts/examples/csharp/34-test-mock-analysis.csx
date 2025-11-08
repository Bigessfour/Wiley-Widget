#!/usr/bin/env dotnet-script
#r "nuget: System.Diagnostics.Process, 4.3.0"

using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

/*
 * C# Script: Test Mock Analysis
 * Purpose: Analyze test mocks to find missing setups that could cause hangs
 */

Console.WriteLine("=== Test Mock Analysis ===");
Console.WriteLine();

var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();

// Analyze DashboardViewModelTests
var dashboardTestFile = Path.Combine(repoRoot, "WileyWidget.Tests", "WileyWidget.ViewModels.Tests", "DashboardViewModelTests.cs");

if (!File.Exists(dashboardTestFile))
{
    Console.WriteLine($"❌ Test file not found: {dashboardTestFile}");
    return 1;
}

var content = File.ReadAllText(dashboardTestFile);

Console.WriteLine("1. Checking DashboardViewModelTests mock setups...");
Console.WriteLine();

// Check for enterprise repository mock setup
var hasGetAllAsyncSetup = content.Contains("_mockEnterpriseRepository") &&
                          content.Contains(".Setup") &&
                          content.Contains("GetAllAsync");

Console.WriteLine($"   Enterprise Repository GetAllAsync mock: {(hasGetAllAsyncSetup ? "✅" : "❌")}");

// Check for cache service mock setup
var hasCacheGetAsyncSetup = content.Contains("_mockCacheService") &&
                            Regex.IsMatch(content, @"_mockCacheService[^;]*\.Setup[^;]*GetAsync");

var hasCacheSetAsyncSetup = content.Contains("_mockCacheService") &&
                            Regex.IsMatch(content, @"_mockCacheService[^;]*\.Setup[^;]*SetAsync");

Console.WriteLine($"   Cache Service GetAsync mock: {(hasCacheGetAsyncSetup ? "✅" : "❌")}");
Console.WriteLine($"   Cache Service SetAsync mock: {(hasCacheSetAsyncSetup ? "✅" : "❌")}");

// Check constructor test
var constructorTestMatch = Regex.Match(content,
    @"public async Task Constructor_WithValidDependencies.*?\{(.*?)\}",
    RegexOptions.Singleline);

if (constructorTestMatch.Success)
{
    var testBody = constructorTestMatch.Groups[1].Value;
    var hasUsing = testBody.Contains("using var");
    var hasTaskDelay = testBody.Contains("Task.Delay");
    var hasDispose = testBody.Contains(".Dispose()");

    Console.WriteLine();
    Console.WriteLine("2. Constructor test analysis:");
    Console.WriteLine($"   Uses 'using var' for disposal: {(hasUsing ? "✅" : "❌")}");
    Console.WriteLine($"   Waits for background tasks (Task.Delay): {(hasTaskDelay ? "✅" : "❌")}");
    Console.WriteLine($"   Explicitly calls Dispose: {(hasDispose ? "⚠️ (unnecessary with 'using')" : "✅")}");
}

Console.WriteLine();

// Check for LoadDashboardDataAsync mock
var hasLoadDataAsyncSetup = Regex.IsMatch(content, @"LoadDashboardDataAsync");

Console.WriteLine("3. Checking for potential async issues...");
Console.WriteLine($"   Tests reference LoadDashboardDataAsync: {(hasLoadDataAsyncSetup ? "⚠️" : "✅")}");

if (hasLoadDataAsyncSetup)
{
    Console.WriteLine("   ⚠️  LoadDashboardDataAsync is called in constructor - ensure mocks handle this!");
}

Console.WriteLine();

// Look for common hanging patterns
Console.WriteLine("4. Checking for common hanging patterns...");

var issues = new List<string>();

// Check if tests are waiting on Task.Result or .Wait() without timeout
if (Regex.IsMatch(content, @"\.Result\s*[;\)]") || Regex.IsMatch(content, @"\.Wait\(\s*\)"))
{
    issues.Add("Synchronous wait on Task without timeout detected");
}

// Check for missing async in test methods that create ViewModels
var testMethodsCreatingVM = Regex.Matches(content, @"public void .*?Test.*?\(.*?\).*?CreateViewModel", RegexOptions.Singleline);
if (testMethodsCreatingVM.Count > 0)
{
    issues.Add($"{testMethodsCreatingVM.Count} non-async test methods create ViewModels with background tasks");
}

if (issues.Any())
{
    Console.WriteLine("   ⚠️  Potential issues found:");
    foreach (var issue in issues)
    {
        Console.WriteLine($"      - {issue}");
    }
}
else
{
    Console.WriteLine("   ✅ No obvious hanging patterns detected");
}

Console.WriteLine();
Console.WriteLine("=== Analysis Complete ===");

if (!hasGetAllAsyncSetup || !hasCacheGetAsyncSetup || !hasCacheSetAsyncSetup)
{
    Console.WriteLine();
    Console.WriteLine("❌ CRITICAL: Missing mock setups detected!");
    Console.WriteLine("   The ViewModel constructor calls async methods that need mocks.");
    Console.WriteLine("   Without proper mocks, these calls may hang or fail.");
    return 1;
}

return 0;
