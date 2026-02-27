using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;
using WileyWidget.UiTests;
using WileyWidget.WinForms.Controls.Panels;
using Xunit;

namespace WileyWidget.UiTests
{
    [Collection("FlaUI Tests")]
    public class MainFormLayoutPersistenceTests : FlaUiTestBase
    {
        private const string TestLayoutFile = "test-layout.xml";

        [StaFact]
        public void MainFormLayoutPersistence_SavesAndRestoresDockingLayout()
        {
            var previousEnv = SetTestEnvironment();
            FlaUI.Core.Application? app = null;

            try
            {
                // Launch app and wait for main window
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                var automation = EnsureAutomation();

                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                // Open a panel that isn't open by default
                var budgetTab = window.FindFirstDescendant(cf => cf.ByName("Budget"));
                budgetTab?.Click();
                
                var budgetPanel = window.WaitForPanel<BudgetPanel>(TimeSpan.FromSeconds(10));
                Assert.NotNull(budgetPanel);

                // Close app to trigger AutoSaveLayout
                app.Close();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(5));

                // Relaunch and verify panel is still visible (restored from layout)
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                var restoredPanel = window.WaitForPanel<BudgetPanel>(TimeSpan.FromSeconds(10));
                Assert.NotNull(restoredPanel);
                Assert.False(restoredPanel.Properties.IsOffscreen.ValueOrDefault);
            }
            finally
            {
                app?.Close();
                ResetEnvironment(previousEnv);
                // Cleanup test layout file if exists
                if (File.Exists(TestLayoutFile)) File.Delete(TestLayoutFile);
            }
        }
    }
}
