using System;
using System.IO;
using System.Linq;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    public static class UiTestConstants
    {
        public const string MainWindowTitle = "Wiley Widget - Municipal Budget Management System";

        public static readonly string[] AccountsPanelTitles =
        {
            "Chart of Accounts",
            "Municipal Accounts",
            "Accounts"
        };

        public const string BudgetPanelTitle = "Budget Management & Analysis";
        public const string JarvisTabTitle = "JARVIS Chat";
        public const string JarvisAutomationStatusName = "JarvisAutomationStatus";
        public const string QuickBooksPanelTitle = "QuickBooks Integration";
        public static readonly string[] QuickBooksNavigationHints = { "QuickBooks", "QBO", "Connect to QuickBooks" };

        public static readonly string[] ExpectedBudgetButtons =
        {
            "Load Budgets",
            "Add Entry",
            "Edit Entry",
            "Delete Entry",
            "Import CSV",
            "Export CSV",
            "Export PDF",
            "Export Excel"
        };

        public static string ResolveWinFormsExePath()
        {
            var envPath = Environment.GetEnvironmentVariable("WILEY_WINFORMS_EXE_PATH");
            if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            {
                return envPath;
            }

            var repoRoot = FindRepoRoot(new DirectoryInfo(AppContext.BaseDirectory));
            if (repoRoot == null)
            {
                throw new DirectoryNotFoundException("Unable to locate repository root (WileyWidget.sln).");
            }

            var binRoot = Path.Combine(repoRoot.FullName, "src", "WileyWidget.WinForms", "bin");
            if (!Directory.Exists(binRoot))
            {
                throw new DirectoryNotFoundException($"Build output folder not found: {binRoot}");
            }

            // Search in both Debug and Release folders and pick the most recently built executable
            var exePath = Directory.EnumerateFiles(binRoot, "WileyWidget.WinForms.exe", SearchOption.AllDirectories)
                .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                .FirstOrDefault();

            if (exePath == null)
            {
                throw new FileNotFoundException("WileyWidget.WinForms.exe not found. Build the app before running UI tests.", binRoot);
            }

            return exePath;
        }

        private static DirectoryInfo? FindRepoRoot(DirectoryInfo? start)
        {
            var current = start;
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "WileyWidget.sln")))
                {
                    return current;
                }

                current = current.Parent;
            }

            return null;
        }
    }
}
