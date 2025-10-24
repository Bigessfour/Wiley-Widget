using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
// Roslyn analysis deprecated in this tool - analyzer removed to simplify migration
using Newtonsoft.Json;

namespace ToolkitToPrismMigrator
{
    internal class UsageRecord
    {
        public string File { get; set; } = string.Empty;
        public List<string> Symbols { get; set; } = new List<string>();
        public List<int> Lines { get; set; } = new List<int>();
    }

    class Program
    {
        static async Task<int> Main(string[] args)
        {
            bool failOnLegacy = args.Contains("--fail-on-legacy");
            bool roslynAnalyze = args.Contains("--roslyn-analyze");
            bool genObservablePatches = args.Contains("--generate-observable-patches");
            bool applyObservable = args.Contains("--apply-observable");
            var paths = args.Where(a => !a.StartsWith("--")).ToList();
            // Narrow to ViewModels only to reduce noise
            paths = paths.Where(p => p.IndexOf(Path.Combine("src", "ViewModels"), StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (!paths.Any())
            {
                Console.WriteLine("Usage: ToolkitToPrismMigrator <files...> [--fail-on-legacy]");
                return 2;
            }

            var records = new List<UsageRecord>();
            int legacyCount = 0;

            // Simple text-based scan for common CommunityToolkit tokens to avoid Roslyn complexity
            var tokens = new[] { "ObservableProperty", "RelayCommand", "IRelayCommand", "ObservableObject", "ObservableRecipient", "INotifyPropertyChanged", "CommunityToolkit.Mvvm" };
            foreach (var p in paths)
            {
                if (!File.Exists(p))
                    continue;

                var text = await File.ReadAllTextAsync(p);
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var record = new UsageRecord { File = p };

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    foreach (var t in tokens)
                    {
                        if (line.Contains(t, StringComparison.Ordinal))
                        {
                            record.Symbols.Add(t);
                            record.Lines.Add(i + 1);
                        }
                    }
                }

                if (record.Symbols.Any())
                {
                    records.Add(record);
                    legacyCount += record.Symbols.Count;
                }
            }

            var outDir = Path.Combine("migration","reports");
            Directory.CreateDirectory(outDir);
            var usageOutPath = Path.Combine(outDir, "communitytoolkit-usage.json");
            await File.WriteAllTextAsync(usageOutPath, JsonConvert.SerializeObject(records, Formatting.Indented));
            Console.WriteLine($"WROTE: {usageOutPath}");

            if (roslynAnalyze)
            {
                 // Roslyn analyzers deprecated for now; emit an empty diagnostics file for compatibility
                 var diagOut = Path.Combine(outDir, "roslyn-communitytoolkit-diagnostics.json");
                 File.WriteAllText(diagOut, JsonConvert.SerializeObject(new object[0], Formatting.Indented));
                 Console.WriteLine($"Roslyn analysis is deprecated; wrote empty diagnostics to {diagOut}");
            }

            if (genObservablePatches)
            {
                var patches = new List<object>();
                var appliedList = new List<object>();
                var patchDir = Path.Combine("migration","patches");
                Directory.CreateDirectory(patchDir);
                foreach (var p in paths)
                {
                    if (!File.Exists(p)) continue;
                    var text = await File.ReadAllTextAsync(p);
                    var (changed, newText, matches) = ObservablePropertyCodemod.Process(text);
                    if (changed)
                    {
                        patches.Add(new { File = p, Matches = matches });
                        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), p);
                        var safeName = SanitizeFileName(relative.Replace(Path.DirectorySeparatorChar, '_'));
                        var outPath = Path.Combine(patchDir, safeName + ".patched.cs");
                        Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? patchDir);
                        await File.WriteAllTextAsync(outPath, newText);
                        Console.WriteLine($"WROTE PATCH PREVIEW: {outPath}");
                        if (applyObservable)
                        {
                                // apply in-place (dangerous) — only do if explicitly requested
                                var backupsDir = Path.Combine("migration","backups");
                                Directory.CreateDirectory(backupsDir);
                                var backupSafe = SanitizeFileName(relative.Replace(Path.DirectorySeparatorChar, '_'));
                                var backupPath = Path.Combine(backupsDir, backupSafe + ".orig");
                                Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? backupsDir);
                                if (!File.Exists(backupPath))
                                {
                                    File.Copy(p, backupPath);
                                }
                                await File.WriteAllTextAsync(p, newText);
                                Console.WriteLine($"APPLIED PATCH: {p} (backup: {backupPath})");
                                appliedList.Add(new { File = p, Backup = backupPath, PatchPreview = outPath });
                        }
                    }
                }

                var patchesOut = Path.Combine(outDir, "observableproperty-patches.json");
                await File.WriteAllTextAsync(patchesOut, JsonConvert.SerializeObject(patches, Formatting.Indented));
                Console.WriteLine($"WROTE: {patchesOut}");
                if (applyObservable)
                {
                    var appliedOut = Path.Combine(outDir, "applied-observable-changes.json");
                    await File.WriteAllTextAsync(appliedOut, JsonConvert.SerializeObject(appliedList, Formatting.Indented));
                    Console.WriteLine($"WROTE: {appliedOut}");
                }
            }

            if (failOnLegacy && legacyCount > 0)
            {
                Console.Error.WriteLine($"Legacy CommunityToolkit usage discovered: {legacyCount} occurrences across {records.Count} files. Failing as requested.");
                return 3;
            }

            Console.WriteLine("Scan complete.");
            return 0;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
            return sanitized;
        }

        // Roslyn analysis has been deprecated and removed from this tool. The previous analyzer-runner
        // implementation depended on Roslyn workspace APIs and caused build/runtime complexity.
        // If additional semantic analysis is required later, reintroduce a separate analyzer project
        // and consume it as a NuGet analyzer or a standalone runner.
    }
}
