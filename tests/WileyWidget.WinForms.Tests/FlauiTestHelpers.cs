using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Controls.Base;

namespace WileyWidget.WinForms.Tests
{
    /// <summary>
    /// Syncfusion 32.2.3 + FLAUI helper library (RibbonControlAdv, SfButton, TabbedMDI, panels)
    /// Drop this in and never write flaky waits again.
    /// </summary>
    public static class FlauiTestHelpers
    {
        public static Window LaunchAndWaitForMainWindow(FlaUI.Core.Application app, TimeSpan timeout = default)
        {
            timeout = timeout == default ? TimeSpan.FromSeconds(15) : timeout;
            var window = app.GetMainWindow(new UIA3Automation(), timeout);
            if (window == null) throw new InvalidOperationException("Main window not found");
            // Wait for window to be ready
            Thread.Sleep(1000); // Simple wait
            return window;
        }

        public static async Task<Window> LaunchAndWaitForMainWindowAsync(FlaUI.Core.Application app, TimeSpan timeout = default)
        {
            timeout = timeout == default ? TimeSpan.FromSeconds(15) : timeout;
            var window = app.GetMainWindow(new UIA3Automation(), timeout);
            if (window == null) throw new InvalidOperationException("Main window not found");
            // Wait for window to be ready
            await Task.Delay(1000); // Simple wait
            return window;
        }

        public static AutomationElement WaitForRibbonTab(this Window window, string tabName, TimeSpan timeout = default)
        {
            timeout = timeout == default ? TimeSpan.FromSeconds(8) : timeout;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var element = window.FindFirstDescendant(cf => cf.ByAutomationId($"RibbonTab_{tabName}"));
                    if (element != null)
                    {
                        return element;
                    }
                }
                catch
                {
                }

                System.Threading.Thread.Sleep(250);
            }

            throw new TimeoutException($"RibbonTab {tabName} not found within {timeout.TotalSeconds}s");
        }

        public static AutomationElement WaitForPanel<T>(this Window window, TimeSpan timeout = default) where T : ScopedPanelBase
        {
            timeout = timeout == default ? TimeSpan.FromSeconds(10) : timeout;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var element = window.FindFirstDescendant(cf => cf.ByAutomationId(typeof(T).Name));
                    if (element != null)
                    {
                        return element;
                    }
                }
                catch
                {
                }

                System.Threading.Thread.Sleep(250);
            }

            throw new TimeoutException($"Panel {typeof(T).Name} not found within {timeout.TotalSeconds}s");
        }

        public static void ClickSfButton(this AutomationElement element, string buttonAutomationId)
        {
            var btn = element.FindFirstDescendant(cf => cf.ByAutomationId(buttonAutomationId));
            btn?.Click();
            Keyboard.Type(VirtualKeyShort.ENTER); // extra safety for Syncfusion focus
        }

        public static void AssertNoCriticalLogs(this ILogger logger, string runLog)
        {
            if (runLog.Contains("[CRITICAL] DockingManager is null"))
                throw new InvalidOperationException("DockingManager null warning still present – use TestDockingManagerStub!");
        }
    }
}
