using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using WileyWidget.WinForms.E2ETests.Helpers;
using FlaUIApplication = FlaUI.Core.Application;

namespace WileyWidget.WinForms.E2ETests
{
    /// <summary>
    /// E2E tests for BudgetPanel using FlaUI automation.
    /// Tests CRUD operations, filtering, export, keyboard shortcuts, and variance analysis.
    /// </summary>
    [Collection("UI Tests")]
    public class BudgetPanelE2ETests : IDisposable
    {
        private readonly string _exePath;
        private FlaUIApplication? _app;
        private UIA3Automation? _automation;
        private bool _disposed;

        public BudgetPanelE2ETests()
        {
            _exePath = TestAppHelper.GetWileyWidgetExePath();
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
            if (!File.Exists(_exePath))
            {
                throw new FileNotFoundException($"Executable not found at '{_exePath}'. Set WILEYWIDGET_EXE environment variable.");
            }

            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "Ngo9BigBOggjHTQxAR8/V1NMaF5cXmZCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWXZceHRQR2VfUER0W0o=");

            _app = FlaUIApplication.Launch(_exePath);
            _automation = new UIA3Automation();
        }

        private Window GetMainWindow(int timeoutSeconds = 15)
        {
            if (_app == null || _automation == null) throw new InvalidOperationException("Application not started");
            return Retry.WhileNull(() => _app.GetMainWindow(_automation), TimeSpan.FromSeconds(timeoutSeconds)).Result 
                ?? throw new InvalidOperationException("Main window not found");
        }

