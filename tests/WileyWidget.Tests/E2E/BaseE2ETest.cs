using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System;
using System.IO;
using Xunit;

namespace WileyWidget.Tests.E2E
{
    public class BaseE2ETest : IDisposable
    {
        protected FlaUI.Core.Application Application { get; private set; }
        protected UIA3Automation Automation { get; private set; }
        protected FlaUI.Core.AutomationElements.Window MainWindow { get; private set; }
        // Optional WinAppDriver session (used by some tests). Not initialized here to avoid test runtime requirements.
        protected object? AppiumSession { get; private set; }

        public BaseE2ETest()
        {
            // Set environment variable to indicate E2E test mode
            Environment.SetEnvironmentVariable("WILEY_WIDGET_E2E_TEST", "true");

            // Kill any existing WileyWidget processes to prevent interference
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = "/f /im WileyWidget.exe /t",
                    UseShellExecute = false,
                    CreateNoWindow = true
                })?.WaitForExit(5000);
            }
            catch { /* Ignore if taskkill fails */ }

            // Launch the WPF app (adjust path to your built exe, e.g., after dotnet build)
            // Use absolute path to avoid Path.Combine issues with relative paths
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var solutionDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(baseDir))))));
            var appPath = Path.Combine(solutionDir, "bin", "Debug", "net9.0-windows10.0.19041.0", "win-x64", "WileyWidget.exe");

            if (!File.Exists(appPath))
            {
                throw new FileNotFoundException($"WPF app executable not found at: {appPath}. Build the project first.");
            }

            // Launch with timeout to prevent hanging
            Application = FlaUI.Core.Application.Launch(appPath);
            Automation = new UIA3Automation();

            // Wait for the main window to be both available and visible (not just enabled)
            // The app starts with Visibility=Hidden and shows it after initialization
            MainWindow = Application.GetMainWindow(Automation, TimeSpan.FromSeconds(10))
                ?? throw new InvalidOperationException("Failed to find main window within 10 seconds. Ensure app launches correctly.");

            // Wait for the window to become visible with a maximum of 10 iterations (5 seconds total)
            const int maxVisibilityChecks = 10;
            int visibilityCheckCount = 0;
            while (MainWindow.Properties.IsOffscreen.Value && visibilityCheckCount < maxVisibilityChecks)
            {
                System.Threading.Thread.Sleep(500); // Wait 500ms between checks
                visibilityCheckCount++;
            }

            if (MainWindow.Properties.IsOffscreen.Value)
            {
                throw new InvalidOperationException($"Main window remained offscreen after {maxVisibilityChecks} checks (5 seconds). App may have failed to initialize properly.");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (MainWindow != null)
                    {
                        MainWindow.Close();
                        System.Threading.Thread.Sleep(1000); // Give time for close to complete
                    }
                }
                catch { /* best effort */ }

                try
                {
                    if (Application != null)
                    {
                        Application.Close();
                        System.Threading.Thread.Sleep(500); // Give time for close to complete
                    }
                }
                catch { /* best effort */ }

                try
                {
                    if (Application != null && !Application.HasExited)
                    {
                        Application.Kill();
                    }
                }
                catch { /* best effort */ }

                Automation?.Dispose();

                // Final cleanup - kill any remaining processes
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = "/f /im WileyWidget.exe /t",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit(2000);
                }
                catch { /* Ignore cleanup failures */ }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
