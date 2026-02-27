using System;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class PaymentsPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void PaymentsPanel_HasGridAndAddButton_WhenPanelLoaded()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "Payments", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Payments Grid", TimeSpan.FromSeconds(10)));
            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Add Payment", TimeSpan.FromSeconds(8)));
        }
    }
}
