using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using WileyWidget.WinForms.E2ETests.PageObjects;
using Application = FlaUI.Core.Application;

namespace WileyWidget.WinForms.E2ETests
{
    /// <summary>
    /// E2E tests for theme toggle and visual style functionality.
    /// Verifies light/dark theme switching and theme persistence across forms.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Disposed via cleanup.")]
    [Collection("UI Tests")]
    public sealed class ThemeTests : IDisposable
    {
        private readonly string _exePath;
        private Application? _app;
        private UIA3Automation? _automation;

        public ThemeTests()
        {
            _exePath = ResolveExecutablePath();

            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
            Environment.SetEnvironmentVariable("WILEYWIDGET_USE_INMEMORY", "true");
            Environment.SetEnvironmentVariable("UI__IsUiTestHarness", "true");
            Environment.SetEnvironmentVariable("UI__UseMdiMode", "false");
        }

        private static string ResolveExecutablePath()
        {
            var envPath = Environment.GetEnvironmentVariable("WILEYWIDGET_EXE");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                return envPath;
            }

            var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory ?? ".", "..", "..", "..", "..", "..", "src", "WileyWidget.WinForms", "bin", "Debug"));
            var standard = Path.Combine(baseDir, "net9.0-windows", "WileyWidget.WinForms.exe");

            if (File.Exists(standard))
            {
                return standard;
            }

            var versioned = Directory.GetDirectories(baseDir, "net9.0-windows*")
                .Select(dir => Path.Combine(dir, "WileyWidget.WinForms.exe"))
                .FirstOrDefault(File.Exists);

            return versioned ?? throw new FileNotFoundException($"Executable not found under '{baseDir}'.");
        }

        [Fact]
        [Trait("Category", "Theme")]
        public void ThemeToggle_ButtonExists_InRibbon()
        {
            // Arrange
            _automation = new UIA3Automation();
            _app = Application.Launch(_exePath);
            Retry.WhileNull(() => _app.GetMainWindow(_automation),
                timeout: TimeSpan.FromSeconds(10),
                throwOnTimeout: true);

            var window = _app.GetMainWindow(_automation);
            Assert.NotNull(window);

            // Act - Find theme toggle button by searching for button containing "Theme" in name
            var allButtons = window.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
            var themeButton = allButtons.FirstOrDefault(btn =>
                btn.Name != null && btn.Name.Contains("Theme", StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.NotNull(themeButton);

            var buttonName = themeButton!.Name;
            Assert.NotNull(buttonName);
            Assert.Contains("Theme", buttonName, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("Category", "Theme")]
        public void ThemeToggle_Click_ChangesButtonText()
        {
            // Arrange
            _automation = new UIA3Automation();
            _app = Application.Launch(_exePath);
            Retry.WhileNull(() => _app.GetMainWindow(_automation),
                timeout: TimeSpan.FromSeconds(10),
                throwOnTimeout: true);

            var window = _app.GetMainWindow(_automation);

            // Find theme button by name containing "Theme"
            var allButtons = window?.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
            Assert.NotNull(allButtons);
            var themeButton = allButtons!.FirstOrDefault(btn =>
                btn.Name?.Contains("Theme", StringComparison.OrdinalIgnoreCase) == true);

            Assert.NotNull(themeButton);
            var initialText = themeButton?.Name;
            Assert.NotNull(initialText);

            // Act - Click theme toggle using Click() for better compatibility
            themeButton?.Click();
            System.Threading.Thread.Sleep(2000);

            // Assert - Button text should change
            var updatedText = themeButton?.Name;
            Assert.NotNull(updatedText);
            Assert.NotEqual(initialText, updatedText);

            // Should contain "Light" or "Dark" in the theme name
            var hasThemeText = updatedText.Contains("Light", StringComparison.OrdinalIgnoreCase) ||
                             updatedText.Contains("Dark", StringComparison.OrdinalIgnoreCase);

            Assert.True(hasThemeText,
                $"Expected theme button to show Light/Dark theme text, but got: {updatedText}");
        }

        [Fact]
        [Trait("Category", "Theme")]
        public void ThemeToggle_DoubleClick_RestoresOriginalTheme()
        {
            // Arrange
            _automation = new UIA3Automation();
            _app = Application.Launch(_exePath);
            Retry.WhileNull(() => _app.GetMainWindow(_automation),
                timeout: TimeSpan.FromSeconds(10),
                throwOnTimeout: true);

            var window = _app.GetMainWindow(_automation);

            // Find theme button by name containing "Theme"
            var allButtons = window?.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
            Assert.NotNull(allButtons);
            var themeButton = allButtons!.FirstOrDefault(btn =>
                btn.Name?.Contains("Theme", StringComparison.OrdinalIgnoreCase) == true);

            Assert.NotNull(themeButton);
            var originalText = themeButton?.Name;
            Assert.NotNull(originalText);

            // Act - Toggle twice using Click()
            themeButton?.Click();
            System.Threading.Thread.Sleep(1500);

            themeButton?.Click();
            System.Threading.Thread.Sleep(1500);

            // Assert - Should return to original theme text (may include emoji)
            var finalText = themeButton?.Name;
            // Both texts should contain same theme name (Light or Dark)
            var originalContainsLight = originalText?.Contains("Light", StringComparison.OrdinalIgnoreCase) ?? false;
            var finalContainsLight = finalText?.Contains("Light", StringComparison.OrdinalIgnoreCase) ?? false;
            Assert.Equal(originalContainsLight, finalContainsLight);
        }

        [Fact]
        [Trait("Category", "Theme")]
        [Trait("Category", "Smoke")]
        public void RibbonButtons_AreAccessible()
        {
            // Arrange
            _automation = new UIA3Automation();
            _app = Application.Launch(_exePath);
            Retry.WhileNull(() => _app.GetMainWindow(_automation),
                timeout: TimeSpan.FromSeconds(10),
                throwOnTimeout: true);

            var window = _app.GetMainWindow(_automation);
            Assert.NotNull(window);
            var mainFormPage = new MainFormPage(_automation, window);

            // Assert - Check that navigation buttons exist and have expected names
            var dashboardBtn = mainFormPage.DashboardButton;
            Assert.NotNull(dashboardBtn);
            Assert.Contains("Dashboard", dashboardBtn.Name, StringComparison.OrdinalIgnoreCase);

            var accountsBtn = mainFormPage.AccountsButton;
            Assert.NotNull(accountsBtn);
            Assert.Contains("Accounts", accountsBtn.Name, StringComparison.OrdinalIgnoreCase);

            var chartsBtn = mainFormPage.ChartsButton;
            Assert.NotNull(chartsBtn);
            Assert.Contains("Charts", chartsBtn.Name, StringComparison.OrdinalIgnoreCase);

            var reportsBtn = mainFormPage.ReportsButton;
            Assert.NotNull(reportsBtn);
            Assert.Contains("Reports", reportsBtn.Name, StringComparison.OrdinalIgnoreCase);

            var settingsBtn = mainFormPage.SettingsButton;
            Assert.NotNull(settingsBtn);
            Assert.Contains("Settings", settingsBtn.Name, StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            try
            {
                _app?.Close();
                _app?.Dispose();
                _automation?.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
