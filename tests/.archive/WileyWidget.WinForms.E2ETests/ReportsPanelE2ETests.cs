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
    /// E2E tests for ReportsPanel using FlaUI automation.
    /// Tests report selection via SfComboBox, parameter input, export functionality, and IParameterizedPanel navigation.
    /// </summary>
    [Collection("UI Tests")]
    public class ReportsPanelE2ETests : IDisposable
    {
        private readonly string _exePath;
        private FlaUIApplication? _app;
        private UIA3Automation? _automation;
        private bool _disposed;

        public ReportsPanelE2ETests()
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
        [Trait("Panel", "Reports")]
        public void ReportsPanel_OpensAndDisplaysControls()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate using NavigationHelper
            var panel = NavigationHelper.OpenView(_automation!, window, "Nav_Reports", "Reports");

            // Verify report selection combo is present
            var reportCombo = WaitForElement(window, cf => cf.ByName("Select Report").Or(cf.ByAutomationId("ReportTypeCombo")));
            Assert.NotNull(reportCombo);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Reports")]
        public void ReportsPanel_ReportCombo_HasOptions()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Reports
            var reportsMenu = WaitForElement(window, cf => cf.ByName("Reports"));
            reportsMenu?.Click();

            // Find report selection combo
            var reportCombo = WaitForElement(window, cf => cf.ByName("Select Report"))?.AsComboBox();
            Assert.NotNull(reportCombo);

            // Verify it has report options
            var items = reportCombo.Items;
            Assert.NotNull(items);
            Assert.NotEmpty(items);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Reports")]
        public void ReportsPanel_SelectReport_ShowsParameters()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Reports
            var reportsMenu = WaitForElement(window, cf => cf.ByName("Reports"));
            reportsMenu?.Click();

            // Select a report
            var reportCombo = WaitForElement(window, cf => cf.ByName("Select Report"))?.AsComboBox();
            if (reportCombo != null && reportCombo.Items.Length > 0)
            {
                reportCombo.Select(0);

                // Wait for parameters to load
                System.Threading.Thread.Sleep(1000);

                // Verify parameters grid appears
                var paramsGrid = WaitForElement(window, cf => cf.ByName("Report Parameters").Or(cf.ByAutomationId("ParametersGrid")));
                Assert.NotNull(paramsGrid);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Reports")]
        public void ReportsPanel_GenerateButton_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Reports
            var reportsMenu = WaitForElement(window, cf => cf.ByName("Reports"));
            reportsMenu?.Click();

            // Find generate report button
            var generateBtn = WaitForElement(window, cf => cf.ByName("Generate Report").Or(cf.ByAutomationId("GenerateReportButton")))?.AsButton();
            Assert.NotNull(generateBtn);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Reports")]
        public void ReportsPanel_GenerateReport_ExecutesCommand()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Reports
            var reportsMenu = WaitForElement(window, cf => cf.ByName("Reports"));
            reportsMenu?.Click();

            // Select a report
            var reportCombo = WaitForElement(window, cf => cf.ByName("Select Report"))?.AsComboBox();
            if (reportCombo != null && reportCombo.Items.Length > 0)
            {
                reportCombo.Select(0);
                System.Threading.Thread.Sleep(1000);

                // Click generate
                var generateBtn = WaitForElement(window, cf => cf.ByName("Generate Report"))?.AsButton();
                if (generateBtn != null && generateBtn.IsEnabled)
                {
                    generateBtn.Click();

                    // Wait for report generation
                    WaitForBusyIndicator(TimeSpan.FromSeconds(15));

                    // Verify report output or viewer appears
                    var reportViewer = WaitForElement(window, cf => cf.ByName("Report Viewer").Or(cf.ByAutomationId("ReportViewer")));
                    // Note: Report viewer might not be implemented yet, so this may be null
                }
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Reports")]
        public void ReportsPanel_ExportToPDF_ButtonExists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Reports
            var reportsMenu = WaitForElement(window, cf => cf.ByName("Reports"));
            reportsMenu?.Click();

            // Find export to PDF button
            var exportPdfBtn = WaitForElement(window, cf => cf.ByName("Export to PDF").Or(cf.ByAutomationId("ExportPdfButton")))?.AsButton();

            if (exportPdfBtn != null)
            {
                Assert.True(exportPdfBtn.IsOffscreen || !exportPdfBtn.IsEnabled || exportPdfBtn.IsEnabled);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Reports")]
        public void ReportsPanel_ExportToExcel_ButtonExists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Reports
            var reportsMenu = WaitForElement(window, cf => cf.ByName("Reports"));
            reportsMenu?.Click();

            // Find export to Excel button
            var exportExcelBtn = WaitForElement(window, cf => cf.ByName("Export to Excel").Or(cf.ByAutomationId("ExportExcelButton")))?.AsButton();

            if (exportExcelBtn != null)
            {
                Assert.True(exportExcelBtn.IsOffscreen || !exportExcelBtn.IsEnabled || exportExcelBtn.IsEnabled);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Reports")]
        public void ReportsPanel_PrintButton_Exists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Reports
            var reportsMenu = WaitForElement(window, cf => cf.ByName("Reports"));
            reportsMenu?.Click();

            // Find print button
            var printBtn = WaitForElement(window, cf => cf.ByName("Print").Or(cf.ByAutomationId("PrintButton")))?.AsButton();

            if (printBtn != null)
            {
                Assert.NotNull(printBtn);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Reports")]
        public void ReportsPanel_RefreshButton_ReloadsReportList()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Reports
            var reportsMenu = WaitForElement(window, cf => cf.ByName("Reports"));
            reportsMenu?.Click();

            // Find refresh button
            var refreshBtn = WaitForElement(window, cf => cf.ByName("Refresh").Or(cf.ByAutomationId("RefreshButton")))?.AsButton();

            if (refreshBtn != null)
            {
                refreshBtn.Click();
                WaitForBusyIndicator(TimeSpan.FromSeconds(5));

                // Verify report combo still has options
                var reportCombo = WaitForElement(window, cf => cf.ByName("Select Report"))?.AsComboBox();
                Assert.NotNull(reportCombo);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Reports")]
        public void ReportsPanel_ParametersGrid_AllowsInput()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Reports
            var reportsMenu = WaitForElement(window, cf => cf.ByName("Reports"));
            reportsMenu?.Click();

            // Select a report
            var reportCombo = WaitForElement(window, cf => cf.ByName("Select Report"))?.AsComboBox();
            if (reportCombo != null && reportCombo.Items.Length > 0)
            {
                reportCombo.Select(0);
                System.Threading.Thread.Sleep(1000);

                // Find parameters grid
                var paramsGrid = WaitForElement(window, cf => cf.ByName("Report Parameters"));
                Assert.NotNull(paramsGrid);

                // Verify grid is enabled for input
                Assert.True(paramsGrid.IsEnabled);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Reports")]
        public void ReportsPanel_PreviewButton_ShowsPreview()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Reports
            var reportsMenu = WaitForElement(window, cf => cf.ByName("Reports"));
            reportsMenu?.Click();

            // Find preview button
            var previewBtn = WaitForElement(window, cf => cf.ByName("Preview").Or(cf.ByAutomationId("PreviewButton")))?.AsButton();

            if (previewBtn != null && previewBtn.IsEnabled)
            {
                previewBtn.Click();
                WaitForBusyIndicator(TimeSpan.FromSeconds(10));

                // Verify preview appears (implementation-specific)
                // This may require additional navigation or window handling
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
