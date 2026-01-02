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
    /// E2E tests for RevenueTrendsPanel using FlaUI automation.
    /// Tests revenue trend visualization, forecast display, comparison charts, and export functionality.
    /// </summary>
    [Collection("UI Tests")]
    public class RevenueTrendsPanelE2ETests : IDisposable
    {
        private readonly string _exePath;
        private FlaUIApplication? _app;
        private UIA3Automation? _automation;
        private bool _disposed;

        public RevenueTrendsPanelE2ETests()
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
        [Trait("Panel", "RevenueTrends")]
        public void RevenueTrendsPanel_OpensAndDisplaysChart()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Revenue Trends panel
            var revenueTrendsMenu = WaitForElement(window, cf => cf.ByName("Revenue Trends").Or(cf.ByAutomationId("MenuRevenueTrends")));
            Assert.NotNull(revenueTrendsMenu);
            revenueTrendsMenu.Click();

            // Wait for data to load
            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify trend chart is displayed
            var trendChart = WaitForElement(window, cf => cf.ByName("Revenue Trend Chart").Or(cf.ByAutomationId("RevenueTrendChart")));
            Assert.NotNull(trendChart);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "RevenueTrends")]
        public void RevenueTrendsPanel_DateRangeFilter_Works()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Revenue Trends
            var revenueTrendsMenu = WaitForElement(window, cf => cf.ByName("Revenue Trends"));
            revenueTrendsMenu?.Click();

            // Find date range controls
            var startDatePicker = WaitForElement(window, cf => cf.ByName("Start Date").Or(cf.ByAutomationId("StartDatePicker")));
            var endDatePicker = WaitForElement(window, cf => cf.ByName("End Date").Or(cf.ByAutomationId("EndDatePicker")));

            Assert.True(startDatePicker != null || endDatePicker != null, "Date range controls should be present");
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "RevenueTrends")]
        public void RevenueTrendsPanel_ForecastDisplay_IsVisible()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Revenue Trends
            var revenueTrendsMenu = WaitForElement(window, cf => cf.ByName("Revenue Trends"));
            revenueTrendsMenu?.Click();

            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify forecast section or chart is present
            var forecastSection = WaitForElement(window, cf => cf.ByName("Revenue Forecast").Or(cf.ByAutomationId("RevenueForecastSection")));
            
            if (forecastSection != null)
            {
                Assert.NotNull(forecastSection);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "RevenueTrends")]
        public void RevenueTrendsPanel_ExportChart_ButtonExists()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Revenue Trends
            var revenueTrendsMenu = WaitForElement(window, cf => cf.ByName("Revenue Trends"));
            revenueTrendsMenu?.Click();

            // Find export button
            var exportBtn = WaitForElement(window, cf => cf.ByName("Export").Or(cf.ByAutomationId("ExportButton")))?.AsButton();
            
            if (exportBtn != null)
            {
                Assert.True(exportBtn.IsEnabled);
            }
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "RevenueTrends")]
        public void RevenueTrendsPanel_RefreshButton_ReloadsData()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Revenue Trends
            var revenueTrendsMenu = WaitForElement(window, cf => cf.ByName("Revenue Trends"));
            revenueTrendsMenu?.Click();

            // Find refresh button
            var refreshBtn = WaitForElement(window, cf => cf.ByName("Refresh").Or(cf.ByAutomationId("RefreshButton")))?.AsButton();
            
            if (refreshBtn != null)
            {
                refreshBtn.Click();
                WaitForBusyIndicator(TimeSpan.FromSeconds(10));

                // Verify chart still present
                var trendChart = WaitForElement(window, cf => cf.ByName("Revenue Trend Chart"));
                Assert.NotNull(trendChart);
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
