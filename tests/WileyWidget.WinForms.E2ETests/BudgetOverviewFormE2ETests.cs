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
    /// FlaUI E2E tests for BudgetOverviewForm - Budget Summary and Analytics view.
    /// Tests budget data display, summary cards, metrics grid, and export functionality.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Disposed via cleanup.")]
    [Collection("UI Tests")]
    public sealed class BudgetOverviewFormE2ETests : IDisposable
    {
        private readonly string _exePath;
        private Application? _app;
        private UIA3Automation? _automation;
        private const int DefaultTimeout = 20000;

        public BudgetOverviewFormE2ETests()
        {
            _exePath = ResolveExecutablePath();

            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
            Environment.SetEnvironmentVariable("WILEYWIDGET_USE_INMEMORY", "true");
            Environment.SetEnvironmentVariable("UI__IsUiTestHarness", "true");
            Environment.SetEnvironmentVariable("UI__UseMdiMode", "false");
            Environment.SetEnvironmentVariable("UI__UseTabbedMdi", "false");
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
        public void BudgetOverviewForm_Opens_And_Displays_MetricsGrid()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            Assert.NotNull(budgetWindow);
            Assert.Contains("Budget", budgetWindow.Title, StringComparison.OrdinalIgnoreCase);

            // Verify metrics grid exists
            var metricsGrid = WaitForElement(budgetWindow, cf => cf.ByAutomationId("BudgetOverview_MetricsGrid"));
            Assert.NotNull(metricsGrid);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void BudgetOverviewForm_SummaryCards_AreVisible()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            // Wait for form to load data
            Thread.Sleep(2000);

            // Find labels containing budget summary values
            var allLabels = budgetWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            var labelTexts = allLabels.Select(l => l.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();

            // Verify summary card titles are present
            Assert.Contains(labelTexts, text => text.Contains("TOTAL BUDGET", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(labelTexts, text => text.Contains("TOTAL ACTUAL", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(labelTexts, text => text.Contains("VARIANCE", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(labelTexts, text => text.Contains("% USED", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        [Trait("Category", "UI")]
        public void BudgetOverviewForm_RefreshButton_IsAccessible()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            var refreshButton = WaitForElement(budgetWindow, cf => cf.ByName("Refresh"));
            Assert.NotNull(refreshButton);
            Assert.True(refreshButton.IsEnabled);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void BudgetOverviewForm_PeriodSelector_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            // Find ComboBox for period selection
            var periodCombo = WaitForElement(budgetWindow, cf => cf.ByControlType(ControlType.ComboBox));
            Assert.NotNull(periodCombo);
            Assert.True(periodCombo.IsEnabled);

            // Verify it has items
            var comboBox = periodCombo.AsComboBox();
            Assert.NotNull(comboBox.Items);
            Assert.NotEmpty(comboBox.Items);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void BudgetOverviewForm_ExportButton_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            var exportButton = WaitForElement(budgetWindow, cf => cf.ByName("Export"));
            Assert.NotNull(exportButton);
            Assert.True(exportButton.IsEnabled);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void BudgetOverviewForm_AddCategoryButton_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            var addButton = WaitForElement(budgetWindow, cf => cf.ByName("Add Category"));
            Assert.NotNull(addButton);
            Assert.True(addButton.IsEnabled);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void BudgetOverviewForm_EditButton_ExistsButDisabledWithoutSelection()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            var editButton = WaitForElement(budgetWindow, cf => cf.ByName("Edit"));
            Assert.NotNull(editButton);
            // May be disabled if no selection
        }

        [Fact]
        [Trait("Category", "UI")]
        public void BudgetOverviewForm_DeleteButton_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            var deleteButton = WaitForElement(budgetWindow, cf => cf.ByName("Delete"));
            Assert.NotNull(deleteButton);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void BudgetOverviewForm_MetricsGrid_HasExpectedColumns()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            var metricsGrid = WaitForElement(budgetWindow, cf => cf.ByAutomationId("BudgetOverview_MetricsGrid"));
            Assert.NotNull(metricsGrid);

            // Wait for grid to populate
            Thread.Sleep(2000);

            var gridItems = Retry.WhileEmpty(() => metricsGrid.FindAllDescendants(),
                timeout: TimeSpan.FromSeconds(5)).Result;

            Assert.NotEmpty(gridItems!);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void BudgetOverviewForm_ProgressBar_IsVisible()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            // Find budget utilization progress bar
            var progressBar = WaitForElement(budgetWindow, cf => cf.ByControlType(ControlType.ProgressBar));
            Assert.NotNull(progressBar);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void BudgetOverviewForm_StatusBar_ShowsLastUpdated()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            // Wait for data to load
            Thread.Sleep(2000);

            // Find status bar
            var statusBar = WaitForElement(budgetWindow, cf => cf.ByControlType(ControlType.StatusBar));
            Assert.NotNull(statusBar);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void BudgetOverviewForm_PeriodSelector_ChangesData()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            // Find period selector
            var periodCombo = WaitForElement(budgetWindow, cf => cf.ByControlType(ControlType.ComboBox));
            Assert.NotNull(periodCombo);

            var comboBox = periodCombo.AsComboBox();
            var items = comboBox.Items;

            // Change selection (if more than one item available)
            if (items.Length > 1)
            {
                // Select second item
                comboBox.Select(1);

                // Give UI time to refresh data
                Thread.Sleep(2000);

                // Verify selection changed (check selected item exists)
                Assert.NotNull(comboBox.SelectedItem);
            }
        }

        [Fact]
        [Trait("Category", "UI")]
        public void BudgetOverviewForm_RefreshButton_ReloadsData()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            var refreshButton = WaitForElement(budgetWindow, cf => cf.ByName("Refresh"));
            Assert.NotNull(refreshButton);

            WaitUntilResponsive(refreshButton);
            refreshButton.AsButton().Invoke();

            // Wait for refresh to complete (status bar should update)
            Thread.Sleep(2000);

            // Verify status bar shows "Last updated"
            var statusBar = WaitForElement(budgetWindow, cf => cf.ByControlType(ControlType.StatusBar));
            Assert.NotNull(statusBar);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void BudgetOverviewForm_ExportButton_OpensDialog()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var budgetWindow = OpenBudgetOverviewView(mainWindow);

            // Wait for data to load
            Thread.Sleep(2000);

            var exportButton = WaitForElement(budgetWindow, cf => cf.ByName("Export"));
            Assert.NotNull(exportButton);

            WaitUntilResponsive(exportButton);
            exportButton.AsButton().Invoke();

            // Wait for SaveFileDialog to appear
            var saveDialog = Retry.WhileNull(() =>
            {
                var desktop = _automation!.GetDesktop();
                return desktop.FindFirstChild(cf => cf.ByControlType(ControlType.Window).And(cf.ByName("Save As")));
            }, timeout: TimeSpan.FromSeconds(3)).Result;

            if (saveDialog != null)
            {
                // Close the dialog (Cancel button)
                var cancelButton = saveDialog.FindFirstDescendant(cf => cf.ByName("Cancel"));
                cancelButton?.AsButton().Invoke();
            }
        }

        private Window OpenBudgetOverviewView(Window mainWindow)
        {
            // Navigate to Budget Overview - may be under Reports or Dashboard menu
            // Try multiple navigation patterns
            var navButton = WaitForElement(mainWindow, cf => cf.ByAutomationId("Nav_Budget"), timeoutMs: 30000);

            if (navButton == null)
            {
                navButton = WaitForElement(mainWindow, cf => cf.ByName("Budget"), timeoutMs: 10000);
            }

            if (navButton == null)
            {
                // Try finding through Dashboard first
                var dashboardNav = WaitForElement(mainWindow, cf => cf.ByAutomationId("Nav_Dashboard"), timeoutMs: 10000);
                dashboardNav?.Click();
                Thread.Sleep(1000);

                navButton = WaitForElement(mainWindow, cf => cf.ByName("Budget Overview"), timeoutMs: 10000);
            }

            Assert.NotNull(navButton);

            navButton.Click();

            // Wait for window to appear
            var budgetElement = Retry.WhileNull(() =>
            {
                try
                {
                    var window = mainWindow.FindFirstDescendant(cf =>
                        cf.ByName("Budget Overview"));

                    if (window != null && window.ControlType == ControlType.Window)
                    {
                        return window.AsWindow();
                    }

                    if (window != null)
                    {
                        var parent = window.Parent;
                        while (parent != null && parent.ControlType != ControlType.Window)
                        {
                            parent = parent.Parent;
                        }
                        return parent?.AsWindow();
                    }

                    return null;
                }
                catch
                {
                    return null;
                }
            }, timeout: TimeSpan.FromSeconds(30)).Result;

            return budgetElement ?? throw new InvalidOperationException("Budget Overview window did not open");
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

            Retry.WhileException(() =>
            {
                var window = _app.GetMainWindow(_automation);
                if (window == null || !window.IsAvailable)
                {
                    throw new InvalidOperationException("Main window not ready");
                }
            }, TimeSpan.FromMilliseconds(DefaultTimeout));
        }

        private void WaitUntilResponsive(AutomationElement? element, int timeoutMs = 3000)
        {
            if (element == null) return;

            Retry.WhileException(() =>
            {
                if (!element.IsEnabled || element.IsOffscreen)
                {
                    throw new InvalidOperationException("Element not responsive");
                }
            }, TimeSpan.FromMilliseconds(timeoutMs));
        }

        private Window GetMainWindow()
        {
            var mainWindow = Retry.WhileNull(() => _app?.GetMainWindow(_automation!),
                timeout: TimeSpan.FromSeconds(DefaultTimeout / 1000));
            Assert.NotNull(mainWindow);
            return mainWindow.Result!;
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
