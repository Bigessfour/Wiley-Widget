using System;
using System.Diagnostics;
using System.IO;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA2;

using FlaUIApp = FlaUI.Core.Application;

namespace WileyWidget.WinForms.Tests.Integration.Ui
{
    /// <summary>
    /// Base class for all FLAUI panel tests (v32.2.3 Syncfusion-ready).
    /// Provides shared app launch, safe shutdown, and IDisposable support.
    /// </summary>
    public abstract class FlaUiTestBase : IDisposable
    {
        protected static FlaUIApp? SharedApp;
        protected static Window? SharedMainWindow;
        protected static UIA2Automation? SharedAutomation;
        private static readonly object Lock = new();

        static FlaUiTestBase()
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION", "true");

            // Kill the shared app cleanly when the test runner process exits so the
            // WileyWidget.WinForms.exe child process does not become an orphan.
            AppDomain.CurrentDomain.ProcessExit += (_, __) =>
            {
                lock (Lock)
                {
                    if (SharedApp != null)
                    {
                        FlaUiHelpers.ShutdownApp(SharedApp);
                        SharedApp = null;
                        SharedMainWindow = null;
                        SharedAutomation = null;
                    }
                }
            };
        }

        protected static FlaUIApp LaunchWinFormsForUiAutomation()
        {
            var exePath = UiTestConstants.ResolveWinFormsExePath();
            var workingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--testmode",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
            };

            startInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            startInfo.Environment["WILEYWIDGET_UI_AUTOMATION"] = "true";
            startInfo.Environment["WILEY_TESTMODE"] = "true";

            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to launch WinForms app for UI automation: {exePath}");

            return FlaUIApp.Attach(process.Id);
        }

        /// <summary>
        /// Returns the single process-wide <see cref="UIA2Automation"/> instance, creating it
        /// on first call.  NEVER create a <c>new UIA2Automation()</c> in a test method — calling
        /// <c>Dispose()</c> on any UIA2Automation instance calls
        /// <c>CUIAutomation.RemoveAllEventHandlers()</c> which tears down the global COM UIA
        /// state for the entire test-runner process, crashing every subsequent test.
        /// </summary>
        protected static UIA2Automation EnsureAutomation()
        {
            lock (Lock)
            {
                return SharedAutomation ??= new UIA2Automation();
            }
        }

        protected static void EnsureAppLaunched()
        {
            lock (Lock)
            {
                // Fast path: fully initialised.
                if (SharedApp != null && SharedMainWindow != null)
                {
                    return;
                }

                // Partial failure path: SharedApp was set by a previous call but WaitForMainWindow
                // threw (e.g. a 45 s timeout), leaving SharedMainWindow null.  Shut down the
                // orphaned process and retry so subsequent tests don't cascade-NRE.
                if (SharedApp != null)
                {
                    FlaUiHelpers.ShutdownApp(SharedApp);
                    SharedApp = null;
                    SharedMainWindow = null;
                }

                SharedApp = LaunchWinFormsForUiAutomation();
                TryWaitForInputIdle(SharedApp, TimeSpan.FromSeconds(15));

                SharedAutomation = EnsureAutomation();
                SharedMainWindow = FlaUiHelpers.WaitForMainWindow(SharedApp, SharedAutomation, TimeSpan.FromSeconds(45));
            }
        }

        public void Dispose()
        {
            // The shared app instance (SharedApp) lives for the entire xUnit
            // [Collection("FlaUI Tests")] collection — one app for all serialised tests.
            // Cleanup happens in the ProcessExit handler above once per test-runner run.
            // Tests that launch their own private instances must clean them up in
            // their own try/finally blocks (see AccountsPanelFlaUiTests, etc.).
            GC.SuppressFinalize(this);
        }

        protected static void TryWaitForInputIdle(FlaUIApp app, TimeSpan timeout)
        {
            FlaUiHelpers.TryWaitForInputIdle(app, timeout);
        }
    }
}