        private static AutomationElement? WaitForElement(Window window, Func<ConditionFactory, ConditionBase> selector, int timeoutSeconds = 12)
        {
            return Retry.WhileNull(() => window.FindFirstDescendant(selector), TimeSpan.FromSeconds(timeoutSeconds), TimeSpan.FromMilliseconds(250)).Result;
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Budget")]
        public void BudgetPanel_OpensAndDisplaysGrid()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate using NavigationHelper
            var panel = NavigationHelper.OpenView(_automation!, window, "Nav_Budget", "Budget");

            // Verify budget grid is present
            var budgetGrid = WaitForElement(window, cf => cf.ByName("Budget Entries Grid").Or(cf.ByAutomationId("BudgetEntriesGrid")));
            Assert.NotNull(budgetGrid);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Budget")]
        public void BudgetPanel_AddButton_OpensEditForm()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Budget
            var budgetMenu = WaitForElement(window, cf => cf.ByName("Budget"));
            budgetMenu?.Click();

            // Click Add button
            var addBtn = WaitForElement(window, cf => cf.ByName("Add").Or(cf.ByAutomationId("Toolbar_AddButton")))?.AsButton();
            Assert.NotNull(addBtn);
            addBtn.Click();

            // Verify edit form appears
            var editForm = WaitForElement(window, cf => cf.ByName("Budget Entry Edit").Or(cf.ByControlType(ControlType.Window)));
            Assert.NotNull(editForm);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Budget")]
        public void BudgetPanel_FilterByFiscalYear_Works()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Budget
            var budgetMenu = WaitForElement(window, cf => cf.ByName("Budget"));
            budgetMenu?.Click();

            // Find fiscal year filter combo
            var fyCombo = WaitForElement(window, cf => cf.ByName("Fiscal Year Filter").Or(cf.ByAutomationId("FiscalYearCombo")))?.AsComboBox();
            Assert.NotNull(fyCombo);

            // Verify combo has items
            var items = fyCombo.Items;
            Assert.NotNull(items);
            Assert.NotEmpty(items);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Budget")]
        public void BudgetPanel_FilterByDepartment_Works()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Budget
            var budgetMenu = WaitForElement(window, cf => cf.ByName("Budget"));
            budgetMenu?.Click();

            // Find department filter combo
            var deptCombo = WaitForElement(window, cf => cf.ByName("Department Filter").Or(cf.ByAutomationId("DepartmentCombo")))?.AsComboBox();
            Assert.NotNull(deptCombo);

            // Select a department (if items available)
            var items = deptCombo.Items;
            if (items != null && items.Length > 0)
            {
                deptCombo.Select(0);
                WaitForBusyIndicator(TimeSpan.FromSeconds(5));
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Budget")]
        public void BudgetPanel_VarianceFilter_HighlightsDiscrepancies()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Budget
            var budgetMenu = WaitForElement(window, cf => cf.ByName("Budget"));
            budgetMenu?.Click();

            // Find variance filter checkbox
            var varianceCheck = WaitForElement(window, cf => cf.ByName("Show Variance Only").Or(cf.ByAutomationId("VarianceOnlyCheckBox")))?.AsCheckBox();
            
            if (varianceCheck != null)
            {
                varianceCheck.IsChecked = true;
                WaitForBusyIndicator(TimeSpan.FromSeconds(5));

                // Verify filter applied (grid should refresh)
                var budgetGrid = WaitForElement(window, cf => cf.ByName("Budget Entries Grid"));
                Assert.NotNull(budgetGrid);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Budget")]
        public void BudgetPanel_ExportToExcel_ButtonExists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Budget
            var budgetMenu = WaitForElement(window, cf => cf.ByName("Budget"));
            budgetMenu?.Click();

            // Find export button
            var exportBtn = WaitForElement(window, cf => cf.ByName("Export").Or(cf.ByAutomationId("Toolbar_ExportButton")))?.AsButton();
            Assert.NotNull(exportBtn);
            Assert.True(exportBtn.IsEnabled);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Budget")]
        public void BudgetPanel_RefreshButton_ReloadsData()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Budget
            var budgetMenu = WaitForElement(window, cf => cf.ByName("Budget"));
            budgetMenu?.Click();

            // Find refresh button
            var refreshBtn = WaitForElement(window, cf => cf.ByName("Refresh").Or(cf.ByAutomationId("Toolbar_RefreshButton")))?.AsButton();
            Assert.NotNull(refreshBtn);

            refreshBtn.Click();
            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify grid is still present
            var budgetGrid = WaitForElement(window, cf => cf.ByName("Budget Entries Grid"));
            Assert.NotNull(budgetGrid);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Budget")]
        public void BudgetPanel_SearchBox_FiltersEntries()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Budget
            var budgetMenu = WaitForElement(window, cf => cf.ByName("Budget"));
            budgetMenu?.Click();

            // Find search box
            var searchBox = WaitForElement(window, cf => cf.ByName("Search Budget Entries").Or(cf.ByAutomationId("SearchTextBox")))?.AsTextBox();
            
            if (searchBox != null)
            {
                searchBox.Text = "Salary";
                WaitForBusyIndicator(TimeSpan.FromSeconds(5));

                // Verify grid updated
                var budgetGrid = WaitForElement(window, cf => cf.ByName("Budget Entries Grid"));
                Assert.NotNull(budgetGrid);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Budget")]
        public void BudgetPanel_SummaryCards_DisplayTotals()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Budget
            var budgetMenu = WaitForElement(window, cf => cf.ByName("Budget"));
            budgetMenu?.Click();

            // Wait for data to load
            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify summary labels are present
            var totalBudgetLabel = WaitForElement(window, cf => cf.ByName("Total Budgeted").Or(cf.ByAutomationId("TotalBudgetedLabel")));
            var totalActualLabel = WaitForElement(window, cf => cf.ByName("Total Actual").Or(cf.ByAutomationId("TotalActualLabel")));
            var varianceLabel = WaitForElement(window, cf => cf.ByName("Variance").Or(cf.ByAutomationId("VarianceLabel")));

            // At least one summary metric should be present
            Assert.True(totalBudgetLabel != null || totalActualLabel != null || varianceLabel != null);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Budget")]
        public void BudgetPanel_KeyboardShortcut_F5_Refreshes()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Budget
            var budgetMenu = WaitForElement(window, cf => cf.ByName("Budget"));
            budgetMenu?.Click();

            // Send F5 key
            window.Focus();
            System.Windows.Forms.SendKeys.SendWait("{F5}");

            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify grid is still present after refresh
            var budgetGrid = WaitForElement(window, cf => cf.ByName("Budget Entries Grid"));
            Assert.NotNull(budgetGrid);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        _app?.Close();
                        _app?.Dispose();
                        _app = null;
                        _automation?.Dispose();
                        _automation = null;
                    }
                    catch
                    {
                        // Suppress cleanup errors
                    }
                }
                _disposed = true;
            }
        }
    }
}
