// View Registration Audit
// Purpose: Scan source for RegisterForNavigation<View, ViewModel> and RegisterViewWithRegion("Region", typeof(View))
// Validate that the referenced View and ViewModel types exist in the repository.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

#nullable enable

Console.WriteLine("[42] View Registration Audit - START\n");

string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");
Directory.CreateDirectory(logsDir);

string[] searchRoots = new[]
{
    Path.Combine(repoRoot, "src"),
    Path.Combine(repoRoot, "WileyWidget.UI"),
};

var navPattern = new Regex(@"RegisterForNavigation<\s*([A-Za-z0-9_\.]+)\s*,\s*([A-Za-z0-9_\.]+)\s*>", RegexOptions.Compiled);
// Use verbatim string; double quotes inside
var regionPattern = new Regex(@"RegisterViewWithRegion\(\s*""([A-Za-z0-9_]+)""\s*,\s*typeof\(\s*([A-Za-z0-9_\.]+)\s*\)\s*\)", RegexOptions.Compiled);

var registrations = new List<(string Kind, string A, string B)>();

foreach (var root in searchRoots.Where(Directory.Exists))
{
    foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
    {
        string src;
        try { src = File.ReadAllText(file); } catch { continue; }

        foreach (Match m in navPattern.Matches(src))
        {
            var view = m.Groups[1].Value;
            var vm = m.Groups[2].Value;
            registrations.Add(("Navigation", view, vm));
        }
        foreach (Match m in regionPattern.Matches(src))
        {
            var region = m.Groups[1].Value;
            var view = m.Groups[2].Value;
            registrations.Add(("Region", view, region));
        }
    }
}

// Simple existence checks by scanning file names for type names
bool TypeExists(string typeName)
{
    string shortName = typeName.Split('.').Last();
    foreach (var root in searchRoots.Where(Directory.Exists))
    {
        var files = Directory.EnumerateFiles(root, shortName + ".cs", SearchOption.AllDirectories);
        if (files.Any()) return true;
    }
    return false;
}

var results = new List<object>();
foreach (var r in registrations.Distinct())
{
    if (r.Kind == "Navigation")
    {
        var viewOk = TypeExists(r.A);
        var vmOk = TypeExists(r.B);
        results.Add(new { kind = r.Kind, view = r.A, viewModel = r.B, viewOk, viewModelOk = vmOk });
    }
    else
    {
        var viewOk = TypeExists(r.A);
        results.Add(new { kind = r.Kind, view = r.A, region = r.B, viewOk });
    }
}

string outFile = Path.Combine(logsDir, "view-registration-audit.json");
var json = System.Text.Json.JsonSerializer.Serialize(new { analyzedAt = DateTime.UtcNow, registrations = results }, new System.Text.Json.JsonSerializerOptions{ WriteIndented = true });
File.WriteAllText(outFile, json);

// Minimal assertions for key views
int pass = 0, total = 0;
void Assert(bool cond, string name){ total++; if (cond){ Console.WriteLine("✓ " + name); pass++; } else { Console.WriteLine("✗ " + name); }}

var resultsText = System.Text.Json.JsonSerializer.Serialize(results);
bool settingsNavOk = resultsText.Contains("SettingsView") && resultsText.Contains("SettingsViewModel");
Assert(settingsNavOk, "SettingsView navigation registration present");

Console.WriteLine("\n[42] Output -> " + outFile);
Console.WriteLine($"Results: {pass}/{total} passed");
Console.WriteLine("\n[42] View Registration Audit - END");

Environment.Exit(pass == total ? 0 : 7);
