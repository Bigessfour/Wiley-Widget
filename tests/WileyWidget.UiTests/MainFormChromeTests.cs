using System;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.Core.Definitions;
using FlaUI.UIA2;
using Microsoft.Extensions.Logging;
using WileyWidget.UiTests;
using WileyWidget.WinForms.Controls.Panels;
using Xunit;

namespace WileyWidget.UiTests
{
    [Collection("FlaUI Tests")]
    public class MainFormChromeTests : FlaUiTestBase
    {
        [StaFact]
        public void MainFormChrome_RendersCustomTitleBar_WithSfSkinManagerTheme()
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

                var titleBar = window.FindFirstDescendant(cf => cf.ByAutomationId("TitleBar"));
                Assert.NotNull(titleBar);
                Assert.True(titleBar.IsEnabled);
                Assert.True((FlaUiHelpers.TryGetName(titleBar) ?? string.Empty).Contains("Wiley Widget", StringComparison.OrdinalIgnoreCase));

                var ribbon = FlaUiHelpers.FindElementByNameOrId(window, "Upper Ribbon", "Ribbon_Main", TimeSpan.FromSeconds(10));
                Assert.NotNull(ribbon);
                Assert.True(ribbon.IsEnabled);

                var statusBar = FlaUiHelpers.FindElementByNameOrId(window, "ProfessionalStatusBar", "ProfessionalStatusBar", TimeSpan.FromSeconds(10));
                Assert.NotNull(statusBar);

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
        public void MainFormChrome_HandlesWindowStateChanges_Correctly()
        {
            var previousEnv = SetTestEnvironment();
            FlaUI.Core.Application? app = null;

            try
            {
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                var automation = EnsureAutomation();

                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));

                var titleBar = window.FindFirstDescendant(cf => cf.ByAutomationId("TitleBar"));
                Assert.NotNull(titleBar);

                if (window.Patterns.Window.IsSupported)
                {
                    window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
                    Wait.UntilInputIsProcessed();
                    Assert.False(window.Properties.IsOffscreen.ValueOrDefault);
                    window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
                }

                Assert.False(window.Properties.IsOffscreen.ValueOrDefault);
                Assert.True(window.IsEnabled);

                var systemMenu = window.FindFirstDescendant(cf => cf.ByName("System").Or(cf.ByAutomationId("Item 1")));
                Assert.NotNull(systemMenu);
                Assert.True(systemMenu.IsEnabled);
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
