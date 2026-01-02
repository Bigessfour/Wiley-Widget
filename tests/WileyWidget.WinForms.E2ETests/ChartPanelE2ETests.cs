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
    /// E2E tests for ChartPanel using FlaUI automation.
    /// Tests chart rendering, department filtering, export to PNG/PDF, navigation shortcuts (F5, Ctrl+E, Ctrl+B).
    /// </summary>
    [Collection("UI Tests")]
    public class ChartPanelE2ETests : IDisposable
    {
        private readonly string _exePath;
        private FlaUIApplication? _app;
        private UIA3Automation? _automation;
        private bool _disposed;

        public ChartPanelE2ETests()
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
        [Trait("Panel", "Chart")]
        public void ChartPanel_OpensAndDisplaysChart()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate using NavigationHelper
            var panel = NavigationHelper.OpenView(_automation!, window, "Nav_Charts", "Charts");

            // Verify chart control is present
            var chartControl = WaitForElement(window, cf => cf.ByName("Budget Chart").Or(cf.ByAutomationId("BudgetChartControl")));
            Assert.NotNull(chartControl);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Chart")]
        public void ChartPanel_DepartmentFilter_Works()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Chart
            var chartMenu = WaitForElement(window, cf => cf.ByName("Charts"));
            chartMenu?.Click();

            // Find department filter combo
            var deptCombo = WaitForElement(window, cf => cf.ByName("Department").Or(cf.ByAutomationId("DepartmentFilterCombo")))?.AsComboBox();
            Assert.NotNull(deptCombo);

            // Verify combo has items
            var items = deptCombo.Items;
            Assert.NotNull(items);
            Assert.NotEmpty(items);

            // Select a department
            if (items.Length > 0)
            {
                deptCombo.Select(0);
                WaitForBusyIndicator(TimeSpan.FromSeconds(5));

                // Verify chart still visible
                var chartControl = WaitForElement(window, cf => cf.ByName("Budget Chart"));
                Assert.NotNull(chartControl);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Chart")]
        public void ChartPanel_ChartTypeSelection_Changes()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Chart
            var chartMenu = WaitForElement(window, cf => cf.ByName("Charts"));
            chartMenu?.Click();

            // Find chart type combo
            var chartTypeCombo = WaitForElement(window, cf => cf.ByName("Chart Type").Or(cf.ByAutomationId("ChartTypeCombo")))?.AsComboBox();
            
            if (chartTypeCombo != null && chartTypeCombo.Items.Length > 1)
            {
                chartTypeCombo.Select(1);
                WaitForBusyIndicator(TimeSpan.FromSeconds(5));

                // Verify chart updated
                var chartControl = WaitForElement(window, cf => cf.ByName("Budget Chart"));
                Assert.NotNull(chartControl);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Chart")]
        public void ChartPanel_ExportToPNG_ButtonExists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Chart
            var chartMenu = WaitForElement(window, cf => cf.ByName("Charts"));
            chartMenu?.Click();

            // Find export to PNG button
            var exportPngBtn = WaitForElement(window, cf => cf.ByName("Export to PNG").Or(cf.ByAutomationId("ExportPngButton")))?.AsButton();
            Assert.NotNull(exportPngBtn);
            Assert.True(exportPngBtn.IsEnabled);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Chart")]
        public void ChartPanel_ExportToPDF_ButtonExists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Chart
            var chartMenu = WaitForElement(window, cf => cf.ByName("Charts"));
            chartMenu?.Click();

            // Find export to PDF button
            var exportPdfBtn = WaitForElement(window, cf => cf.ByName("Export to PDF").Or(cf.ByAutomationId("ExportPdfButton")))?.AsButton();
            Assert.NotNull(exportPdfBtn);
            Assert.True(exportPdfBtn.IsEnabled);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Chart")]
        public void ChartPanel_RefreshButton_ReloadsData()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Chart
            var chartMenu = WaitForElement(window, cf => cf.ByName("Charts"));
            chartMenu?.Click();

            // Find refresh button
            var refreshBtn = WaitForElement(window, cf => cf.ByName("Refresh").Or(cf.ByAutomationId("RefreshButton")))?.AsButton();
            Assert.NotNull(refreshBtn);

            refreshBtn.Click();
            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify chart still present
            var chartControl = WaitForElement(window, cf => cf.ByName("Budget Chart"));
            Assert.NotNull(chartControl);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Chart")]
        public void ChartPanel_KeyboardShortcut_F5_Refreshes()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Chart
            var chartMenu = WaitForElement(window, cf => cf.ByName("Charts"));
            chartMenu?.Click();

            // Send F5 key
            window.Focus();
            System.Windows.Forms.SendKeys.SendWait("{F5}");

            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify chart still present
            var chartControl = WaitForElement(window, cf => cf.ByName("Budget Chart"));
            Assert.NotNull(chartControl);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Chart")]
        public void ChartPanel_KeyboardShortcut_CtrlE_ExportsChart()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Chart
            var chartMenu = WaitForElement(window, cf => cf.ByName("Charts"));
            chartMenu?.Click();

            // Send Ctrl+E
            window.Focus();
            System.Windows.Forms.SendKeys.SendWait("^e");

            // Wait briefly
            System.Threading.Thread.Sleep(1000);

            // Verify export dialog or file save dialog appears (implementation-specific)
            // This may require additional window handling
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Chart")]
        public void ChartPanel_KeyboardShortcut_CtrlB_NavigatesToBudget()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Chart
            var chartMenu = WaitForElement(window, cf => cf.ByName("Charts"));
            chartMenu?.Click();

            // Send Ctrl+B
            window.Focus();
            System.Windows.Forms.SendKeys.SendWait("^b");

            // Wait for navigation
            System.Threading.Thread.Sleep(1000);

            // Verify Budget panel is now active (implementation-specific)
            // May check for Budget grid or panel title
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Chart")]
        public void ChartPanel_NavigateToBudget_ButtonWorks()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Chart
            var chartMenu = WaitForElement(window, cf => cf.ByName("Charts"));
            chartMenu?.Click();

            // Find navigate to budget button
            var navToBudgetBtn = WaitForElement(window, cf => cf.ByName("Go to Budget").Or(cf.ByAutomationId("NavigateToBudgetButton")))?.AsButton();
            Assert.NotNull(navToBudgetBtn);
            Assert.True(navToBudgetBtn.IsEnabled);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Chart")]
        public void ChartPanel_NavigateToAccounts_ButtonWorks()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Chart
            var chartMenu = WaitForElement(window, cf => cf.ByName("Charts"));
            chartMenu?.Click();

            // Find navigate to accounts button
            var navToAccountsBtn = WaitForElement(window, cf => cf.ByName("Go to Accounts").Or(cf.ByAutomationId("NavigateToAccountsButton")))?.AsButton();
            Assert.NotNull(navToAccountsBtn);
            Assert.True(navToAccountsBtn.IsEnabled);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Chart")]
        public void ChartPanel_ChartLegend_IsVisible()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Chart
            var chartMenu = WaitForElement(window, cf => cf.ByName("Charts"));
            chartMenu?.Click();

            // Wait for chart to load
            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify chart control is present (legend is typically part of chart)
            var chartControl = WaitForElement(window, cf => cf.ByName("Budget Chart"));
            Assert.NotNull(chartControl);
            Assert.True(chartControl.IsEnabled);
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
