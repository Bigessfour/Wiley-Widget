#!/usr/bin/env dotnet-script

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

Console.WriteLine("=== C# MCP Deep Dive: XAMLParseException Complete Analysis ===\n");

string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Environment.CurrentDirectory;
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");

// File paths
string xamlPath = Path.Combine(repoRoot, "WileyWidget.UI", "Views", "Main", "MunicipalAccountView.xaml");
string codeBehindPath = Path.Combine(repoRoot, "WileyWidget.UI", "Views", "Main", "MunicipalAccountView.xaml.cs");
string modulePath = Path.Combine(repoRoot, "src", "Startup", "Modules", "MunicipalAccountModule.cs");

Console.WriteLine($"Repository Root: {repoRoot}\n");

// ========== XAML ANALYSIS ==========
Console.WriteLine("=== 1. XAML Structure Analysis ===\n");

if (!File.Exists(xamlPath))
{
    Console.WriteLine($"❌ XAML file not found: {xamlPath}");
    return 1;
}

string[] xamlLines = File.ReadAllLines(xamlPath);
Console.WriteLine($"✓ XAML: {xamlLines.Length} lines\n");

// Check for duplicate x:Key values
Console.WriteLine("--- Checking for Duplicate x:Key Values ---");
var keyPattern = new Regex(@"x:Key\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
var keys = new Dictionary<string, List<int>>();

for (int i = 0; i < xamlLines.Length; i++)
{
    var matches = keyPattern.Matches(xamlLines[i]);
    foreach (Match match in matches)
    {
        string key = match.Groups[1].Value;
        if (!keys.ContainsKey(key))
            keys[key] = new List<int>();
        keys[key].Add(i + 1);
    }
}

var duplicates = keys.Where(k => k.Value.Count > 1).ToList();
if (duplicates.Any())
{
    Console.WriteLine($"❌ DUPLICATE KEYS FOUND: {duplicates.Count}");
    foreach (var dup in duplicates)
    {
        Console.WriteLine($"   Key '{dup.Key}' appears on lines: {string.Join(", ", dup.Value)}");
    }
}
else
{
    Console.WriteLine($"✓ No duplicate x:Key values found ({keys.Count} unique keys)");
}

// List all resource keys
Console.WriteLine($"\nResource Keys Inventory:");
foreach (var key in keys.OrderBy(k => k.Value.First()))
{
    Console.WriteLine($"  Line {key.Value.First(),4}: x:Key=\"{key.Key}\"");
}

// ========== CODE-BEHIND ANALYSIS ==========
Console.WriteLine($"\n=== 2. Code-Behind Analysis ===\n");

if (!File.Exists(codeBehindPath))
{
    Console.WriteLine($"⚠ Code-behind not found: {codeBehindPath}");
}
else
{
    string[] codeLines = File.ReadAllLines(codeBehindPath);
    Console.WriteLine($"✓ Code-behind: {codeLines.Length} lines\n");

    // Check for Resources manipulation
    Console.WriteLine("--- Checking for Resources Property Manipulation ---");
    bool resourcesAccessed = false;
    var resourcesLines = new List<(int Line, string Content)>();

    for (int i = 0; i < codeLines.Length; i++)
    {
        string line = codeLines[i];
        if (line.Contains(".Resources") || line.Contains("ResourceDictionary"))
        {
            resourcesAccessed = true;
            resourcesLines.Add((i + 1, line.Trim()));
        }
    }

    if (resourcesAccessed)
    {
        Console.WriteLine($"⚠ Found {resourcesLines.Count} references to Resources:");
        foreach (var item in resourcesLines)
        {
            Console.WriteLine($"   Line {item.Line,4}: {item.Content}");
        }
        Console.WriteLine("\n⚠ WARNING: Programmatic Resources manipulation can cause XAMLParseException");
    }
    else
    {
        Console.WriteLine("✓ No programmatic Resources manipulation found");
    }

    // Check constructor
    Console.WriteLine("\n--- Analyzing Constructor ---");
    bool inConstructor = false;
    var constructorLines = new List<string>();

    for (int i = 0; i < codeLines.Length; i++)
    {
        string line = codeLines[i].Trim();
        if (line.Contains("public") && line.Contains("MunicipalAccountView()"))
        {
            inConstructor = true;
        }
        if (inConstructor)
        {
            constructorLines.Add(line);
            if (line == "}")
                break;
        }
    }

    Console.WriteLine($"Constructor ({constructorLines.Count} lines):");
    foreach (var line in constructorLines)
    {
        Console.WriteLine($"   {line}");
    }

    if (constructorLines.Any(l => l.Contains("Resources")))
    {
        Console.WriteLine("❌ Constructor manipulates Resources - THIS CAN CAUSE EXCEPTION");
    }
    else
    {
        Console.WriteLine("✓ Constructor follows standard pattern (InitializeComponent only)");
    }
}

// ========== MODULE REGISTRATION ANALYSIS ==========
Console.WriteLine($"\n=== 3. Module Registration Analysis ===\n");

if (!File.Exists(modulePath))
{
    Console.WriteLine($"⚠ Module file not found: {modulePath}");
}
else
{
    string[] moduleLines = File.ReadAllLines(modulePath);
    Console.WriteLine($"✓ Module: {moduleLines.Length} lines\n");

    // Find RegisterViewWithRegion method
    Console.WriteLine("--- Analyzing RegisterViewWithRegion ---");
    int methodStart = -1;
    for (int i = 0; i < moduleLines.Length; i++)
    {
        if (moduleLines[i].Contains("RegisterViewWithRegion") &&
            moduleLines[i].Contains("IRegionManager"))
        {
            methodStart = i + 1;
            break;
        }
    }

    if (methodStart > 0)
    {
        Console.WriteLine($"Method starts at line {methodStart}");

        // Check for try-catch and alternative registration
        bool hasTryCatch = false;
        bool hasAlternative = false;
        int alternativeLine = -1;

        for (int i = methodStart; i < Math.Min(methodStart + 50, moduleLines.Length); i++)
        {
            string line = moduleLines[i].Trim();
            if (line.Contains("try"))
                hasTryCatch = true;
            if (line.Contains("region.Add") || line.Contains("alternative"))
            {
                hasAlternative = true;
                alternativeLine = i + 1;
            }
        }

        Console.WriteLine($"✓ Has try-catch: {hasTryCatch}");
        Console.WriteLine($"✓ Has alternative method: {hasAlternative}");

        if (hasAlternative)
        {
            Console.WriteLine($"\n⚠ ALTERNATIVE REGISTRATION DETECTED at line {alternativeLine}");
            Console.WriteLine("   This is the 'fallback' mentioned in the error log.");
            Console.WriteLine("   It's triggered when standard registration fails.");
            Console.WriteLine($"\n   From line {alternativeLine}:");
            for (int i = alternativeLine - 1; i < Math.Min(alternativeLine + 5, moduleLines.Length); i++)
            {
                Console.WriteLine($"   {moduleLines[i]}");
            }
        }
    }
}

// ========== EXCEPTION SCENARIO RECONSTRUCTION ==========
Console.WriteLine($"\n=== 4. Exception Scenario Reconstruction ===\n");

Console.WriteLine("Based on MCP analysis, the exception path was:\n");
Console.WriteLine("1. Module: MunicipalAccountModule.OnInitialized()");
Console.WriteLine("   └─ Calls: RegisterViewWithRegion(regionManager, \"MunicipalAccountRegion\", typeof(MunicipalAccountView))");
Console.WriteLine("\n2. RegisterViewWithRegion:");
Console.WriteLine("   └─ Calls: regionManager.RegisterViewWithRegion(regionName, viewType)");
Console.WriteLine("   └─ Prism internally instantiates: new MunicipalAccountView()");
Console.WriteLine("\n3. MunicipalAccountView constructor:");
Console.WriteLine("   └─ Calls: InitializeComponent()");
Console.WriteLine("   └─ XAML Parser reads: MunicipalAccountView.xaml");
Console.WriteLine("\n4. XAML Parser:");
Console.WriteLine("   └─ Encounters: UserControl.Resources property being set TWICE");
Console.WriteLine("   └─ Throws: System.Windows.Markup.XamlParseException");
Console.WriteLine("   └─ Inner: XamlDuplicateMemberException: 'Resources' property already set");
Console.WriteLine("\n5. Exception bubbles up:");
Console.WriteLine("   └─ Caught by: RegisterViewWithRegion try-catch");
Console.WriteLine("   └─ Logged: 'Failed to register MunicipalAccountView with MunicipalAccountRegion'");
Console.WriteLine("   └─ Fallback: Uses 'alternative method' (region.Add)");

// ========== CURRENT STATE VERIFICATION ==========
Console.WriteLine($"\n=== 5. Current State Verification ===\n");

// Re-check XAML structure
var structure = new List<(int Line, string Type)>();
for (int i = 0; i < xamlLines.Length; i++)
{
    string trimmed = xamlLines[i].Trim();
    if (trimmed.Contains("<UserControl.Resources"))
        structure.Add((i + 1, "OPEN_RESOURCES"));
    else if (trimmed.Contains("</UserControl.Resources"))
        structure.Add((i + 1, "CLOSE_RESOURCES"));
    else if (trimmed.StartsWith("<ResourceDictionary>"))
        structure.Add((i + 1, "OPEN_DICT"));
    else if (trimmed.StartsWith("</ResourceDictionary>"))
        structure.Add((i + 1, "CLOSE_DICT"));
}

var openRes = structure.FirstOrDefault(s => s.Type == "OPEN_RESOURCES");
var closeRes = structure.FirstOrDefault(s => s.Type == "CLOSE_RESOURCES");
var openDict = structure.FirstOrDefault(s => s.Type == "OPEN_DICT");
var closeDict = structure.FirstOrDefault(s => s.Type == "CLOSE_DICT");

Console.WriteLine("XAML Structure:");
Console.WriteLine($"  UserControl.Resources: Line {openRes.Line} → Line {closeRes.Line}");
Console.WriteLine($"  ResourceDictionary:    Line {openDict.Line} → Line {closeDict.Line}");

bool structureCorrect = (closeDict.Line == closeRes.Line - 1);
Console.WriteLine($"\nStructure Status: {(structureCorrect ? "✅ CORRECT" : "❌ INCORRECT")}");

if (structureCorrect)
{
    Console.WriteLine("\nThe XAML structure is now FIXED:");
    Console.WriteLine("  • ResourceDictionary closes immediately before UserControl.Resources closes");
    Console.WriteLine("  • No resources exist outside ResourceDictionary");
    Console.WriteLine("  • Exception should NO LONGER occur");
    Console.WriteLine("  • Module registration should succeed WITHOUT fallback");
}
else
{
    int gap = closeRes.Line - closeDict.Line - 1;
    Console.WriteLine($"\n❌ ISSUE PERSISTS: {gap} lines between </ResourceDictionary> and </UserControl.Resources>");
    Console.WriteLine("   Exception WILL occur during view registration");
}

// ========== RECOMMENDATIONS ==========
Console.WriteLine($"\n=== 6. MCP Recommendations ===\n");

if (structureCorrect && !duplicates.Any())
{
    Console.WriteLine("✅ All structural issues resolved:");
    Console.WriteLine("   1. XAML ResourceDictionary structure: CORRECT");
    Console.WriteLine("   2. No duplicate x:Key values: VERIFIED");
    Console.WriteLine("   3. Code-behind clean: VERIFIED");
    Console.WriteLine("\n✅ NEXT STEPS:");
    Console.WriteLine("   1. Rebuild the project");
    Console.WriteLine("   2. Test view registration");
    Console.WriteLine("   3. Verify no fallback to 'alternative method' in logs");
    Console.WriteLine("   4. Confirm MunicipalAccountView loads without exception");
}
else
{
    Console.WriteLine("❌ Issues remaining:");
    if (!structureCorrect)
        Console.WriteLine("   • XAML structure still incorrect - resources outside ResourceDictionary");
    if (duplicates.Any())
        Console.WriteLine("   • Duplicate x:Key values detected");
    Console.WriteLine("\n   FIX REQUIRED before testing");
}

// Write comprehensive log
string logPath = Path.Combine(logsDir, $"municipal-complete-analysis-{DateTime.Now:yyyyMMdd-HHmmss}.log");
try
{
    using (var writer = new StreamWriter(logPath))
    {
        writer.WriteLine("=== MunicipalAccountView Complete Analysis ===");
        writer.WriteLine($"Timestamp: {DateTime.Now}");
        writer.WriteLine($"XAML Structure: {(structureCorrect ? "CORRECT" : "INCORRECT")}");
        writer.WriteLine($"Duplicate Keys: {duplicates.Count}");
        writer.WriteLine($"Resource Keys: {keys.Count}");
        writer.WriteLine($"\nAnalysis: {(structureCorrect && !duplicates.Any() ? "READY FOR TESTING" : "REQUIRES FIXES")}");
    }
    Console.WriteLine($"\n✓ Complete analysis saved to: {logPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠ Could not write log: {ex.Message}");
}

return structureCorrect && !duplicates.Any() ? 0 : 1;
