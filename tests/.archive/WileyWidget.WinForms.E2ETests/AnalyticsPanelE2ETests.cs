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
    /// E2E tests for AnalyticsPanel using FlaUI automation.
    /// Tests exploratory analysis, scenario modeling, forecasting, and navigation features.
    /// </summary>
    [Collection("UI Tests")]
    public class AnalyticsPanelE2ETests : IDisposable
    {
        private readonly string _exePath;
        private FlaUIApplication? _app;
        private UIA3Automation? _automation;
        private bool _disposed;

        public AnalyticsPanelE2ETests()
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
        [Trait("Panel", "Analytics")]
        public void AnalyticsPanel_OpensAndDisplaysControls()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Analytics panel using NavigationHelper
            var analyticsPanel = NavigationHelper.OpenView(_automation!, window, "Nav_Analytics", "Analytics");

            // Verify panel elements are present
            var performAnalysisBtn = WaitForElement(analyticsPanel, cf => cf.ByName("Perform Analysis"))?.AsButton();
            var runScenarioBtn = WaitForElement(analyticsPanel, cf => cf.ByName("Run Scenario"))?.AsButton();
            var generateForecastBtn = WaitForElement(analyticsPanel, cf => cf.ByName("Generate Forecast"))?.AsButton();

            Assert.NotNull(performAnalysisBtn);
            Assert.NotNull(runScenarioBtn);
            Assert.NotNull(generateForecastBtn);
            Assert.True(performAnalysisBtn.IsEnabled);
            Assert.True(runScenarioBtn.IsEnabled);
            Assert.True(generateForecastBtn.IsEnabled);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Analytics")]
        public void AnalyticsPanel_ScenarioParametersAreEditable()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Analytics
            var analyticsMenu = WaitForElement(window, cf => cf.ByName("Analytics"));
            analyticsMenu?.Click();

            // Find scenario parameter inputs
            var rateIncreaseInput = WaitForElement(window, cf => cf.ByName("Rate Increase Percentage"))?.AsTextBox();
            var expenseIncreaseInput = WaitForElement(window, cf => cf.ByName("Expense Increase Percentage"))?.AsTextBox();
            var revenueTargetInput = WaitForElement(window, cf => cf.ByName("Revenue Target Percentage"))?.AsTextBox();
            var projectionYearsInput = WaitForElement(window, cf => cf.ByName("Projection Years"))?.AsTextBox();

            Assert.NotNull(rateIncreaseInput);
            Assert.NotNull(expenseIncreaseInput);
            Assert.NotNull(revenueTargetInput);
            Assert.NotNull(projectionYearsInput);

            // Test input
            rateIncreaseInput.Text = "7.5";
            expenseIncreaseInput.Text = "3.2";
            revenueTargetInput.Text = "10.0";
            projectionYearsInput.Text = "5";

            Assert.Equal("7.5", rateIncreaseInput.Text);
            Assert.Equal("3.2", expenseIncreaseInput.Text);
            Assert.Equal("10.0", revenueTargetInput.Text);
            Assert.Equal("5", projectionYearsInput.Text);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Analytics")]
        public void AnalyticsPanel_PerformAnalysisCommand_Executes()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Analytics
            var analyticsMenu = WaitForElement(window, cf => cf.ByName("Analytics"));
            analyticsMenu?.Click();

            var performAnalysisBtn = WaitForElement(window, cf => cf.ByName("Perform Analysis"))?.AsButton();
            Assert.NotNull(performAnalysisBtn);

            // Click perform analysis
            performAnalysisBtn.Click();

            // Wait for operation to complete (look for status or results)
            WaitForBusyIndicator(TimeSpan.FromSeconds(15));

            // Verify analytics results are displayed
            var metricsGrid = WaitForElement(window, cf => cf.ByName("Analytics Metrics Grid"));
            Assert.NotNull(metricsGrid);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Analytics")]
        public void AnalyticsPanel_RunScenarioCommand_Executes()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Analytics
            var analyticsMenu = WaitForElement(window, cf => cf.ByName("Analytics"));
            analyticsMenu?.Click();

            // Set scenario parameters
            var rateIncreaseInput = WaitForElement(window, cf => cf.ByName("Rate Increase Percentage"))?.AsTextBox();
            if (rateIncreaseInput != null)
            {
                rateIncreaseInput.Text = "6.0";
            }

            var runScenarioBtn = WaitForElement(window, cf => cf.ByName("Run Scenario"))?.AsButton();
            Assert.NotNull(runScenarioBtn);

            // Execute scenario
            runScenarioBtn.Click();

            // Wait for completion
            WaitForBusyIndicator(TimeSpan.FromSeconds(15));

            // Verify scenario results
            var varianceGrid = WaitForElement(window, cf => cf.ByName("Variance Analysis Grid"));
            Assert.NotNull(varianceGrid);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Analytics")]
        public void AnalyticsPanel_GenerateForecastCommand_Executes()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Analytics
            var analyticsMenu = WaitForElement(window, cf => cf.ByName("Analytics"));
            analyticsMenu?.Click();

            // Set projection years
            var projectionYearsInput = WaitForElement(window, cf => cf.ByName("Projection Years"))?.AsTextBox();
            if (projectionYearsInput != null)
            {
                projectionYearsInput.Text = "3";
            }

            var generateForecastBtn = WaitForElement(window, cf => cf.ByName("Generate Forecast"))?.AsButton();
            Assert.NotNull(generateForecastBtn);

            // Generate forecast
            generateForecastBtn.Click();

            // Wait for completion
            WaitForBusyIndicator(TimeSpan.FromSeconds(15));

            // Verify forecast chart is displayed
            var forecastChart = WaitForElement(window, cf => cf.ByName("Forecast Chart"));
            Assert.NotNull(forecastChart);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Analytics")]
        public void AnalyticsPanel_NavigationButtons_Work()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Analytics
            var analyticsMenu = WaitForElement(window, cf => cf.ByName("Analytics"));
            analyticsMenu?.Click();

            // Test navigation to Budget panel
            var navToBudgetBtn = WaitForElement(window, cf => cf.ByName("Navigate to Budget Panel"))?.AsButton();
            Assert.NotNull(navToBudgetBtn);
            Assert.True(navToBudgetBtn.IsEnabled);

            // Test navigation to Accounts panel
            var navToAccountsBtn = WaitForElement(window, cf => cf.ByName("Navigate to Accounts Panel"))?.AsButton();
            Assert.NotNull(navToAccountsBtn);
            Assert.True(navToAccountsBtn.IsEnabled);

            // Test navigation to Dashboard panel
            var navToDashboardBtn = WaitForElement(window, cf => cf.ByName("Navigate to Dashboard Panel"))?.AsButton();
            Assert.NotNull(navToDashboardBtn);
            Assert.True(navToDashboardBtn.IsEnabled);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Analytics")]
        public void AnalyticsPanel_RefreshCommand_ReloadsData()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Analytics
            var analyticsMenu = WaitForElement(window, cf => cf.ByName("Analytics"));
            analyticsMenu?.Click();

            var refreshBtn = WaitForElement(window, cf => cf.ByName("Refresh"))?.AsButton();
            Assert.NotNull(refreshBtn);

            // Execute refresh
            refreshBtn.Click();

            // Wait for refresh to complete
            WaitForBusyIndicator(TimeSpan.FromSeconds(10));

            // Verify grids are present after refresh
            var metricsGrid = WaitForElement(window, cf => cf.ByName("Analytics Metrics Grid"));
            Assert.NotNull(metricsGrid);
        }

        [StaFact]
        [Trait("Category", "UI")]
        [Trait("Panel", "Analytics")]
        public void AnalyticsPanel_ChartsAreDisplayed()
        {
            if (!EnsureInteractiveOrSkip()) return;
            StartApp();
            var window = GetMainWindow();

            // Navigate to Analytics
            var analyticsMenu = WaitForElement(window, cf => cf.ByName("Analytics"));
            analyticsMenu?.Click();

            // Perform analysis to populate charts
            var performAnalysisBtn = WaitForElement(window, cf => cf.ByName("Perform Analysis"))?.AsButton();
            performAnalysisBtn?.Click();

            WaitForBusyIndicator(TimeSpan.FromSeconds(15));

            // Verify charts are displayed
            var trendsChart = WaitForElement(window, cf => cf.ByName("Trends Chart"));
            var forecastChart = WaitForElement(window, cf => cf.ByName("Forecast Chart"));

            Assert.NotNull(trendsChart);
            Assert.NotNull(forecastChart);
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
