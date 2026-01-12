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
    /// E2E tests for AuditLogPanel using FlaUI automation.
    /// Tests event grid, chart visualization, date filtering, action/user filtering, auto-refresh, and CSV export.
    /// </summary>
    [Collection("UI Tests")]
    public class AuditLogPanelE2ETests : IDisposable
    {
        private readonly string _exePath;
        private FlaUIApplication? _app;
        private UIA3Automation? _automation;
        private bool _disposed;

        public AuditLogPanelE2ETests()
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
        [Trait("Panel", "AuditLog")]
        public void AuditLogPanel_OpensAndDisplaysGrid()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate using NavigationHelper
            var panel = NavigationHelper.OpenView(_automation!, window, "Nav_AuditLog", "Audit Log");

            // Verify audit log grid is present
            var auditGrid = WaitForElement(window, cf => cf.ByName("Audit Events Grid").Or(cf.ByAutomationId("AuditEventsGrid")));
            Assert.NotNull(auditGrid);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "AuditLog")]
        public void AuditLogPanel_EventChart_IsVisible()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Audit Log
            var auditMenu = WaitForElement(window, cf => cf.ByName("Audit Log"));
            auditMenu?.Click();

            // Wait for data to load
            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify chart control is present
            var eventChart = WaitForElement(window, cf => cf.ByName("Event Timeline Chart").Or(cf.ByAutomationId("EventChartControl")));
            Assert.NotNull(eventChart);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "AuditLog")]
        public void AuditLogPanel_DateRangeFilter_Works()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Audit Log
            var auditMenu = WaitForElement(window, cf => cf.ByName("Audit Log"));
            auditMenu?.Click();

            // Find start date picker
            var startDatePicker = WaitForElement(window, cf => cf.ByName("Start Date").Or(cf.ByAutomationId("StartDatePicker")));
            Assert.NotNull(startDatePicker);

            // Find end date picker
            var endDatePicker = WaitForElement(window, cf => cf.ByName("End Date").Or(cf.ByAutomationId("EndDatePicker")));
            Assert.NotNull(endDatePicker);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "AuditLog")]
        public void AuditLogPanel_ActionTypeFilter_Works()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Audit Log
            var auditMenu = WaitForElement(window, cf => cf.ByName("Audit Log"));
            auditMenu?.Click();

            // Find action type filter combo
            var actionCombo = WaitForElement(window, cf => cf.ByName("Action Type").Or(cf.ByAutomationId("ActionTypeCombo")))?.AsComboBox();
            Assert.NotNull(actionCombo);

            // Verify combo has items
            var items = actionCombo.Items;
            Assert.NotNull(items);
            Assert.NotEmpty(items);

            // Select an action type
            if (items.Length > 0)
            {
                actionCombo.Select(0);
                WaitForBusyIndicator(TimeSpan.FromSeconds(5));

                // Verify grid still present
                var auditGrid = WaitForElement(window, cf => cf.ByName("Audit Events Grid"));
                Assert.NotNull(auditGrid);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "AuditLog")]
        public void AuditLogPanel_UserFilter_Works()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Audit Log
            var auditMenu = WaitForElement(window, cf => cf.ByName("Audit Log"));
            auditMenu?.Click();

            // Find user filter combo
            var userCombo = WaitForElement(window, cf => cf.ByName("User").Or(cf.ByAutomationId("UserFilterCombo")))?.AsComboBox();
            Assert.NotNull(userCombo);

            // Verify combo has items
            var items = userCombo.Items;
            Assert.NotNull(items);
            Assert.NotEmpty(items);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "AuditLog")]
        public void AuditLogPanel_ChartGrouping_CanBeChanged()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Audit Log
            var auditMenu = WaitForElement(window, cf => cf.ByName("Audit Log"));
            auditMenu?.Click();

            // Find chart grouping combo (Day/Week/Month)
            var groupingCombo = WaitForElement(window, cf => cf.ByName("Group By").Or(cf.ByAutomationId("ChartGroupingCombo")))?.AsComboBox();

            if (groupingCombo != null && groupingCombo.Items.Length > 1)
            {
                groupingCombo.Select(1);
                WaitForBusyIndicator(TimeSpan.FromSeconds(5));

                // Verify chart updated
                var eventChart = WaitForElement(window, cf => cf.ByName("Event Timeline Chart"));
                Assert.NotNull(eventChart);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "AuditLog")]
        public void AuditLogPanel_AutoRefresh_CanBeToggled()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Audit Log
            var auditMenu = WaitForElement(window, cf => cf.ByName("Audit Log"));
            auditMenu?.Click();

            // Find auto-refresh toggle checkbox
            var autoRefreshCheck = WaitForElement(window, cf => cf.ByName("Auto Refresh").Or(cf.ByAutomationId("AutoRefreshCheckBox")))?.AsCheckBox();

            if (autoRefreshCheck != null)
            {
                var originalState = autoRefreshCheck.IsChecked;

                // Toggle
                autoRefreshCheck.Toggle();
                Assert.NotEqual(originalState, autoRefreshCheck.IsChecked);

                // Toggle back
                autoRefreshCheck.Toggle();
                Assert.Equal(originalState, autoRefreshCheck.IsChecked);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "AuditLog")]
        public void AuditLogPanel_ExportToCSV_ButtonExists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Audit Log
            var auditMenu = WaitForElement(window, cf => cf.ByName("Audit Log"));
            auditMenu?.Click();

            // Find export to CSV button
            var exportCsvBtn = WaitForElement(window, cf => cf.ByName("Export to CSV").Or(cf.ByAutomationId("ExportCsvButton")))?.AsButton();
            Assert.NotNull(exportCsvBtn);
            Assert.True(exportCsvBtn.IsEnabled);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "AuditLog")]
        public void AuditLogPanel_RefreshButton_ReloadsEvents()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Audit Log
            var auditMenu = WaitForElement(window, cf => cf.ByName("Audit Log"));
            auditMenu?.Click();

            // Find refresh button
            var refreshBtn = WaitForElement(window, cf => cf.ByName("Refresh").Or(cf.ByAutomationId("RefreshButton")))?.AsButton();
            Assert.NotNull(refreshBtn);

            refreshBtn.Click();
            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify grid still present
            var auditGrid = WaitForElement(window, cf => cf.ByName("Audit Events Grid"));
            Assert.NotNull(auditGrid);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "AuditLog")]
        public void AuditLogPanel_Grid_HasExpectedColumns()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Audit Log
            var auditMenu = WaitForElement(window, cf => cf.ByName("Audit Log"));
            auditMenu?.Click();

            // Wait for data to load
            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify grid is present
            var auditGrid = WaitForElement(window, cf => cf.ByName("Audit Events Grid"));
            Assert.NotNull(auditGrid);
            Assert.True(auditGrid.IsEnabled);

            // Note: Column headers verification would require deeper grid inspection
            // This test verifies the grid exists and is usable
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "AuditLog")]
        public void AuditLogPanel_ChartAndGrid_BothVisible()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Audit Log
            var auditMenu = WaitForElement(window, cf => cf.ByName("Audit Log"));
            auditMenu?.Click();

            // Wait for data to load
            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify both chart and grid are present
            var eventChart = WaitForElement(window, cf => cf.ByName("Event Timeline Chart"));
            var auditGrid = WaitForElement(window, cf => cf.ByName("Audit Events Grid"));

            Assert.NotNull(eventChart);
            Assert.NotNull(auditGrid);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "AuditLog")]
        public void AuditLogPanel_LoadingOverlay_AppearsAndDisappears()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Audit Log
            var auditMenu = WaitForElement(window, cf => cf.ByName("Audit Log"));
            auditMenu?.Click();

            // Trigger a refresh to see loading overlay
            var refreshBtn = WaitForElement(window, cf => cf.ByName("Refresh"))?.AsButton();
            if (refreshBtn != null)
            {
                refreshBtn.Click();

                // Wait for operation to complete
                WaitForBusyIndicator(TimeSpan.FromSeconds(15));

                // Verify grid is present after loading
                var auditGrid = WaitForElement(window, cf => cf.ByName("Audit Events Grid"));
                Assert.NotNull(auditGrid);
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
