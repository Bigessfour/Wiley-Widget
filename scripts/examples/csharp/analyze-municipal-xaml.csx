#!/usr/bin/env dotnet-script
#r "nuget: System.IO, 4.3.0"

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

Console.WriteLine("=== C# MCP Server: MunicipalAccountView.xaml Analysis ===\n");

// Use environment variables set by MCP/Docker context
string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Environment.CurrentDirectory;
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

Console.WriteLine($"WW_REPO_ROOT: {repoRoot}");
Console.WriteLine($"WW_LOGS_DIR: {logsDir}");
Console.WriteLine($"Current Directory: {Environment.CurrentDirectory}\n");

// Build the path to the XAML file
string xamlRelativePath = Path.Combine("WileyWidget.UI", "Views", "Main", "MunicipalAccountView.xaml");
string xamlPath = Path.Combine(repoRoot, xamlRelativePath);

Console.WriteLine($"Target XAML file: {xamlPath}");
Console.WriteLine($"File exists: {File.Exists(xamlPath)}\n");

if (!File.Exists(xamlPath))
{
    Console.WriteLine("❌ ERROR: Cannot locate MunicipalAccountView.xaml");
    Console.WriteLine($"\nSearching in repository root: {repoRoot}");

    // List what's actually available
    if (Directory.Exists(repoRoot))
    {
        Console.WriteLine("\nDirectory structure:");
        try
        {
            var dirs = Directory.GetDirectories(repoRoot, "*", SearchOption.TopDirectoryOnly);
            foreach (var dir in dirs.Take(20))
            {
                Console.WriteLine($"  - {Path.GetFileName(dir)}/");
            }

            // Try to find WileyWidget.UI
            var uiDir = Path.Combine(repoRoot, "WileyWidget.UI");
            if (Directory.Exists(uiDir))
            {
                Console.WriteLine($"\n✓ Found WileyWidget.UI at: {uiDir}");
                var viewsDir = Path.Combine(uiDir, "Views");
                if (Directory.Exists(viewsDir))
                {
                    Console.WriteLine($"✓ Found Views directory");
                    var mainDir = Path.Combine(viewsDir, "Main");
                    if (Directory.Exists(mainDir))
                    {
                        Console.WriteLine($"✓ Found Main directory");
                        var files = Directory.GetFiles(mainDir, "*.xaml");
                        Console.WriteLine($"  XAML files in Main: {files.Length}");
                        foreach (var file in files)
                        {
                            Console.WriteLine($"    - {Path.GetFileName(file)}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"✗ Main directory not found at: {mainDir}");
                    }
                }
                else
                {
                    Console.WriteLine($"✗ Views directory not found at: {viewsDir}");
                }
            }
            else
            {
                Console.WriteLine($"\n✗ WileyWidget.UI not found at: {uiDir}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exploring directories: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine($"✗ Repository root does not exist: {repoRoot}");
    }

    return 1;
}

// File exists - perform deep analysis
Console.WriteLine("=== Reading XAML File ===\n");
string[] lines = File.ReadAllLines(xamlPath);
Console.WriteLine($"✓ Loaded {lines.Length} lines from MunicipalAccountView.xaml\n");

// Structure analysis
var structure = new List<(int LineNum, string Type, string Content)>();

for (int i = 0; i < lines.Length; i++)
{
    string line = lines[i];
    string trimmed = line.Trim();
    int lineNum = i + 1;

    if (trimmed.Contains("<UserControl.Resources"))
        structure.Add((lineNum, "OPEN_RESOURCES", trimmed));
    else if (trimmed.Contains("</UserControl.Resources"))
        structure.Add((lineNum, "CLOSE_RESOURCES", trimmed));
    else if (trimmed.StartsWith("<ResourceDictionary>") && !trimmed.Contains("</ResourceDictionary>"))
        structure.Add((lineNum, "OPEN_DICT", trimmed));
    else if (trimmed.StartsWith("</ResourceDictionary>"))
        structure.Add((lineNum, "CLOSE_DICT", trimmed));
    else if (trimmed.Contains("<ResourceDictionary.MergedDictionaries"))
        structure.Add((lineNum, "OPEN_MERGED", trimmed));
    else if (trimmed.Contains("</ResourceDictionary.MergedDictionaries"))
        structure.Add((lineNum, "CLOSE_MERGED", trimmed));
}

Console.WriteLine("=== XAML Structure Timeline ===");
foreach (var item in structure)
{
    string preview = item.Content.Length > 60 ? item.Content.Substring(0, 60) + "..." : item.Content;
    Console.WriteLine($"Line {item.LineNum,4}: {item.Type,-20} | {preview}");
}

// Find boundaries
var openRes = structure.FirstOrDefault(s => s.Type == "OPEN_RESOURCES");
var closeRes = structure.FirstOrDefault(s => s.Type == "CLOSE_RESOURCES");
var openDict = structure.FirstOrDefault(s => s.Type == "OPEN_DICT");
var closeDict = structure.FirstOrDefault(s => s.Type == "CLOSE_DICT");

Console.WriteLine($"\n=== Boundary Analysis ===");
Console.WriteLine($"UserControl.Resources:  Line {openRes.LineNum} → Line {closeRes.LineNum}");
Console.WriteLine($"ResourceDictionary:     Line {openDict.LineNum} → Line {closeDict.LineNum}");

// CRITICAL CHECK: Is there content between </ResourceDictionary> and </UserControl.Resources>?
Console.WriteLine($"\n=== XAMLParseException Root Cause Analysis ===");

bool issueFound = false;
var problemResources = new List<(int LineNum, string Content, string Type)>();

if (closeDict.LineNum > 0 && closeDict.LineNum < closeRes.LineNum - 1)
{
    Console.WriteLine($"⚠ INVESTIGATION: Checking gap between line {closeDict.LineNum} and {closeRes.LineNum}");
    Console.WriteLine($"   Expected: Only whitespace or comments");
    Console.WriteLine($"   Checking: Lines {closeDict.LineNum + 1} through {closeRes.LineNum - 1}\n");

    for (int i = closeDict.LineNum; i < closeRes.LineNum - 1; i++)
    {
        string line = lines[i].Trim();

        // Skip empty lines and comments
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("<!--"))
            continue;

        issueFound = true;

        // Categorize the resource
        string resourceType = "UNKNOWN";
        if (line.Contains("Converter") && line.Contains("x:Key"))
            resourceType = "CONVERTER";
        else if (line.Contains("<Style") && line.Contains("x:Key"))
            resourceType = "STYLE";
        else if (line.Contains("</Style"))
            resourceType = "STYLE_END";
        else if (line.Contains("<DataTemplate"))
            resourceType = "DATATEMPLATE";
        else if (line.Contains("<Setter"))
            resourceType = "SETTER";

        problemResources.Add((i + 1, line, resourceType));
    }
}

Console.WriteLine($"=== DIAGNOSIS RESULTS ===\n");

if (issueFound)
{
    Console.WriteLine($"❌ ISSUE CONFIRMED: XamlDuplicateMemberException");
    Console.WriteLine($"   {problemResources.Count} resources found OUTSIDE ResourceDictionary\n");

    Console.WriteLine("Problematic resources:");
    foreach (var res in problemResources.Take(10))
    {
        string preview = res.Content.Length > 70 ? res.Content.Substring(0, 70) + "..." : res.Content;
        Console.WriteLine($"  Line {res.LineNum,4} [{res.Type,-12}]: {preview}");
    }

    if (problemResources.Count > 10)
        Console.WriteLine($"  ... and {problemResources.Count - 10} more resources");

    // Count by type
    int converters = problemResources.Count(r => r.Type == "CONVERTER");
    int styles = problemResources.Count(r => r.Type == "STYLE");
    int templates = problemResources.Count(r => r.Type == "DATATEMPLATE");

    Console.WriteLine($"\n=== Resource Summary ===");
    Console.WriteLine($"Converters:    {converters}");
    Console.WriteLine($"Styles:        {styles}");
    Console.WriteLine($"DataTemplates: {templates}");
    Console.WriteLine($"Total:         {problemResources.Count}");

    Console.WriteLine($"\n=== EXCEPTION EXPLANATION ===");
    Console.WriteLine("The XAML parser throws XamlDuplicateMemberException because:");
    Console.WriteLine($"  1. Line {openDict.LineNum}: UserControl.Resources is SET to a ResourceDictionary object");
    Console.WriteLine($"  2. Line {closeDict.LineNum}: ResourceDictionary is CLOSED");
    Console.WriteLine($"  3. Lines {closeDict.LineNum + 1}-{closeRes.LineNum - 1}: Additional resources are added");
    Console.WriteLine("     ❌ This attempts to SET UserControl.Resources AGAIN = EXCEPTION");
    Console.WriteLine("\nIn WPF XAML, once you assign a ResourceDictionary to UserControl.Resources,");
    Console.WriteLine("ALL resources must be INSIDE that ResourceDictionary element.");

    Console.WriteLine($"\n=== REQUIRED FIX ===");
    Console.WriteLine($"Action: Move lines {closeDict.LineNum + 1}-{closeRes.LineNum - 1}");
    Console.WriteLine($"        to BEFORE line {closeDict.LineNum} (before </ResourceDictionary>)");
    Console.WriteLine("\nCorrect structure:");
    Console.WriteLine("  <UserControl.Resources>");
    Console.WriteLine("    <ResourceDictionary>");
    Console.WriteLine("      <ResourceDictionary.MergedDictionaries>...</ResourceDictionary.MergedDictionaries>");
    Console.WriteLine("      <converters:Converter1 .../>");
    Console.WriteLine("      <Style x:Key=\"...\" ...>");
    Console.WriteLine("      <!-- ALL resources here -->");
    Console.WriteLine("    </ResourceDictionary>");
    Console.WriteLine("  </UserControl.Resources>");

    // Write analysis to log file
    string logPath = Path.Combine(logsDir, "municipal-xaml-analysis.log");
    try
    {
        using (var writer = new StreamWriter(logPath))
        {
            writer.WriteLine($"MunicipalAccountView.xaml Analysis - {DateTime.Now}");
            writer.WriteLine($"Issue: XamlDuplicateMemberException");
            writer.WriteLine($"Resources outside ResourceDictionary: {problemResources.Count}");
            writer.WriteLine($"Lines affected: {closeDict.LineNum + 1}-{closeRes.LineNum - 1}");
            writer.WriteLine();
            foreach (var res in problemResources)
            {
                writer.WriteLine($"Line {res.LineNum}: {res.Type} - {res.Content}");
            }
        }
        Console.WriteLine($"\n✓ Detailed analysis saved to: {logPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n⚠ Could not write log: {ex.Message}");
    }

    return 1; // Exit with error code
}
else
{
    Console.WriteLine("✅ STRUCTURE IS CORRECT");
    Console.WriteLine("   No resources found outside ResourceDictionary");
    Console.WriteLine("   XAML structure follows WPF best practices");
    Console.WriteLine("\nIf XAMLParseException still occurs, check:");
    Console.WriteLine("  1. Code-behind (MunicipalAccountView.xaml.cs) for Resources manipulation");
    Console.WriteLine("  2. Duplicate x:Key values within ResourceDictionary");
    Console.WriteLine("  3. Base class or attached behavior conflicts");

    return 0; // Success
}
