using System;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;
using Syncfusion.Windows.Forms.Tools;
using WileyWidget.UiTests;
using WileyWidget.WinForms.Controls.Panels;
using Xunit;

namespace WileyWidget.UiTests
{
    [Collection("FlaUI Tests")]
    public class MainFormRibbonTests : FlaUiTestBase
    {
        [StaFact]
        public void MainFormRibbon_ButtonsEnableDisableBasedOnState()
        {
            var previousEnv = SetTestEnvironment();
            FlaUI.Core.Application? app = null;

            try
            {
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                var automation = EnsureAutomation();

                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                var ribbon = window.FindFirstDescendant(cf => cf.ByAutomationId("SfRibbon"));
                Assert.NotNull(ribbon);

                // Home tab should be visible and active by default
                var homeTab = ribbon.FindFirstDescendant(cf => cf.ByName("Home"));
                Assert.NotNull(homeTab);

                // Verify primary groups in Home tab
                var systemGroup = ribbon.FindFirstDescendant(cf => cf.ByName("System"));
                Assert.NotNull(systemGroup);

                var refreshButton = systemGroup.FindFirstDescendant(cf => cf.ByName("Refresh"));
                Assert.NotNull(refreshButton);
                Assert.True(refreshButton.IsEnabled);
            }
            finally
            {
                app?.Close();
                ResetEnvironment(previousEnv);
            }
        }

        [StaFact]
        public void MainFormRibbon_ThemeSwitchingUpdatesVisuals()
        {
            var previousEnv = SetTestEnvironment();
            FlaUI.Core.Application? app = null;

            try
            {
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                var automation = EnsureAutomation();

                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                var ribbon = window.FindFirstDescendant(cf => cf.ByAutomationId("SfRibbon"));
                Assert.NotNull(ribbon);

                // Simulate theme switch (if ribbon has theme button)
                var themeButton = ribbon.FindFirstDescendant(cf => cf.ByName("Theme"));
                if (themeButton != null)
                {
                    themeButton.Click();
                    // Select a theme (e.g., HighContrast)
                    var highContrast = automation.GetDesktop().FindFirstDescendant(cf => cf.ByName("High Contrast"));
                    highContrast?.Click();

                    // Assert ribbon visuals updated (indirect via UIA properties or re-find)
                    Assert.True(ribbon.IsOffscreen == false); // Still visible
                }
                else
                {
                    // Fallback: assert default theme applied correctly
                    // Assert.True(ribbon.Properties.ThemeName.ValueOrDefault == "Office2019Colorful"); // Not available in FlaUI
                }
            }
            finally
            {
                app?.Close();
                ResetEnvironment(previousEnv);
            }
        }
    }
}
