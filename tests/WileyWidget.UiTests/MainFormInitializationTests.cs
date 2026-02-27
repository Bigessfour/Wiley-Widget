using System;
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
    public class MainFormInitializationTests : FlaUiTestBase
    {
        [StaFact]
        public void MainFormInitialization_CompletesAsyncInitAfterShow()
        {
            var previousEnv = SetTestEnvironment();
            FlaUI.Core.Application? app = null;

            try
            {
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(30)); // Extended for async init
                var automation = EnsureAutomation();

                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                // Wait for initialization completion (e.g., status bar message or panel ready)
                var statusBar = window.FindFirstDescendant(cf => cf.ByAutomationId("ProfessionalStatusBar"));
                Assert.NotNull(statusBar);

                var readyLabel = statusBar.FindFirstDescendant(cf => cf.ByName("Ready"));
                Assert.NotNull(readyLabel); // Indicates init complete

                // Verify default panel is loaded (EnterpriseVitalSignsPanel)
                var defaultPanel = window.WaitForPanel<EnterpriseVitalSignsPanel>(TimeSpan.FromSeconds(10));
                Assert.NotNull(defaultPanel);
            }
            finally
            {
                app?.Close();
                ResetEnvironment(previousEnv);
            }
        }

        [StaFact]
        public void MainFormInitialization_ResolvesMainViewModelAndServices()
        {
            var previousEnv = SetTestEnvironment();
            FlaUI.Core.Application? app = null;

            try
            {
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                var automation = EnsureAutomation();

                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                // Assert services are available by interacting with a feature requiring DI
                var ribbon = window.FindFirstDescendant(cf => cf.ByAutomationId("SfRibbon"));
                Assert.NotNull(ribbon);

                // Click a button that requires service resolution
                var newButton = ribbon.FindFirstDescendant(cf => cf.ByName("New"));
                Assert.NotNull(newButton);
                newButton.Click();

                // Assert no errors in status (indirect DI validation)
                var statusBar = window.FindFirstDescendant(cf => cf.ByAutomationId("ProfessionalStatusBar"));
                var errorLabel = statusBar.FindFirstDescendant(cf => cf.ByName("Error"));
                Assert.Null(errorLabel);
            }
            finally
            {
                app?.Close();
                ResetEnvironment(previousEnv);
            }
        }
    }
}
