using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using FlaUI.Core;
using FlaUI.UIA3;
using Xunit;

namespace WileyWidget.WinForms.E2ETests
{
    public class DashboardUiAutomationTests
    {
        [Fact(Skip = "Interactive UI E2E requires a display / WinAppDriver environment. Enable locally or in CI with dedicated interactive runner.")]
        public void LaunchApp_WhenRun_ShouldOpenMainWindow()
        {
            // This test is a scaffold showing how to start the app with FlaUI.
            var exePath = Path.Combine("..", "..", "..", "WileyWidget.WinForms", "bin", "Debug", "net9.0-windows", "WileyWidget.WinForms.exe");
            Assert.True(File.Exists(exePath), "Build the WinForms app locally to create the exe before running UI tests.");

            using var app = FlaUI.Core.Application.Launch(exePath);
            using var automation = new UIA3Automation();

            var window = app.GetMainWindow(automation);
            Assert.NotNull(window);
            Assert.Contains("Dashboard - Wiley Widget", window.Title, StringComparison.OrdinalIgnoreCase);
        }

        [Fact(Skip = "Interactive UI E2E requires a display / WinAppDriver environment. Enable locally or in CI with dedicated interactive runner.")]
        public void LaunchApp_LoadDashboard_ShouldNotCrash()
        {
            // This test verifies that the app launches and can load dashboard without crashing
            var exePath = Path.Combine("..", "..", "..", "WileyWidget.WinForms", "bin", "Debug", "net9.0-windows", "WileyWidget.WinForms.exe");
            Assert.True(File.Exists(exePath), "Build the WinForms app locally to create the exe before running UI tests.");

            using var app = FlaUI.Core.Application.Launch(exePath);
            using var automation = new UIA3Automation();

            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(10));
            Assert.NotNull(window);
            Assert.Contains("Dashboard - Wiley Widget", window.Title, StringComparison.OrdinalIgnoreCase);

            // Wait for the window to be ready
            Thread.Sleep(2000);

            // Try to find and click the Load Dashboard button
            var loadButton = window.FindFirstDescendant(cf => cf.ByName("Toolbar_LoadButton"));
            if (loadButton != null)
            {
                loadButton.Click();
                // Wait for loading to complete
                Thread.Sleep(5000);

                // Verify no crash - window should still be responsive
                Assert.NotNull(window);
                Assert.True(window.IsAvailable);
            }
            else
            {
                // If button not found, at least verify the window loaded
                Assert.NotNull(window);
            }
        }
    }
}
