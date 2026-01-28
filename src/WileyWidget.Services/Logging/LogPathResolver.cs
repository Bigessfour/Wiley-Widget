using System;
using System.IO;

namespace WileyWidget.Services.Logging
{
    /// <summary>
    /// Resolves a consistent logs directory for the workspace and deployed runs.
    /// Prefers the solution root (WileyWidget.sln) when available.
    /// </summary>
    public static class LogPathResolver
    {
        public static string GetLogsDirectory()
        {
            var repoRoot = FindRepoRoot(new DirectoryInfo(Directory.GetCurrentDirectory()))
                ?? FindRepoRoot(new DirectoryInfo(AppContext.BaseDirectory));

            var baseDir = repoRoot?.FullName ?? Directory.GetCurrentDirectory();
            var logsDir = Path.Combine(baseDir, "log");
            Directory.CreateDirectory(logsDir);
            return logsDir;
        }

        private static DirectoryInfo? FindRepoRoot(DirectoryInfo? start)
        {
            while (start != null)
            {
                var solutionPath = Path.Combine(start.FullName, "WileyWidget.sln");
                if (File.Exists(solutionPath))
                {
                    return start;
                }

                start = start.Parent;
            }

            return null;
        }
    }
}
