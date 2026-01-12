using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using FlaUI.Core.Conditions;
using WileyWidget.WinForms.E2ETests.Helpers;
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
        private bool _disposed;

        public ThemeTests()
        {
            _exePath = TestAppHelper.GetWileyWidgetExePath();

            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
            Environment.SetEnvironmentVariable("WILEYWIDGET_USE_INMEMORY", "true");
            Environment.SetEnvironmentVariable("UI__IsUiTestHarness", "true");

            // Set the license key to avoid popup
            // trunk-ignore(gitleaks/generic-api-key): Test license key, not a real secret
            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "Ngo9BigBOggjHTQxAR8/V1NMaF5cXmZCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWXZceXRQR2VfUER0W0o=");

            // Ensure only one test runs at a time to prevent UI automation conflicts
            System.Threading.Monitor.Enter(_testLock);
        }

        private static readonly object _testLock = new object();

        private static AutomationElement? WaitForElement(Window window, Func<ConditionFactory, ConditionBase> selector, int timeoutSeconds = 15)
        {
            return Retry.WhileNull(() => window.FindFirstDescendant(selector), TimeSpan.FromSeconds(timeoutSeconds), TimeSpan.FromMilliseconds(250)).Result;
        }

        private void WaitForBusyIndicator(TimeSpan timeout)
        {
            var window = _app?.GetMainWindow(_automation!);
            if (window == null) return;

            try
            {
                Retry.WhileTrue(
                    () =>
                    {
                        var busyIndicator = window.FindFirstDescendant(cf => cf.ByName("BusyIndicator").Or(cf.ByControlType(ControlType.ProgressBar)));
                        return busyIndicator != null && !busyIndicator.IsOffscreen;
                    },
                    timeout,
                    TimeSpan.FromMilliseconds(100));
            }
            catch
            {
                // Ignore timeouts or failures while probing busy indicator
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void ThemeToggle_ButtonExists_InRibbon()
        {
            // Arrange
            _automation = new UIA3Automation();
            _app = Application.Launch(_exePath);
            DismissLicensePopups();
            Retry.WhileNull(() => _app.GetMainWindow(_automation!),
                timeout: TimeSpan.FromSeconds(10),
                throwOnTimeout: true);

            var window = _app.GetMainWindow(_automation!) ?? throw new InvalidOperationException("Main window not found");

            // Act - Find theme toggle using common selectors with a longer timeout and fallbacks
            var themeButton = WaitForElement(window, cf =>
                    cf.ByAutomationId("themeToggle")
                        .Or(cf.ByName("Toggle Theme").Or(cf.ByName("Theme")).Or(cf.ByControlType(ControlType.Button))),
                    15)?.AsButton();

            // Assert
            Assert.NotNull(themeButton);

            var buttonName = themeButton!.Name;
            Assert.NotNull(buttonName);
            Assert.Contains("Theme", buttonName, StringComparison.OrdinalIgnoreCase);
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void ThemeToggle_Click_ChangesButtonText()
        {
            // Arrange
            _automation = new UIA3Automation();
            _app = Application.Launch(_exePath);
            DismissLicensePopups();
            Retry.WhileNull(() => _app.GetMainWindow(_automation!),
                timeout: TimeSpan.FromSeconds(10),
                throwOnTimeout: true);

            var window = _app.GetMainWindow(_automation!) ?? throw new InvalidOperationException("Main window not found");

            // Find theme button with fallbacks
            var themeButton = WaitForElement(window, cf =>
                cf.ByAutomationId("themeToggle")
                  .Or(cf.ByName("Toggle Theme").Or(cf.ByName("Theme")).Or(cf.ByControlType(ControlType.Button))),
                15)?.AsButton();

            Assert.NotNull(themeButton);
            var initialText = themeButton!.Name;
            Assert.NotNull(initialText);

            // Act - Click theme toggle and wait for UI to update
            themeButton.Click();
            WaitForBusyIndicator(TimeSpan.FromSeconds(5));

            // Re-query the button in case the control instance changed
            var updatedButton = WaitForElement(window, cf =>
                cf.ByAutomationId("themeToggle")
                  .Or(cf.ByName("Toggle Theme").Or(cf.ByName("Theme")).Or(cf.ByControlType(ControlType.Button))),
                8)?.AsButton();

            var updatedText = updatedButton?.Name ?? themeButton.Name;
            Assert.NotNull(updatedText);
            Assert.NotEqual(initialText, updatedText);

            // Should contain "Light" or "Dark" in the theme name
            var hasThemeText = updatedText.Contains("Light", StringComparison.OrdinalIgnoreCase) ||
                             updatedText.Contains("Dark", StringComparison.OrdinalIgnoreCase);

            Assert.True(hasThemeText,
                $"Expected theme button to show Light/Dark theme text, but got: {updatedText}");
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void ThemeToggle_DoubleClick_RestoresOriginalTheme()
        {
            // Arrange
            _automation = new UIA3Automation();
            _app = Application.Launch(_exePath);
            DismissLicensePopups();
            Retry.WhileNull(() => _app.GetMainWindow(_automation!),
                timeout: TimeSpan.FromSeconds(10),
                throwOnTimeout: true);

            var window = _app.GetMainWindow(_automation!) ?? throw new InvalidOperationException("Main window not found");

            // Find theme button with fallbacks
            var themeButton = WaitForElement(window, cf =>
                    cf.ByAutomationId("themeToggle")
                        .Or(cf.ByName("Toggle Theme").Or(cf.ByName("Theme")).Or(cf.ByControlType(ControlType.Button))),
                    15)?.AsButton();

            Assert.NotNull(themeButton);
            var originalText = themeButton!.Name;
            Assert.NotNull(originalText);

            // Act - Toggle twice using Click()
            themeButton.Click();
            WaitForBusyIndicator(TimeSpan.FromSeconds(3));

            themeButton.Click();
            WaitForBusyIndicator(TimeSpan.FromSeconds(3));

            // Re-query button for final text
            var finalButton = WaitForElement(window, cf =>
                    cf.ByAutomationId("themeToggle")
                        .Or(cf.ByName("Toggle Theme").Or(cf.ByName("Theme")).Or(cf.ByControlType(ControlType.Button))),
                    8)?.AsButton();

            var finalText = finalButton?.Name ?? originalText;
            var originalContainsLight = originalText?.Contains("Light", StringComparison.OrdinalIgnoreCase) ?? false;
            var finalContainsLight = finalText?.Contains("Light", StringComparison.OrdinalIgnoreCase) ?? false;
            Assert.Equal(originalContainsLight, finalContainsLight);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Category", "Smoke")]
        public void RibbonButtons_AreAccessible()
        {
            // Arrange
            _automation = new UIA3Automation();
            _app = Application.Launch(_exePath);
            DismissLicensePopups();
            Retry.WhileNull(() => _app.GetMainWindow(_automation!),
                timeout: TimeSpan.FromSeconds(10),
                throwOnTimeout: true);

            var window = _app.GetMainWindow(_automation!) ?? throw new InvalidOperationException("Main window not found");
            var mainFormPage = new MainFormPage(_automation!, window);

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

        private void DismissLicensePopups()
        {
            if (_automation == null) return;

            // Wait for popups to appear (event-pumped)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 2000)
            {
                try { System.Windows.Forms.Application.DoEvents(); } catch { }
            }

            // Find all windows
            var allWindows = _automation.GetDesktop().FindAllChildren();

            foreach (var window in allWindows)
            {
                if (window.Name != null &&
                    (window.Name.Contains("License", StringComparison.OrdinalIgnoreCase) ||
                     window.Name.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        // Try to close the popup
                        var closeButton = window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("OK").Or(cf.ByName("Close"))));
                        if (closeButton != null)
                        {
                            closeButton.AsButton().Click();
                        }
                        else
                        {
                            // Close the window via the Window wrapper (AutomationElement has no Close method)
                            // Wrapped in try/catch above so any failures will be ignored
                            window.AsWindow().Close();
                        }
                    }
                    catch
                    {
                        // Ignore if can't close
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                // Close application gracefully first
                _app?.Close();

                // Give it time to close gracefully
                System.Threading.Thread.Sleep(500);

                // Force kill only if still running
                if (_app != null && !_app.HasExited)
                {
                    try
                    {
                        _app.Kill();
                        System.Threading.Thread.Sleep(250);
                    }
                    catch { }
                }

                _app?.Dispose();
                _automation?.Dispose();

                // Only clean up test-specific processes when NOT running in IDE/vstest
                var vstestHost = Environment.GetEnvironmentVariable("VSTEST_HOST_PROCESSID");
                var isInIDE = !string.IsNullOrWhiteSpace(vstestHost) ||
                             !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VSCODE_PID"));

                if (!isInIDE)
                {
                    // Only kill processes that match our specific test app
                    var testAppPath = _exePath.ToLowerInvariant();
                    var processes = System.Diagnostics.Process.GetProcessesByName("WileyWidget.WinForms");

                    foreach (var p in processes)
                    {
                        try
                        {
                            // Double-check this is our test process, not a running dev instance
                            var processPath = p.MainModule?.FileName?.ToLowerInvariant();
                            if (processPath != null && processPath.Contains("debug", StringComparison.Ordinal) && processPath.Contains("e2etests", StringComparison.Ordinal))
                            {
                                p.Kill();
                                p.WaitForExit(1000);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
            finally
            {
                // Release the test lock
                try
                {
                    System.Threading.Monitor.Exit(_testLock);
                }
                catch { }
            }
        }
    }
}
