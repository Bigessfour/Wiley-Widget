using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using Application = FlaUI.Core.Application;

namespace WileyWidget.WinForms.E2ETests
{
    /// <summary>
    /// FlaUI smoke tests covering the primary WinForms views opened from MainForm navigation.
    /// These are opt-in and return early when no interactive UI runner is available.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Disposed via cleanup.")]
    [Collection("UI Tests")]
    public sealed class AllViewsUITests : IDisposable
    {
        private readonly string _exePath;
        private Application? _app;
        private UIA3Automation? _automation;

        public AllViewsUITests()
        {
            _exePath = ResolveExecutablePath();

            // Enable in-memory mode and simplified chrome for UI automation stability.
            // Disable MDI mode so forms open as separate windows that FlaUI can detect
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
            Environment.SetEnvironmentVariable("WILEYWIDGET_USE_INMEMORY", "true");
            Environment.SetEnvironmentVariable("UI:UseMdiMode", "false");
            Environment.SetEnvironmentVariable("UI:UseTabbedMdi", "false");
        }

        private static string ResolveExecutablePath()
        {
            var envPath = Environment.GetEnvironmentVariable("WILEYWIDGET_EXE");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                return envPath;
            }

            var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory ?? ".", "..", "..", "..", "..", "..", "src", "WileyWidget.WinForms", "bin", "Debug"));
            if (!Directory.Exists(baseDir))
            {
                throw new DirectoryNotFoundException($"Build output directory not found at '{baseDir}'. Build WileyWidget.WinForms or set WILEYWIDGET_EXE to a published executable.");
            }

            var standard = Path.Combine(baseDir, "net9.0-windows", "WileyWidget.WinForms.exe");
            if (File.Exists(standard))
            {
                return standard;
            }

            var versioned = Directory.GetDirectories(baseDir, "net9.0-windows*")
                .Select(dir => Path.Combine(dir, "WileyWidget.WinForms.exe"))
                .FirstOrDefault(File.Exists);

            if (!string.IsNullOrEmpty(versioned))
            {
                return versioned;
            }

            throw new FileNotFoundException($"Executable not found. Set WILEYWIDGET_EXE to a published executable or build Debug output under '{baseDir}'.");
        }

        [Theory]
        [InlineData("Nav_Dashboard", "Dashboard", "Toolbar_LoadButton", "Load Dashboard")]
        [InlineData("Nav_Accounts", "Municipal Accounts", "dataGridAccounts", "Apply Filters")]
        [InlineData("Nav_Charts", "Budget Analytics", "Chart_Cartesian", "Budget Trend")]
        [InlineData("Nav_Settings", "Settings", "themeCombo", "Close")]
        [InlineData("Nav_Reports", "Reports", null, "Generate")]
        [Trait("Category", "UI")]
        public void Navigation_opens_expected_view(string navAutomationId, string expectedTitleContains, string? elementAutomationId, string fallbackElementName)
        {
            if (!EnsureInteractiveOrSkip())
            {
                return;
            }

            StartApp();
            var mainWindow = GetMainWindow();

            var viewWindow = OpenView(mainWindow, navAutomationId, expectedTitleContains);
            Assert.NotNull(viewWindow);

            var target = WaitForElement(viewWindow, cf => BuildElementCondition(cf, elementAutomationId, fallbackElementName));
            Assert.NotNull(target);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void Dashboard_refresh_updates_status_label()
        {
            if (!EnsureInteractiveOrSkip())
            {
                return;
            }

            StartApp();
            var mainWindow = GetMainWindow();
            var dashboard = OpenView(mainWindow, "Nav_Dashboard", "Dashboard");

            var statusLabel = WaitForElement(dashboard, cf => cf.ByAutomationId("LastUpdatedLabel").Or(cf.ByName("Last Updated:")))?.AsLabel();
            Assert.NotNull(statusLabel);
            var initial = statusLabel.Text ?? string.Empty;

            var refreshButton = WaitForElement(dashboard, cf => cf.ByAutomationId("Toolbar_RefreshButton").Or(cf.ByName("Refresh")))?.AsButton();
            Assert.NotNull(refreshButton);

            refreshButton.Invoke();

            var changed = Retry.While(() => string.Equals(statusLabel.Text, initial, StringComparison.Ordinal), same => same, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200));
            Assert.False(changed.Success, "LastUpdated label did not update after refresh within timeout.");
        }

        [Fact]
        [Trait("Category", "UI")]
        public void Settings_theme_combo_allows_selection()
        {
            if (!EnsureInteractiveOrSkip())
            {
                return;
            }

            StartApp();
            var mainWindow = GetMainWindow();
            var settings = OpenView(mainWindow, "Nav_Settings", "Settings");

            var themeCombo = WaitForElement(settings, cf => cf.ByAutomationId("themeCombo"))?.AsComboBox();
            Assert.NotNull(themeCombo);

            var items = themeCombo.Items ?? Array.Empty<AutomationElement>();
            if (items.Length > 1)
            {
                var currentIndex = Array.IndexOf(items, themeCombo.SelectedItem);
                var newIndex = currentIndex == 0 ? 1 : 0;
                themeCombo.Select(newIndex);

                var selected = themeCombo.SelectedItem;
                Assert.Equal(items[newIndex].Name, selected?.Name);
            }
            else
            {
                Assert.True(items.Length >= 1, "Theme combo should expose at least one option.");
            }
        }

        [Fact]
        [Trait("Category", "UI")]
        public void Accounts_toolbar_buttons_are_accessible()
        {
            if (!EnsureInteractiveOrSkip())
            {
                return;
            }

            StartApp();
            var mainWindow = GetMainWindow();
            var accounts = OpenView(mainWindow, "Nav_Accounts", "Municipal Accounts");

            var loadButton = WaitForElement(accounts, cf => cf.ByName("Load Accounts"))?.AsButton();
            var filterButton = WaitForElement(accounts, cf => cf.ByName("Apply Filters"))?.AsButton();
            Assert.NotNull(loadButton);
            Assert.NotNull(filterButton);

            loadButton.Invoke();
            filterButton.Invoke();

            var grid = WaitForElement(accounts, cf => cf.ByAutomationId("dataGridAccounts"))?.AsGrid();
            Assert.NotNull(grid);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void Charts_view_renders_primary_and_pie_charts()
        {
            if (!EnsureInteractiveOrSkip())
            {
                return;
            }

            StartApp();
            var mainWindow = GetMainWindow();
            var charts = OpenView(mainWindow, "Nav_Charts", "Budget Analytics");

            var cartesian = WaitForElement(charts, cf => cf.ByAutomationId("Chart_Cartesian"));
            var pie = WaitForElement(charts, cf => cf.ByAutomationId("Chart_Pie"));

            Assert.NotNull(cartesian);
            Assert.NotNull(pie);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void Reports_view_shows_toolbar_and_preview_placeholder()
        {
            if (!EnsureInteractiveOrSkip())
            {
                return;
            }

            StartApp();
            var mainWindow = GetMainWindow();

            var reports = OpenView(mainWindow, "Nav_Reports", "Reports");

            var generate = WaitForElement(reports, cf => cf.ByName("Generate"))?.AsButton();
            var exportPdf = WaitForElement(reports, cf => cf.ByName("Export PDF"))?.AsButton();

            Assert.NotNull(generate);
            Assert.NotNull(exportPdf);

            // Viewer placeholder or real viewer should exist; check for any window control under the reports window.
            var viewerHost = WaitForElement(reports, cf => cf.ByControlType(ControlType.Pane).Or(cf.ByControlType(ControlType.Custom)));
            Assert.NotNull(viewerHost);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void Navigation_toggles_update_state_text()
        {
            if (!EnsureInteractiveOrSkip())
            {
                return;
            }

            StartApp();
            var mainWindow = GetMainWindow();

            var stateLabel = WaitForElementByNameContains(mainWindow, "Docking:", 8);
            Assert.NotNull(stateLabel);

            var initial = stateLabel!.Name ?? string.Empty;

            var dockToggle = WaitForElement(mainWindow, cf => cf.ByAutomationId("Nav_DockingToggle"))?.AsButton();
            var mdiToggle = WaitForElement(mainWindow, cf => cf.ByAutomationId("Nav_MdiToggle"))?.AsButton();

            Assert.NotNull(dockToggle);
            Assert.NotNull(mdiToggle);

            dockToggle.Invoke();
            mdiToggle.Invoke();

            var changed = Retry.While(() => string.Equals(stateLabel.Name, initial, StringComparison.Ordinal), same => same, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200));
            Assert.False(changed.Success, "State text did not change after toggling docking/MDI buttons.");
        }

        public void Dispose()
        {
            try { _automation?.Dispose(); } catch { }
            try
            {
                if (_app != null && !_app.HasExited)
                {
                    _app.Kill();
                    _app.Dispose();
                }
            }
            catch
            {
                // Ignore disposal issues; process may already be closed.
            }
        }

        private bool EnsureInteractiveOrSkip()
        {
            var labels = Environment.GetEnvironmentVariable("RUNNER_LABELS") ?? string.Empty;
            var optedIn = string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"), "true", StringComparison.OrdinalIgnoreCase);
            var selfHosted = labels.IndexOf("self-hosted", StringComparison.OrdinalIgnoreCase) >= 0;
            var isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

            if (isCi && !optedIn && !selfHosted)
            {
                return false;
            }

            return true;
        }

        private void StartApp()
        {
            if (_app != null && !_app.HasExited)
            {
                return;
            }

            if (!File.Exists(_exePath))
            {
                throw new FileNotFoundException($"Executable not found at '{_exePath}'. Set WILEYWIDGET_EXE to a published executable before running UI tests.");
            }

            // Set the license key in the current process environment so the launched app inherits it
            // trunk-ignore(gitleaks/generic-api-key): Test license key, not a real secret
            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "Ngo9BigBOggjHTQxAR8/V1NMaF5cXmZCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWXZceXRQR2VfUER0W0o=");

            _app = Application.Launch(_exePath);
            _automation = new UIA3Automation();
        }

        private Window GetMainWindow(int timeoutSeconds = 15)
        {
            if (_app == null || _automation == null)
            {
                throw new InvalidOperationException("Application has not been started.");
            }

            var main = Retry.WhileNull(() => _app.GetMainWindow(_automation), TimeSpan.FromSeconds(timeoutSeconds));
            return main.Result ?? throw new InvalidOperationException("Main window was not found.");
        }

        private Window OpenView(Window mainWindow, string navAutomationId, string expectedTitleContains)
        {
            var navElement = Retry.WhileNull(
                () => FindNavigationElement(mainWindow, navAutomationId, expectedTitleContains),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMilliseconds(250)).Result;

            Assert.NotNull(navElement);

            if (navElement!.AsButton() is { } button)
            {
                button.Invoke();
            }
            else
            {
                var invoke = navElement.Patterns.Invoke.PatternOrDefault;
                Assert.NotNull(invoke);
                invoke.Invoke();
            }

            var window = Retry.WhileNull(
                () => FindWindowByTitle(expectedTitleContains),
                TimeSpan.FromSeconds(12),
                TimeSpan.FromMilliseconds(250));

            return window.Result ?? throw new InvalidOperationException($"View window containing '{expectedTitleContains}' was not found.");
        }

        private Window? FindWindowByTitle(string titleContains)
        {
            if (_automation == null)
            {
                return null;
            }

            var desktop = _automation.GetDesktop();
            var window = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .FirstOrDefault(w => !string.IsNullOrEmpty(w.Name)
                                     && w.Name.IndexOf(titleContains, StringComparison.OrdinalIgnoreCase) >= 0)
                ?.AsWindow();

            return window;
        }

        private static AutomationElement? FindNavigationElement(AutomationElement root, string navAutomationId, string expectedTitleContains)
        {
            return root.FindFirstDescendant(cf => cf.ByAutomationId(navAutomationId)
                .Or(cf.ByName(navAutomationId))
                .Or(cf.ByName(expectedTitleContains)));
        }

        private static ConditionBase BuildElementCondition(ConditionFactory cf, string? automationId, string fallbackName)
        {
            if (!string.IsNullOrWhiteSpace(automationId))
            {
                return cf.ByAutomationId(automationId!);
            }

            return cf.ByName(fallbackName);
        }

        private static AutomationElement? WaitForElement(AutomationElement root, Func<ConditionFactory, ConditionBase> selector, int timeoutSeconds = 10)
        {
            return Retry.WhileNull(() => root.FindFirstDescendant(selector), TimeSpan.FromSeconds(timeoutSeconds), TimeSpan.FromMilliseconds(200)).Result;
        }

        private static AutomationElement? WaitForElementByNameContains(AutomationElement root, string text, int timeoutSeconds = 10)
        {
            return Retry.WhileNull(
                () => root.FindAllDescendants().FirstOrDefault(a => !string.IsNullOrEmpty(a.Name) && a.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0),
                TimeSpan.FromSeconds(timeoutSeconds),
                TimeSpan.FromMilliseconds(200)).Result;
        }
    }
}
