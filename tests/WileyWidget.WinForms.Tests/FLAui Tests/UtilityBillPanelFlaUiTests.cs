using System;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class UtilityBillPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void UtilityBillPanel_HasGridAndCreateBillButton_WhenPanelLoaded()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "Utility Bills", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Utility Bills Grid", TimeSpan.FromSeconds(10)));
            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Create Bill", TimeSpan.FromSeconds(8)));
        }
    }
}
