using System;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA2;
using WileyWidget.UiTests;
using WileyWidget.WinForms.Controls.Panels;
using Xunit;

namespace WileyWidget.UiTests
{
    [Collection("FlaUI Tests")]
    public class MainFormKeyboardTests : FlaUiTestBase
    {
        [StaFact]
        public void MainFormKeyboard_AltKeySequenceNavigatesRibbon()
        {
            var previousEnv = SetTestEnvironment();
            FlaUI.Core.Application? app = null;

            try
            {
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                var automation = EnsureAutomation();

                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                Keyboard.Press(VirtualKeyShort.ALT);
                Keyboard.Release(VirtualKeyShort.ALT);
                Wait.UntilInputIsProcessed();

                var ribbon = FlaUiHelpers.FindElementByNameOrId(window, "Upper Ribbon", "Ribbon_Main", TimeSpan.FromSeconds(10));
                Assert.NotNull(ribbon);
                Assert.True(ribbon.IsEnabled);

                var systemMenu = FlaUiHelpers.FindElementByNameOrId(window, "System", "Item 1", TimeSpan.FromSeconds(10));
                Assert.NotNull(systemMenu);

                Keyboard.Type(VirtualKeyShort.KEY_B);
                Wait.UntilInputIsProcessed();

                var navigationStatus = FlaUiHelpers.FindElementByNameOrId(window, "NavAutomationStatus", "NavAutomationStatus", TimeSpan.FromSeconds(10));
                Assert.NotNull(navigationStatus);
                FlaUiHelpers.CaptureScreenshot(window);
            }
            finally
            {
                Keyboard.Press(VirtualKeyShort.ESCAPE); // Exit menu mode
                app?.Close();
                ResetEnvironment(previousEnv);
            }
        }

        [StaFact]
        public void MainFormKeyboard_ArrowKeysNavigatePanels()
        {
            var previousEnv = SetTestEnvironment();
            FlaUI.Core.Application? app = null;

            try
            {
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                var automation = EnsureAutomation();

                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                PanelActivationHelpers.ActivatePanel<BudgetPanel>(window, automation, TimeSpan.FromSeconds(10));
                var panel = window.WaitForPanel<BudgetPanel>(TimeSpan.FromSeconds(15));
                Assert.NotNull(panel);

                panel.Focus();

                Keyboard.Type(VirtualKeyShort.DOWN);
                Wait.UntilInputIsProcessed();
                Assert.False(panel.Properties.IsOffscreen.ValueOrDefault);

                Keyboard.Type(VirtualKeyShort.TAB);
                Wait.UntilInputIsProcessed();
                Assert.True(panel.IsEnabled);
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
