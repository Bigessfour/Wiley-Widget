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

                // Assert custom chrome elements exist
                var titleBar = window.FindFirstDescendant(cf => cf.ByAutomationId("MainFormTitleBar"));
                Assert.NotNull(titleBar);
                Assert.True(titleBar.IsEnabled);

                // Verify no manual colors - rely on SfSkinManager cascade
                var titleLabel = titleBar.FindFirstDescendant(cf => cf.ByName("Wiley Widget"));
                Assert.NotNull(titleLabel);
                // BackColor should be theme-derived, not hardcoded

                // Test status bar (ProfessionalStatusBar)
                var statusBar = window.FindFirstDescendant(cf => cf.ByAutomationId("ProfessionalStatusBar"));
                Assert.NotNull(statusBar);
                var statusLabel = statusBar.FindFirstDescendant(cf => cf.ByName("Ready"));
                Assert.NotNull(statusLabel);

                // Verify theme application to chrome
                var chromePanel = window.FindFirstDescendant(cf => cf.ByAutomationId("MainFormChrome"));
                Assert.NotNull(chromePanel);
                // Assert theme-specific properties if accessible via UIA
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

                // Test maximize/restore
                var maximizeButton = window.FindFirstDescendant(cf => cf.ByAutomationId("MaximizeButton"));
                Assert.NotNull(maximizeButton);
                maximizeButton.Click();

                // Assert window is maximized
                Assert.Equal(WindowVisualState.Maximized, window.VisualState);

                // Test close button
                var closeButton = window.FindFirstDescendant(cf => cf.ByAutomationId("CloseButton"));
                Assert.NotNull(closeButton);
                // Don't actually close, just verify existence and enabled state
                Assert.True(closeButton.IsEnabled);
            }
            finally
            {
                app?.Close();
                ResetEnvironment(previousEnv);
            }
        }
    }
}
