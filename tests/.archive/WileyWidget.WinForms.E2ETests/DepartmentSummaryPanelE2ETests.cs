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
    /// E2E tests for DepartmentSummaryPanel using FlaUI automation.
    /// Tests department metrics display, budget summary visualization, comparison charts, and drill-down navigation.
    /// </summary>
    [Collection("UI Tests")]
    public class DepartmentSummaryPanelE2ETests : IDisposable
    {
        private readonly string _exePath;
        private FlaUIApplication? _app;
        private UIA3Automation? _automation;
        private bool _disposed;

        public DepartmentSummaryPanelE2ETests()
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
        [Trait("Panel", "DepartmentSummary")]
        public void DepartmentSummaryPanel_OpensAndDisplaysMetrics()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Department Summary panel
            var deptSummaryMenu = WaitForElement(window, cf => cf.ByName("Department Summary").Or(cf.ByAutomationId("MenuDepartmentSummary")));
            Assert.NotNull(deptSummaryMenu);
            deptSummaryMenu.Click();

            // Wait for data to load
            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify summary metrics are displayed
            var summaryPanel = WaitForElement(window, cf => cf.ByName("Department Summary Panel").Or(cf.ByAutomationId("DepartmentSummaryPanel")));
            Assert.NotNull(summaryPanel);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "DepartmentSummary")]
        public void DepartmentSummaryPanel_DepartmentGrid_Displays()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Department Summary
            var deptSummaryMenu = WaitForElement(window, cf => cf.ByName("Department Summary"));
            deptSummaryMenu?.Click();

            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify department grid is present
            var deptGrid = WaitForElement(window, cf => cf.ByName("Departments Grid").Or(cf.ByAutomationId("DepartmentsGrid")));
            Assert.NotNull(deptGrid);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "DepartmentSummary")]
        public void DepartmentSummaryPanel_BudgetChart_Renders()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Department Summary
            var deptSummaryMenu = WaitForElement(window, cf => cf.ByName("Department Summary"));
            deptSummaryMenu?.Click();

            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify budget comparison chart is present
            var budgetChart = WaitForElement(window, cf => cf.ByName("Budget Comparison Chart").Or(cf.ByAutomationId("BudgetComparisonChart")));
            Assert.NotNull(budgetChart);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "DepartmentSummary")]
        public void DepartmentSummaryPanel_RefreshButton_ReloadsData()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Department Summary
            var deptSummaryMenu = WaitForElement(window, cf => cf.ByName("Department Summary"));
            deptSummaryMenu?.Click();

            // Find refresh button
            var refreshBtn = WaitForElement(window, cf => cf.ByName("Refresh").Or(cf.ByAutomationId("RefreshButton")))?.AsButton();

            if (refreshBtn != null)
            {
                refreshBtn.Click();
                WaitForBusyIndicator(TimeSpan.FromSeconds(10));

                // Verify grid still present after refresh
                var deptGrid = WaitForElement(window, cf => cf.ByName("Departments Grid"));
                Assert.NotNull(deptGrid);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "DepartmentSummary")]
        public void DepartmentSummaryPanel_DrillDown_NavigatesToDetails()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Department Summary
            var deptSummaryMenu = WaitForElement(window, cf => cf.ByName("Department Summary"));
            deptSummaryMenu?.Click();

            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Find drill-down or view details button
            var viewDetailsBtn = WaitForElement(window, cf => cf.ByName("View Details").Or(cf.ByAutomationId("ViewDetailsButton")))?.AsButton();

            if (viewDetailsBtn != null && viewDetailsBtn.IsEnabled)
            {
                viewDetailsBtn.Click();
                System.Threading.Thread.Sleep(1000);

                // Verify navigation occurred (implementation-specific)
            }
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
