using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace WileyWidget.WinForms.E2ETests.Helpers
{
    /// <summary>
    /// Helper for launching and managing the test application process with proper environment setup.
    /// </summary>
    internal static class TestAppHelper
    {
        /// <summary>
        /// Gets the path to the WileyWidget.WinForms executable.
        /// </summary>
        public static string GetWileyWidgetExePath()
        {
            // Check for environment variable override first
            var envPath = Environment.GetEnvironmentVariable("WILEYWIDGET_EXE");
            if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            {
                return envPath;
            }

            // Look in the AppContext base directory (test assembly location)
            var baseDir = AppContext.BaseDirectory;
            var searchPaths = new[]
            {
                // Relative to test assembly: navigate up to find src/WileyWidget.WinForms/bin/Debug
                Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "WileyWidget.WinForms", "bin", "Debug"),
                // Absolute path from current working directory
                Path.Combine(Directory.GetCurrentDirectory(), "src", "WileyWidget.WinForms", "bin", "Debug"),
            };

            foreach (var basePath in searchPaths)
            {
                var fullBasePath = Path.GetFullPath(basePath);
                if (!Directory.Exists(fullBasePath))
                    continue;

                // Look for net9.0-windows* pattern
                var dirs = Directory.GetDirectories(fullBasePath, "net9.0-windows*");
                if (dirs.Length > 0)
                {
                    var exePath = Path.Combine(dirs[0], "WileyWidget.WinForms.exe");
                    if (File.Exists(exePath))
                        return exePath;
                }

                // Also check for net8.0-windows
                var exePath8 = Path.Combine(fullBasePath, "net8.0-windows", "WileyWidget.WinForms.exe");
                if (File.Exists(exePath8))
                    return exePath8;
            }

            throw new FileNotFoundException($"WileyWidget.WinForms.exe not found. Searched in: {string.Join(", ", searchPaths.Select(Path.GetFullPath))}. Set WILEYWIDGET_EXE environment variable to override.");
        }

        /// <summary>
        /// Builds the environment variables dictionary for test app launch.
        /// </summary>
        public static IDictionary<string, string> BuildTestEnvironment(bool isTestHarness, bool useMdiMode, bool useTabbedMdi)
        {
            var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Copy current environment
            foreach (var kvp in Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>())
            {
                var key = kvp.Key as string;
                var value = kvp.Value as string;
                if (key != null && value != null)
                {
                    env[key] = value;
                }
            }

            // Set test-specific environment variables
            env["WILEYWIDGET_UI_TESTS"] = "true";
            env["WILEYWIDGET_USE_INMEMORY"] = "true";
            env["UI__IsUiTestHarness"] = isTestHarness ? "true" : "false";
            env["UI__UseMdiMode"] = useMdiMode ? "true" : "false";
            env["UI__UseTabbedMdi"] = useTabbedMdi ? "true" : "false";

            // Inject Syncfusion license key if available
            var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (!string.IsNullOrEmpty(licenseKey))
            {
                env["SYNCFUSION_LICENSE_KEY"] = licenseKey;
            }

            return env;
        }

        /// <summary>
        /// Launches the test application with the specified environment setup.
        /// </summary>
        public static Process LaunchApp(string exePath, IDictionary<string, string> environment)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            // Set environment variables
            foreach (var kvp in environment)
            {
                psi.EnvironmentVariables[kvp.Key] = kvp.Value;
            }

            var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start process");
            }

            // Give the process a moment to start up
            System.Threading.Thread.Sleep(500);

            return process;
        }
    }
}
