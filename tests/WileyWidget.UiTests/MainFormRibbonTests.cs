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

                var ribbon = FlaUiHelpers.FindElementByNameOrId(window, "Upper Ribbon", "Ribbon_Main", TimeSpan.FromSeconds(10));
                Assert.NotNull(ribbon);
                Assert.True(ribbon.IsEnabled);

                var systemMenu = FlaUiHelpers.FindElementByNameOrId(window, "System", "Item 1", TimeSpan.FromSeconds(10));
                Assert.NotNull(systemMenu);
                Assert.True(systemMenu.IsEnabled);

                var navigationStatus = FlaUiHelpers.FindElementByNameOrId(window, "NavAutomationStatus", "NavAutomationStatus", TimeSpan.FromSeconds(10));
                Assert.NotNull(navigationStatus);
                FlaUiHelpers.CaptureScreenshot(window);
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

                var ribbon = FlaUiHelpers.FindElementByNameOrId(window, "Upper Ribbon", "Ribbon_Main", TimeSpan.FromSeconds(10));
                Assert.NotNull(ribbon);
                Assert.True(ribbon.IsEnabled);

                window.Focus();
                FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL);
                FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.SHIFT);
                FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_T);
                FlaUI.Core.Input.Keyboard.Release(FlaUI.Core.WindowsAPI.VirtualKeyShort.SHIFT);
                FlaUI.Core.Input.Keyboard.Release(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL);
                FlaUI.Core.Input.Wait.UntilInputIsProcessed();

                var statusBar = FlaUiHelpers.FindElementByNameOrId(window, "ProfessionalStatusBar", "ProfessionalStatusBar", TimeSpan.FromSeconds(10));
                Assert.NotNull(statusBar);
                Assert.False(ribbon.Properties.IsOffscreen.ValueOrDefault);

                FlaUiHelpers.CaptureScreenshot(window);
            }
            finally
            {
                app?.Close();
                ResetEnvironment(previousEnv);
            }
        }
    }
}
