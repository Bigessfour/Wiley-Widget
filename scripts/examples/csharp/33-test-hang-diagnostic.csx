#!/usr/bin/env dotnet-script
#r "nuget: System.Diagnostics.Process, 4.3.0"

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

/*
 * C# Script: Test Hang Diagnostic
 * Purpose: Diagnose why dotnet test is hanging
 * Checks for:
 * - Hanging testhost processes
 * - Background tasks not completing
 * - Task.Run issues in constructors
 */

Console.WriteLine("=== Test Hang Diagnostic ===");
Console.WriteLine();

// 1. Check for running test processes
Console.WriteLine("1. Checking for test processes...");
var testhostProcesses = Process.GetProcesses()
    .Where(p => p.ProcessName.Contains("testhost", StringComparison.OrdinalIgnoreCase))
    .ToList();

if (testhostProcesses.Any())
{
    Console.WriteLine($"   ⚠️  Found {testhostProcesses.Count} testhost process(es):");
    foreach (var proc in testhostProcesses)
    {
        try
        {
            var runtime = DateTime.Now - proc.StartTime;
            Console.WriteLine($"      PID {proc.Id}: Running for {runtime.TotalSeconds:F1}s");
        }
        catch
        {
            Console.WriteLine($"      PID {proc.Id}: (cannot read start time)");
        }
    }
}
else
{
    Console.WriteLine("   ✅ No testhost processes found");
}

Console.WriteLine();

// 2. Check for long-running dotnet processes
Console.WriteLine("2. Checking for long-running dotnet processes...");
var dotnetProcesses = Process.GetProcesses()
    .Where(p => p.ProcessName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
    .ToList();

var longRunning = dotnetProcesses
    .Where(p =>
    {
        try
        {
            return (DateTime.Now - p.StartTime).TotalMinutes > 1;
        }
        catch
        {
            return false;
        }
    })
    .ToList();

if (longRunning.Any())
{
    Console.WriteLine($"   ⚠️  Found {longRunning.Count} long-running dotnet process(es):");
    foreach (var proc in longRunning)
    {
        try
        {
            var runtime = DateTime.Now - proc.StartTime;
            var cpuTime = proc.TotalProcessorTime.TotalSeconds;
            Console.WriteLine($"      PID {proc.Id}: Running for {runtime.TotalMinutes:F1}m, CPU time {cpuTime:F1}s");
        }
        catch { }
    }
}
else
{
    Console.WriteLine($"   ✅ No long-running dotnet processes ({dotnetProcesses.Count} total)");
}

Console.WriteLine();

// 3. Analyze test files for potential hanging patterns
Console.WriteLine("3. Analyzing test files for hanging patterns...");

var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
var testFiles = new[]
{
    Path.Combine(repoRoot, "WileyWidget.Tests", "WileyWidget.ViewModels.Tests", "DashboardViewModelTests.cs"),
    Path.Combine(repoRoot, "WileyWidget.Tests", "WileyWidget.ViewModels.Tests", "BudgetViewModelTests.cs")
};

foreach (var testFile in testFiles)
{
    if (!File.Exists(testFile))
    {
        Console.WriteLine($"   ⚠️  File not found: {Path.GetFileName(testFile)}");
        continue;
    }

    var content = File.ReadAllText(testFile);
    var fileName = Path.GetFileName(testFile);

    // Check for Task.Run without await
    var taskRunCount = System.Text.RegularExpressions.Regex.Matches(content, @"Task\.Run\s*\(").Count;
    var awaitTaskCount = System.Text.RegularExpressions.Regex.Matches(content, @"await\s+Task\.").Count;

    // Check for async void (dangerous in tests)
    var asyncVoidCount = System.Text.RegularExpressions.Regex.Matches(content, @"async\s+void\s+").Count;

    // Check for missing using/Dispose
    var usingCount = System.Text.RegularExpressions.Regex.Matches(content, @"using\s+var\s+").Count;
    var disposeCount = System.Text.RegularExpressions.Regex.Matches(content, @"\.Dispose\(\)").Count;

    Console.WriteLine($"   📄 {fileName}:");
    Console.WriteLine($"      Task.Run calls: {taskRunCount}");
    Console.WriteLine($"      await Task calls: {awaitTaskCount}");
    Console.WriteLine($"      async void methods: {asyncVoidCount} {(asyncVoidCount > 0 ? "⚠️" : "")}");
    Console.WriteLine($"      using statements: {usingCount}");
    Console.WriteLine($"      Dispose calls: {disposeCount}");
}

Console.WriteLine();

// 4. Check ViewModel constructors for background tasks
Console.WriteLine("4. Checking ViewModel constructors for background tasks...");

var viewModelFiles = new[]
{
    Path.Combine(repoRoot, "WileyWidget.UI", "ViewModels", "Main", "DashboardViewModel.cs"),
    Path.Combine(repoRoot, "WileyWidget.UI", "ViewModels", "Main", "BudgetViewModel.cs")
};

foreach (var vmFile in viewModelFiles)
{
    if (!File.Exists(vmFile))
    {
        Console.WriteLine($"   ⚠️  File not found: {Path.GetFileName(vmFile)}");
        continue;
    }

    var content = File.ReadAllText(vmFile);
    var fileName = Path.GetFileName(vmFile);

    // Check for _cacheLoadingTask field
    var hasCacheLoadingTask = content.Contains("_cacheLoadingTask");

    // Check for Task.Run in constructor
    var hasTaskRun = System.Text.RegularExpressions.Regex.IsMatch(content, @"public\s+\w+ViewModel\([^)]*\)[^{]*\{[^}]*Task\.Run");

    // Check for ConfigureAwait(false)
    var hasConfigureAwait = content.Contains("ConfigureAwait(false)");

    // Check for Dispose waiting on task
    var disposeWaitsForTask = content.Contains("_cacheLoadingTask") && content.Contains("_cacheLoadingTask.Wait");

    Console.WriteLine($"   📄 {fileName}:");
    Console.WriteLine($"      Has _cacheLoadingTask field: {(hasCacheLoadingTask ? "✅" : "❌")}");
    Console.WriteLine($"      Has Task.Run in constructor: {(hasTaskRun ? "⚠️" : "✅")}");
    Console.WriteLine($"      Uses ConfigureAwait(false): {(hasConfigureAwait ? "✅" : "❌")}");
    Console.WriteLine($"      Dispose waits for task: {(disposeWaitsForTask ? "✅" : "❌")}");
}

Console.WriteLine();

// 5. Recommendations
Console.WriteLine("=== Recommendations ===");

if (testhostProcesses.Any())
{
    Console.WriteLine("❌ Kill hanging testhost processes before running tests");
    Console.WriteLine("   Run: pwsh scripts/kill-test-processes.ps1");
}

if (longRunning.Any())
{
    Console.WriteLine("⚠️  Long-running dotnet processes detected");
    Console.WriteLine("   These may be holding test DLL locks");
}

Console.WriteLine();
Console.WriteLine("✅ Diagnostic complete");
