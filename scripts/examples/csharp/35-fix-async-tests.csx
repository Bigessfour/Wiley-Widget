#!/usr/bin/env dotnet-script

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

/*
 * C# Script: Fix Non-Async Test Methods
 * Purpose: Convert non-async test methods that create ViewModels to async methods
 * This prevents test hangs caused by background tasks in ViewModel constructors
 */

Console.WriteLine("=== Fixing Non-Async Test Methods ===");
Console.WriteLine();

var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
var testFile = Path.Combine(repoRoot, "WileyWidget.Tests", "WileyWidget.ViewModels.Tests", "DashboardViewModelTests.cs");

if (!File.Exists(testFile))
{
    Console.WriteLine($"❌ Test file not found: {testFile}");
    return 1;
}

var content = File.ReadAllText(testFile);
var originalContent = content;

Console.WriteLine("1. Finding test methods that create ViewModels...");
Console.WriteLine();

// Pattern to find: public void TestName() { ... CreateViewModel() ... }
var pattern = @"(\[Fact\]\s+public\s+void\s+(\w+)\s*\([^)]*\)\s*\{(?:[^{}]*\{[^{}]*\})*[^{}]*CreateViewModel\(\)[^}]*\})";
var matches = Regex.Matches(content, pattern, RegexOptions.Singleline);

Console.WriteLine($"   Found {matches.Count} non-async test methods creating ViewModels");
Console.WriteLine();

if (matches.Count == 0)
{
    Console.WriteLine("✅ No non-async test methods found that create ViewModels");
    return 0;
}

int fixedCount = 0;

foreach (Match match in matches)
{
    var methodName = match.Groups[2].Value;
    var fullMethod = match.Groups[1].Value;

    // Skip if already async
    if (fullMethod.Contains("public async Task"))
    {
        continue;
    }

    Console.WriteLine($"   🔧 Converting: {methodName}");

    // Convert: public void MethodName() to public async Task MethodName()
    var fixedMethod = Regex.Replace(fullMethod,
        @"public\s+void\s+(\w+)\s*\(",
        "public async Task $1(");

    // Add Task.Delay after CreateViewModel if not present
    if (!fixedMethod.Contains("Task.Delay"))
    {
        fixedMethod = Regex.Replace(fixedMethod,
            @"((?:var|using var)\s+viewModel\s*=\s*CreateViewModel\(\);)",
            "$1\n        \n        // Give background tasks time to complete\n        await Task.Delay(100);");
    }

    // Add using var if just var
    fixedMethod = Regex.Replace(fixedMethod,
        @"var\s+viewModel\s*=\s*CreateViewModel\(\);",
        "using var viewModel = CreateViewModel();");

    content = content.Replace(fullMethod, fixedMethod);
    fixedCount++;
}

Console.WriteLine();
Console.WriteLine($"2. Applied {fixedCount} fixes");
Console.WriteLine();

if (fixedCount > 0)
{
    // Write output to a temp file for review
    var outputFile = Path.Combine(repoRoot, "test-logs", "DashboardViewModelTests-fixed.cs");
    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
    File.WriteAllText(outputFile, content);

    Console.WriteLine($"✅ Fixed test file written to: {outputFile}");
    Console.WriteLine();
    Console.WriteLine("⚠️  MANUAL STEP REQUIRED:");
    Console.WriteLine($"   Review the fixed file and copy it to:");
    Console.WriteLine($"   {testFile}");
}
else
{
    Console.WriteLine("✅ No fixes needed");
}

Console.WriteLine();
Console.WriteLine("=== Fix Complete ===");

return 0;
