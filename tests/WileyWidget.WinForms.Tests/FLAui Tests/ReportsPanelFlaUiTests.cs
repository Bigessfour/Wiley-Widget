using System;
using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class ReportsPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void ReportsPanel_LoadsReportAndEnablesExports_WhenGenerateClicked()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "Reports", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            var reportSelector = FlaUiHelpers.FindElementByName(SharedMainWindow!, "Report Selector", TimeSpan.FromSeconds(8));
            Assert.NotNull(reportSelector);

            var generateButton = FlaUiHelpers.FindElementByName(SharedMainWindow!, "Generate", TimeSpan.FromSeconds(8));
            Assert.NotNull(generateButton);
            Assert.True(generateButton!.IsEnabled, "Generate should be enabled when at least one report template is available.");

            generateButton.AsButton().Invoke();

            var exportPdfButton = FlaUiHelpers.FindElementByName(SharedMainWindow!, "Export PDF", TimeSpan.FromSeconds(8));
            Assert.NotNull(exportPdfButton);

            var wait = Stopwatch.StartNew();
            while (wait.Elapsed < TimeSpan.FromSeconds(15))
            {
                if (exportPdfButton!.IsEnabled)
                {
                    return;
                }

                System.Threading.Thread.Sleep(250);
            }

            Assert.True(exportPdfButton!.IsEnabled, "Export PDF should become enabled after a report is generated.");
        }
    }
}
