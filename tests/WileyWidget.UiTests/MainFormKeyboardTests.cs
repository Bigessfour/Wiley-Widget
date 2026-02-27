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

                // Simulate Alt key to activate ribbon
                Keyboard.Press(VirtualKeyShort.ALT); // Alt key
                Keyboard.Release(VirtualKeyShort.ALT);

                // Assert ribbon is focused
                var ribbon = window.FindFirstDescendant(cf => cf.ByAutomationId("SfRibbon"));
                Assert.NotNull(ribbon);
                Assert.True(ribbon.Properties.HasKeyboardFocus.ValueOrDefault);

                // Type a letter to navigate to tab (e.g., 'B' for Budget)
                Keyboard.Type(VirtualKeyShort.KEY_B);
                var budgetTab = ribbon.FindFirstDescendant(cf => cf.ByName("Budget"));
                Assert.NotNull(budgetTab);
                Assert.True(budgetTab.Properties.HasKeyboardFocus.ValueOrDefault);
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

                // Activate a panel
                PanelActivationHelpers.ActivatePanel<BudgetPanel>(window, automation, TimeSpan.FromSeconds(10));

                // Simulate arrow key navigation within panel
                var panel = window.FindFirstDescendant(cf => cf.ByAutomationId("BudgetPanel"));
                panel.Focus();

                Keyboard.Type(VirtualKeyShort.DOWN); // Move focus down
                // Assert focus moved to next control (e.g., grid or button)
                var focusedElement = automation.GetFocusedElement();
                Assert.NotNull(focusedElement);
                Assert.True(focusedElement.Name.Contains("Budget")); // Approximate

                Keyboard.Type(VirtualKeyShort.TAB); // Tab navigation
                focusedElement = automation.GetFocusedElement();
                Assert.NotNull(focusedElement);
            }
            finally
            {
                app?.Close();
                ResetEnvironment(previousEnv);
            }
        }
    }
}
