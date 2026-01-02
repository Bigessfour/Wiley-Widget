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
    /// Collection fixture to manage shared UI test session lifecycle.
    /// Ensures app runs across all tests and is only disposed at collection end.
    /// </summary>
    public sealed class UiTestSessionFixture : IDisposable
    {
        internal UiTestSession.SessionState? Session { get; private set; }
        internal UiTestSessionOptions Options { get; }

        public UiTestSessionFixture()
        {
            Options = UiTestSessionOptions.UiHarness(ResolveExecutablePath());
        }

        internal UiTestSession.SessionState GetOrStartSession()
        {
            if (Session == null || Session.App.HasExited)
            {
                Session = UiTestSession.GetOrStart(Options);
                DismissLicensePopups(Session);
            }
            return Session;
        }

        private static string ResolveExecutablePath()
        {
            var envPath = Environment.GetEnvironmentVariable("WILEYWIDGET_EXE");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                return envPath;
            }

            // Check multiple possible build configurations and directories
            var possibleBaseDirs = new[]
            {
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory ?? ".", "..", "..", "..", "..", "..", "src", "WileyWidget.WinForms", "bin", "Debug")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory ?? ".", "..", "..", "..", "..", "..", "src", "WileyWidget.WinForms", "bin", "Release")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory ?? ".", "..", "..", "..", "..", "..", "src", "WileyWidget.WinForms", "bin"))
            };

            foreach (var baseDir in possibleBaseDirs)
            {
                if (!Directory.Exists(baseDir))
                {
                    continue;
                }

                // Try full TFM path (net9.0-windows10.0.26100.0)
                var fullTfm = Path.Combine(baseDir, "net9.0-windows10.0.26100.0", "WileyWidget.WinForms.exe");
                if (File.Exists(fullTfm))
                {
                    return fullTfm;
                }

                // Try standard net9.0-windows path
                var standard = Path.Combine(baseDir, "net9.0-windows", "WileyWidget.WinForms.exe");
                if (File.Exists(standard))
                {
                    return standard;
                }

                // Try framework-dependent path
                var fxDependent = Path.Combine(baseDir, "WileyWidget.WinForms.exe");
                if (File.Exists(fxDependent))
                {
                    return fxDependent;
                }
            }

            throw new FileNotFoundException("WileyWidget.WinForms.exe not found. Build the project or set WILEYWIDGET_EXE environment variable.");
        }

        private static void DismissLicensePopups(UiTestSession.SessionState session)
        {
            // Wait for popups to appear (event-pumped)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 2000)
            {
                try { System.Windows.Forms.Application.DoEvents(); } catch { }
            }

            try
            {
                // Find windows more efficiently - limit to windows that might be popups
                // Avoid full desktop scan by using a more targeted approach
                var desktop = session.Automation.GetDesktop();
                var allWindows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));

                foreach (var window in allWindows)
                {
                    try
                    {
                        var title = window.Name;
                        if (title != null && (title.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase) ||
                                             title.Contains("License", StringComparison.OrdinalIgnoreCase) ||
                                             title.Contains("Registration", StringComparison.OrdinalIgnoreCase) ||
                                             title.Contains("Trial", StringComparison.OrdinalIgnoreCase)))
                        {
                            // Try to close it
                            var closeButton = window.FindFirstDescendant(cf =>
                                cf.ByControlType(ControlType.Button).And(
                                    cf.ByName("OK").Or(cf.ByName("Close")).Or(cf.ByName("Cancel"))));

                            if (closeButton != null)
                            {
                                closeButton.AsButton().Click();
                            }
                            else
                            {
                                window.AsWindow().Close();
                            }
                        }
                    }
                    catch
                    {
                        // Ignore errors on individual window inspection
                    }
                }
            }
            catch
            {
                // Ignore popup dismissal errors
            }
        }

        public void Dispose()
        {
            try
            {
                // DO NOT dispose session here immediately - let the collection fixture manage it
                // This prevents killing the app when the fixture is disposed between tests
                // Only dispose if app has actually exited
                if (Session != null && Session.App.HasExited)
                {
                    Session.Dispose();
                    Session = null;
                }

                // Give time for graceful cleanup
                System.Threading.Thread.Sleep(500);
            }
            catch
            {
                // Ignore disposal errors
            }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }
    }

    [CollectionDefinition("UI Tests")]
    public class UiTestCollection : ICollectionFixture<UiTestSessionFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    /// <summary>
    /// FlaUI smoke tests covering the primary WinForms views opened from MainForm navigation.
    /// These are opt-in and return early when no interactive UI runner is available.
    /// Uses shared test session fixture to prevent app disposal on test failures.
    /// </summary>
    [Collection("UI Tests")]
    public sealed class AllViewsUITests : IDisposable
    {
        private readonly UiTestSessionFixture _fixture;

        // Session is managed by fixture - not disposed here to prevent killing app on test failure
#pragma warning disable CA2213 // Disposable fields should be disposed
        private UiTestSession.SessionState? _session;
#pragma warning restore CA2213 // Disposable fields should be disposed

        private bool _disposed;
        private static readonly object _testLock = new object();

        public AllViewsUITests(UiTestSessionFixture fixture)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));

            // Ensure only one test runs at a time to prevent UI automation conflicts
            System.Threading.Monitor.Enter(_testLock);
        }

        [StaTheory]
        [InlineData("Nav_Dashboard", "Dashboard", "Toolbar_Load", "Load Dashboard")]
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

            // Wait for panel to stabilize (docking activation)
            System.Threading.Thread.Sleep(3000);

            // Verify child controls exist post-activation
            if (!string.IsNullOrEmpty(elementAutomationId))
            {
                var childControl = WaitForElement(viewWindow, cf => cf.ByAutomationId(elementAutomationId).Or(cf.ByName(fallbackElementName)), timeoutSeconds: 15);
                Assert.NotNull(childControl); // No message parameter - use separate assertion if child not found
                if (childControl == null)
                {
                    throw new Xunit.Sdk.XunitException($"{elementAutomationId ?? fallbackElementName} not found after {expectedTitleContains} activation");
                }
            }

            var target = WaitForElement(viewWindow, cf => BuildElementCondition(cf, elementAutomationId, fallbackElementName));
            Assert.NotNull(target);

            // Fallback verification: Check status bar text as alternative validation
            // Status bars typically contain "Ready", panel titles, or load confirmations
            var statusBar = WaitForElement(viewWindow, cf => cf.ByControlType(ControlType.StatusBar), timeoutSeconds: 5);
            if (statusBar != null)
            {
                var statusLabels = statusBar.FindAllChildren(cf => cf.ByControlType(ControlType.Text));
                var hasStatusText = statusLabels.Any(label => !string.IsNullOrWhiteSpace(label.Name));
                Assert.True(hasStatusText, $"Status bar found but contains no readable text after {expectedTitleContains} activation");
            }

            // Close the view to ensure test isolation
            NavigationHelper.CloseView(viewWindow);
        }

        [StaFact]
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

        [StaFact]
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

        [StaFact]
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

        [StaFact]
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

        [StaFact]
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

        [StaFact]
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

            Assert.NotNull(dockToggle);

            dockToggle.Click();

            var changed = Retry.While(() => string.Equals(stateLabel.Name, initial, StringComparison.Ordinal), same => same, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200));
            Assert.False(changed.Success, "State text did not change after toggling docking buttons.");
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
            _session = _fixture.GetOrStartSession();
        }

        private UiTestSession.SessionState RequireSession()
        {
            return _session ?? throw new InvalidOperationException("Application has not been started.");
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                // DO NOT dispose session here - it's managed by the collection fixture
                // This prevents killing the app when a single test fails
                _session = null;

                // Only clean up test-specific processes when NOT running in IDE/vstest
                var vstestHost = Environment.GetEnvironmentVariable("VSTEST_HOST_PROCESSID");
                var isInIDE = !string.IsNullOrWhiteSpace(vstestHost) ||
                             !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VSCODE_PID"));

                if (!isInIDE)
                {
                    // Kill any lingering WileyWidget test processes
                    var processes = System.Diagnostics.Process.GetProcessesByName("WileyWidget.WinForms");
                    foreach (var p in processes)
                    {
                        try
                        {
                            // Double-check this is our test process, not a running dev instance
                            var processPath = p.MainModule?.FileName?.ToLowerInvariant();
                            if (processPath != null && processPath.Contains("debug", StringComparison.OrdinalIgnoreCase) && processPath.Contains("e2etests", StringComparison.OrdinalIgnoreCase))
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
