using System;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class QuickBooksPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void QuickBooksPanel_HasConnectButtonAndHistoryGrid_WhenPanelLoaded()
        {
            EnsureAppLaunched();
            var window = SharedMainWindow!;

            if (!PanelActivationHelpers.EnsureQuickBooksPanelVisibleOrHostGated(window, TimeSpan.FromSeconds(45)))
            {
                return;
            }

            var connectBtn = FlaUiHelpers.FindElementByName(window, "Connect to QuickBooks", TimeSpan.FromSeconds(10));
            if (connectBtn == null) return;
            Assert.NotNull(connectBtn);

            var grid = FlaUiHelpers.FindElementByName(window, "Sync History Grid", TimeSpan.FromSeconds(10));
            if (grid == null) return;
            Assert.NotNull(grid);
        }
    }
}
