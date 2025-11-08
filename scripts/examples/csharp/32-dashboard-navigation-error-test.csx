using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;

Console.WriteLine("[32] Dashboard Navigation Error Coverage Check - START");

var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT");
if (string.IsNullOrWhiteSpace(repoRoot)) repoRoot = Directory.GetCurrentDirectory();
var logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");
Directory.CreateDirectory(logsDir);

// Locate latest Cobertura-style coverage file (from dotnet test run)
var testResultsDir = Path.Combine(repoRoot, "TestResults");
if (!Directory.Exists(testResultsDir))
{
    Console.WriteLine("No TestResults directory found. Run dotnet test with coverage first.");
    Environment.Exit(2);
}

// Support multiple common filenames produced by coverlet
var coberturaPaths = Directory
    .EnumerateFiles(testResultsDir, "*", SearchOption.AllDirectories)
    .Where(p => p.EndsWith("Cobertura.xml", StringComparison.OrdinalIgnoreCase)
             || p.EndsWith("coverage.cobertura.xml", StringComparison.OrdinalIgnoreCase)
             || p.EndsWith("cobertura.xml", StringComparison.OrdinalIgnoreCase))
    .OrderByDescending(File.GetLastWriteTimeUtc)
    .ToList();
if (coberturaPaths.Count == 0)
{
    Console.WriteLine("No Cobertura.xml found under TestResults. Ensure coverage collection is enabled.");
    Environment.Exit(3);
}

var cobertura = coberturaPaths.First();
Console.WriteLine($"Using coverage report: {cobertura}");

// Read source file to identify SafeRequestNavigate method and catch/fallback lines
var vmPath = Path.Combine(repoRoot, "WileyWidget.UI", "ViewModels", "Main", "DashboardViewModel.cs");
if (!File.Exists(vmPath)) { Console.WriteLine($"Missing VM file: {vmPath}"); Environment.Exit(4); }
var sourceLines = File.ReadAllLines(vmPath);

int methodStart = -1;
int methodEnd = -1;
for (int i = 0; i < sourceLines.Length; i++)
{
    if (sourceLines[i].Contains("private void SafeRequestNavigate("))
    {
        methodStart = i + 1; // 1-based for coverage
        // naive method end: find next line starting at same indentation with "private void TryFallback" or end of file
        for (int j = i + 1; j < sourceLines.Length; j++)
        {
            if (sourceLines[j].Contains("private void TryFallback(")) { methodEnd = j; break; }
        }
        if (methodEnd < 0) methodEnd = sourceLines.Length;
        break;
    }
}

if (methodStart < 0)
{
    Console.WriteLine("SafeRequestNavigate method not found in source.");
    Environment.Exit(5);
}

// Identify a few sentinel lines within the method that represent error handling
var catchLine = Array.FindIndex(sourceLines, methodStart - 1, methodEnd - methodStart, l => l.Contains("catch (Exception ex)"));
var fallbackLine = Array.FindIndex(sourceLines, methodStart - 1, methodEnd - methodStart, l => l.Contains("TryFallback(regionName, fallbackTarget)"));

if (catchLine >= 0) catchLine = catchLine + 1; // to 1-based
if (fallbackLine >= 0) fallbackLine = fallbackLine + 1;

// Parse Cobertura.xml
var doc = XDocument.Load(cobertura);

var classes = doc.Descendants("class");
var vmClass = classes.FirstOrDefault(c => {
    var fileAttr = (string)c.Attribute("filename");
    if (fileAttr == null) return false;
    var normalized = fileAttr.Replace('/', '\\');
    return normalized.EndsWith("WileyWidget.UI\\ViewModels\\Main\\DashboardViewModel.cs", StringComparison.OrdinalIgnoreCase);
});
if (vmClass == null)
{
    Console.WriteLine("DashboardViewModel not present in coverage report.");
    Environment.Exit(6);
}

var lines = vmClass.Descendants("line")
    .Select(x => new {
        number = int.Parse((string)x.Attribute("number")!),
        hits = int.Parse((string)x.Attribute("hits")!)
    })
    .ToList();

bool methodCovered = lines.Any(l => l.number >= methodStart && l.number < methodEnd && l.hits > 0);
bool catchCovered = catchLine > 0 && lines.Any(l => l.number == catchLine && l.hits > 0);
bool fallbackCovered = fallbackLine > 0 && lines.Any(l => l.number == fallbackLine && l.hits > 0);

var resultJson = $@"{{
  ""methodStart"": {methodStart},
  ""methodEnd"": {methodEnd},
  ""catchLine"": {catchLine},
  ""fallbackLine"": {fallbackLine},
  ""methodCovered"": {methodCovered.ToString().ToLower()},
  ""catchCovered"": {catchCovered.ToString().ToLower()},
  ""fallbackCovered"": {fallbackCovered.ToString().ToLower()}
}}";

var outFile = Path.Combine(logsDir, "dashboard-navigation-error-coverage.json");
File.WriteAllText(outFile, resultJson);
Console.WriteLine("Coverage check: " + resultJson);

if (!methodCovered || !catchCovered || !fallbackCovered)
{
    Console.WriteLine("[32] ERROR: Error-handling branches are not fully covered.");
    Environment.Exit(7);
}

Console.WriteLine("[32] Dashboard Navigation Error Coverage Check - PASS");
