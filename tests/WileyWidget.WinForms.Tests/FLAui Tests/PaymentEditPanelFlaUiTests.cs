using System;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class PaymentEditPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void PaymentAddDialog_HasAllFieldsAndSaveButton_WhenOpenedFromPaymentsPanel()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "Payments", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            var addPaymentButton = FlaUiHelpers.FindElementByName(SharedMainWindow!, "Add Payment", TimeSpan.FromSeconds(8));
            Assert.NotNull(addPaymentButton);
            addPaymentButton!.AsButton().Invoke();

            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Check Number", TimeSpan.FromSeconds(8)));
            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Save Payment", TimeSpan.FromSeconds(8)));
        }
    }
}
