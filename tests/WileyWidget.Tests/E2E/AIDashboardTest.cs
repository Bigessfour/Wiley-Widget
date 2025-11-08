using System;
using System.Threading;
using Xunit;
using FluentAssertions;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using Xunit.Abstractions;

namespace WileyWidget.Tests.E2E
{
    // AI-Generated Test (via Grok-4 prompt: "Generate E2E test for WPF Syncfusion SfDataGrid interaction using FlaUI + Appium + WinAppDriver v1.2, including wait for launch and experimental features")
    public class AIDashboardTest : BaseE2ETest
    {
        [Fact]
        [Trait("Category", "E2E")]
        public void CanInteractWithDashboardAndSfDataGrid()
        {
            // Wait for app to fully load (using FlaUI condition instead of Thread.Sleep for reliability)
            MainWindow.Wait(TimeSpan.FromSeconds(10), cf => cf.IsEnabled);

            // Verify main window is loaded and has expected title
            MainWindow.Title.Should().Contain("Wiley Widget");

            // Wait for dashboard to load - look for ribbon controls that indicate dashboard is ready
            var ribbon = MainWindow.FindFirstDescendant(cf => cf.ByClassName("Ribbon"));
            ribbon.Should().NotBeNull("Ribbon control should be present");

            // Find Refresh button in the ribbon (this indicates dashboard is loaded)
            var refreshButton = MainWindow.FindFirstDescendant(cf => cf.ByName("Refresh"));
            refreshButton.Should().NotBeNull("Refresh button should be available on dashboard");

            // Test that we can find SfDataGrid controls (they have specific names in the actual app)
            // Look for RecentActivityGrid or SystemAlertsGrid
            var recentActivityGrid = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("RecentActivityGrid"));
            var systemAlertsGrid = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("SystemAlertsGrid"));

            // At least one of these grids should exist
            (recentActivityGrid != null || systemAlertsGrid != null).Should().BeTrue("At least one SfDataGrid should be present on dashboard");

            // Test basic interaction - click refresh button
            refreshButton.AsButton().Invoke();

            // Wait a moment for any refresh operation
            Thread.Sleep(1000);

            // Verify app is still responsive after refresh
            MainWindow.IsEnabled.Should().BeTrue("App should remain responsive after refresh");
        }

        [Fact]
        [Trait("Category", "E2E")]
        public void CanValidateDashboardNavigationElements()
        {
            // Test app responsiveness after launch (using FlaUI wait)
            MainWindow.Wait(TimeSpan.FromSeconds(10), cf => cf.IsEnabled);
            MainWindow.IsEnabled.Should().BeTrue("App responsive after launch delay");

            // Verify main window elements are accessible
            var elements = MainWindow.FindAllDescendants();
            elements.Should().NotBeEmpty("Main window has accessible elements");

            // Check for navigation elements that should be present
            var accountsButton = MainWindow.FindFirstDescendant(cf => cf.ByName("Accounts"));
            accountsButton.Should().NotBeNull("Accounts navigation button should be present");

            // Verify the button is clickable
            accountsButton.AsButton().IsEnabled.Should().BeTrue("Accounts button should be enabled");
        }
    }
}
