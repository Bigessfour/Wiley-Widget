#r "nuget:System.Text.RegularExpressions, 4.3.1"
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

Console.WriteLine("[32] Dashboard Navigation Analyzer - START");

var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT");
if (string.IsNullOrWhiteSpace(repoRoot))
{
    repoRoot = Directory.GetCurrentDirectory();
}
var logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");
Directory.CreateDirectory(logsDir);

var vmPath = Path.Combine(repoRoot, "WileyWidget.UI", "ViewModels", "Main", "DashboardViewModel.cs");
var testsPath = Path.Combine(repoRoot, "WileyWidget.Tests", "WileyWidget.ViewModels.Tests", "DashboardViewModelTests.cs");

if (!File.Exists(vmPath))
{
    Console.WriteLine($"ERROR: Target file not found: {vmPath}");
    Environment.Exit(2);
}

var vmSource = File.ReadAllText(vmPath);
var testsSource = File.Exists(testsPath) ? File.ReadAllText(testsPath) : string.Empty;

// Regex to extract SafeRequestNavigate("Region", "Target", fallbackTarget: "Fallback") with optional fallback
var pattern = new Regex("SafeRequestNavigate\\(\\s*\"([^\"]+)\"\\s*,\\s*\"([^\"]+)\"(?:\\s*,\\s*fallbackTarget\\s*:\\s*\"([^\"]+)\")?\\s*\\)", RegexOptions.Compiled);
var matches = pattern.Matches(vmSource);

var entries = new List<object>();
var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (Match m in matches)
{
    var region = m.Groups[1].Value;
    var target = m.Groups[2].Value;
    var fallback = m.Groups[3].Success ? m.Groups[3].Value : "DashboardView";
    var key = $"{region}|{target}|{fallback}";
    if (seen.Contains(key)) continue;
    seen.Add(key);

    var existsInTests = !string.IsNullOrEmpty(testsSource) && (testsSource.Contains(target) || testsSource.Contains(fallback));

    entries.Add(new {
        region,
        target,
        fallback,
        existsInTests
    });
}

var result = new {
    analyzedAt = DateTime.UtcNow,
    vmPath,
    testsPath = File.Exists(testsPath) ? testsPath : null,
    navigationTargets = entries,
};

var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions{ WriteIndented = true });
var outFile = Path.Combine(logsDir, "dashboard-navigation-params.json");
File.WriteAllText(outFile, json);

Console.WriteLine("[32] Analysis Output -> " + outFile);
Console.WriteLine(json);
Console.WriteLine("[32] Dashboard Navigation Analyzer - END");
