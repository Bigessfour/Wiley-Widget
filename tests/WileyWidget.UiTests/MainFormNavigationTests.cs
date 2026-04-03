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
        public void MainFormNavigation_UnifiedDropdownActivatesAccountsPanel_Correctly()
        {
            var previousEnv = SetTestEnvironment();
            FlaUI.Core.Application? app = null;

            try
            {
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                var automation = EnsureAutomation();

                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));
                FlaUiHelpers.DumpUiTree(window, "MainFormNavigation_UnifiedDropdown");
                var accountsPanelVisible = PanelActivationHelpers.EnsureAccountsPanelVisibleOrHostGated(window, automation, TimeSpan.FromSeconds(15));
                Assert.True(accountsPanelVisible);
                FlaUiHelpers.CaptureScreenshot(window);
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

                PanelActivationHelpers.ActivatePanel<BudgetPanel>(window, automation, TimeSpan.FromSeconds(15));

                var budgetPanel = window.WaitForPanel<BudgetPanel>(TimeSpan.FromSeconds(15));
                Assert.NotNull(budgetPanel);
                Assert.True(budgetPanel.IsEnabled);
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
