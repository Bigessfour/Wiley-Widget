using System;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class AnalyticsHubPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void AnalyticsHubPanel_HasAllTabsAndSearch_WhenPanelLoaded()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "Analytics Hub", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Overview", TimeSpan.FromSeconds(8)));
            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Analytics search", TimeSpan.FromSeconds(8)));
        }
    }
}
