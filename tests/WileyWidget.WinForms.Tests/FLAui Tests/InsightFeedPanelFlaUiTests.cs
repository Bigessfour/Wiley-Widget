using System;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class InsightFeedPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void InsightFeedPanel_HasGridAndRefreshButton_WhenPanelLoaded()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "Insight Feed", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Insights Data Grid", TimeSpan.FromSeconds(10)));
            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Refresh", TimeSpan.FromSeconds(8)));
        }
    }
}
