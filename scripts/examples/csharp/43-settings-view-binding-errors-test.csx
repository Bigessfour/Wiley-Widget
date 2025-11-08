// SettingsView Binding Errors Test
// Purpose: Scan provided startup log for WPF binding errors related to SettingsView/SettingsViewModel

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

#nullable enable

Console.WriteLine("=== SettingsView Binding Errors Test ===\n");

string repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? Directory.GetCurrentDirectory();
string logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? Path.Combine(repoRoot, "logs");
string? explicitLog = Environment.GetEnvironmentVariable("WW_LOG_PATH");
if (string.IsNullOrWhiteSpace(explicitLog) && Environment.GetCommandLineArgs().Length > 1)
{
    var argv = Environment.GetCommandLineArgs().Skip(1).ToArray();
    if (argv.Length > 0) explicitLog = argv[0];
}
if (!Directory.Exists(logsDir)) { Console.WriteLine($"Logs dir not found: {logsDir}"); Environment.Exit(2); }

string logPath;
if (!string.IsNullOrWhiteSpace(explicitLog))
{
    logPath = Path.IsPathRooted(explicitLog!) ? explicitLog! : Path.Combine(logsDir, explicitLog!);
    if (!File.Exists(logPath)) { Console.WriteLine($"Specified log not found: {logPath}"); Environment.Exit(2); }
}
else
{
    var latest = Directory.EnumerateFiles(logsDir, "startup-*.log").Concat(Directory.EnumerateFiles(logsDir, "wiley-widget-*.log"))
        .Select(f => new FileInfo(f)).OrderByDescending(fi => fi.LastWriteTimeUtc).FirstOrDefault();
    if (latest == null) { Console.WriteLine("No logs found"); Environment.Exit(2); }
    logPath = latest.FullName;
}
Console.WriteLine($"Using log: {logPath}\n");

var bindingError = new Regex(@"BindingExpression.*(SettingsView|SettingsViewModel).*error", RegexOptions.IgnoreCase);
int errors = 0; int lines = 0;
using (var reader = new StreamReader(logPath))
{
    string? line;
    while ((line = reader.ReadLine()) != null)
    {
        lines++;
        if (bindingError.IsMatch(line))
        {
            errors++;
            Console.WriteLine("ERR> " + line);
        }
    }
}

int pass = 0, total = 1;
void Assert(bool cond, string name){ total++; if (cond){ Console.WriteLine("✓ " + name); pass++; } else { Console.WriteLine("✗ " + name); }}
Assert(errors == 0, "No binding errors for SettingsView/SettingsViewModel");

Console.WriteLine($"\nResults: {pass}/{total} passed; scanned {lines} lines");
Environment.Exit(errors == 0 ? 0 : 8);
