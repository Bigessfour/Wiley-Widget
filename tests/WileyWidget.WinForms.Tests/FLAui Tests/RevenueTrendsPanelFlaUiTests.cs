using System;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class RevenueTrendsPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void RevenueTrendsPanel_HasChartAndGrid_WhenPanelLoaded()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "Revenue Trends", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Revenue trends line chart", TimeSpan.FromSeconds(10)));
            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Monthly revenue breakdown data grid", TimeSpan.FromSeconds(10)));
        }
    }
}
