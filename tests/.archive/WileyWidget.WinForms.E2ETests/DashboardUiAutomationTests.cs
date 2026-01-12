using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using FlaUI.Core;
using FlaUI.UIA3;
using WileyWidget.WinForms.E2ETests.PageObjects;
using Xunit;

namespace WileyWidget.WinForms.E2ETests
{
    public class DashboardUiAutomationTests
    {
        [StaFact(Skip = "Interactive UI E2E requires a display / WinAppDriver environment. Enable locally or in CI with dedicated interactive runner.")]
        public void LaunchApp_WhenRun_ShouldOpenMainWindow()
        {
            // This test is a scaffold showing how to start the app with FlaUI.
            var exePath = Path.Combine("..", "..", "..", "src", "WileyWidget.WinForms", "bin", "Debug", "net9.0-windows", "WileyWidget.WinForms.exe");
            Assert.True(File.Exists(exePath), "Build the WinForms app locally to create the exe before running UI tests.");

            using var app = FlaUI.Core.Application.Launch(exePath);
            using var automation = new UIA3Automation();

            var window = app.GetMainWindow(automation);
            Assert.NotNull(window);
            Assert.Contains("Dashboard - Wiley Widget", window.Title, StringComparison.OrdinalIgnoreCase);
        }

        [StaFact(Skip = "Interactive UI E2E requires a display / WinAppDriver environment. Enable locally or in CI with dedicated interactive runner.")]
        public void LaunchApp_LoadDashboard_ShouldNotCrash()
        {
            // This test verifies that the app launches and can load dashboard without crashing
            var exePath = Path.Combine("..", "..", "..", "src", "WileyWidget.WinForms", "bin", "Debug", "net9.0-windows", "WileyWidget.WinForms.exe");
            Assert.True(File.Exists(exePath), "Build the WinForms app locally to create the exe before running UI tests.");

            using var app = FlaUI.Core.Application.Launch(exePath);
            using var automation = new UIA3Automation();

            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(10));
            Assert.NotNull(window);
            Assert.Contains("Dashboard - Wiley Widget", window.Title, StringComparison.OrdinalIgnoreCase);

            // Use MainFormPage for UI interactions
            var mainFormPage = new MainFormPage(automation, window);

            // Navigate to Dashboard to ensure it's loaded (may already be loaded based on title)
            mainFormPage.NavigateToDashboard();

            // Verify no crash - window should still be responsive
            Assert.NotNull(window);
            Assert.True(window.IsAvailable);

            // Verify dashboard elements are present
            Assert.True(mainFormPage.IsLoaded(), "MainForm should be loaded with navigation elements");
        }
    }
}
