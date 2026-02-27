using System;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class PaymentEditPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void PaymentEditPanel_HasAllFieldsAndSaveButton_WhenPanelLoaded()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "New Payment", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Check Number", TimeSpan.FromSeconds(8)));
            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Save Changes", TimeSpan.FromSeconds(8)));
        }
    }
}
