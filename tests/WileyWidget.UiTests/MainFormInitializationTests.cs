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

                var ribbon = FlaUiHelpers.FindElementByNameOrId(window, "Upper Ribbon", "Ribbon_Main", TimeSpan.FromSeconds(10));
                Assert.NotNull(ribbon);

                var statusBar = FlaUiHelpers.FindElementByNameOrId(window, "ProfessionalStatusBar", "ProfessionalStatusBar", TimeSpan.FromSeconds(10));
                Assert.NotNull(statusBar);

                var navigationStatus = FlaUiHelpers.FindElementByNameOrId(window, "NavAutomationStatus", "NavAutomationStatus", TimeSpan.FromSeconds(10));
                Assert.NotNull(navigationStatus);

                var defaultPanel = window.WaitForPanel<EnterpriseVitalSignsPanel>(TimeSpan.FromSeconds(10));
                Assert.NotNull(defaultPanel);
                FlaUiHelpers.CaptureScreenshot(window);
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

                var ribbon = FlaUiHelpers.FindElementByNameOrId(window, "Upper Ribbon", "Ribbon_Main", TimeSpan.FromSeconds(10));
                Assert.NotNull(ribbon);

                var statusBar = FlaUiHelpers.FindElementByNameOrId(window, "ProfessionalStatusBar", "ProfessionalStatusBar", TimeSpan.FromSeconds(10));
                Assert.NotNull(statusBar);

                var navigationStatus = FlaUiHelpers.FindElementByNameOrId(window, "NavAutomationStatus", "NavAutomationStatus", TimeSpan.FromSeconds(10));
                Assert.NotNull(navigationStatus);

                var defaultPanel = window.WaitForPanel<EnterpriseVitalSignsPanel>(TimeSpan.FromSeconds(10));
                Assert.NotNull(defaultPanel);

                var startupError = window.FindFirstDescendant(cf => cf.ByName("Startup Error"));
                Assert.Null(startupError);
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
