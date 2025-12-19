using System;
using System.IO;
using System.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using Xunit;
using WileyWidget.WinForms.E2ETests.Helpers;

namespace WileyWidget.WinForms.E2ETests
{
    /// <summary>
    /// FlaUI smoke tests covering the primary WinForms views opened from MainForm navigation.
    /// These are opt-in and return early when no interactive UI runner is available.
    /// </summary>
    [Collection("UI Tests")]
    public sealed class AllViewsUITests
    {
        private readonly UiTestSessionOptions _options;
        private UiTestSession.SessionState? _session;

        public AllViewsUITests()
        {
            _options = UiTestSessionOptions.UiHarness(ResolveExecutablePath());
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
            var session = RequireSession();
            var mainWindow = session.GetMainWindow();

            var viewWindow = NavigationHelper.OpenView(session.Automation, mainWindow, navAutomationId, expectedTitleContains);
            Assert.NotNull(viewWindow);

            var target = WaitForElement(viewWindow, cf => BuildElementCondition(cf, elementAutomationId, fallbackElementName));
            Assert.NotNull(target);

            // Close the view to ensure test isolation
            NavigationHelper.CloseView(viewWindow);
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
            var session = RequireSession();
            var mainWindow = session.GetMainWindow();
            var dashboard = NavigationHelper.OpenView(session.Automation, mainWindow, "Nav_Dashboard", "Dashboard");

            var statusLabel = WaitForElement(dashboard, cf => cf.ByAutomationId("LastUpdatedLabel").Or(cf.ByName("Last Updated:")))?.AsLabel();
            Assert.NotNull(statusLabel);
            var initial = statusLabel.Text ?? string.Empty;

            var refreshButton = WaitForElement(dashboard, cf => cf.ByAutomationId("Toolbar_RefreshButton").Or(cf.ByName("Refresh")))?.AsButton();
            Assert.NotNull(refreshButton);

            refreshButton.Click();

            var changed = Retry.While(() => string.Equals(statusLabel.Text, initial, StringComparison.Ordinal), same => same, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200));
            Assert.False(changed.Success, "LastUpdated label did not update after refresh within timeout.");

            // Close the view to ensure test isolation
            NavigationHelper.CloseView(dashboard);
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
            var session = RequireSession();
            var mainWindow = session.GetMainWindow();
            var settings = NavigationHelper.OpenView(session.Automation, mainWindow, "Nav_Settings", "Settings");

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

            // Close the view to ensure test isolation
            NavigationHelper.CloseView(settings);
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
            var session = RequireSession();
            var mainWindow = session.GetMainWindow();
            var accounts = NavigationHelper.OpenView(session.Automation, mainWindow, "Nav_Accounts", "Municipal Accounts");

            var loadButton = WaitForElement(accounts, cf => cf.ByName("Load Accounts"))?.AsButton();
            var filterButton = WaitForElement(accounts, cf => cf.ByName("Apply Filters"))?.AsButton();
            Assert.NotNull(loadButton);
            Assert.NotNull(filterButton);

            loadButton.Click();
            filterButton.Click();

            var grid = WaitForElement(accounts, cf => cf.ByAutomationId("dataGridAccounts"))?.AsGrid();
            Assert.NotNull(grid);

            // Close the view to ensure test isolation
            NavigationHelper.CloseView(accounts);
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
            var session = RequireSession();
            var mainWindow = session.GetMainWindow();
            var charts = NavigationHelper.OpenView(session.Automation, mainWindow, "Nav_Charts", "Budget Analytics");

            var cartesian = WaitForElement(charts, cf => cf.ByAutomationId("Chart_Cartesian"));
            var pie = WaitForElement(charts, cf => cf.ByAutomationId("Chart_Pie"));

            Assert.NotNull(cartesian);
            Assert.NotNull(pie);

            // Close the view to ensure test isolation
            NavigationHelper.CloseView(charts);
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
            var session = RequireSession();
            var mainWindow = session.GetMainWindow();

            var reports = NavigationHelper.OpenView(session.Automation, mainWindow, "Nav_Reports", "Reports");

            var generate = WaitForElement(reports, cf => cf.ByName("Generate"))?.AsButton();
            var exportPdf = WaitForElement(reports, cf => cf.ByName("Export PDF"))?.AsButton();

            Assert.NotNull(generate);
            Assert.NotNull(exportPdf);

            // Viewer placeholder or real viewer should exist; check for any window control under the reports window.
            var viewerHost = WaitForElement(reports, cf => cf.ByControlType(ControlType.Pane).Or(cf.ByControlType(ControlType.Custom)));
            Assert.NotNull(viewerHost);

            // Close the view to ensure test isolation
            NavigationHelper.CloseView(reports);
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
            var session = RequireSession();
            var mainWindow = session.GetMainWindow();

            var stateLabel = WaitForElementByNameContains(mainWindow, "Docking:", 8);
            Assert.NotNull(stateLabel);

            var initial = stateLabel!.Name ?? string.Empty;

            var dockToggle = WaitForElement(mainWindow, cf => cf.ByAutomationId("Nav_DockingToggle"))?.AsButton();
            var mdiToggle = WaitForElement(mainWindow, cf => cf.ByAutomationId("Nav_MdiToggle"))?.AsButton();

            Assert.NotNull(dockToggle);
            Assert.NotNull(mdiToggle);

            dockToggle.Click();
            mdiToggle.Click();

            var changed = Retry.While(() => string.Equals(stateLabel.Name, initial, StringComparison.Ordinal), same => same, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200));
            Assert.False(changed.Success, "State text did not change after toggling docking/MDI buttons.");
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
            _session = UiTestSession.GetOrStart(_options);
            DismissLicensePopups(_session);
        }

        private UiTestSession.SessionState RequireSession()
        {
            return _session ?? throw new InvalidOperationException("Application has not been started.");
        }

        private static void DismissLicensePopups(UiTestSession.SessionState session)
        {
            // Wait a bit for popups to appear
            System.Threading.Thread.Sleep(2000);

            try
            {
                // Find all windows
                var allWindows = session.Automation.GetDesktop().FindAllChildren();

                foreach (var window in allWindows)
                {
                    try
                    {
                        // Safely access window name (can throw COM exceptions during UI transitions)
                        var windowName = window?.Name;
                        if (!string.IsNullOrEmpty(windowName) &&
                            (windowName.Contains("License", StringComparison.OrdinalIgnoreCase) ||
                             windowName.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase)))
                        {
                            try
                            {
                                // Try to close the popup
                                var closeButton = window!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("OK").Or(cf.ByName("Close"))));
                                if (closeButton != null)
                                {
                                    var button = closeButton!.AsButton();
                                    if (button != null)
                                    {
                                        button!.Click();
                                    }
                                }
                                else
                                {
                                    if (window != null)
                                    {
                                        window!.AsWindow().Close();
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore if can't close
                            }
                        }
                    }
                    catch
                    {
                        // Ignore COM exceptions when accessing window properties
                        continue;
                    }
                }
            }
            catch
            {
                // Ignore if desktop enumeration fails - not critical
            }
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
