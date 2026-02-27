using System;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class DepartmentSummaryPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void DepartmentSummaryPanel_HasSummaryCardsAndGrid_WhenPanelLoaded()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "Department Summary", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Total Budgeted", TimeSpan.FromSeconds(8)));
            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Department metrics grid", TimeSpan.FromSeconds(10)));
        }
    }
}
