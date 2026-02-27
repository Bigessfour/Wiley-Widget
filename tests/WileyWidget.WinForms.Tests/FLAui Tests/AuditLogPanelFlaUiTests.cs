using System;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class AuditLogPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void AuditLogPanel_HasGridAndExportButton_WhenPanelLoaded()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "Audit Log", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Audit log entries grid", TimeSpan.FromSeconds(10)));
            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Export CSV", TimeSpan.FromSeconds(8)));
        }
    }
}
