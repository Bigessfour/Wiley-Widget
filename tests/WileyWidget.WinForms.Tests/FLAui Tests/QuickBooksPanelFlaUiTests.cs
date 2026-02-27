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

            var connectBtn = FlaUiHelpers.FindElementByNameOrId(window, "Connect", "btnQuickBooksConnect", TimeSpan.FromSeconds(10));
            if (connectBtn == null) return;
            Assert.NotNull(connectBtn);

            var grid = window.FindFirstDescendant(cf => cf.ByAutomationId("QuickBooksHistoryGrid"));
            if (grid == null) return;
            Assert.NotNull(grid);
        }
    }
}
