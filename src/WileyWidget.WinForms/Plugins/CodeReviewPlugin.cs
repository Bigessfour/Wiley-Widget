using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.SemanticKernel;

namespace WileyWidget.WinForms.Plugins
{
    /// <summary>
    /// Provides safe, read-only code review helpers for kernel-based agents.
    /// All operations are restricted to paths under the solution root (directory containing WileyWidget.sln).
    /// </summary>
    public sealed class CodeReviewPlugin
    {
        private const string SolutionFileName = "WileyWidget.sln";

        private static readonly string[] ExcludedDirectoryNames =
        {
            ".git",
            ".vs",
            ".vsdbg",
            ".symbols",
            "bin",
            "obj",
            "node_modules",
            "TestResults",
            "logs",
            "tmp",
        };

        private static readonly Lazy<string> SolutionRootFull = new(GetSolutionRootFull);

        private static string GetSolutionRootFull()
        {
            var envRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT");
            if (!string.IsNullOrWhiteSpace(envRoot))
            {
                var full = Path.GetFullPath(envRoot);
                if (Directory.Exists(full))
                {
                    return full;
                }
            }

            var start = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory ?? ".");
            var current = new DirectoryInfo(start);

            while (current != null)
            {
                var slnPath = Path.Combine(current.FullName, SolutionFileName);
                if (File.Exists(slnPath))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            // Fallback: restrict to app base directory if solution root cannot be found.
            return start;
        }

        private static bool IsExcludedDirectory(string directoryFullPath)
        {
            var name = Path.GetFileName(directoryFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return ExcludedDirectoryNames.Any(ex => string.Equals(ex, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolveUnderSolutionRoot(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Path must be provided.", nameof(relativePath));
            }

            if (Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException("Path must be relative to the solution root.", nameof(relativePath));
            }

            var root = SolutionRootFull.Value;
            var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
            var rootFull = Path.GetFullPath(root) + Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Requested path is outside the solution root.", nameof(relativePath));
            }

            return candidate;
        }

        private static IEnumerable<string> EnumerateFilesRecursive(string startDirectoryFullPath, string searchPattern)
        {
            var stack = new Stack<string>();
            stack.Push(startDirectoryFullPath);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();

                if (IsExcludedDirectory(dir))
                {
                    continue;
                }

                try
                {
                    var dirAttributes = File.GetAttributes(dir);
                    if ((dirAttributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(dir, searchPattern, SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                {
                    yield return file;
                }

                string[] subdirs;
                try
                {
                    subdirs = Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    continue;
                }

                foreach (var subdir in subdirs)
                {
                    stack.Push(subdir);
                }
            }
        }

        private static bool IsSearchableTextFile(string fullPath)
        {
            var ext = Path.GetExtension(fullPath);
            return ext.Equals(".cs", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".props", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".targets", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".json", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".yml", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".md", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".ps1", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".py", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".sql", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".ts", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".js", StringComparison.OrdinalIgnoreCase);
        }

        private static string TrimForDisplay(string line)
        {
            if (line == null) return string.Empty;

            var trimmed = line.Trim();
            const int MaxChars = 200;
            return trimmed.Length <= MaxChars ? trimmed : trimmed.Substring(0, MaxChars);
        }

        [KernelFunction("read_file")]
        [Description("Read a file relative to the solution root (directory containing WileyWidget.sln).")]
        public string ReadFile([Description("Relative file path under solution root") ] string path)
        {
            var fullPath = ResolveUnderSolutionRoot(path);

            if (Directory.Exists(fullPath))
            {
                throw new InvalidOperationException("Requested path refers to a directory.");
            }

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: '{path}'", fullPath);
            }

            const long MaxFileBytes = 5L * 1024 * 1024; // 5MB
            var info = new FileInfo(fullPath);
            if (info.Length > MaxFileBytes)
            {
                throw new InvalidOperationException("File is too large to be read by this plugin.");
            }

            return File.ReadAllText(fullPath);
        }

        [KernelFunction("list_files")]
        [Description("List files matching a wildcard pattern under the solution root (supports '*' and '?'; optional relative directory prefix like 'src/*.cs').")]
        public string[] ListFiles([Description("Wildcard pattern (e.g., '*.cs' or 'src/*.cs')") ] string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("Pattern must be provided.", nameof(pattern));
            }

            var normalized = pattern.Replace('\\', '/');
            var lastSlash = normalized.LastIndexOf('/');

            var directoryPart = lastSlash >= 0 ? normalized.Substring(0, lastSlash) : string.Empty;
            var filePattern = lastSlash >= 0 ? normalized.Substring(lastSlash + 1) : normalized;

            if (string.IsNullOrWhiteSpace(filePattern))
            {
                throw new ArgumentException("Pattern must include a file wildcard, e.g. '*.cs'.", nameof(pattern));
            }

            if (directoryPart.Contains('*', StringComparison.Ordinal) || directoryPart.Contains('?', StringComparison.Ordinal))
            {
                throw new ArgumentException("Directory portion of the pattern must not contain wildcards.", nameof(pattern));
            }

            var root = SolutionRootFull.Value;
            var startDir = string.IsNullOrWhiteSpace(directoryPart)
                ? root
                : ResolveUnderSolutionRoot(directoryPart.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(startDir))
            {
                throw new DirectoryNotFoundException($"Directory not found: '{directoryPart}'");
            }

            var files = EnumerateFilesRecursive(startDir, filePattern)
                .Select(f => Path.GetRelativePath(root, f))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Take(500)
                .ToArray();

            return files;
        }

        [KernelFunction("search_code")]
        [Description("Search for a text query across common source/config files under the solution root. Returns up to 200 matches as 'path:line: text'.")]
        public string[] SearchCode([Description("Text to search for (case-insensitive)") ] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Query must be provided.", nameof(query));
            }

            var root = SolutionRootFull.Value;

            const int MaxMatches = 200;
            const int MaxFilesScanned = 3000;
            const long MaxSearchFileBytes = 1L * 1024 * 1024; // 1MB

            var results = new List<string>(capacity: Math.Min(50, MaxMatches));
            var filesScanned = 0;

            foreach (var file in EnumerateFilesRecursive(root, "*.*"))
            {
                if (filesScanned >= MaxFilesScanned || results.Count >= MaxMatches)
                {
                    break;
                }

                if (!IsSearchableTextFile(file))
                {
                    continue;
                }

                FileInfo info;
                try
                {
                    info = new FileInfo(file);
                }
                catch
                {
                    continue;
                }

                if (!info.Exists || info.Length > MaxSearchFileBytes)
                {
                    continue;
                }

                filesScanned++;

                var relative = Path.GetRelativePath(root, file);

                try
                {
                    var lineNumber = 0;
                    foreach (var line in File.ReadLines(file))
                    {
                        lineNumber++;

                        if (results.Count >= MaxMatches)
                        {
                            break;
                        }

                        if (line.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            results.Add($"{relative}:{lineNumber}: {TrimForDisplay(line)}");
                        }
                    }
                }
                catch
                {
                    // Ignore file read failures; keep searching.
                }
            }

            return results.ToArray();
        }
    }
}
