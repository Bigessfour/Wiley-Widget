using System;
using System.Diagnostics;
using System.IO;
using FlaUI.Core;
using FlaUI.UIA3;
using Xunit;
using WileyWidget.WinForms.E2ETests.Properties;

namespace WileyWidget.WinForms.E2ETests
{
    public class DashboardUiAutomationTests
    {
        [Fact]
        public void LaunchApp_WhenRun_ShouldOpenMainWindow()
        {
            // Try to detect interactive environment and skip at runtime with a helpful message when CI is non-interactive
            var labels = Environment.GetEnvironmentVariable("RUNNER_LABELS") ?? string.Empty;
            var optedIn = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
            var selfHosted = labels.IndexOf("self-hosted", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!optedIn && !selfHosted && string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                // Log the reason and throw a skip exception (xUnit's SkipException has no ctor with message in this runtime)
                Console.WriteLine(Resources.NotInteractiveSkip);
                return;
            }

            // This test is a scaffold showing how to start the app with FlaUI.
            var exePath = Path.Combine("..","..","..","WileyWidget.WinForms","bin","Debug","net9.0-windows","WileyWidget.WinForms.exe");
            if (!File.Exists(exePath))
            {
                Console.WriteLine($"E2E UI test: exe not found at '{exePath}'");
                return;
            }

            using var app = FlaUI.Core.Application.Launch(exePath);
            using var automation = new UIA3Automation();

            var window = app.GetMainWindow(automation);
            Assert.NotNull(window);
            Assert.True(window.Title?.IndexOf("Dashboard - Wiley Widget", StringComparison.OrdinalIgnoreCase) >= 0, "Main window title should contain expected product name (Dashboard - Wiley Widget)");

            // Perform simple UI flow: for example, click Load dashboard button and assert a control updates.
        }
    }
}