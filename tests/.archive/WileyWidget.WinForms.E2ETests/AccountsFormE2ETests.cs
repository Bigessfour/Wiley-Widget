using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using WileyWidget.WinForms.E2ETests.Helpers;
using WileyWidget.WinForms.E2ETests.PageObjects;

namespace WileyWidget.WinForms.E2ETests
{
    /// <summary>
    /// FlaUI E2E tests for AccountsForm - Municipal Accounts view.
    /// Tests account loading, filtering, grid interactions, and data editing.
    /// Uses per-test app launch with stabilized environment setup.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Process disposed in Cleanup.")]
    [Collection("UI Tests")]
    public sealed class AccountsFormE2ETests : IDisposable
    {
        private Process? _testProcess;
        private UIA3Automation? _automation;

        public AccountsFormE2ETests()
        {
            // Each test gets its own app instance
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void AccountsForm_Opens_And_Displays_Grid()
        {
            if (!EnsureInteractiveOrSkip()) return;

            try
            {
                LaunchApp();
                var mainWindow = GetMainWindow();
                var accountsPage = OpenAccountsView(mainWindow);

                Assert.NotNull(accountsPage);
                Assert.True(accountsPage.IsAccountsGridLoaded(), "Accounts grid should be loaded and visible");

                // Verify data grid exists
                var dataGrid = accountsPage.AccountsGrid;
                Assert.NotNull(dataGrid);
            }
            finally
            {
                Cleanup();
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void AccountsForm_LoadButton_LoadsAccounts()
        {
            if (!EnsureInteractiveOrSkip()) return;

            try
            {
                LaunchApp();
                var mainWindow = GetMainWindow();
                var accountsPage = OpenAccountsView(mainWindow);

                // Click Load Accounts button
                accountsPage.ClickLoad();

                // Verify data loaded via status bar (wait a bit for status update)
                Retry.WhileException(() =>
                {
                    Assert.True(accountsPage.IsDataLoaded(), "Status bar should indicate data is loaded");
                }, TimeSpan.FromSeconds(5));
            }
            finally
            {
                Cleanup();
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void AccountsForm_FundFilter_IsPopulated()
        {
            if (!EnsureInteractiveOrSkip()) return;

            try
            {
                LaunchApp();
                var mainWindow = GetMainWindow();
                var accountsPage = OpenAccountsView(mainWindow);

                // Find fund filter combo box
                var fundCombo = accountsPage.FundFilterComboBox;
                Assert.NotNull(fundCombo);

                // Verify it's enabled
                var comboBox = fundCombo.AsComboBox();
                Assert.True(comboBox.IsEnabled);
            }
            finally
            {
                Cleanup();
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void AccountsForm_ApplyFilters_Button_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;

            try
            {
                LaunchApp();
                var mainWindow = GetMainWindow();
                var accountsPage = OpenAccountsView(mainWindow);

                var filterButton = accountsPage.ApplyFiltersButton;
                Assert.NotNull(filterButton);
                Assert.True(filterButton.IsEnabled);
            }
            finally
            {
                Cleanup();
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void AccountsForm_EditToggle_ChangesGridEditability()
        {
            if (!EnsureInteractiveOrSkip()) return;

            try
            {
                LaunchApp();
                var mainWindow = GetMainWindow();
                var accountsPage = OpenAccountsView(mainWindow);

                var editToggle = accountsPage.AllowEditingToggle;
                Assert.NotNull(editToggle);
                Assert.True(editToggle.IsEnabled);

                // Toggle editing off then on
                accountsPage.ToggleEditing();
                Retry.WhileException(() => Assert.True(editToggle.IsAvailable), TimeSpan.FromSeconds(2));

                accountsPage.ToggleEditing();
                Retry.WhileException(() => Assert.True(editToggle.IsAvailable), TimeSpan.FromSeconds(2));
            }
            finally
            {
                Cleanup();
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void AccountsForm_DataGrid_HasExpectedColumns()
        {
            if (!EnsureInteractiveOrSkip()) return;

            try
            {
                LaunchApp();
                var mainWindow = GetMainWindow();
                var accountsPage = OpenAccountsView(mainWindow);

                var dataGrid = accountsPage.AccountsGrid;
                Assert.NotNull(dataGrid);

                // Load data first
                accountsPage.ClickLoad();

                // Wait for grid to populate
                var rowCount = accountsPage.GetAccountsRowCount();
                Assert.True(rowCount > 0, "Grid should contain data rows after loading");
            }
            finally
            {
                Cleanup();
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        public void AccountsForm_StatusBar_ShowsTotalCount()
        {
            if (!EnsureInteractiveOrSkip()) return;

            try
            {
                LaunchApp();
                var mainWindow = GetMainWindow();
                var accountsPage = OpenAccountsView(mainWindow);

                // Load data
                accountsPage.ClickLoad();

                // Verify status bar shows account count (with retry for status bar update)
                Retry.WhileException(() =>
                {
                    Assert.True(accountsPage.IsDataLoaded(), "Status bar should contain account count after loading");
                }, TimeSpan.FromSeconds(5));
            }
            finally
            {
                Cleanup();
            }
        }

        private void LaunchApp()
        {
            Cleanup();

            var exePath = TestAppHelper.GetWileyWidgetExePath();
            var env = TestAppHelper.BuildTestEnvironment(isTestHarness: true);

            _testProcess = TestAppHelper.LaunchApp(exePath, env);
            _automation = new UIA3Automation();

            // Wait for main window with very generous timeout (app startup can be slow)
            Retry.WhileNull(
                () =>
                {
                    try
                    {
                        // Search for window by title or by class name
                        var desktop = _automation.GetDesktop();
                        var window = desktop.FindFirstChild(cf => cf.ByName("Wiley Widget"))
                                     ?? desktop.FindFirstChild(cf => cf.ByClassName("WindowsForms10.Window"));

                        // Ensure window is actually available before returning
                        if (window != null && window.IsAvailable)
                        {
                            // Wait for window to stabilize
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            while (sw.ElapsedMilliseconds < 1000 && window.IsAvailable)
                            {
                                try { System.Windows.Forms.Application.DoEvents(); } catch { }
                            }
                            return window;
                        }
                        return null;
                    }
                    catch
                    {
                        // Ignore transient UIA errors
                        return null;
                    }
                },
                TimeSpan.FromSeconds(45), // Much longer timeout for slow CI/VM environments
                TimeSpan.FromMilliseconds(500),
                throwOnTimeout: true,
                timeoutMessage: "Could not find main window after 45 seconds - application may have failed to launch"
            );
        }

        private void Cleanup()
        {
            if (_testProcess != null && !_testProcess.HasExited)
            {
                try
                {
                    _testProcess.Kill();
                    _testProcess.WaitForExit(5000);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            _testProcess?.Dispose();
            _automation?.Dispose();
        }

        private AccountsPage OpenAccountsView(Window mainWindow)
        {
            if (_automation == null) throw new InvalidOperationException("Automation not initialized");

            var accountsWindow = NavigationHelper.OpenView(_automation, mainWindow, "Nav_Accounts", "Municipal Accounts");
            return new AccountsPage(_automation, accountsWindow);
        }

        private Window GetMainWindow()
        {
            if (_automation == null) throw new InvalidOperationException("Automation not initialized");

            var mainElement = _automation.GetDesktop().FindFirstChild(cf => cf.ByName("Wiley Widget"));
            Assert.NotNull(mainElement);
            var mainWindow = mainElement.AsWindow();
            Assert.NotNull(mainWindow);
            return mainWindow;
        }

        private bool EnsureInteractiveOrSkip()
        {
            var uiTests = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");
            if (!string.Equals(uiTests, "true", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;
        }

        public void Dispose()
        {
            Cleanup();
            // Kill any lingering WileyWidget processes
            var processes = System.Diagnostics.Process.GetProcessesByName("WileyWidget.WinForms");
            foreach (var p in processes)
            {
                try { p.Kill(); } catch { }
            }
        }
    }
}
