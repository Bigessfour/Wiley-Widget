using System;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using WileyWidget.UiTests;
using WileyWidget.WinForms.Controls.Panels;
using Xunit;

namespace WileyWidget.UiTests
{
    [Collection("FlaUI Tests")]
    public class MainFormNavigationTests : FlaUiTestBase
    {
        [StaFact]
        public void MainFormNavigation_RibbonTabActivatesPanel_Correctly()
        {
            var previousEnv = SetTestEnvironment();
            FlaUI.Core.Application? app = null;

            try
            {
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                var automation = EnsureAutomation();

                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));
                FlaUiHelpers.DumpUiTree(window);

                // Activate a panel via ribbon tab (e.g., Budget panel)
                var budgetTab = window.FindFirstDescendant(cf => cf.ByName("Budget"));
                Assert.NotNull(budgetTab);
                budgetTab.Click();

                // Assert panel is activated
                var budgetPanel = window.WaitForPanel<BudgetPanel>(TimeSpan.FromSeconds(10));
                Assert.NotNull(budgetPanel);
                Assert.False(budgetPanel.Properties.IsOffscreen.ValueOrDefault);

                // Verify focus is set to panel
                Assert.True(budgetPanel.Properties.HasKeyboardFocus.ValueOrDefault);
            }
            finally
            {
                app?.Close();
                ResetEnvironment(previousEnv);
            }
        }

        [StaFact]
        public void MainFormNavigation_PanelLifecycle_HandlesInitializeAsync()
        {
            var previousEnv = SetTestEnvironment();
            FlaUI.Core.Application? app = null;

            try
            {
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                var automation = EnsureAutomation();

                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                // Activate panel and wait for async init completion
                PanelActivationHelpers.ActivatePanel<BudgetPanel>(window, automation, TimeSpan.FromSeconds(15));

                // Assert panel is fully initialized (e.g., controls loaded)
                var panelContent = window.FindFirstDescendant(cf => cf.ByAutomationId("BudgetPanelContent"));
                Assert.NotNull(panelContent);
                Assert.True(panelContent.IsEnabled);
            }
            finally
            {
                app?.Close();
                ResetEnvironment(previousEnv);
            }
        }
    }
}
