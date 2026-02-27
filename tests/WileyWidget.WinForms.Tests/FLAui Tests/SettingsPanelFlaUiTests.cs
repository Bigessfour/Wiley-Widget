using System;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class SettingsPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void SettingsPanel_HasThemeComboAndSaveButton_WhenPanelLoaded()
        {
            EnsureAppLaunched();
            if (!PanelActivationHelpers.EnsurePanelVisibleOrHostGated(SharedMainWindow!, "Settings", TimeSpan.FromSeconds(30)))
            {
                return;
            }

            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Theme", TimeSpan.FromSeconds(8)));
            Assert.NotNull(FlaUiHelpers.FindElementByName(SharedMainWindow!, "Save Changes", TimeSpan.FromSeconds(8)));
        }
    }
}
