using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.SemanticKernel;

namespace WileyWidget.WinForms.Plugins
{
    /// <summary>
    /// Provides safe, read-only filesystem helpers for kernel-based plugins.
    /// Methods are restricted to paths under AppDomain.CurrentDomain.BaseDirectory.
    /// </summary>
    public sealed class FileSystemTools
    {
        private static string GetBaseDirectoryFull()
        {
            return Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory ?? ".");
        }

        private static string ResolvePath(string relativePath)
        {
            var baseDir = GetBaseDirectoryFull();
            var candidate = Path.GetFullPath(Path.Combine(baseDir, relativePath ?? string.Empty));
            if (!candidate.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Requested path is outside the application root.", nameof(relativePath));
            }

            return candidate;
        }

        [KernelFunction("list_directory")]
        [Description("List files and directories within the specified directory (relative to application base directory).")]
        public string[] ListDirectory([Description("Relative directory path (relative to application base directory)")] string path)
        {
            var baseDir = GetBaseDirectoryFull();
            var fullPath = ResolvePath(path ?? string.Empty);

            if (Directory.Exists(fullPath))
            {
                return Directory.EnumerateFileSystemEntries(fullPath)
                    .Select(p => Path.GetRelativePath(baseDir, p))
                    .OrderBy(p => p)
                    .ToArray();
            }

            if (File.Exists(fullPath))
            {
                return new[] { Path.GetRelativePath(baseDir, fullPath) };
            }

            throw new DirectoryNotFoundException($"Path not found: '{path}'");
        }

        [KernelFunction("read_file")]
        [Description("Read the contents of a file relative to the application's base directory.")]
        public string ReadFile([Description("Relative file path (relative to application base directory)")] string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Path must be provided.", nameof(relativePath));
            }

            var fullPath = ResolvePath(relativePath);

            // Reject directory inputs explicitly so call sites don't accidentally attempt to read directories.
            if (Directory.Exists(fullPath))
            {
                throw new InvalidOperationException("Requested path refers to a directory. Use ListDirectory to inspect directories.");
            }

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: '{relativePath}'", fullPath);
            }

            const long MaxFileBytes = 5L * 1024 * 1024; // 5MB
            var info = new FileInfo(fullPath);
            if (info.Length > MaxFileBytes)
            {
                throw new InvalidOperationException("File is too large to be read by this plugin.");
            }

            return File.ReadAllText(fullPath);
        }

        [KernelFunction("read_or_list")]
        [Description("Read the contents of a file or list directory entries (returns newline-separated list for directories).")]
        public string ReadOrList([Description("Relative file or directory path (relative to application base directory)")] string path)
        {
            var baseDir = GetBaseDirectoryFull();
            var safePath = path ?? string.Empty;
            var fullPath = ResolvePath(safePath);

            if (Directory.Exists(fullPath))
            {
                var items = Directory.EnumerateFileSystemEntries(fullPath)
                    .Select(p => Path.GetRelativePath(baseDir, p))
                    .OrderBy(p => p);
                return string.Join(Environment.NewLine, items);
            }

            if (File.Exists(fullPath))
            {
                return ReadFile(safePath);
            }

            throw new FileNotFoundException($"Path not found: '{path}'", fullPath);
        }
    }
}
