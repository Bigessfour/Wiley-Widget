using System;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class ReportsPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void ReportsPanel_HasReportSelectorAndGenerateButton_WhenPanelLoaded()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "Reports", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Report Selector", TimeSpan.FromSeconds(8)));
            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Generate", TimeSpan.FromSeconds(8)));
        }
    }
}
