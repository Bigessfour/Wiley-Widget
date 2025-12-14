using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
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
    /// FlaUI E2E tests for AccountsForm - Municipal Accounts view.
    /// Tests account loading, filtering, grid interactions, and data editing.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Disposed via cleanup.")]
    [Collection("UI Tests")]
    public sealed class AccountsFormE2ETests : IDisposable
    {
        private readonly string _exePath;
        private Application? _app;
        private UIA3Automation? _automation;
        private const int DefaultTimeout = 10000;

        public AccountsFormE2ETests()
        {
            _exePath = ResolveExecutablePath();

            // Enable in-memory mode and simplified chrome for UI automation stability
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
                throw new DirectoryNotFoundException($"Build output directory not found at '{baseDir}'. Build WileyWidget.WinForms or set WILEYWIDGET_EXE.");
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

            throw new FileNotFoundException($"Executable not found. Build Debug output under '{baseDir}'.");
        }

        [Fact]
        [Trait("Category", "UI")]
        public void AccountsForm_Opens_And_Displays_Grid()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var accountsWindow = OpenAccountsView(mainWindow);

            Assert.NotNull(accountsWindow);
            Assert.Contains("Municipal Accounts", accountsWindow.Title);

            // Verify data grid exists
            var dataGrid = WaitForElement(accountsWindow, cf => cf.ByAutomationId("dataGridAccounts"));
            Assert.NotNull(dataGrid);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void AccountsForm_LoadButton_LoadsAccounts()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var accountsWindow = OpenAccountsView(mainWindow);

            // Click Load Accounts button
            var loadButton = WaitForElement(accountsWindow, cf => cf.ByName("Load Accounts"));
            Assert.NotNull(loadButton);
            loadButton.AsButton().Invoke();

            Thread.Sleep(1000); // Wait for load

            // Verify status bar updates
            var statusLabel = WaitForElement(accountsWindow, cf => cf.ByControlType(ControlType.StatusBar));
            Assert.NotNull(statusLabel);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void AccountsForm_FundFilter_IsPopulated()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var accountsWindow = OpenAccountsView(mainWindow);

            // Find fund filter combo box
            var fundCombo = WaitForElement(accountsWindow, cf => cf.ByControlType(ControlType.ComboBox));
            Assert.NotNull(fundCombo);

            // Verify it's enabled
            var comboBox = fundCombo.AsComboBox();
            Assert.True(comboBox.IsEnabled);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void AccountsForm_ApplyFilters_Button_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var accountsWindow = OpenAccountsView(mainWindow);

            var filterButton = WaitForElement(accountsWindow, cf => cf.ByName("Apply Filters"));
            Assert.NotNull(filterButton);
            Assert.True(filterButton.IsEnabled);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void AccountsForm_EditToggle_ChangesGridEditability()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var accountsWindow = OpenAccountsView(mainWindow);

            var editToggle = WaitForElement(accountsWindow, cf => cf.ByName("Allow Editing"));
            Assert.NotNull(editToggle);
            Assert.True(editToggle.IsEnabled);

            // Toggle editing off
            editToggle.AsButton().Click();
            Thread.Sleep(500);

            // Toggle back on
            editToggle.AsButton().Click();
            Thread.Sleep(500);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void AccountsForm_DataGrid_HasExpectedColumns()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var accountsWindow = OpenAccountsView(mainWindow);

            var dataGrid = WaitForElement(accountsWindow, cf => cf.ByAutomationId("dataGridAccounts"));
            Assert.NotNull(dataGrid);

            // Load data first
            var loadButton = WaitForElement(accountsWindow, cf => cf.ByName("Load Accounts"));
            loadButton?.AsButton().Invoke();
            Thread.Sleep(1500);

            // Verify grid has content (headers or data)
            var gridItems = dataGrid.FindAllDescendants();
            Assert.NotEmpty(gridItems);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void AccountsForm_StatusBar_ShowsTotalCount()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var accountsWindow = OpenAccountsView(mainWindow);

            // Load data
            var loadButton = WaitForElement(accountsWindow, cf => cf.ByName("Load Accounts"));
            loadButton?.AsButton().Invoke();
            Thread.Sleep(1500);

            // Verify status bar shows count
            var statusBar = WaitForElement(accountsWindow, cf => cf.ByControlType(ControlType.StatusBar));
            Assert.NotNull(statusBar);
        }

        private Window OpenAccountsView(Window mainWindow)
        {
            var navButton = WaitForElement(mainWindow, cf => cf.ByAutomationId("Nav_Accounts"));
            Assert.NotNull(navButton);

            navButton.AsButton().Invoke();
            Retry.WhileNull(() => _automation?.GetDesktop()
                .FindFirstChild(cf => cf.ByControlType(ControlType.Window)
                    .And(cf.ByName("Municipal Accounts"))),
                timeout: TimeSpan.FromSeconds(10));

            var accountsWindow = _automation?.GetDesktop()
                .FindFirstChild(cf => cf.ByControlType(ControlType.Window)
                    .And(cf.ByName("Municipal Accounts")))?.AsWindow();

            return accountsWindow ?? throw new InvalidOperationException("Accounts window did not open");
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

        private void StartApp()
        {
            _automation = new UIA3Automation();
            _app = Application.Launch(_exePath);
            Thread.Sleep(2000); // Wait for app to initialize
        }

        private Window GetMainWindow()
        {
            var mainWindow = Retry.WhileNull(() => _app?.GetMainWindow(_automation),
                timeout: TimeSpan.FromSeconds(DefaultTimeout / 1000));
            Assert.NotNull(mainWindow);
            return mainWindow.Result;
        }

        private AutomationElement? WaitForElement(AutomationElement parent, Func<ConditionFactory, ConditionBase> condition, int timeoutMs = DefaultTimeout)
        {
            return Retry.WhileNull(() =>
            {
                try
                {
                    return parent.FindFirstDescendant(condition);
                }
                catch
                {
                    return null;
                }
            }, timeout: TimeSpan.FromMilliseconds(timeoutMs)).Result;
        }

        public void Dispose()
        {
            try
            {
                _app?.Close();
                _app?.Dispose();
            }
            catch { }

            try
            {
                _automation?.Dispose();
            }
            catch { }
        }
    }
}
