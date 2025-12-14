using System;
using System.Diagnostics;
using System.IO;
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

            // Perform simple UI flow: for example, click Load dashboard button and assert a control updates.
        }
    }
}
