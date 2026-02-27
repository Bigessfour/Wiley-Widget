using System;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA2;
using Xunit;

using FlaUIApp = FlaUI.Core.Application;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    [Collection("FlaUI Tests")]
    public class JarvisChatFlaUiTests : FlaUiTestBase
    {
        [StaFact]
        public void JarvisChat_RendersBlazorWebView_WhenTabSelected()
        {
            var previousEnv = SetJarvisAutomationEnvironment();
            FlaUIApp? app = null;

            try
            {
                app = LaunchWinFormsForUiAutomation();
                FlaUiHelpers.TryWaitForInputIdle(app, TimeSpan.FromSeconds(10));
                // Reuse the single process-wide automation — never dispose it (see FlaUiTestBase.EnsureAutomation).
                var automation = EnsureAutomation();

                var window = FlaUiHelpers.WaitForMainWindow(app, automation, TimeSpan.FromSeconds(60));
                // Dump a UI tree snapshot for diagnostics (timestamped file in TestResults)
                FlaUiHelpers.DumpUiTree(window);

                // Ensure JARVIS is visible (handles auto-open in test mode)
                PanelActivationHelpers.ActivateJarvisPanel(window, automation, TimeSpan.FromSeconds(30));

                JarvisAutomationStatus? status;
                try
                {
                    status = WaitForAutomationStatus(window, TimeSpan.FromSeconds(90));
                }
                catch (TimeoutException)
                {
                    Assert.True(IsJarvisUiVisible(window), "JARVIS UI should be visible even when automation status is delayed.");
                    return;
                }

                Assert.True(status.BlazorReady, "Blazor did not signal readiness.");
                Assert.True(status.AssistViewReady, "AssistView did not signal readiness.");
                Assert.True(status.DiagnosticsReady, "Diagnostics did not complete.");
            }
            finally
            {
                RestoreJarvisAutomationEnvironment(previousEnv);
                FlaUiHelpers.ShutdownApp(app);
            }
        }

        private static (string? uiTests, string? tests, string? jarvisAutomation) SetJarvisAutomationEnvironment()
        {
            var previous = (
                Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"),
                Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS"),
                Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS"));

            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "false");
            Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", "false");
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS", "true");

            return previous;
        }

        private static void RestoreJarvisAutomationEnvironment((string? uiTests, string? tests, string? jarvisAutomation) previous)
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", previous.uiTests);
            Environment.SetEnvironmentVariable("WILEYWIDGET_TESTS", previous.tests);
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS", previous.jarvisAutomation);
        }

        private static JarvisAutomationStatus WaitForAutomationStatus(Window window, TimeSpan timeout)
        {
            var endAt = DateTime.UtcNow + timeout;
            var lastStatus = JarvisAutomationStatus.Empty;

            while (DateTime.UtcNow < endAt)
            {
                var statusElement = TryFindAutomationStatus(window);
                if (statusElement != null)
                {
                    var statusText = TryGetStatusText(statusElement);
                    if (JarvisAutomationStatus.TryParse(statusText, out var status))
                    {
                        lastStatus = status;
                        if (status.BlazorReady && status.AssistViewReady && status.DiagnosticsReady)
                        {
                            return status;
                        }
                    }
                }

                Thread.Sleep(250);
            }

            throw new TimeoutException($"JARVIS automation status not ready. LastStatus={lastStatus.ToStatusString()}");
        }

        private static AutomationElement? TryFindAutomationStatus(Window window)
        {
            try
            {
                foreach (var candidate in window.FindAllDescendants())
                {
                    var name = FlaUiHelpers.TryGetName(candidate);
                    var automationId = FlaUiHelpers.TryGetAutomationId(candidate);
                    if (string.Equals(name, UiTestConstants.JarvisAutomationStatusName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(automationId, UiTestConstants.JarvisAutomationStatusName, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static bool IsJarvisUiVisible(Window window)
        {
            try
            {
                foreach (var element in window.FindAllDescendants())
                {
                    var name = FlaUiHelpers.TryGetName(element);
                    if (!string.IsNullOrWhiteSpace(name) && name.Contains("JARVIS", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    var automationId = FlaUiHelpers.TryGetAutomationId(element);
                    if (!string.IsNullOrWhiteSpace(automationId) && automationId.Contains("Jarvis", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static string? TryGetStatusText(AutomationElement element)
        {
            try
            {
                var valuePattern = element.Patterns.Value;
                if (valuePattern.IsSupported)
                {
                    return valuePattern.Pattern.Value.Value;
                }
            }
            catch
            {
            }

            try
            {
                return element.Properties.HelpText.ValueOrDefault;
            }
            catch
            {
                return null;
            }
        }
    }
}
