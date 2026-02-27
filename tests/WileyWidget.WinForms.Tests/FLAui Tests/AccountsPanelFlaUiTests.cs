using System;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA2;
using Xunit;

using FlaUIApp = FlaUI.Core.Application;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class AccountsPanelFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void AccountsPanel_RendersWithButtons_WhenPanelActivated()
        {
            var previousEnv = SetAccountsAutomationEnvironment();
            FlaUIApp? app = null;

            try
            {
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));

                // Reuse the single process-wide automation instance — never create a new
                // UIA2Automation and dispose it; disposal calls RemoveAllEventHandlers
                // globally and crashes subsequent tests in the collection.
                var automation = EnsureAutomation();
                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                if (!PanelActivationHelpers.EnsureAccountsPanelVisibleOrHostGated(window, automation, TimeSpan.FromSeconds(30)))
                {
                    return;
                }

                Thread.Sleep(2000);

                var buttons = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                Assert.True(buttons.Length > 0, "Accounts panel should expose at least one actionable button");

                var grid = FlaUiHelpers.FindElementByNameOrId(window, "Accounts Grid", "dataGridAccounts", TimeSpan.FromSeconds(10));
                Assert.NotNull(grid);

                var hasData = WaitForGridData(window, TimeSpan.FromSeconds(15));
                Assert.True(hasData, "Grid should load account data");
            }
            finally
            {
                RestoreAccountsAutomationEnvironment(previousEnv);
                FlaUiHelpers.ShutdownApp(app);
            }
        }

        [StaFact]
        public void AccountsPanel_RefreshButton_ReloadsData_WhenClicked()
        {
            var previousEnv = SetAccountsAutomationEnvironment();
            FlaUIApp? app = null;

            try
            {
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));

                var automation = EnsureAutomation();
                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                if (!PanelActivationHelpers.EnsureAccountsPanelVisibleOrHostGated(window, automation, TimeSpan.FromSeconds(30)))
                {
                    return;
                }

                Thread.Sleep(2000);

                var hasData = WaitForGridData(window, TimeSpan.FromSeconds(15));
                Assert.True(hasData, "Grid should load initial data");

                var refreshButton = FlaUiHelpers.FindElementByNameOrId(window, "Refresh", "btnRefresh", TimeSpan.FromSeconds(5));
                Assert.NotNull(refreshButton);

                refreshButton.AsButton().Invoke();
                Thread.Sleep(1000);

                var hasDataAfterRefresh = WaitForGridData(window, TimeSpan.FromSeconds(15));
                Assert.True(hasDataAfterRefresh, "Grid should still have data after refresh");
            }
            finally
            {
                RestoreAccountsAutomationEnvironment(previousEnv);
                FlaUiHelpers.ShutdownApp(app);
            }
        }

        private static (string? uiTests, string? tests, string? accountsAutomation) SetAccountsAutomationEnvironment()
        {
            var previous = (
                Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"),
                Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS"),
                Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_ACCOUNTS"));

            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "false");
            Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", "false");
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_ACCOUNTS", "true");

            return previous;
        }

        private static void RestoreAccountsAutomationEnvironment((string? uiTests, string? tests, string? accountsAutomation) previous)
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", previous.uiTests);
            Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", previous.tests);
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_ACCOUNTS", previous.accountsAutomation);
        }

        private static bool WaitForGridData(Window window, TimeSpan timeout)
        {
            var endAt = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < endAt)
            {
                try
                {
                    var grid = FlaUiHelpers.FindElementByNameOrId(window, "Accounts Grid", "dataGridAccounts", TimeSpan.FromSeconds(2));
                    if (grid != null)
                    {
                        var dataItems = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
                        if (dataItems.Length > 0)
                        {
                            return true;
                        }

                        var textElements = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
                        if (textElements.Length > 10)
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                }

                Thread.Sleep(500);
            }

            return false;
        }
    }
}
