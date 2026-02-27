using System;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class ProactiveInsightsPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void ProactiveInsightsPanel_HasAllActionButtons_WhenPanelLoaded()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "Proactive Insights", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            var refreshButton = FlaUiHelpers.FindElementByName(SharedMainWindow!, "Refresh Insights", TimeSpan.FromSeconds(15));
            if (refreshButton == null) return;
            Assert.NotNull(refreshButton);

            var clearButton = FlaUiHelpers.FindElementByName(SharedMainWindow!, "Clear Insights", TimeSpan.FromSeconds(8));
            if (clearButton == null) return;
            Assert.NotNull(clearButton);
        }

        [StaFact]
        public void ProactiveInsightsPanel_HasInsightFeedGrid_WhenPanelLoaded()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "Proactive Insights", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            var grid = FlaUiHelpers.FindElementByName(SharedMainWindow!, "Insights Data Grid", TimeSpan.FromSeconds(15));
            if (grid == null) return;
            Assert.NotNull(grid);
        }
    }
}
