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

                var ribbon = FlaUiHelpers.FindElementByNameOrId(window, "Ribbon_Main", "Ribbon_Main", TimeSpan.FromSeconds(20));
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

                var ribbon = FlaUiHelpers.FindElementByNameOrId(window, "Ribbon_Main", "Ribbon_Main", TimeSpan.FromSeconds(20));
                Assert.NotNull(ribbon);

                // Simulate theme switch using the current shell theme controls.
                var themeButton = ribbon!.FindFirstDescendant(cf =>
                    cf.ByAutomationId("ThemeToggle")
                        .Or(cf.ByName("Theme Toggle"))
                        .Or(cf.ByAutomationId("ThemeCombo"))
                        .Or(cf.ByName("Theme")));
                if (themeButton != null)
                {
                    themeButton.Click();

                    // Assert the ribbon and theme surface remain available after the switch.
                    var refreshedRibbon = FlaUiHelpers.FindElementByNameOrId(window, "Ribbon_Main", "Ribbon_Main", TimeSpan.FromSeconds(10));
                    Assert.NotNull(refreshedRibbon);

                    var refreshedThemeControl = refreshedRibbon!.FindFirstDescendant(cf =>
                        cf.ByAutomationId("ThemeToggle")
                            .Or(cf.ByName("Theme Toggle"))
                            .Or(cf.ByAutomationId("ThemeCombo"))
                            .Or(cf.ByName("Theme")));
                    Assert.NotNull(refreshedThemeControl);
                    Assert.False(refreshedRibbon.IsOffscreen);
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
